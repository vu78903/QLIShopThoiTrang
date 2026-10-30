using System.Data;
using System.Security.Cryptography;
using System.Text;
using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Services;

// Task 2: DEV-only transactional order workflow, using the same SQL schema as Web + Windows.
// Stock is NOT deducted at checkout. Existing SQL procedures own confirmation/cancellation.
public sealed class OrderService(FashionStoreDbContext db, SupabaseOrderService? postgres = null)
{
    private SqlConnection Connection()
    {
        var original = db.Database.GetDbConnection();
        var builder = new SqlConnectionStringBuilder(original.ConnectionString);
        // Never allow a demo checkout to modify a production DB accidentally.
        if (!string.Equals(builder.InitialCatalog, "FashionStoreDB_Dev", StringComparison.OrdinalIgnoreCase))
            throw new CustomerRequestException(503, "Task 2 chỉ bật trên FashionStoreDB_Dev.");
        return new SqlConnection(builder.ConnectionString);
    }

    private static SqlCommand Sql(SqlConnection conn, SqlTransaction? tx, string text, params (string Name, object? Value)[] args)
    {
        var cmd = new SqlCommand(text, conn, tx) { CommandTimeout = 15 };
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }
    private static string OrderId(Guid requestId)
    {
        var digest = SHA256.HashData(requestId.ToByteArray());
        return "ON" + Convert.ToHexString(digest).Substring(0, 28); // SQL SoHD VARCHAR(30)
    }
    private sealed record CartRow(string Sku, string Name, string Size, string Color, int Quantity, decimal Price, int Stock, bool Active);

