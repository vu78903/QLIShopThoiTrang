namespace FashionStore.Backend.Models;

public sealed class KhachHang
{
    public string MaKH { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string SoDienThoai { get; set; } = "";
    public string? Email { get; set; }
    public string? DiaChi { get; set; }
    public int DiemTichLuy { get; set; }
    public string HangThe { get; set; } = "Bạc";
    public bool NhanKhuyenMai { get; set; }
    public bool TrangThai { get; set; } = true;
    public DateTime NgayTao { get; set; }
    public byte[] RowVer { get; set; } = [];
    public uint PgVersion { get; set; }
}
public sealed class TaiKhoanKhachHang
{
    public string MaKH { get; set; } = "";
    public string TenDangNhap { get; set; } = "";
    public string MatKhauHash { get; set; } = "";
    public bool TrangThai { get; set; } = true;
    public DateTime NgayTao { get; set; }
    public DateTime? DangNhapCuoi { get; set; }
    public KhachHang Customer { get; set; } = null!;
}
public sealed class GioHang
{
    public long MaGioHang { get; set; }
    public string MaKH { get; set; } = "";
    public DateTime NgayTao { get; set; }
    public DateTime NgayCapNhat { get; set; }
    public KhachHang Customer { get; set; } = null!;
    public ICollection<ChiTietGioHang> Lines { get; set; } = new List<ChiTietGioHang>();
}
public sealed class ChiTietGioHang
{
    public long MaGioHang { get; set; }
    public string MaSKU { get; set; } = "";
    public int SoLuong { get; set; }
    public DateTime NgayCapNhat { get; set; }
    public GioHang Cart { get; set; } = null!;
    public BienTheSanPham Variant { get; set; } = null!;
}
