using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Data;

// Read mapping: current data has multiple SKU rows per masp. Never migrates remote tables.
public sealed class SupabaseCatalogDbContext(DbContextOptions<SupabaseCatalogDbContext> options) : DbContext(options)
{
    public DbSet<SupabaseProduct> Products => Set<SupabaseProduct>();
    public DbSet<SupabaseCategory> Categories => Set<SupabaseCategory>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var p = model.Entity<SupabaseProduct>();
        p.ToTable("sanpham", "public");
        p.HasKey(x => x.masku);
        p.Property(x => x.dongia).HasPrecision(18, 2);
        p.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.maloai);
        var c = model.Entity<SupabaseCategory>();
        c.ToTable("loaisanpham", "public");
        c.HasKey(x => x.maloai);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("Supabase catalog is read-only during migration.");
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Supabase catalog is read-only during migration.");
}

public sealed class SupabaseProduct
{
    public string masp { get; set; } = "";
    public string masku { get; set; } = "";
    public string maloai { get; set; } = "";
    public string tensanpham { get; set; } = "";
    public string? danhmuc { get; set; }
    public string? kichco { get; set; }
    public string? mausac { get; set; }
    public decimal dongia { get; set; }
    public int soluongtonkho { get; set; }
    public bool trangthai { get; set; }
    public string? hinhanh { get; set; }
    public SupabaseCategory Category { get; set; } = null!;
}

public sealed class SupabaseCategory
{
    public string maloai { get; set; } = "";
    public string tenloai { get; set; } = "";
    public bool trangthai { get; set; }
}

