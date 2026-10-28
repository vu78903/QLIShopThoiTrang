using System.Collections.Concurrent;
using System.Text.Json;
using Npgsql;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using QLIShopThoiTrang.Api.Data;
using QLIShopThoiTrang.Api.Models;

namespace QLIShopThoiTrang.Api.Services;

public sealed class PayOsPaymentService(
    IConfiguration configuration,
    SqlServerConnectionFactory db,
    StoreRepository store)
{
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> FinalizationLocks = new();
    private readonly string _clientId = configuration["PayOS:ClientId"]
        ?? Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID") ?? "";
    private readonly string _apiKey = configuration["PayOS:ApiKey"]
        ?? Environment.GetEnvironmentVariable("PAYOS_API_KEY") ?? "";
    private readonly string _checksumKey = configuration["PayOS:ChecksumKey"]
        ?? Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY") ?? "";
    private readonly string _returnUrl = configuration["PayOS:ReturnUrl"]
        ?? Environment.GetEnvironmentVariable("PAYOS_RETURN_URL") ?? "https://payos.vn";
    private readonly string _cancelUrl = configuration["PayOS:CancelUrl"]
        ?? Environment.GetEnvironmentVariable("PAYOS_CANCEL_URL") ?? "https://payos.vn";

    private PayOSClient Client => IsConfigured
        ? new PayOSClient(_clientId, _apiKey, _checksumKey)
        : throw new InvalidOperationException("Chưa cấu hình PAYOS_CLIENT_ID, PAYOS_API_KEY và PAYOS_CHECKSUM_KEY.");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId)
        && !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_checksumKey);

    public async Task<QrPaymentSessionDto> CreateAsync(InvoiceCreateDto invoice, CancellationToken cancellationToken)
    {
        invoice = invoice with { PaymentMethod = "Quet ma QR", Paid = invoice.Total, IsCod = false };
        ValidateInvoice(invoice);
        await EnsureTableAsync(cancellationToken);
        if (await store.InvoiceExistsAsync(invoice.Id))
            throw new InvalidOperationException("Hóa đơn đã được thanh toán trước đó.");

        await store.ValidatePayableInvoiceAsync(invoice);

        await using (var existingConnection = db.CreateConnection())
        {
            await existingConnection.OpenAsync(cancellationToken);
            await using var existing = new NpgsqlCommand("""
                SELECT magd,malienket,duongdanthanhtoan,manganhang,sotaikhoan,tentaikhoan,noidung,trangthai
                FROM giaodichthanhtoanqr WHERE sohd=@invoice
                """, existingConnection);
            existing.Parameters.AddWithValue("invoice", invoice.Id);
            await using var reader = await existing.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
                return new(reader.GetInt64(0), invoice.Id, invoice.Total, reader.GetString(6), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(1), reader.GetString(2), reader.GetString(7));
        }

        var amount = checked((long)invoice.Total);
        var orderCode = NewOrderCode();
        var description = BuildDescription(invoice.Id);
        var response = await Client.PaymentRequests.CreateAsync(new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = amount,
            Description = description,
            ReturnUrl = _returnUrl,
            CancelUrl = _cancelUrl,
            ExpiredAt = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()
        });

        try
        {
            await using var connection = db.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("""
                INSERT INTO giaodichthanhtoanqr
                    (magd,sohd,nguondon,sotien,malienket,duongdanthanhtoan,manganhang,sotaikhoan,
                     tentaikhoan,noidung,trangthai,dulieuhoadon,ngaytao)
                VALUES(@code,@invoice,'POS',@amount,@linkId,@checkout,@bin,@account,@accountName,@description,
                       'PENDING',CAST(@payload AS jsonb),CURRENT_TIMESTAMP)
                """, connection);
            command.Parameters.AddWithValue("code", response.OrderCode);
            command.Parameters.AddWithValue("invoice", invoice.Id);
            command.Parameters.AddWithValue("amount", invoice.Total);
            command.Parameters.AddWithValue("linkId", response.PaymentLinkId);
            command.Parameters.AddWithValue("checkout", response.CheckoutUrl);
            command.Parameters.AddWithValue("bin", response.Bin);
            command.Parameters.AddWithValue("account", response.AccountNumber);
            command.Parameters.AddWithValue("accountName", response.AccountName);
            command.Parameters.AddWithValue("description", response.Description);
            command.Parameters.AddWithValue("payload", JsonSerializer.Serialize(invoice));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            try { await Client.PaymentRequests.CancelAsync(response.OrderCode, "Không lưu được phiên thanh toán"); }
            catch { }
            throw;
        }

        return new(response.OrderCode, invoice.Id, invoice.Total, response.Description, response.Bin,
            response.AccountNumber, response.AccountName, response.PaymentLinkId, response.CheckoutUrl, "PENDING");
    }

    public async Task<QrPaymentStatusDto> GetStatusAsync(long orderCode, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(cancellationToken);
        var payment = await Client.PaymentRequests.GetAsync(orderCode);
        var status = PaymentLinkStatusConverter.ToSerializedString(payment.Status);
        var expected = await GetExpectedAmountAsync(orderCode, cancellationToken);
        if (payment.Status == PaymentLinkStatus.Paid)
        {
            if (payment.AmountPaid < expected)
                throw new InvalidOperationException("payOS báo số tiền nhận được chưa đủ.");
            await FinalizeAsync(orderCode, payment.Transactions?.FirstOrDefault()?.Reference, cancellationToken);
            status = "PAID";
        }
        else
        {
            await UpdateProviderStatusAsync(orderCode, status, cancellationToken);
        }
        return new(orderCode, status, payment.Amount, payment.AmountPaid, status == "PAID");
    }

    public async Task CancelAsync(long orderCode, CancellationToken cancellationToken)
    {
        var current = await Client.PaymentRequests.GetAsync(orderCode);
        if (current.Status == PaymentLinkStatus.Paid)
        {
            if (current.AmountPaid < await GetExpectedAmountAsync(orderCode, cancellationToken))
                throw new InvalidOperationException("Giao dich da nhan tien nhung chua du so tien hoa don.");
            await FinalizeAsync(orderCode, current.Transactions?.FirstOrDefault()?.Reference, cancellationToken);
            throw new InvalidOperationException("Giao dich da thanh toan nen khong the huy. Hoa don da duoc ghi nhan.");
        }
        var payment = await Client.PaymentRequests.CancelAsync(orderCode, "Khách hủy tại quầy");
        await UpdateProviderStatusAsync(orderCode,
            PaymentLinkStatusConverter.ToSerializedString(payment.Status), cancellationToken);
    }

    public async Task HandleWebhookAsync(Webhook webhook, CancellationToken cancellationToken)
    {
        var verified = await Client.Webhooks.VerifyAsync(webhook);
        if (verified.Amount != await GetExpectedAmountAsync(verified.OrderCode, cancellationToken))
            throw new InvalidOperationException("Số tiền webhook không khớp phiên thanh toán.");
        await FinalizeAsync(verified.OrderCode, verified.Reference, cancellationToken);
    }

    private async Task FinalizeAsync(long orderCode, string? reference, CancellationToken cancellationToken)
    {
        var gate = FinalizationLocks.GetOrAdd(orderCode, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
        InvoiceCreateDto invoice;
        await using (var connection = db.CreateConnection())
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("""
                SELECT dulieuhoadon::text,trangthai FROM giaodichthanhtoanqr WHERE magd=@code
                """, connection);
            command.Parameters.AddWithValue("code", orderCode);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Không tìm thấy phiên thanh toán QR.");
            if (reader.GetString(1) == "PAID") return;
            invoice = JsonSerializer.Deserialize<InvoiceCreateDto>(reader.GetString(0))
                ?? throw new InvalidOperationException("Dữ liệu hóa đơn thanh toán không hợp lệ.");
        }

        if (!await store.InvoiceExistsAsync(invoice.Id)) await store.CreateInvoiceAsync(invoice);

        await using var updateConnection = db.CreateConnection();
        await updateConnection.OpenAsync(cancellationToken);
        await using var update = new NpgsqlCommand("""
            UPDATE giaodichthanhtoanqr SET trangthai='PAID',mathamchieu=@reference,ngaythanhtoan=CURRENT_TIMESTAMP
            WHERE magd=@code
            """, updateConnection);
        update.Parameters.AddWithValue("code", orderCode);
        update.Parameters.AddWithValue("reference", (object?)reference ?? DBNull.Value);
        await update.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<decimal> GetExpectedAmountAsync(long orderCode, CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT sotien FROM giaodichthanhtoanqr WHERE magd=@code", connection);
        command.Parameters.AddWithValue("code", orderCode);
        return await command.ExecuteScalarAsync(cancellationToken) is decimal amount
            ? amount : throw new InvalidOperationException("Không tìm thấy phiên thanh toán QR.");
    }

    private async Task UpdateProviderStatusAsync(long orderCode, string status, CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE giaodichthanhtoanqr SET trangthai=@status WHERE magd=@code AND trangthai<>'PAID'", connection);
        command.Parameters.AddWithValue("code", orderCode);
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureTableAsync(CancellationToken cancellationToken)
    {
        await using var connection = db.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS giaodichthanhtoanqr (
                magd bigint PRIMARY KEY,
                sohd varchar(30) UNIQUE NOT NULL,
                nguondon varchar(10) NOT NULL,
                sotien numeric(18,2) NOT NULL CHECK(sotien>0),
                malienket varchar(100) NOT NULL,
                duongdanthanhtoan text NOT NULL,
                manganhang varchar(20) NOT NULL,
                sotaikhoan varchar(50) NOT NULL,
                tentaikhoan varchar(200) NOT NULL,
                noidung varchar(50) NOT NULL,
                trangthai varchar(20) NOT NULL,
                mathamchieu varchar(100),
                dulieuhoadon jsonb,
                ngaytao timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ngaythanhtoan timestamp without time zone
            );
            CREATE INDEX IF NOT EXISTS ix_giaodichthanhtoanqr_sohd ON giaodichthanhtoanqr(sohd);
            """, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static long NewOrderCode() => checked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
        + Random.Shared.Next(100, 999));

    private static string BuildDescription(string invoiceId)
    {
        var clean = new string(invoiceId.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (clean.Length > 7) clean = clean[^7..];
        return "HD" + clean;
    }

    private static void ValidateInvoice(InvoiceCreateDto invoice)
    {
        if (string.IsNullOrWhiteSpace(invoice.Id) || invoice.Id.Length > 30 || invoice.Items.Count == 0)
            throw new InvalidOperationException("Hóa đơn thanh toán QR không hợp lệ.");
        if (invoice.Items.Any(item => item.Qty <= 0 || item.Price < 0 || Math.Abs(item.Total - item.Price * item.Qty) > 1))
            throw new InvalidOperationException("Chi tiết hóa đơn thanh toán QR không hợp lệ.");
        var subtotal = invoice.Items.Sum(item => item.Price * item.Qty);
        var taxable = Math.Max(0, subtotal - invoice.Discount);
        var expectedVat = Math.Round(taxable * 0.08m, 0, MidpointRounding.AwayFromZero);
        var expected = taxable + expectedVat + invoice.ShippingFee;
        if (invoice.Total <= 0 || invoice.Total != decimal.Truncate(invoice.Total)
            || Math.Abs(invoice.Subtotal - subtotal) > 1 || Math.Abs(invoice.VatAmount - expectedVat) > 1
            || Math.Abs(invoice.Total - expected) > 1)
            throw new InvalidOperationException("Số tiền thanh toán QR không hợp lệ.");
    }
}
