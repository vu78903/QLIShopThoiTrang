namespace FashionStore.Backend.Models;

// Maps only the columns needed for administration. Existing SQL schema is unchanged.
public sealed class NhanVien
{
    public string MaNV { get; set; } = "";
    public string MaQuyen { get; set; } = "";
    public string HoTen { get; set; } = "";
    public string? ChucVu { get; set; }
    public string? MatKhauHash { get; set; }
    public bool TrangThai { get; set; }
    public PhanQuyen Quyen { get; set; } = null!;
}
public sealed class PhanQuyen
{
    public string MaQuyen { get; set; } = "";
    public string TenQuyen { get; set; } = "";
    public bool TrangThai { get; set; }
}
public sealed class HoaDonAdmin
{
    public string SoHD { get; set; } = "";
    public DateTime NgayLap { get; set; }
    public string LoaiHoaDon { get; set; } = "";
    public string TrangThaiHoaDon { get; set; } = "";
}