    internal static decimal DiscountAmount(decimal subtotal,string kind,decimal value) =>
        Math.Min(subtotal, Math.Round(kind == "PHAN_TRAM" ? subtotal * value / 100m : value,2,MidpointRounding.AwayFromZero));
    private static async Task<decimal> VoucherDiscount(SqlConnection conn,SqlTransaction? tx,string? voucher,decimal subtotal,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(voucher))return 0;
        await using var cmd=Sql(conn,tx,@"SELECT LoaiKM,GiaTriGiam,GiaTriDonHangToiThieu FROM dbo.KhuyenMai WITH(UPDLOCK,HOLDLOCK) WHERE MaKM=@code AND TrangThai=1 AND SYSDATETIME() BETWEEN NgayBatDau AND NgayKetThuc AND (SoLuotToiDa IS NULL OR SoLuotDaDung<SoLuotToiDa)",("@code",voucher.Trim()));
        await using var rd=await cmd.ExecuteReaderAsync(ct);
        if(!await rd.ReadAsync(ct))throw new CustomerRequestException(409,"Mã ưu đãi không hợp lệ, đã hết hạn hoặc hết lượt.");
        if(subtotal<rd.GetDecimal(2))throw new CustomerRequestException(409,"Đơn hàng chưa đạt giá trị tối thiểu của ưu đãi.");
        return DiscountAmount(subtotal,rd.GetString(0),rd.GetDecimal(1));
    }
    public async Task<CheckoutQuote> QuoteAsync(string kh,string? voucher,CancellationToken ct)
    {
        if (db.Database.IsNpgsql()) return await postgres!.QuoteAsync(kh, voucher, ct);
        await using var conn=Connection();await conn.OpenAsync(ct);
        await using var cmd=Sql(conn,null,@"SELECT COALESCE(SUM(b.DonGia*ct.SoLuong),0) FROM dbo.GioHang g JOIN dbo.ChiTietGioHang ct ON ct.MaGioHang=g.MaGioHang JOIN dbo.BienTheSanPham b ON b.MaSKU=ct.MaSKU WHERE g.MaKH=@kh",("@kh",kh));
        var subtotal=Convert.ToDecimal(await cmd.ExecuteScalarAsync(ct));
        var discount=await VoucherDiscount(conn,null,voucher,subtotal,ct);
        return new CheckoutQuote(subtotal,discount,subtotal-discount);
    }
    public async Task CancelCustomerAsync(string kh,string id,CancellationToken ct)
    {
        if (db.Database.IsNpgsql()) throw new CustomerRequestException(409, "Vui lòng liên hệ cửa hàng để hủy đơn giao hàng.");
        await using var conn=Connection();await conn.OpenAsync(ct);
        await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using(var check=Sql(conn,tx,"SELECT COUNT(*) FROM dbo.HoaDon WITH(UPDLOCK,HOLDLOCK) WHERE SoHD=@id AND MaKH=@kh AND LoaiHoaDon='ONLINE'",("@id",id),("@kh",kh)))
            if(Convert.ToInt32(await check.ExecuteScalarAsync(ct))!=1)throw new CustomerRequestException(404,"Không tìm thấy đơn hàng.");
        // Ownership lock remains held while the existing procedure checks payment/shipping and restores stock.
        await using var cmd=new SqlCommand("dbo.usp_HuyDonOnline",conn,tx){CommandType=CommandType.StoredProcedure};
        cmd.Parameters.Add("@SoHD",SqlDbType.VarChar,30).Value=id;
        cmd.Parameters.Add("@MaNV",SqlDbType.VarChar,20).Value=DBNull.Value;
        cmd.Parameters.Add("@LyDo",SqlDbType.NVarChar,500).Value="Khách hàng hủy đơn từ website";
        cmd.Parameters.Add("@KetQua",SqlDbType.NVarChar,30).Direction=ParameterDirection.Output;
        try {await cmd.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);}
        catch(SqlException e) when(e.Number>=54000 && e.Number<55000) {throw new CustomerRequestException(409,"Không thể hủy đơn ở trạng thái hiện tại. Đơn đã thanh toán hoặc đang giao cần cửa hàng hỗ trợ.");}
    }

    public async Task<PlaceOrderResponse> PlaceAsync(string customerId, CheckoutRequest request, CancellationToken ct)
    {
        if (db.Database.IsNpgsql()) return await postgres!.PlaceAsync(customerId, request, ct);
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Recipient) ||
            string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.Phone) ||
            request.Recipient.Trim().Length > 150 || request.Address.Trim().Length > 300 ||
            request.Note?.Length > 500 || (request.PaymentMethod != "COD" && request.PaymentMethod != "CHUYEN_KHOAN"))
            throw new CustomerRequestException(400, "Thông tin giao hàng không hợp lệ.");
        var orderId = OrderId(request.RequestId);
        await using var conn = Connection();
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Lock customer row to serialize checkout with cart updates for this customer.
            await using (var check = Sql(conn, tx,
                "SELECT COUNT(*) FROM dbo.KhachHang WITH (UPDLOCK,HOLDLOCK) WHERE MaKH=@kh AND TrangThai=1", ("@kh", customerId)))
                if ((int)(await check.ExecuteScalarAsync(ct) ?? 0) != 1)
                    throw new CustomerRequestException(401, "Tài khoản khách hàng không còn hoạt động.");

            // Deterministic order ID: retrying the SAME form cannot create a second order.
            await using (var exists = Sql(conn, tx,
                "SELECT MaKH FROM dbo.HoaDon WITH (UPDLOCK,HOLDLOCK) WHERE SoHD=@id AND LoaiHoaDon='ONLINE'", ("@id", orderId)))
            {
                var existing = await exists.ExecuteScalarAsync(ct);
                if (existing is not null && existing != DBNull.Value)
                {
                    if (!string.Equals((string)existing, customerId, StringComparison.OrdinalIgnoreCase))
                        throw new CustomerRequestException(409, "Mã yêu cầu đặt hàng bị trùng.");
                    await tx.CommitAsync(ct);
                    var previous = await DetailAsync(orderId, customerId, ct);
                    return new PlaceOrderResponse(previous.OrderId, previous.Status, previous.Total, previous.PaymentMethod);
                }
            }
            var rows = new List<CartRow>();
            await using (var cmd = Sql(conn, tx, @"
SELECT ct.MaSKU, sp.TenSanPham, bt.KichCo, bt.MauSac, ct.SoLuong, bt.DonGia, bt.SoLuongTonKho,
       CAST(CASE WHEN sp.TrangThai=1 AND bt.TrangThai=1 AND lo.TrangThai=1 THEN 1 ELSE 0 END AS bit)
FROM dbo.GioHang gh WITH (UPDLOCK,HOLDLOCK)
JOIN dbo.ChiTietGioHang ct WITH (UPDLOCK,HOLDLOCK) ON ct.MaGioHang=gh.MaGioHang
JOIN dbo.BienTheSanPham bt WITH (UPDLOCK,HOLDLOCK) ON bt.MaSKU=ct.MaSKU
JOIN dbo.SanPham sp ON sp.MaSP=bt.MaSP
JOIN dbo.LoaiSanPham lo ON lo.MaLoai=sp.MaLoai
WHERE gh.MaKH=@kh ORDER BY ct.MaSKU", ("@kh", customerId)))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct))
                    rows.Add(new CartRow(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                        reader.GetString(3), reader.GetInt32(4), reader.GetDecimal(5), reader.GetInt32(6), reader.GetBoolean(7)));
            if (rows.Count == 0) throw new CustomerRequestException(400, "Giỏ hàng đang trống.");
            if (rows.Any(l => !l.Active || l.Quantity < 1 || l.Quantity > l.Stock || l.Price <= 0))
                throw new CustomerRequestException(409, "Sản phẩm, giá hoặc tồn kho đã thay đổi. Vui lòng xem lại giỏ hàng.");
            var subtotal = rows.Sum(l => l.Price * l.Quantity);
            var discount = await VoucherDiscount(conn,tx,request.Voucher,subtotal,ct);
            var total = subtotal-discount;
            if (total <= 0 || total > 9999999999999999m)
                throw new CustomerRequestException(400, "Giá trị thanh toán phải lớn hơn 0. Ưu đãi miễn phí toàn đơn chưa được hỗ trợ.");

            await using (var cmd = Sql(conn, tx, @"
INSERT dbo.HoaDon (SoHD,LoaiHoaDon,MaKH,PhuongThucThanhToan,TrangThaiHoaDon,TrangThaiGiaoHang,
TrangThaiThanhToan,TongTienHang,TongCong,KhachPhaiTra,TienCOD,TenNguoiNhan,SoDienThoaiNhan,DiaChiGiao,GhiChu,MaKM,TienKhuyenMai)
VALUES (@id,'ONLINE',@kh,@pay,'MOI','CHO_XAC_NHAN','CHUA_THANH_TOAN',@subtotal,@total,@total,@cod,@name,@phone,@address,@note,@voucher,@discount)",
                ("@subtotal",subtotal),("@discount",discount),("@voucher",string.IsNullOrWhiteSpace(request.Voucher)?null:request.Voucher.Trim()),
                ("@id", orderId), ("@kh", customerId), ("@pay", request.PaymentMethod), ("@total", total),
                ("@cod", request.PaymentMethod == "COD" ? total : 0m), ("@name", request.Recipient.Trim()),
                ("@phone", request.Phone.Trim()), ("@address", request.Address.Trim()), ("@note", request.Note?.Trim())))
                await cmd.ExecuteNonQueryAsync(ct);
            if(!string.IsNullOrWhiteSpace(request.Voucher)) {
                await using var used=Sql(conn,tx,"UPDATE dbo.KhuyenMai SET SoLuotDaDung=SoLuotDaDung+1 WHERE MaKM=@code",("@code",request.Voucher.Trim()));
                await used.ExecuteNonQueryAsync(ct);
            }
            foreach (var line in rows)
            {
                await using var cmd = Sql(conn, tx, @"
INSERT dbo.ChiTietHoaDon (SoHD,MaSKU,SoLuong,DonGia,TenSanPhamLucMua,KichCoLucMua,MauSacLucMua)
VALUES (@id,@sku,@qty,@price,@name,@size,@color)", ("@id", orderId), ("@sku", line.Sku),
                    ("@qty", line.Quantity), ("@price", line.Price), ("@name", line.Name),
                    ("@size", line.Size), ("@color", line.Color));
                await cmd.ExecuteNonQueryAsync(ct);
            }
            // No payment success is fabricated. Both methods remain pending manual settlement.
            await using (var cmd = Sql(conn, tx, @"
INSERT dbo.ThanhToan (SoHD,SoTien,PhuongThuc,TrangThai,LoaiGiaoDich,GhiChu)
VALUES (@id,@amount,@method,'CHO_XU_LY','THU',N'Đang chờ xác nhận thanh toán thủ công')",
                ("@id", orderId), ("@amount", total), ("@method", request.PaymentMethod)))
                await cmd.ExecuteNonQueryAsync(ct);
            await using (var cmd = Sql(conn, tx, @"
INSERT dbo.LichSuTrangThaiDonHang (SoHD,LoaiTrangThai,TrangThaiMoi,GhiChu)
VALUES (@id,'DON_HANG','MOI',N'Khách hàng đặt đơn từ website')", ("@id", orderId)))
                await cmd.ExecuteNonQueryAsync(ct);
            await using (var cmd = Sql(conn, tx, @"
DELETE ct FROM dbo.ChiTietGioHang ct JOIN dbo.GioHang gh ON ct.MaGioHang=gh.MaGioHang WHERE gh.MaKH=@kh",
                ("@kh", customerId)))
                await cmd.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return new PlaceOrderResponse(orderId, "MOI", total, request.PaymentMethod);
        }
        catch
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch (InvalidOperationException) { }
            throw;
        }
    }

    public async Task<IReadOnlyList<OrderSummary>> ListCustomerAsync(string customerId, CancellationToken ct, string? q=null,string? status=null,int page=1) => db.Database.IsNpgsql() ? await postgres!.ListAsync(customerId, ct, q, status, page) : await ListAsync(customerId, ct,q,status,page);
    public async Task<IReadOnlyList<OrderSummary>> ListAdminAsync(CancellationToken ct) => await ListAsync(null, ct);
    private async Task<IReadOnlyList<OrderSummary>> ListAsync(string? customerId, CancellationToken ct,string? q=null,string? status=null,int page=1)
    {
        await using var conn = Connection(); await conn.OpenAsync(ct);
        const string query = @"SELECT SoHD,NgayLap,TrangThaiHoaDon,TrangThaiGiaoHang,
TrangThaiThanhToan,PhuongThucThanhToan,KhachPhaiTra,COALESCE(TenNguoiNhan,N'')
FROM dbo.HoaDon WHERE LoaiHoaDon='ONLINE' AND (@kh IS NULL OR MaKH=@kh) AND (@q='' OR SoHD LIKE '%'+@q+'%') AND (@status='' OR TrangThaiHoaDon=@status)
ORDER BY NgayLap DESC,SoHD DESC OFFSET @skip ROWS FETCH NEXT 100 ROWS ONLY";
        await using var cmd = Sql(conn, null, query, ("@kh", customerId),("@q",q??""),("@status",status??""),("@skip",(page-1)*100));
        // Explicit type: AddWithValue(DBNULL) doesn't infer NVARCHAR for @kh reliably.
        cmd.Parameters["@kh"].SqlDbType = SqlDbType.VarChar; cmd.Parameters["@kh"].Size = 20;
        var items = new List<OrderSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(new OrderSummary(reader.GetString(0), reader.GetDateTime(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetDecimal(6), reader.GetString(7)));
        return items;
    }

    public async Task<OrderDetails> DetailAsync(string id, string? customerId, CancellationToken ct)
    {
        if (db.Database.IsNpgsql()) return await postgres!.DetailAsync(id, customerId ?? "", ct);
        if (string.IsNullOrWhiteSpace(id) || id.Length > 30) throw new CustomerRequestException(404, "Không tìm thấy đơn hàng.");
        await using var conn = Connection(); await conn.OpenAsync(ct);
        const string query = @"SELECT SoHD,NgayLap,TrangThaiHoaDon,TrangThaiGiaoHang,TrangThaiThanhToan,
PhuongThucThanhToan,KhachPhaiTra,COALESCE(TenNguoiNhan,N''),COALESCE(SoDienThoaiNhan,''),
COALESCE(DiaChiGiao,N''),GhiChu,TongTienHang,TienKhuyenMai,MaKM FROM dbo.HoaDon
WHERE SoHD=@id AND LoaiHoaDon='ONLINE' AND (@kh IS NULL OR MaKH=@kh)";
        OrderDetails? order;
        await using (var cmd = Sql(conn, null, query, ("@id", id), ("@kh", customerId)))
        {
            cmd.Parameters["@kh"].SqlDbType = SqlDbType.VarChar; cmd.Parameters["@kh"].Size = 20;
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw new CustomerRequestException(404, "Không tìm thấy đơn hàng.");
            order = new OrderDetails(r.GetString(0), r.GetDateTime(1), r.GetString(2), r.GetString(3),
                r.GetString(4), r.GetString(5), r.GetDecimal(6), r.GetString(7), r.GetString(8),
                r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10), [],r.GetDecimal(11),r.GetDecimal(12),r.IsDBNull(13)?null:r.GetString(13));
        }
        var lines = new List<OrderLine>();
        await using (var cmd = Sql(conn, null, @"SELECT MaSKU,TenSanPhamLucMua,COALESCE(KichCoLucMua,N''),
COALESCE(MauSacLucMua,N''),SoLuong,DonGia,ThanhTien FROM dbo.ChiTietHoaDon WHERE SoHD=@id ORDER BY MaSKU", ("@id", id)))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                lines.Add(new OrderLine(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
                    r.GetInt32(4), r.GetDecimal(5), r.GetDecimal(6)));
        return order with { Items = lines };
    }

    public async Task<OrderActionResponse> ChangeStatusAsync(string id, string employeeId, bool cancel, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 30) throw new CustomerRequestException(404, "Mã đơn không hợp lệ.");
        await using var conn = Connection(); await conn.OpenAsync(ct);
        // Procedures validate invoice state, row locks and stock history atomically.
        // Never replace this with ad-hoc UPDATE SoLuongTonKho.
        await using var cmd = new SqlCommand(cancel ? "dbo.usp_HuyDonOnline" : "dbo.usp_XacNhanDonOnline", conn)
        { CommandType = CommandType.StoredProcedure, CommandTimeout = 20 };
        cmd.Parameters.Add("@SoHD", SqlDbType.VarChar, 30).Value = id;
        cmd.Parameters.Add("@MaNV", SqlDbType.VarChar, 20).Value = employeeId;
        if (cancel) cmd.Parameters.Add("@LyDo", SqlDbType.NVarChar, 500).Value = "Admin hủy đơn trước khi giao";
        var result = cmd.Parameters.Add("@KetQua", SqlDbType.NVarChar, 30); result.Direction = ParameterDirection.Output;
        try { await cmd.ExecuteNonQueryAsync(ct); }
        catch (SqlException e) when (e.Number is >= 54000 and < 55000)
        {
            throw new CustomerRequestException(409,
                e.Number is 54002 or 54100 ? "Cần cài đặt stored procedure phiên bản DEV trước khi xác nhận/hủy đơn." :
                "Không thể cập nhật đơn hàng: trạng thái, tồn kho hoặc lịch sử giao dịch không hợp lệ.");
        }
        var state = await DetailAsync(id, null, ct);
        return new OrderActionResponse(id, state.Status, (string?)result.Value ?? "OK");
    }
}
