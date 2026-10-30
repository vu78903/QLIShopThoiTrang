using System.Data;
using FashionStore.Backend.Data;
using FashionStore.Backend.Middleware;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Services;

// All SQL is fixed application text, values are parameters. No table/column comes from the caller.
public sealed class CustomerFeaturesService(FashionStoreDbContext db)
{
    internal SqlConnection Connection()
    {
        var b = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (!string.Equals(b.InitialCatalog,"FashionStoreDB_Dev",StringComparison.OrdinalIgnoreCase))
            throw new CustomerRequestException(503,"Chức năng chỉ bật trên FashionStoreDB_Dev.");
        return new SqlConnection(b.ConnectionString);
    }
    internal static SqlCommand Command(SqlConnection c, SqlTransaction? tx, string sql, params (string,object?)[] args)
    {
        var cmd = new SqlCommand(sql,c,tx) { CommandTimeout=15 };
        foreach(var (k,v) in args) cmd.Parameters.AddWithValue(k,v ?? DBNull.Value);
        return cmd;
    }
    public async Task<List<Dictionary<string,object?>>> Query(string sql, CancellationToken ct, params (string,object?)[] args)
    {
        await using var c=Connection(); await c.OpenAsync(ct);
        await using var cmd=Command(c,null,sql,args); await using var rd=await cmd.ExecuteReaderAsync(ct);
        var rows=new List<Dictionary<string,object?>>();
        while(await rd.ReadAsync(ct)) { var row=new Dictionary<string,object?>(); for(var i=0;i<rd.FieldCount;i++) row[rd.GetName(i)]=rd.IsDBNull(i)?null:rd.GetValue(i); rows.Add(row); }
        return rows;
    }
    public Task<List<Dictionary<string,object?>>> Promotions(CancellationToken ct) => Query(@"SELECT MaKM,TenChuongTrinh,LoaiKM,GiaTriGiam,GiaTriDonHangToiThieu,NgayKetThuc,SoLuotToiDa,SoLuotDaDung FROM dbo.KhuyenMai WHERE TrangThai=1 AND SYSDATETIME() BETWEEN NgayBatDau AND NgayKetThuc AND (SoLuotToiDa IS NULL OR SoLuotDaDung<SoLuotToiDa) ORDER BY NgayKetThuc,MaKM",ct);
    public Task<List<Dictionary<string,object?>>> Points(string kh,int page,CancellationToken ct) => Query(@"SELECT SoHD,LoaiGiaoDich,SoDiemThayDoi,ThoiDiem,GhiChu FROM dbo.LichSuDiem WHERE MaKH=@kh ORDER BY ThoiDiem DESC,MaGiaoDichDiem DESC OFFSET @skip ROWS FETCH NEXT 20 ROWS ONLY",ct,("@kh",kh),("@skip",(page-1)*20));
    public Task<List<Dictionary<string,object?>>> History(string kh,string id,CancellationToken ct) => Query(@"SELECT l.LoaiTrangThai,l.TrangThaiMoi,l.ThoiDiem,l.GhiChu FROM dbo.LichSuTrangThaiDonHang l JOIN dbo.HoaDon h ON h.SoHD=l.SoHD WHERE h.MaKH=@kh AND h.SoHD=@id AND h.LoaiHoaDon='ONLINE' ORDER BY l.ThoiDiem,l.MaLichSu",ct,("@kh",kh),("@id",id));
    public Task<List<Dictionary<string,object?>>> Reviews(string id,int page,CancellationToken ct) => Query(@"SELECT d.SoSao,d.NoiDung,d.NgayDanhGia,N'Khách hàng' AS TenHienThi FROM dbo.DanhGiaSanPham d JOIN dbo.SanPham p ON p.MaSP=d.MaSP WHERE d.MaSP=@id AND d.TrangThai='HIEN_THI' AND p.TrangThai=1 ORDER BY d.NgayDanhGia DESC,d.MaDanhGia DESC OFFSET @skip ROWS FETCH NEXT 10 ROWS ONLY",ct,("@id",id),("@skip",(page-1)*10));
    public Task<List<Dictionary<string,object?>>> Returns(string kh,int page,CancellationToken ct) => Query(@"SELECT p.SoPhieuDT,p.SoHD,p.NgayLap,p.HinhThucXuLy,p.TrangThai,p.LyDo,p.LyDoTuChoi FROM dbo.PhieuDoiTra p JOIN dbo.HoaDon h ON h.SoHD=p.SoHD WHERE h.MaKH=@kh ORDER BY p.NgayLap DESC,p.SoPhieuDT DESC OFFSET @skip ROWS FETCH NEXT 20 ROWS ONLY",ct,("@kh",kh),("@skip",(page-1)*20));
    public Task<List<Dictionary<string,object?>>> Wishlist(string kh,CancellationToken ct) => Query(@"SELECT p.MaSP,p.TenSanPham,p.TrangThai FROM dbo.WebWishlist w JOIN dbo.SanPham p ON p.MaSP=w.MaSP WHERE w.MaKH=@kh ORDER BY w.CreatedAt DESC,p.MaSP",ct,("@kh",kh));
    public Task<List<Dictionary<string,object?>>> Addresses(string kh,CancellationToken ct) => Query("SELECT Id,Recipient,Phone,Address FROM dbo.WebAddress WHERE MaKH=@kh ORDER BY Id DESC",ct,("@kh",kh));
    public async Task SaveWishlist(string kh,string id,bool remove,CancellationToken ct)
    {
        await using var c=Connection();await c.OpenAsync(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var cmd=Command(c,tx,remove?"DELETE dbo.WebWishlist WHERE MaKH=@kh AND MaSP=@id":@"IF NOT EXISTS(SELECT 1 FROM dbo.SanPham WHERE MaSP=@id AND TrangThai=1) THROW 55201,N'Sản phẩm không khả dụng.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.WebWishlist WITH(UPDLOCK,HOLDLOCK) WHERE MaKH=@kh AND MaSP=@id) INSERT dbo.WebWishlist(MaKH,MaSP) VALUES(@kh,@id)",("@kh",kh),("@id",id));
        await cmd.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task SaveAddress(string kh, AddressInput input,CancellationToken ct)
    {
        await using var c=Connection();await c.OpenAsync(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var cmd=Command(c,tx,@"IF (SELECT COUNT(*) FROM dbo.WebAddress WITH(UPDLOCK,HOLDLOCK) WHERE MaKH=@kh)>=20 THROW 55202,N'Tối đa 20 địa chỉ.',1;
INSERT dbo.WebAddress(MaKH,Recipient,Phone,Address) VALUES(@kh,@name,@phone,@address)",("@kh",kh),("@name",input.Recipient.Trim()),("@phone",input.Phone),("@address",input.Address.Trim()));
        await cmd.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task RemoveAddress(string kh,long id,CancellationToken ct)
    {
        await using var c=Connection();await c.OpenAsync(ct);await using var cmd=Command(c,null,"DELETE dbo.WebAddress WHERE MaKH=@kh AND Id=@id",("@kh",kh),("@id",id));await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task Review(string kh,ReviewInput input,CancellationToken ct)
    {
        await using var c=Connection(); await c.OpenAsync(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await using var cmd=Command(c,tx,@"
IF NOT EXISTS(SELECT 1 FROM dbo.HoaDon h JOIN dbo.ChiTietHoaDon ct ON h.SoHD=ct.SoHD JOIN dbo.BienTheSanPham b ON b.MaSKU=ct.MaSKU WHERE h.MaKH=@kh AND h.SoHD=@hd AND b.MaSP=@sp AND h.TrangThaiHoaDon='HOAN_TAT') THROW 55203,N'Chỉ đánh giá sản phẩm trong đơn đã hoàn tất của bạn.',1;
IF EXISTS(SELECT 1 FROM dbo.DanhGiaSanPham WITH(UPDLOCK,HOLDLOCK) WHERE MaKH=@kh AND MaSP=@sp AND SoHD=@hd) THROW 55204,N'Bạn đã gửi đánh giá cho sản phẩm trong đơn này.',1;
INSERT dbo.DanhGiaSanPham(MaKH,MaSP,SoHD,SoSao,NoiDung) VALUES(@kh,@sp,@hd,@stars,@body)",("@kh",kh),("@sp",input.ProductId),("@hd",input.OrderId),("@stars",input.Stars),("@body",input.Body.Trim()));
        await cmd.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    }
    public async Task CreateReturn(string kh,ReturnInput input,CancellationToken ct)
    {
        if(input.RequestId==Guid.Empty)throw new CustomerRequestException(400,"Vui lòng tải lại biểu mẫu đổi trả.");
        await using var c=Connection();await c.OpenAsync(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        // Lock invoice first, serializing concurrent requests for any SKU of this invoice.
        await using var cmd=Command(c,tx,@"
IF NOT EXISTS(SELECT 1 FROM dbo.HoaDon WITH(UPDLOCK,HOLDLOCK) WHERE SoHD=@hd AND MaKH=@kh AND TrangThaiHoaDon='HOAN_TAT' AND TrangThaiGiaoHang='DA_GIAO') THROW 55205,N'Đơn chưa hoàn tất giao hàng hoặc không thuộc tài khoản.',1;
IF EXISTS(SELECT 1 FROM dbo.PhieuDoiTra WITH(UPDLOCK,HOLDLOCK) WHERE SoPhieuDT=@id) BEGIN
 IF EXISTS(SELECT 1 FROM dbo.PhieuDoiTra WHERE SoPhieuDT=@id AND SoHD=@hd) RETURN;
 THROW 55209,N'Mã yêu cầu bị trùng. Vui lòng tải lại.',1; END;
DECLARE @delivered datetime2=(SELECT MIN(ThoiDiem) FROM dbo.LichSuTrangThaiDonHang WHERE SoHD=@hd AND LoaiTrangThai='GIAO_HANG' AND TrangThaiMoi='DA_GIAO');
IF @delivered IS NULL OR @delivered>SYSDATETIME() OR SYSDATETIME()>DATEADD(day,15,@delivered) THROW 55206,N'Đơn quá 15 ngày hoặc chưa có mốc giao hàng xác thực. Vui lòng liên hệ cửa hàng.',1;
DECLARE @bought int=(SELECT SoLuong FROM dbo.ChiTietHoaDon WHERE SoHD=@hd AND MaSKU=@sku);
DECLARE @used bigint=(SELECT COALESCE(SUM(CAST(d.SoLuong AS bigint)),0) FROM dbo.ChiTietDoiTra d JOIN dbo.PhieuDoiTra p ON p.SoPhieuDT=d.SoPhieuDT WHERE p.SoHD=@hd AND d.MaSKU=@sku AND p.TrangThai<>'TU_CHOI');
IF @bought IS NULL OR @qty>@bought-@used THROW 55207,N'Số lượng đổi/trả vượt số lượng còn hợp lệ.',1;
DECLARE @price decimal(18,2)=NULL;
IF @kind='DOI' BEGIN
 SELECT @price=b.DonGia FROM dbo.BienTheSanPham b JOIN dbo.SanPham p ON p.MaSP=b.MaSP WHERE b.MaSKU=@newSku AND b.TrangThai=1 AND p.TrangThai=1 AND b.SoLuongTonKho>=@qty;
 IF @price IS NULL THROW 55208,N'SKU đổi mới không hợp lệ hoặc thiếu tồn kho.',1;
END;
INSERT dbo.PhieuDoiTra(SoPhieuDT,SoHD,HinhThucXuLy,LyDo) VALUES(@id,@hd,@kind,@reason);
INSERT dbo.ChiTietDoiTra(SoPhieuDT,MaSKU,SoLuong,LoaiGiaoDich,LyDoTra,MaSKUMoi,SoLuongMoi,GiaTriHangDoi)
VALUES(@id,@sku,@qty,@kind,@reason,CASE WHEN @kind='DOI' THEN @newSku END,CASE WHEN @kind='DOI' THEN @qty END,@price*@qty);",
("@kh",kh),("@hd",input.OrderId),("@sku",input.Sku),("@qty",input.Quantity),("@kind",input.Kind),("@reason",input.Reason.Trim()),("@newSku",input.NewSku ?? ""),("@id","DT"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input.RequestId.ToByteArray())).Substring(0,28)));
        await cmd.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    }
}
public sealed class AddressInput
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(150,MinimumLength=2)] public string Recipient {get;set;}="";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.RegularExpression(@"^\+?[0-9]{9,15}$")] public string Phone {get;set;}="";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(300,MinimumLength=10)] public string Address {get;set;}="";
}
public sealed class ReviewInput
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(20)] public string ProductId {get;set;}="";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(30)] public string OrderId {get;set;}="";
    [System.ComponentModel.DataAnnotations.Range(1,5)] public int Stars {get;set;}
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(2000)] public string Body {get;set;}="";
}
public sealed class ReturnInput
{
    public Guid RequestId {get;set;}
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(30)] public string OrderId {get;set;}="";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(40)] public string Sku {get;set;}="";
    [System.ComponentModel.DataAnnotations.Range(1,100000)] public int Quantity {get;set;}
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.RegularExpression("^(DOI|TRA)$")] public string Kind {get;set;}="TRA";
    [System.ComponentModel.DataAnnotations.StringLength(40)] public string? NewSku {get;set;}
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(500,MinimumLength=5)] public string Reason {get;set;}="";
}
