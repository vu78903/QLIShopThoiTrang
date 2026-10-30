using System.Data;
using System.Security.Cryptography;
using System.Text;
using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using Npgsql;

namespace FashionStore.Backend.Services;

// WEB invoices follow the partner's COD workflow. Only Windows completes stock/points.
public sealed class SupabaseOrderService(DatabaseMode mode)
{
    private NpgsqlConnection Connection()
    {
        if (!mode.OrdersEnabled) throw new CustomerRequestException(503, "Đặt hàng chưa được bật.");
        return new NpgsqlConnection(mode.SupabaseConnection);
    }
    private static NpgsqlCommand Sql(NpgsqlConnection c, NpgsqlTransaction? tx, string sql,
        params (string Name, object Value)[] args)
    {
        var cmd = new NpgsqlCommand(sql, c, tx) { CommandTimeout = 15 };
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
        return cmd;
    }
    private static async Task Execute(NpgsqlConnection c, NpgsqlTransaction tx, string sql,
        CancellationToken ct, params (string Name, object Value)[] args)
    {
        await using var cmd = Sql(c, tx, sql, args); await cmd.ExecuteNonQueryAsync(ct);
    }
    private static void CheckVoucher(string? voucher)
    {
        if (!string.IsNullOrWhiteSpace(voucher)) throw new CustomerRequestException(409, "Ưu đãi trực tuyến chưa được bật.");
    }
    public async Task<CheckoutQuote> QuoteAsync(string customer, string? voucher, CancellationToken ct)
    {
        CheckVoucher(voucher);
        await using var c = Connection(); await c.OpenAsync(ct);
        await using var cmd = Sql(c, null, """
            SELECT COALESCE(SUM(l.soluong*p.dongia),0) FROM webstore.cart g
            JOIN webstore.cart_line l USING(magiohang) JOIN public.sanpham p USING(masku)
            WHERE g.makh=@customer
            """, ("customer", customer));
        var subtotal = Convert.ToDecimal(await cmd.ExecuteScalarAsync(ct));
        return new(subtotal, 0, subtotal);
    }
    public async Task<PlaceOrderResponse> PlaceAsync(string customer, CheckoutRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || request.PaymentMethod != "COD")
            throw new CustomerRequestException(400, "Hiện chỉ hỗ trợ thanh toán khi nhận hàng (COD).");
        CheckVoucher(request.Voucher);
        if (string.IsNullOrWhiteSpace(mode.WebOrderEmployeeId))
            throw new CustomerRequestException(503, "Cửa hàng chưa cấu hình nhân viên tiếp nhận đơn.");
        // Customer-scoped identity: retries cannot duplicate or expose another customer's order.
        var id = "WEB" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(customer + ":" + request.RequestId.ToString("D"))))[..27];
        await using var c = Connection(); await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using (var lockCustomer = Sql(c, tx, "SELECT makh FROM public.khachhang WHERE makh=@customer AND trangthai=true FOR UPDATE", ("customer", customer)))
            if (await lockCustomer.ExecuteScalarAsync(ct) is null) throw new CustomerRequestException(401, "Vui lòng đăng nhập lại.");
        await using (var exists = Sql(c, tx, "SELECT sohd FROM webstore.order_snapshot WHERE sohd=@id AND makh=@customer", ("id", id), ("customer", customer)))
            if (await exists.ExecuteScalarAsync(ct) is not null)
            {
                await tx.CommitAsync(ct);
                var previous = await DetailAsync(id, customer, ct);
                return new(previous.OrderId, previous.Status, previous.Total, previous.PaymentMethod);
            }
        await using (var employee = Sql(c, tx, "SELECT manv FROM public.nhanvien WHERE manv=@employee AND trangthai=true FOR SHARE", ("employee", mode.WebOrderEmployeeId)))
            if (await employee.ExecuteScalarAsync(ct) is null)
                throw new CustomerRequestException(503, "Nhân viên tiếp nhận đơn không tồn tại hoặc đã ngừng hoạt động.");
        var lines = new List<OrderLine>();
        await using (var cmd = Sql(c, tx, """
            SELECT p.masku,p.tensanpham,p.kichco,p.mausac,l.soluong,p.dongia,p.soluongtonkho,p.trangthai AND cat.trangthai
            FROM webstore.cart g JOIN webstore.cart_line l USING(magiohang)
            JOIN public.sanpham p USING(masku) JOIN public.loaisanpham cat USING(maloai)
            WHERE g.makh=@customer ORDER BY p.masku FOR UPDATE OF g,l,p FOR SHARE OF cat
            """, ("customer", customer)))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                var quantity = r.GetInt32(4); var price = r.GetDecimal(5);
                if (!r.GetBoolean(7) || quantity < 1 || quantity > r.GetInt32(6) || price <= 0)
                    throw new CustomerRequestException(409, "Giá hoặc tồn kho đã thay đổi. Vui lòng kiểm tra giỏ hàng.");
                lines.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), quantity, price, quantity * price));
            }
        if (lines.Count == 0) throw new CustomerRequestException(400, "Giỏ hàng đang trống.");
        var total = lines.Sum(l => l.Total);
        if (total <= 0 || total > 9999999999999999m) throw new CustomerRequestException(400, "Giá trị đơn hàng không hợp lệ.");
        await Execute(c, tx, """
            INSERT INTO public.hoadon(sohd,ngaylap,loaihoadon,phuongthucthanhtoan,tongcong,chietkhau,thuevat,
                khachphaitra,tienkhachdua,tienthua,sodienthoai_kh,makh,tennguoinhan,diachigiao,donvivanchuyen,
                trangthaigiaohang,trangthaithanhtoan,phiship,tiencod,manv,nguondon,ghichugiaohang)
            VALUES(@id,LOCALTIMESTAMP,'COD','COD',@total,0,0,@total,0,0,@phone,@customer,@name,@address,
                'Nội bộ','Vừa xác nhận đơn','CHUA_THANH_TOAN',0,@total,@employee,'WEB',@note);
            INSERT INTO webstore.order_snapshot(sohd,makh,ngaylap,recipient,phone,address,note,total)
            VALUES(@id,@customer,LOCALTIMESTAMP,@name,@phone,@address,@note,@total);
            """, ct, ("id", id), ("total", total), ("customer", customer), ("phone", request.Phone.Trim()),
            ("name", request.Recipient.Trim()), ("address", request.Address.Trim()), ("note", request.Note?.Trim() ?? ""), ("employee", mode.WebOrderEmployeeId));
        foreach (var line in lines)
            await Execute(c, tx, """
                INSERT INTO public.chitiethoadon(sohd,masku,soluong,dongia,thanhtien) VALUES(@id,@sku,@qty,@price,@total);
                INSERT INTO webstore.order_line_snapshot(sohd,masku,name,size,color,quantity,price)
                VALUES(@id,@sku,@name,@size,@color,@qty,@price);
                """, ct, ("id", id), ("sku", line.Sku), ("qty", line.Quantity), ("price", line.UnitPrice),
                ("total", line.Total), ("name", line.Name), ("size", line.Size), ("color", line.Color));
        await Execute(c, tx, "DELETE FROM webstore.cart_line WHERE magiohang IN (SELECT magiohang FROM webstore.cart WHERE makh=@customer)", ct, ("customer", customer));
        await tx.CommitAsync(ct);
        return new(id, "XAC_NHAN", total, "COD");
    }
    private const string StatusSql = """
        CASE WHEN h.sohd IS NULL THEN 'CAN_HO_TRO'
             WHEN h.trangthaigiaohang='Hàng giao thành công' THEN 'HOAN_TAT'
             WHEN h.trangthaigiaohang IN ('Shipper đã lấy hàng','Hàng đang được vận chuyển') THEN 'DANG_XU_LY'
             ELSE 'XAC_NHAN' END
        """;
    public async Task<IReadOnlyList<OrderSummary>> ListAsync(string customer, CancellationToken ct, string? q = null, string? status = null, int page = 1)
    {
        await using var c = Connection(); await c.OpenAsync(ct);
        await using var cmd = Sql(c, null, $"""
            SELECT s.sohd,s.ngaylap,{StatusSql},COALESCE(h.trangthaigiaohang,'Cần liên hệ cửa hàng'),
                COALESCE(h.trangthaithanhtoan,'CHUA_XAC_MINH'),COALESCE(h.khachphaitra,s.total),s.recipient
            FROM webstore.order_snapshot s LEFT JOIN public.hoadon h ON h.sohd=s.sohd AND h.makh=s.makh
            WHERE s.makh=@customer AND strpos(lower(s.sohd),lower(@q))>0 AND (@status='' OR ({StatusSql})=@status)
            ORDER BY s.ngaylap DESC,s.sohd DESC LIMIT 100 OFFSET @skip
            """, ("customer", customer), ("q", q ?? ""), ("status", status ?? ""), ("skip", (page - 1) * 100));
        var items = new List<OrderSummary>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) items.Add(new(r.GetString(0), r.GetDateTime(1), r.GetString(2), r.GetString(3), r.GetString(4), "COD", r.GetDecimal(5), r.GetString(6)));
        return items;
    }
    public async Task<OrderDetails> DetailAsync(string id, string customer, CancellationToken ct)
    {
        await using var c = Connection(); await c.OpenAsync(ct);
        // Repeatable read keeps header and lines consistent, while ownership is always server-side.
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        OrderDetails order;
        await using (var cmd = Sql(c, tx, $"""
            SELECT s.sohd,s.ngaylap,{StatusSql},COALESCE(h.trangthaigiaohang,'Cần liên hệ cửa hàng'),
                COALESCE(h.trangthaithanhtoan,'CHUA_XAC_MINH'),COALESCE(h.khachphaitra,s.total),s.recipient,s.phone,s.address,s.note,s.total
            FROM webstore.order_snapshot s LEFT JOIN public.hoadon h ON h.sohd=s.sohd AND h.makh=s.makh
            WHERE s.sohd=@id AND s.makh=@customer
            """, ("id", id), ("customer", customer)))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct)) throw new CustomerRequestException(404, "Không tìm thấy đơn hàng.");
            order = new(r.GetString(0), r.GetDateTime(1), r.GetString(2), r.GetString(3), r.GetString(4), "COD", r.GetDecimal(5), r.GetString(6), r.GetString(7), r.GetString(8), r.GetString(9), [], r.GetDecimal(10));
        }
        var lines = new List<OrderLine>();
        await using (var cmd = Sql(c, tx, "SELECT masku,name,size,color,quantity,price FROM webstore.order_line_snapshot WHERE sohd=@id ORDER BY masku", ("id", id)))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) lines.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetDecimal(5), r.GetInt32(4) * r.GetDecimal(5)));
        await tx.CommitAsync(ct);
        return order with { Items = lines, SupportsHistory = false, SupportsReturns = false };
    }
}
