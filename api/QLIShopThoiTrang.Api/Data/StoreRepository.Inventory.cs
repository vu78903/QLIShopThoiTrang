using Npgsql;
using QLIShopThoiTrang.Api.Models;

namespace QLIShopThoiTrang.Api.Data;

public sealed partial class StoreRepository
{
    private static bool IsCancelled(string? status) => status?.Trim().ToUpperInvariant() is
        "WEB_HUY" or "HUY" or "DA_HUY" or "DA HUY" or "CANCELLED" or "CANCELED" or
        "ĐÃ HỦY" or "ĐÃ HUỶ" or "HỦY" or "HUỶ" or "ĐƠN ĐÃ HỦY" or "ĐƠN ĐÃ HUỶ" or "ĐÃ NHẬN TIỀN - CẦN CỬA HÀNG XỬ LÝ";

    private static async Task ReserveInvoiceStockAsync(NpgsqlConnection connection,NpgsqlTransaction tx,string id)
    {
        object? flag;
        await using(var command=NewCommand(connection,tx,"SELECT DaTruTonKho FROM HoaDon WHERE SoHD=@id FOR UPDATE",("@id",id)))
            flag=await command.ExecuteScalarAsync();
        if(flag is true)return;
        if(flag is null || flag is DBNull)throw new InvalidOperationException("Đơn cũ chưa xác định đã trừ kho. Cửa hàng trưởng cần đối chiếu tại Tồn kho trước khi xử lý.");
        await using(var command=NewCommand(connection,tx,"""
            SELECT s.MaSKU FROM SanPham s JOIN ChiTietHoaDon c ON c.MaSKU=s.MaSKU
            WHERE c.SoHD=@id ORDER BY s.MaSKU FOR UPDATE OF s
            """,("@id",id)))
        {await using var reader=await command.ExecuteReaderAsync();while(await reader.ReadAsync()) { }}
        if(await ScalarLongAsync(connection,tx,"SELECT COUNT(*) FROM ChiTietHoaDon WHERE SoHD=@id",("@id",id))==0)
            throw new InvalidOperationException("Đơn chưa có chi tiết sản phẩm.");
        if(await ScalarLongAsync(connection,tx,"""
            SELECT COUNT(*) FROM ChiTietHoaDon c LEFT JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE c.SoHD=@id AND (c.SoLuong<=0 OR s.MaSKU IS NULL OR s.SoLuongTonKho<c.SoLuong)
            """,("@id",id))>0)throw new InvalidOperationException("Tồn kho không đủ để giữ hàng cho đơn.");
        await ExecuteAsync(connection,tx,"""
            UPDATE SanPham s SET SoLuongTonKho=s.SoLuongTonKho-c.SoLuong,SoLuong=s.SoLuong-c.SoLuong
            FROM ChiTietHoaDon c WHERE c.SoHD=@id AND c.MaSKU=s.MaSKU;
            UPDATE HoaDon SET DaTruTonKho=TRUE WHERE SoHD=@id;
            """,("@id",id));
    }

    public async Task<bool> CancelShipmentAsync(string id,string? reason)
    {
        reason=(reason??"").Trim();
        if(reason.Length>500)throw new InvalidOperationException("Lý do hủy tối đa 500 ký tự.");
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        await using var tx=await connection.BeginTransactionAsync();
        string type,status;
        await using(var command=NewCommand(connection,tx,"SELECT UPPER(LoaiHoaDon),COALESCE(TrangThaiGiaoHang,'') FROM HoaDon WHERE SoHD=@id FOR UPDATE",("@id",id)))
        {await using var reader=await command.ExecuteReaderAsync();if(!await reader.ReadAsync())return false;type=reader.GetString(0);status=reader.GetString(1);}
        if(IsCancelled(status)||IsCancelled(type)){await tx.CommitAsync();return true;}
        if(type is not ("COD" or "WEB_COD" or "GIAO_HANG" or "WEB_DA_THANH_TOAN" or "WEB_THANH_TOAN"))
            throw new InvalidOperationException("Chỉ hủy đơn giao hàng tại đây.");
        if(status=="Hàng giao thành công")throw new InvalidOperationException("Đơn giao thành công phải xử lý bằng phiếu đổi trả.");
        await ExecuteAsync(connection,tx,"UPDATE HoaDon SET TrangThaiGiaoHang='Đã hủy',GhiChuGiaoHang=@reason WHERE SoHD=@id",("@id",id),("@reason",reason));
        // Giữ nguyên hóa đơn/chi tiết, tiền đã thu và tồn. Kiểm nhận riêng mới cộng hàng về kho.
        await tx.CommitAsync();return true;
    }

