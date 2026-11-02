using Npgsql;
using QLIShopThoiTrang.Api.Models;
using System.Globalization;
using System.Text.Json;

namespace QLIShopThoiTrang.Api.Data;

public sealed partial class StoreRepository
{
    private static async Task<List<ReturnRequestedLineDto>> RequestedLinesAsync(NpgsqlConnection connection,NpgsqlTransaction? tx,string id)
    {
        var items=new List<ReturnRequestedLineDto>();
        await using var command=NewCommand(connection,tx,"""
            SELECT c.MaSKU,s.TenSanPham,s.MauSac,s.KichCo,c.SoLuong,COALESCE(c.LyDoTra,''),
                   COALESCE(NULLIF(c.LoaiGiaoDich,''),p.HinhThucXuLy),c.MaSKUDoi,n.TenSanPham,c.SoLuongDoi
            FROM ChiTietDoiTra c JOIN PhieuDoiTra p ON p.SoPhieuDT=c.SoPhieuDT
            JOIN SanPham s ON s.MaSKU=c.MaSKU LEFT JOIN SanPham n ON n.MaSKU=c.MaSKUDoi
            WHERE c.SoPhieuDT=@id ORDER BY c.MaSKU
            """,("@id",id));
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())items.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4),reader.GetString(5),reader.GetString(6),
            reader.IsDBNull(7)?null:reader.GetString(7),reader.IsDBNull(8)?null:reader.GetString(8),reader.GetInt32(9)));
        return items;
    }

    public async Task<List<ReturnRequestDto>> GetWebReturnRequestsAsync()
    {
        await using var connection=db.CreateConnection();await connection.OpenAsync();
        var headers=new List<(string Id,string Invoice,string Name,string Phone,DateTime Date,string Status,string? Reason,List<string> Images)>();
        await using(var command=NewCommand(connection,null,"""
            SELECT p.SoPhieuDT,p.SoHD,COALESCE(k.HoTen,'Khách hàng'),COALESCE(h.SoDienThoai_KH,''),p.NgayLap,
                   p.TrangThai,p.LyDoTuChoi,p.AnhMinhChung::text
            FROM PhieuDoiTra p JOIN HoaDon h ON h.SoHD=p.SoHD LEFT JOIN KhachHang k ON k.MaKH=h.MaKH
            WHERE UPPER(TRIM(p.NguonDon)) IN ('WEB','WEBSITE') AND p.TrangThai IN ('CHO_DUYET','CHO_TIEP_NHAN','TU_CHOI')
            ORDER BY p.NgayLap DESC,p.SoPhieuDT LIMIT 200
            """))
        await using(var reader=await command.ExecuteReaderAsync())while(await reader.ReadAsync())
            headers.Add((reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetDateTime(4),reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6),
                JsonSerializer.Deserialize<List<string>>(reader.GetString(7))??[]));
        var result=new List<ReturnRequestDto>();
        foreach(var h in headers)result.Add(new(h.Id,h.Invoice,h.Name,h.Phone,h.Date.ToString("dd/MM/yyyy HH:mm"),h.Status,h.Reason,h.Images,await RequestedLinesAsync(connection,null,h.Id)));
        return result;
    }

    public async Task RejectWebReturnAsync(string id,string reason,string employee)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>1000)throw new InvalidOperationException("Vui lòng nhập lý do từ chối (tối đa 1000 ký tự).");
        await using var connection=db.CreateConnection();await connection.OpenAsync();await using var tx=await connection.BeginTransactionAsync();
        if(await ScalarLongAsync(connection,tx,"SELECT COUNT(*) FROM NhanVien WHERE MaNV=@id AND TrangThai=TRUE",("@id",employee))==0)
            throw new InvalidOperationException("Không xác định được nhân viên tiếp nhận.");
        var count=await ExecuteAsync(connection,tx,"""
            UPDATE PhieuDoiTra SET TrangThai='TU_CHOI',LyDoTuChoi=@reason,MaNV=@employee
            WHERE SoPhieuDT=@id AND UPPER(TRIM(NguonDon)) IN ('WEB','WEBSITE') AND TrangThai IN ('CHO_DUYET','CHO_TIEP_NHAN')
            """,("@id",id),("@reason",reason.Trim()),("@employee",employee));
        if(count==0)throw new InvalidOperationException("Yêu cầu đã được xử lý hoặc không tồn tại.");
        await tx.CommitAsync();
    }

    public Task<ReturnReceiptDto> ApproveWebReturnAsync(string id,string employee) => ProcessReturnAsync(null,id,employee);

    private async Task<ReturnReceiptDto> ProcessReturnAsync(ReturnCreateDto? direct,string? requestId,string? employee)
    {
        if(string.IsNullOrWhiteSpace(employee))throw new InvalidOperationException("Không xác định được nhân viên tiếp nhận.");
        await using var connection=db.CreateConnection();await connection.OpenAsync();await using var tx=await connection.BeginTransactionAsync();
        if(await ScalarLongAsync(connection,tx,"SELECT COUNT(*) FROM NhanVien WHERE MaNV=@id AND TrangThai=TRUE",("@id",employee))==0)
            throw new InvalidOperationException("Nhân viên không còn hoạt động.");
        var key=direct?.ClientRequestId?.Trim();var digest=direct is null ? null : Fingerprint(direct);
        if(key?.Length>80)throw new InvalidOperationException("Mã yêu cầu quá dài.");
        if(!string.IsNullOrEmpty(key))
        {
            await ExecuteAsync(connection,tx,"SELECT pg_advisory_xact_lock(hashtext(@key))",("@key","return:"+key));
            string? oldId=null,oldDigest=null;
            await using(var command=NewCommand(connection,tx,"SELECT SoPhieuDT,DauVanTayYeuCau FROM PhieuDoiTra WHERE MaYeuCau=@key",("@key",key)))
            await using(var reader=await command.ExecuteReaderAsync())if(await reader.ReadAsync()){oldId=reader.GetString(0);oldDigest=reader.IsDBNull(1)?null:reader.GetString(1);}
            if(oldId is not null){if(oldDigest!=digest)throw new InvalidOperationException("Mã yêu cầu đã dùng cho nội dung khác.");await tx.CommitAsync();return(await GetReturnAsync(oldId))!;}
        }
        string invoice;List<ReturnRequestedLineDto> lines;
        if(requestId is not null)
        {
            string status;
            await using(var command=NewCommand(connection,tx,"SELECT SoHD,TrangThai FROM PhieuDoiTra WHERE SoPhieuDT=@id AND UPPER(TRIM(NguonDon)) IN ('WEB','WEBSITE') FOR UPDATE",("@id",requestId)))
            await using(var reader=await command.ExecuteReaderAsync())
            {
                if(!await reader.ReadAsync())throw new InvalidOperationException("Không tìm thấy yêu cầu từ website.");
                invoice=reader.GetString(0);status=reader.GetString(1);
            }
            if(status=="HOAN_TAT"){await tx.CommitAsync();return(await GetReturnAsync(requestId))!;}
            if(status is not ("CHO_DUYET" or "CHO_TIEP_NHAN"))throw new InvalidOperationException("Yêu cầu không còn chờ tiếp nhận.");
            lines=await RequestedLinesAsync(connection,tx,requestId);
        }
        else
        {
            invoice=direct!.InvoiceId.Trim();
            lines=[new(direct.ReturnSku.Trim().ToUpperInvariant(),"","","",direct.Qty,direct.Reason.Trim(),direct.Resolution.Trim().ToUpperInvariant(),direct.ReplacementSku?.Trim().ToUpperInvariant(),null,direct.Qty)];
        }
        if(lines.Count==0||lines.Any(l=>l.Qty<=0||string.IsNullOrWhiteSpace(l.Reason)||l.Reason.Length>1000||l.Resolution is not ("HOAN_TIEN" or "DOI_SAN_PHAM")))
            throw new InvalidOperationException("Yêu cầu thiếu SKU, số lượng, lý do hoặc hình thức xử lý hợp lệ.");
        string source,payment;DateTime created;DateTime? delivered;string? delivery,customer;
        await using(var command=NewCommand(connection,tx,"SELECT LoaiHoaDon,NgayLap,TrangThaiGiaoHang,NgayGiaoThanhCong,TrangThaiThanhToan,MaKH FROM HoaDon WHERE SoHD=@id FOR UPDATE",("@id",invoice)))
        await using(var reader=await command.ExecuteReaderAsync())
        {
            if(!await reader.ReadAsync())throw new InvalidOperationException("Không tìm thấy hóa đơn.");
            source=reader.GetString(0);created=reader.GetDateTime(1);delivery=reader.IsDBNull(2)?null:reader.GetString(2);
            delivered=reader.IsDBNull(3)?null:reader.GetDateTime(3);payment=reader.GetString(4);customer=reader.IsDBNull(5)?null:reader.GetString(5);
        }
        var eligibility=GetReturnEligibility(source,created,delivery,delivered,payment);
        if(!eligibility.Allowed)throw new InvalidOperationException(eligibility.Message);
        foreach(var sku in lines.SelectMany(l=>new[]{l.Sku,l.ReplacementSku}).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s=>s,StringComparer.Ordinal))
        {
            await using var command=NewCommand(connection,tx,"SELECT MaSKU FROM SanPham WHERE MaSKU=@sku FOR UPDATE",("@sku",sku!));await command.ExecuteScalarAsync();
        }
        var paid=await PaidLineTotalsAsync(connection,tx,invoice);
        var calculated=new List<(ReturnRequestedLineDto Line,decimal Value,decimal NewValue,int NewQty)>();
        foreach(var line in lines)
        {
            int purchased,returned;decimal refunded;
            await using(var command=NewCommand(connection,tx,"""
                SELECT c.SoLuong,COALESCE((SELECT SUM(d.SoLuong) FROM ChiTietDoiTra d JOIN PhieuDoiTra p ON p.SoPhieuDT=d.SoPhieuDT WHERE p.SoHD=c.SoHD AND d.MaSKU=c.MaSKU AND p.TrangThai='HOAN_TAT'),0),
                       COALESCE((SELECT SUM(d.ThanhTienNhanLai) FROM ChiTietDoiTra d JOIN PhieuDoiTra p ON p.SoPhieuDT=d.SoPhieuDT WHERE p.SoHD=c.SoHD AND d.MaSKU=c.MaSKU AND p.TrangThai='HOAN_TAT'),0)
                FROM ChiTietHoaDon c WHERE c.SoHD=@invoice AND c.MaSKU=@sku
                """,("@invoice",invoice),("@sku",line.Sku)))
            await using(var reader=await command.ExecuteReaderAsync())
            {
                if(!await reader.ReadAsync())throw new InvalidOperationException($"SKU {line.Sku} không thuộc hóa đơn.");
                purchased=reader.GetInt32(0);returned=Convert.ToInt32(reader.GetInt64(1));refunded=reader.GetDecimal(2);
            }
            if(line.Qty>purchased-returned)throw new InvalidOperationException($"SKU {line.Sku} vượt số lượng còn được đổi trả.");
            var value=RefundForQuantity(paid[line.Sku],purchased,returned,refunded,line.Qty);
            decimal newValue=0;var newQty=0;
            if(line.Resolution=="DOI_SAN_PHAM")
            {
                if(string.IsNullOrWhiteSpace(line.ReplacementSku))throw new InvalidOperationException("Yêu cầu đổi hàng chưa có mã SKU muốn nhận.");
                newQty=line.ReplacementQty>0?line.ReplacementQty:line.Qty;
                var price=await EffectivePriceAsync(connection,tx,line.ReplacementSku);
                newValue=RoundMoney(price*newQty*1.08m);
                var stock=await ScalarLongAsync(connection,tx,"SELECT SoLuongTonKho FROM SanPham WHERE MaSKU=@sku AND TrangThai=TRUE",("@sku",line.ReplacementSku));
                var alreadyUsed=calculated.Where(x=>x.Line.ReplacementSku==line.ReplacementSku).Sum(x=>x.NewQty);
                if(stock<newQty+alreadyUsed)throw new InvalidOperationException($"SKU đổi mới {line.ReplacementSku} không đủ tồn kho.");
            }
            calculated.Add((line,value,newValue,newQty));
        }
        var total=calculated.Sum(x=>x.Value);var newTotal=calculated.Sum(x=>x.NewValue);var refund=Math.Max(0,total-newTotal);var extra=Math.Max(0,newTotal-total);
        var id=requestId??("PDT-"+DateTime.Now.ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N")[..10]);
        var resolution=lines.Select(l=>l.Resolution).Distinct().Count()==1?lines[0].Resolution:"DOI_VA_TRA";
        if(requestId is null)
            await ExecuteAsync(connection,tx,"""
                INSERT INTO PhieuDoiTra(SoPhieuDT,NgayLap,HinhThucXuLy,SoTienHoan,SoHD,MaNV,NguonDon,NgayMocTinh,GiaTriHangNhanLai,GiaTriHangDoiMoi,SoTienThuThem,TrangThai,MaYeuCau,DauVanTayYeuCau)
                VALUES(@id,CURRENT_TIMESTAMP,@resolution,@refund,@invoice,@employee,@source,@anchor,@value,@newValue,@extra,'HOAN_TAT',@key,@digest)
                """,("@id",id),("@resolution",resolution),("@refund",refund),("@invoice",invoice),("@employee",employee),("@source",MapInvoiceSource(source)),("@anchor",eligibility.AnchorDate),
                ("@value",total),("@newValue",newTotal),("@extra",extra),("@key",DbValue(key)),("@digest",DbValue(digest)));
        else
            await ExecuteAsync(connection,tx,"UPDATE PhieuDoiTra SET TrangThai='HOAN_TAT',MaNV=@employee,HinhThucXuLy=@resolution,SoTienHoan=@refund,SoTienThuThem=@extra,GiaTriHangNhanLai=@value,GiaTriHangDoiMoi=@newValue,NgayMocTinh=@anchor WHERE SoPhieuDT=@id",
                ("@id",id),("@employee",employee),("@resolution",resolution),("@refund",refund),("@extra",extra),("@value",total),("@newValue",newTotal),("@anchor",eligibility.AnchorDate));
        foreach(var c in calculated)
        {
            await ExecuteAsync(connection,tx,"""
                INSERT INTO ChiTietDoiTra(SoPhieuDT,MaSKU,SoLuong,LoaiGiaoDich,TinhTrang,LyDoTra,MaSKUDoi,SoLuongDoi,DonGiaNhanLai,DonGiaDoi,ThanhTienNhanLai,ThanhTienDoi)
                VALUES(@id,@sku,@qty,@kind,'Đã tiếp nhận',@reason,@newSku,@newQty,@price,@newPrice,@value,@newValue)
                ON CONFLICT(SoPhieuDT,MaSKU) DO UPDATE SET TinhTrang=EXCLUDED.TinhTrang,DonGiaNhanLai=EXCLUDED.DonGiaNhanLai,DonGiaDoi=EXCLUDED.DonGiaDoi,
                    ThanhTienNhanLai=EXCLUDED.ThanhTienNhanLai,ThanhTienDoi=EXCLUDED.ThanhTienDoi,SoLuongDoi=EXCLUDED.SoLuongDoi
                """,("@id",id),("@sku",c.Line.Sku),("@qty",c.Line.Qty),("@kind",c.Line.Resolution),("@reason",c.Line.Reason),("@newSku",c.NewQty>0?DbValue(c.Line.ReplacementSku):DBNull.Value),
                ("@newQty",c.NewQty),("@price",c.Value/c.Line.Qty),("@newPrice",c.NewQty==0?0:c.NewValue/c.NewQty),("@value",c.Value),("@newValue",c.NewValue));
            if(c.NewQty>0)await ExecuteAsync(connection,tx,"UPDATE SanPham SET SoLuongTonKho=SoLuongTonKho-@qty,SoLuong=SoLuong-@qty WHERE MaSKU=@sku",("@qty",c.NewQty),("@sku",c.Line.ReplacementSku!));
        }
        if(customer is not null)
            await ExecuteAsync(connection,tx,"""
                UPDATE KhachHang SET TongChiTieu=GREATEST(0,TongChiTieu+@delta),DiemTichLuy=FLOOR(GREATEST(0,TongChiTieu+@delta)/10000)::int,
                  HangThe=CASE WHEN TongChiTieu+@delta>15000000 THEN 'Kim Cương' WHEN TongChiTieu+@delta>5000000 THEN 'Vàng' ELSE 'Bạc' END WHERE MaKH=@id
                """,("@id",customer),("@delta",newTotal-total));
        await tx.CommitAsync();return(await GetReturnAsync(id))!;
    }

    private static async Task<List<ReturnReceiptLineDto>> ReturnReceiptLinesAsync(NpgsqlConnection connection,string id)
    {
        var result=new List<ReturnReceiptLineDto>();
        await using var command=NewCommand(connection,null,"""
            SELECT c.MaSKU,s.TenSanPham,s.MauSac,s.KichCo,c.SoLuong,c.ThanhTienNhanLai,c.MaSKUDoi,n.TenSanPham,c.ThanhTienDoi,COALESCE(c.LyDoTra,''),c.SoLuongDoi
            FROM ChiTietDoiTra c JOIN SanPham s ON s.MaSKU=c.MaSKU LEFT JOIN SanPham n ON n.MaSKU=c.MaSKUDoi WHERE c.SoPhieuDT=@id ORDER BY c.MaSKU
            """,("@id",id));
        await using var reader=await command.ExecuteReaderAsync();
        while(await reader.ReadAsync())result.Add(new(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4),reader.GetDecimal(5),reader.IsDBNull(6)?null:reader.GetString(6),reader.IsDBNull(7)?null:reader.GetString(7),reader.GetDecimal(8),reader.GetString(9),reader.GetInt32(10)));
        return result;
    }
}
