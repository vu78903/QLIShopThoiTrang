using FashionStore.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Data;

public sealed class FashionStoreDbContext(DbContextOptions<FashionStoreDbContext> options) : DbContext(options)
{
    public DbSet<SanPham> Products => Set<SanPham>();
    public DbSet<LoaiSanPham> Categories => Set<LoaiSanPham>();
    public DbSet<BienTheSanPham> Variants => Set<BienTheSanPham>();
    public DbSet<HinhAnhSanPham> Images => Set<HinhAnhSanPham>();
    public DbSet<KhachHang> Customers => Set<KhachHang>();
    public DbSet<TaiKhoanKhachHang> CustomerAccounts => Set<TaiKhoanKhachHang>();
    public DbSet<GioHang> Carts => Set<GioHang>();
    public DbSet<ChiTietGioHang> CartLines => Set<ChiTietGioHang>();
    public DbSet<NhanVien> Employees => Set<NhanVien>();
    public DbSet<PhanQuyen> Permissions => Set<PhanQuyen>();
    public DbSet<HoaDonAdmin> AdminOrders => Set<HoaDonAdmin>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        if (Database.IsNpgsql()) { CustomerPostgresMapping.Configure(model); return; }
        model.Entity<KhachHang>().Ignore(c => c.PgVersion);
        var product = model.Entity<SanPham>();
        product.ToTable("SanPham", "dbo");
        product.HasKey(p => p.MaSP);
        product.Property(p => p.MaSP).HasMaxLength(20).IsUnicode(false);
        product.Property(p => p.MaLoai).HasMaxLength(20).IsUnicode(false);
        product.Property(p => p.TenSanPham).HasMaxLength(200);
        product.Property(p => p.MoTa).HasColumnType("nvarchar(max)");
        product.Property(p => p.DonGiaNiemYet).HasPrecision(18, 2);
        product.Property(p => p.DoiTuong).HasMaxLength(10).IsUnicode(false);
        product.Property(p => p.NgayTao).HasColumnType("datetime2(0)");
        product.HasOne(p => p.Loai).WithMany().HasForeignKey(p => p.MaLoai).OnDelete(DeleteBehavior.NoAction);
        product.HasMany(p => p.Variants).WithOne().HasForeignKey(v => v.MaSP).OnDelete(DeleteBehavior.NoAction);
        product.HasMany(p => p.Images).WithOne().HasForeignKey(i => i.MaSP).OnDelete(DeleteBehavior.NoAction);

        var category = model.Entity<LoaiSanPham>();
        category.ToTable("LoaiSanPham", "dbo");
        category.HasKey(c => c.MaLoai);
        category.Property(c => c.MaLoai).HasMaxLength(20).IsUnicode(false);
        category.Property(c => c.TenLoai).HasMaxLength(100);

        var variant = model.Entity<BienTheSanPham>();
        variant.ToTable("BienTheSanPham", "dbo");
        variant.HasKey(v => v.MaSKU);
        variant.Property(v => v.MaSKU).HasMaxLength(40).IsUnicode(false);
        variant.Property(v => v.MaSP).HasMaxLength(20).IsUnicode(false);
        variant.Property(v => v.KichCo).HasMaxLength(30);
        variant.Property(v => v.MauSac).HasMaxLength(60);
        variant.Property(v => v.DonGia).HasPrecision(18, 2);

        var image = model.Entity<HinhAnhSanPham>();
        image.ToTable("HinhAnhSanPham", "dbo");
        image.HasKey(i => i.MaHinh);
        image.Property(i => i.MaSP).HasMaxLength(20).IsUnicode(false);
        image.Property(i => i.MaSKU).HasMaxLength(40).IsUnicode(false);
        image.Property(i => i.DuongDan).HasMaxLength(500);

        var permission = model.Entity<PhanQuyen>();
        permission.ToTable("PhanQuyen", "dbo");
        permission.HasKey(p => p.MaQuyen);
        permission.Property(p => p.MaQuyen).HasMaxLength(20).IsUnicode(false);
        permission.Property(p => p.TenQuyen).HasMaxLength(100);

