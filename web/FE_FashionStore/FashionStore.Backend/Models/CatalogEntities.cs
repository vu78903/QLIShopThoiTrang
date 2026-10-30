namespace FashionStore.Backend.Models;

// Catalog subset of the existing SQL schema. No database creation/migration.
public sealed class SanPham
{
    public string MaSP { get; set; } = "";
    public string MaLoai { get; set; } = "";
    public string TenSanPham { get; set; } = "";
    public string? MoTa { get; set; }
    public decimal DonGiaNiemYet { get; set; }
    public string? DoiTuong { get; set; }
    public bool TrangThai { get; set; }
    public DateTime NgayTao { get; set; }
    public LoaiSanPham Loai { get; set; } = null!;
    public ICollection<BienTheSanPham> Variants { get; set; } = new List<BienTheSanPham>();
    public ICollection<HinhAnhSanPham> Images { get; set; } = new List<HinhAnhSanPham>();
}
public sealed class LoaiSanPham
{
    public string MaLoai { get; set; } = "";
    public string TenLoai { get; set; } = "";
    public bool TrangThai { get; set; }
}
public sealed class BienTheSanPham
{
    public string MaSKU { get; set; } = "";
    public string MaSP { get; set; } = "";
    public string KichCo { get; set; } = "";
    public string MauSac { get; set; } = "";
    public decimal DonGia { get; set; }
    public int SoLuongTonKho { get; set; }
    public bool TrangThai { get; set; }
}
public sealed class HinhAnhSanPham
{
    public long MaHinh { get; set; }
    public string MaSP { get; set; } = "";
    public string? MaSKU { get; set; }
    public string DuongDan { get; set; } = "";
    public bool AnhChinh { get; set; }
    public int ThuTu { get; set; }
}