    public async Task<List<StockUnconfirmedDto>> GetUnconfirmedStockAsync()
    {
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        var result=new List<StockUnconfirmedDto>();
        await using var command=NewCommand(connection,null,"""
            SELECT h.SoHD,h.LoaiHoaDon,COALESCE(h.TrangThaiGiaoHang,''),h.DaTruTonKho,h.DauVanTayYeuCau IS NOT NULL
            FROM HoaDon h WHERE (h.DaTruTonKho IS NULL AND (h.TrangThaiGiaoHang IS NOT NULL OR UPPER(h.LoaiHoaDon) IN ('WEB_HUY','HUY','DA_HUY','CANCELLED','CANCELED') OR EXISTS(
                SELECT 1 FROM PhieuDoiTra p WHERE p.SoHD=h.SoHD AND p.TrangThai='HOAN_TAT')))
             OR (h.DaTruTonKho=FALSE AND h.LoaiHoaDon IN ('COD','WEB_COD','GIAO_HANG','WEB_DA_THANH_TOAN','WEB_THANH_TOAN')
                 AND COALESCE(h.TrangThaiGiaoHang,'') NOT IN ('Đã hủy','Đã huỷ','Hàng giao thành công'))
            ORDER BY h.NgayLap,h.SoHD
            """);
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.IsDBNull(3)?null:reader.GetBoolean(3),reader.GetBoolean(4)));
        return result;
    }

    public async Task ReconcileInvoiceStockAsync(string id,bool alreadyDeducted)
    {
        await using var connection=db.CreateConnection();await connection.OpenAsync();await using var tx=await connection.BeginTransactionAsync();
        string status,type;object? deducted;bool credited;
        await using(var command=NewCommand(connection,tx,"SELECT COALESCE(TrangThaiGiaoHang,''),DaTruTonKho,COALESCE(DaGhiNhanThanhToan,UPPER(TrangThaiThanhToan)='DA_THANH_TOAN',FALSE),LoaiHoaDon FROM HoaDon WHERE SoHD=@id FOR UPDATE",("@id",id)))
        {await using var reader=await command.ExecuteReaderAsync();if(!await reader.ReadAsync())throw new InvalidOperationException("Không tìm thấy đơn.");status=reader.GetString(0);deducted=reader.IsDBNull(1)?null:reader.GetBoolean(1);credited=reader.GetBoolean(2);type=reader.GetString(3);}
        if(deducted is true){await tx.CommitAsync();return;}
        if(deducted is false && alreadyDeducted)throw new InvalidOperationException("Đơn đã được xác định chưa trừ tồn; không thể đánh dấu đã trừ mà bỏ qua xuất kho.");
        await ExecuteAsync(connection,tx,"UPDATE HoaDon SET DaTruTonKho=@deducted,DaGhiNhanThanhToan=@credited WHERE SoHD=@id",("@id",id),("@deducted",alreadyDeducted),("@credited",credited));
        if(!alreadyDeducted && !IsCancelled(status) && !IsCancelled(type))await ReserveInvoiceStockAsync(connection,tx,id);
        await tx.CommitAsync();
    }

    public async Task<List<StockReviewDto>> GetStockReviewsAsync()
    {
        await using var connection=db.CreateConnection();await connection.OpenAsync();var result=new List<StockReviewDto>();
        await using var command=NewCommand(connection,null,"""
            SELECT 'DON_HUY',h.SoHD,h.SoHD,c.MaSKU,s.TenSanPham,s.MauSac,s.KichCo,c.SoLuong,
                CASE WHEN h.DaTruTonKho IS NULL THEN 'CAN_DOI_CHIEU' ELSE COALESCE(c.TrangThaiKiemNhan,'CHO_KIEM') END,
                COALESCE(c.GhiChuKiemNhan,h.GhiChuGiaoHang,''),c.MaNVKiemNhan,c.NgayKiemNhan
            FROM HoaDon h JOIN ChiTietHoaDon c ON c.SoHD=h.SoHD JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE (h.DaTruTonKho IS DISTINCT FROM FALSE) AND (UPPER(h.LoaiHoaDon) IN ('WEB_HUY','HUY','DA_HUY','CANCELLED','CANCELED') OR h.TrangThaiGiaoHang ILIKE '%hủy%' OR h.TrangThaiGiaoHang ILIKE '%huỷ%' OR UPPER(h.TrangThaiGiaoHang) LIKE '%HUY%' OR UPPER(h.TrangThaiGiaoHang) IN ('CANCELLED','CANCELED'))
            UNION ALL
            SELECT 'DOI_TRA',p.SoPhieuDT,p.SoHD,c.MaSKU,s.TenSanPham,s.MauSac,s.KichCo,c.SoLuong,
                CASE WHEN h.DaTruTonKho IS DISTINCT FROM TRUE THEN 'CAN_DOI_CHIEU' ELSE COALESCE(c.TrangThaiKiemNhan,'CHO_KIEM') END,
                COALESCE(c.GhiChuKiemNhan,c.LyDoTra,''),c.MaNVKiemNhan,c.NgayKiemNhan
            FROM PhieuDoiTra p JOIN HoaDon h ON h.SoHD=p.SoHD JOIN ChiTietDoiTra c ON c.SoPhieuDT=p.SoPhieuDT JOIN SanPham s ON s.MaSKU=c.MaSKU
            WHERE p.TrangThai='HOAN_TAT'
            ORDER BY 2,4
            """);
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),reader.GetString(6),reader.GetInt32(7),reader.GetString(8),reader.GetString(9),reader.IsDBNull(10)?null:reader.GetString(10),reader.IsDBNull(11)?null:reader.GetDateTime(11).ToString("dd/MM/yyyy HH:mm")));
        return result;
    }

    public async Task ReviewReturnedStockAsync(string source,string id,string sku,StockReviewActionDto input,string employee)
    {
        var (table,key,header,headerKey)=source switch {
            "DON_HUY" => ("ChiTietHoaDon","SoHD","HoaDon","SoHD"),
            "DOI_TRA" => ("ChiTietDoiTra","SoPhieuDT","PhieuDoiTra","SoPhieuDT"),
            _ => throw new InvalidOperationException("Nguồn kiểm nhận không hợp lệ.")};
        var note=(input.Note??"").Trim();
        if(!input.Received)throw new InvalidOperationException("Chỉ kiểm nhận sau khi đã nhận hàng thực tế. Hàng chưa về tiếp tục để chờ kiểm nhận.");
        if(note.Length==0||note.Length>1000)throw new InvalidOperationException("Ghi nhận tình trạng hàng thực nhận (tối đa 1000 ký tự).");
        var desired=input.Restock?"DA_NHAP_LAI":"KHONG_NHAP_LAI";
        await using var connection=db.CreateConnection();await connection.OpenAsync();await using var tx=await connection.BeginTransactionAsync();
        var invoice=await ScalarStringAsync(connection,tx,$"SELECT SoHD FROM {header} WHERE {headerKey}=@id FOR UPDATE",("@id",id));
        if(invoice is null)throw new InvalidOperationException("Không tìm thấy chứng từ.");
        string type,status;bool deducted;
        await using(var command=NewCommand(connection,tx,"SELECT LoaiHoaDon,COALESCE(TrangThaiGiaoHang,''),COALESCE(DaTruTonKho,FALSE) FROM HoaDon WHERE SoHD=@id FOR UPDATE",("@id",invoice)))
        {await using var reader=await command.ExecuteReaderAsync();await reader.ReadAsync();type=reader.GetString(0);status=reader.GetString(1);deducted=reader.GetBoolean(2);}
        if(!deducted)throw new InvalidOperationException("Chưa xác định xuất kho của đơn gốc; không thể cộng kho. Hãy đối chiếu đơn cũ trước.");
        if(source=="DON_HUY" && !IsCancelled(type) && !IsCancelled(status))throw new InvalidOperationException("Đơn chưa hủy.");
        if(source=="DOI_TRA" && await ScalarStringAsync(connection,tx,"SELECT TrangThai FROM PhieuDoiTra WHERE SoPhieuDT=@id",("@id",id))!="HOAN_TAT")throw new InvalidOperationException("Phiếu đổi trả chưa được tiếp nhận.");
        int qty;string? previous;
        await using(var command=NewCommand(connection,tx,$"SELECT SoLuong,TrangThaiKiemNhan FROM {table} WHERE {key}=@id AND MaSKU=@sku FOR UPDATE",("@id",id),("@sku",sku)))
        {await using var reader=await command.ExecuteReaderAsync();if(!await reader.ReadAsync())throw new InvalidOperationException("SKU không thuộc chứng từ.");qty=reader.GetInt32(0);previous=reader.IsDBNull(1)?null:reader.GetString(1);}
        if(previous is not null){if(previous!=desired)throw new InvalidOperationException("Hàng đã kiểm nhận, không được đổi quyết định và cộng kho lần nữa.");await tx.CommitAsync();return;}
        if(qty<=0)throw new InvalidOperationException("Số lượng kiểm nhận không hợp lệ.");
        if(input.Restock)await ExecuteAsync(connection,tx,"UPDATE SanPham SET SoLuongTonKho=SoLuongTonKho+@qty,SoLuong=SoLuong+@qty WHERE MaSKU=@sku",("@qty",qty),("@sku",sku));
        await ExecuteAsync(connection,tx,$"UPDATE {table} SET TrangThaiKiemNhan=@status,GhiChuKiemNhan=@note,MaNVKiemNhan=@employee,NgayKiemNhan=CURRENT_TIMESTAMP WHERE {key}=@id AND MaSKU=@sku",("@status",desired),("@note",note),("@employee",employee),("@id",id),("@sku",sku));
        await tx.CommitAsync();
    }
}
