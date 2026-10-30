using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Services;

public sealed class SupabaseCatalogService(SupabaseCatalogDbContext db) : ICatalogService
{
    private IQueryable<SupabaseProduct> Visible() => db.Products.AsNoTracking()
        .Where(p => p.trangthai && p.Category.trangthai);

    internal IQueryable<SupabaseProduct> Filter(ProductQuery query)
    {
        var products = Visible();
        if (!string.IsNullOrWhiteSpace(query.Audience))
            products = products.Where(p => p.danhmuc == query.Audience);
        var text = query.Q?.Trim();
        if (!string.IsNullOrEmpty(text))
            products = products.Where(p => p.tensanpham.ToLower().Contains(text.ToLower()) || p.masp.ToLower().Contains(text.ToLower()));
        if (!string.IsNullOrEmpty(query.Category)) products = products.Where(p => p.maloai == query.Category);
        if (!string.IsNullOrEmpty(query.Size)) products = products.Where(p => p.kichco == query.Size);
        if (!string.IsNullOrEmpty(query.Color)) products = products.Where(p => p.mausac == query.Color);
        if (query.InStock) products = products.Where(p => p.soluongtonkho > 0);
        if (query.MinPrice.HasValue) products = products.Where(p => p.dongia >= query.MinPrice.Value);
        if (query.MaxPrice.HasValue) products = products.Where(p => p.dongia <= query.MaxPrice.Value);
        return products;
    }

    internal sealed class ProductGroup
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public decimal Price { get; init; }
    }

    internal IQueryable<ProductGroup> Page(ProductQuery query)
    {
        // One SKU must satisfy every filter. Aggregate all visible SKUs for matching products,
        // matching the existing SQL Server catalog's price/stock semantics.
        var matchingIds = Filter(query).Select(p => p.masp).Distinct();
        var products = Visible().Where(p => matchingIds.Contains(p.masp))
            .GroupBy(p => p.masp).Select(g => new ProductGroup
            {
                Id = g.Key, Name = g.Min(p => p.tensanpham)!, Price = g.Min(p => p.dongia)
            });
        // No creation timestamp exists in the partner schema; use stable ID order.
        var sorted = query.Sort switch
        {
            "price_asc" => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price_desc" => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "name" => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            _ => products.OrderBy(p => p.Id)
        };
        return sorted.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
    }

    public async Task<ProductListResponse> SearchAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var total = await Filter(query).Select(p => p.masp).Distinct().CountAsync(cancellationToken);
        var page = await Page(query).ToListAsync(cancellationToken);
        var ids = page.Select(p => p.Id).ToArray();
        var rows = await Visible().Where(p => ids.Contains(p.masp)).Include(p => p.Category).ToListAsync(cancellationToken);
        var groups = rows.GroupBy(p => p.masp).ToDictionary(g => g.Key, g => Map(g));
        var items = page.Where(p => groups.ContainsKey(p.Id)).Select(p => (ProductSummaryResponse)groups[p.Id]).ToList();
        return new ProductListResponse(items, total, query.Page, query.PageSize)
        { SupportsAudience = true, SupportsNewest = false };
    }

    public Task<List<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        db.Categories.AsNoTracking().Where(c => c.trangthai).OrderBy(c => c.tenloai).ThenBy(c => c.maloai)
            .Select(c => new CategoryResponse(c.maloai, c.tenloai)).ToListAsync(cancellationToken);

    public async Task<ProductDetailResponse?> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        var rows = await Visible().Include(p => p.Category).Where(p => p.masp == id).ToListAsync(cancellationToken);
        return rows.Count == 0 ? null : Map(rows);
    }

    public async Task<List<VariantResponse>?> GetVariantsAsync(string id, CancellationToken cancellationToken)
    {
        var rows = await Visible().Where(p => p.masp == id).OrderBy(p => p.masku).ToListAsync(cancellationToken);
        return rows.Count == 0 ? null : rows.Select(Variant).ToList();
    }

    private static VariantResponse Variant(SupabaseProduct p) =>
        new(p.masku, p.kichco ?? "", p.mausac ?? "", p.dongia, p.soluongtonkho);

    internal static ProductDetailResponse Map(IEnumerable<SupabaseProduct> source)
    {
        var rows = source.OrderBy(p => p.masku, StringComparer.Ordinal).ToList();
        var first = rows.First();
        var images = rows.Where(p => !string.IsNullOrWhiteSpace(p.hinhanh))
            .Select(p => new ProductImageResponse(p.masku, p.hinhanh!)).ToList();
        return new ProductDetailResponse
        {
            Id = first.masp, Name = first.tensanpham,
            Category = new CategoryResponse(first.maloai, first.Category.tenloai),
            Price = rows.Min(p => p.dongia), Stock = rows.Sum(p => (long)p.soluongtonkho),
            ImageUrl = images.FirstOrDefault()?.Url,
            CreatedAt = null, Audience = first.danhmuc, Description = null,
            Variants = rows.Select(Variant).ToList(), Images = images
        };
    }
}