        var employee = model.Entity<NhanVien>();
        employee.ToTable("NhanVien", "dbo");
        employee.HasKey(e => e.MaNV);
        employee.Property(e => e.MaNV).HasMaxLength(20).IsUnicode(false);
        employee.Property(e => e.MaQuyen).HasMaxLength(20).IsUnicode(false);
        employee.Property(e => e.HoTen).HasMaxLength(150);
        employee.Property(e => e.ChucVu).HasMaxLength(100);
        employee.Property(e => e.MatKhauHash).HasMaxLength(500);
        employee.HasOne(e => e.Quyen).WithMany().HasForeignKey(e => e.MaQuyen).OnDelete(DeleteBehavior.NoAction);

        var adminOrder = model.Entity<HoaDonAdmin>();
        adminOrder.ToTable("HoaDon", "dbo");
        adminOrder.HasKey(o => o.SoHD);
        adminOrder.Property(o => o.SoHD).HasMaxLength(30).IsUnicode(false);
        adminOrder.Property(o => o.LoaiHoaDon).HasMaxLength(10).IsUnicode(false);
        adminOrder.Property(o => o.TrangThaiHoaDon).HasMaxLength(20).IsUnicode(false);
        adminOrder.Property(o => o.NgayLap).HasColumnType("datetime2(0)");

        // Exact mapping from the user's 2026-10-15 DEV metadata export.
        var customer = model.Entity<KhachHang>();
        customer.ToTable("KhachHang", "dbo");
        customer.HasKey(c => c.MaKH);
        customer.Property(c => c.MaKH).HasMaxLength(20).IsUnicode(false);
        customer.Property(c => c.HoTen).HasMaxLength(150);
        customer.Property(c => c.SoDienThoai).HasMaxLength(20).IsUnicode(false);
        customer.Property(c => c.Email).HasMaxLength(254).IsUnicode(false);
        customer.Property(c => c.DiaChi).HasMaxLength(300);
        customer.Property(c => c.HangThe).HasMaxLength(30);
        customer.Property(c => c.NgayTao).HasColumnType("datetime2(0)");
        customer.Property(c => c.RowVer).IsRowVersion();
        customer.HasIndex(c => c.SoDienThoai).IsUnique();
        customer.HasIndex(c => c.Email).IsUnique().HasFilter("[Email] IS NOT NULL");

        var account = model.Entity<TaiKhoanKhachHang>();
        account.ToTable("TaiKhoanKhachHang", "dbo");
        account.HasKey(a => a.MaKH);
        account.Property(a => a.MaKH).HasMaxLength(20).IsUnicode(false);
        account.Property(a => a.TenDangNhap).HasMaxLength(100);
        account.Property(a => a.MatKhauHash).HasMaxLength(500);
        account.Property(a => a.NgayTao).HasColumnType("datetime2(0)");
        account.Property(a => a.DangNhapCuoi).HasColumnType("datetime2(0)");
        account.HasIndex(a => a.TenDangNhap).IsUnique();
        account.HasOne(a => a.Customer).WithOne().HasForeignKey<TaiKhoanKhachHang>(a => a.MaKH).OnDelete(DeleteBehavior.NoAction);

        var cart = model.Entity<GioHang>();
        cart.ToTable("GioHang", "dbo");
        cart.HasKey(c => c.MaGioHang);
        cart.Property(c => c.MaGioHang).UseIdentityColumn();
        cart.Property(c => c.MaKH).HasMaxLength(20).IsUnicode(false);
        cart.Property(c => c.NgayTao).HasColumnType("datetime2(0)");
        cart.Property(c => c.NgayCapNhat).HasColumnType("datetime2(0)");
        cart.HasIndex(c => c.MaKH).IsUnique();
        cart.HasOne(c => c.Customer).WithOne().HasForeignKey<GioHang>(c => c.MaKH).OnDelete(DeleteBehavior.NoAction);
        cart.HasMany(c => c.Lines).WithOne(l => l.Cart).HasForeignKey(l => l.MaGioHang).OnDelete(DeleteBehavior.Cascade);

        var line = model.Entity<ChiTietGioHang>();
        line.ToTable("ChiTietGioHang", "dbo");
        line.HasKey(l => new { l.MaGioHang, l.MaSKU });
        line.Property(l => l.MaSKU).HasMaxLength(40).IsUnicode(false);
        line.Property(l => l.NgayCapNhat).HasColumnType("datetime2(0)");
        line.HasOne(l => l.Variant).WithMany().HasForeignKey(l => l.MaSKU).OnDelete(DeleteBehavior.NoAction);
    }
}
