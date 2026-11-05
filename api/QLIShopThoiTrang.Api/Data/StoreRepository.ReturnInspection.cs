using System.Text.Json;
using Npgsql;
using QLIShopThoiTrang.Api.Models;

namespace QLIShopThoiTrang.Api.Data;

public sealed partial class StoreRepository
{
    private static readonly JsonSerializerOptions InspectionJson = new(JsonSerializerDefaults.Web);

    private async Task InspectReturnLineAsync(string id, string sku, StockReviewActionDto input, string employee)
    {
        var note=(input.Note??"").Trim();
        if(!input.Received || note.Length is <1 or >1000)
            throw new InvalidOperationException("Xác nhận đã nhận hàng và ghi tình trạng thực tế (tối đa 1000 ký tự).");
        var explicitQuantities=input.ReceivedQty.HasValue || input.RestockQty.HasValue;
        if(explicitQuantities && (!input.ReceivedQty.HasValue || !input.RestockQty.HasValue || input.ReviewId is null || input.ReviewId==Guid.Empty || !input.ExpectedReceivedQty.HasValue))
            throw new InvalidOperationException("Thiếu mã kiểm nhận hoặc phiên bản số lượng. Vui lòng tải lại phiếu.");
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        await using var tx=await connection.BeginTransactionAsync();
        if(await ScalarLongAsync(connection,tx,"SELECT COUNT(*) FROM NhanVien WHERE MaNV=@id AND TrangThai=TRUE AND MaQuyen='CHT'",("@id",employee))!=1)
            throw new InvalidOperationException("Chỉ cửa hàng trưởng đang hoạt động được xác nhận nhập lại kho.");
        string invoice,status;
        await using(var command=NewCommand(connection,tx,"SELECT SoHD,TrangThai FROM PhieuDoiTra WHERE SoPhieuDT=@id FOR UPDATE",("@id",id)))
        {
            await using var reader=await command.ExecuteReaderAsync();
            if(!await reader.ReadAsync())throw new InvalidOperationException("Không tìm thấy phiếu đổi trả.");
            invoice=reader.GetString(0);status=reader.GetString(1);
        }
        if(status!="HOAN_TAT")throw new InvalidOperationException("Yêu cầu đổi trả chưa được duyệt/tiếp nhận.");
        if(await ScalarLongAsync(connection,tx,"SELECT COUNT(*) FROM HoaDon WHERE SoHD=@id AND DaTruTonKho=TRUE",("@id",invoice))!=1)
            throw new InvalidOperationException("Chưa xác định xuất kho của đơn gốc. Cần đối chiếu trước khi nhập lại hàng.");
        int qty,checkedQty,restockedQty;string? previous;List<StockReviewEventDto> history;
        await using(var command=NewCommand(connection,tx,"""
            SELECT SoLuong,SoLuongDaKiemNhan,SoLuongNhapLai,TrangThaiKiemNhan,NhatKyKiemNhan::text
            FROM ChiTietDoiTra WHERE SoPhieuDT=@id AND MaSKU=@sku FOR UPDATE
            """,("@id",id),("@sku",sku)))
        {
            await using var reader=await command.ExecuteReaderAsync();
            if(!await reader.ReadAsync())throw new InvalidOperationException("Sản phẩm không thuộc phiếu đổi trả.");
            qty=reader.GetInt32(0);checkedQty=reader.GetInt32(1);restockedQty=reader.GetInt32(2);
            previous=reader.IsDBNull(3)?null:reader.GetString(3);
            history=JsonSerializer.Deserialize<List<StockReviewEventDto>>(reader.GetString(4),InspectionJson)??[];
        }
        var received=input.ReceivedQty??qty;var accepted=input.RestockQty??(input.Restock?received:0);
        if(received<1 || accepted<0 || accepted>received || input.Restock!=(accepted>0))
            throw new InvalidOperationException("Số lượng kiểm nhận và lựa chọn nhập kho không hợp lệ.");
        if(input.ReviewId is Guid eventId && history.FirstOrDefault(e=>e.ReviewId==eventId) is {} old)
        {
            if(old.ReceivedQty!=received || old.RestockQty!=accepted || old.Note!=note)
                throw new InvalidOperationException("Mã kiểm nhận đã dùng cho nội dung khác.");
            await tx.CommitAsync();return;
        }
        // Các màn hình cũ gửi toàn bộ một dòng: giữ khả năng gửi lại mà không cộng kho lần hai.
        if(!explicitQuantities && checkedQty==qty && previous==(input.Restock?"DA_NHAP_LAI":"KHONG_NHAP_LAI"))
        {await tx.CommitAsync();return;}
        if(explicitQuantities && input.ExpectedReceivedQty!=checkedQty)
            throw new InvalidOperationException("Số lượng đã thay đổi do một lượt kiểm nhận khác. Tải lại trước khi tiếp tục.");
        if(!explicitQuantities && checkedQty>0)
            throw new InvalidOperationException("Phiếu đã kiểm nhận một phần. Vui lòng dùng màn hình kiểm nhận mới.");
        if(received<1 || received>qty-checkedQty || accepted<0 || accepted>received || input.Restock!=(accepted>0))
            throw new InvalidOperationException("Số lượng nhận phải nằm trong phần còn chờ; lượng nhập kho từ 0 đến lượng thực nhận.");
        if(accepted>0)
            await ExecuteAsync(connection,tx,"UPDATE SanPham SET SoLuongTonKho=SoLuongTonKho+@qty,SoLuong=SoLuong+@qty WHERE MaSKU=@sku",("@qty",accepted),("@sku",sku));
        var nextChecked=checkedQty+received;var nextStock=restockedQty+accepted;
        var nextStatus=nextChecked<qty?"DANG_KIEM":nextStock==qty?"DA_NHAP_LAI":nextStock==0?"KHONG_NHAP_LAI":"NHAP_LAI_MOT_PHAN";
        history.Add(new(input.ReviewId??Guid.NewGuid(),received,accepted,note,employee,DateTime.UtcNow));
        await ExecuteAsync(connection,tx,"""
            UPDATE ChiTietDoiTra SET SoLuongDaKiemNhan=@checked,SoLuongNhapLai=@stock,NhatKyKiemNhan=CAST(@history AS jsonb),
                TrangThaiKiemNhan=@status,GhiChuKiemNhan=@note,MaNVKiemNhan=@employee,NgayKiemNhan=LOCALTIMESTAMP
            WHERE SoPhieuDT=@id AND MaSKU=@sku
            """,("@checked",nextChecked),("@stock",nextStock),("@history",JsonSerializer.Serialize(history,InspectionJson)),
            ("@status",nextStatus),("@note",note),("@employee",employee),("@id",id),("@sku",sku));
        // Kiểm hàng không thay đổi tiền, trạng thái hoàn tiền hoặc số lượng sản phẩm đổi mới.
        await tx.CommitAsync();
    }
}
