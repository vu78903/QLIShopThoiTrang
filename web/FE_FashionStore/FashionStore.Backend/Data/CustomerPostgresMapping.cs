using FashionStore.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Data;

internal static class CustomerPostgresMapping
{
    internal static void Configure(ModelBuilder model)
    {
        model.Ignore<SanPham>(); model.Ignore<LoaiSanPham>(); model.Ignore<BienTheSanPham>();
        model.Ignore<HinhAnhSanPham>(); model.Ignore<NhanVien>(); model.Ignore<PhanQuyen>(); model.Ignore<HoaDonAdmin>();
        var customer = model.Entity<KhachHang>();
        customer.ToTable("khachhang", "public");
        customer.HasKey(c => c.MaKH);
        customer.Ignore(c => c.RowVer); customer.Ignore(c => c.NgayTao);
        customer.Property(c => c.PgVersion).IsRowVersion().HasColumnName("xmin");
        var account = model.Entity<TaiKhoanKhachHang>();
        account.ToTable("customer_account", "webstore"); account.HasKey(a => a.MaKH);
        account.HasIndex(a => a.TenDangNhap).IsUnique();
        account.HasOne(a => a.Customer).WithOne().HasForeignKey<TaiKhoanKhachHang>(a => a.MaKH).OnDelete(DeleteBehavior.Restrict);
        var cart = model.Entity<GioHang>();
        cart.ToTable("cart", "webstore"); cart.HasKey(c => c.MaGioHang);
        cart.Property(c => c.MaGioHang).UseIdentityByDefaultColumn();
        cart.HasIndex(c => c.MaKH).IsUnique();
        cart.HasOne(c => c.Customer).WithOne().HasForeignKey<GioHang>(c => c.MaKH).OnDelete(DeleteBehavior.Restrict);
        cart.HasMany(c => c.Lines).WithOne(l => l.Cart).HasForeignKey(l => l.MaGioHang).OnDelete(DeleteBehavior.Cascade);
        var line = model.Entity<ChiTietGioHang>();
        line.ToTable("cart_line", "webstore"); line.HasKey(l => new { l.MaGioHang, l.MaSKU });
        line.Ignore(l => l.Variant);
        var product = model.Entity<SupabaseProduct>();
        product.ToTable("sanpham", "public"); product.HasKey(p => p.masku);
        product.Property(p => p.dongia).HasPrecision(18, 2);
        product.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.maloai);
        var category = model.Entity<SupabaseCategory>();
        category.ToTable("loaisanpham", "public"); category.HasKey(c => c.maloai);
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
            {
                if (property.Name != nameof(KhachHang.PgVersion)) property.SetColumnName(property.Name.ToLowerInvariant());
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetColumnType("timestamp without time zone");
            }
    }
}
