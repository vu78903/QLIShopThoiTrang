using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Services;

public sealed class CatalogService(FashionStoreDbContext db) : ICatalogService
{
    private IQueryable<SanPham> VisibleProducts() => db.Products.Where(p =>
        p.TrangThai && p.Loai.TrangThai && p.Variants.Any(v => v.TrangThai));

    private IQueryable<SanPham> FilterProducts(ProductQuery query)
    {
        var products = VisibleProducts();
        var text = query.Q?.Trim();
        if (!string.IsNullOrEmpty(text))
            products = products.Where(p => p.TenSanPham.Contains(text) || p.MaSP.Contains(text));
        if (!string.IsNullOrEmpty(query.Category))
            products = products.Where(p => p.MaLoai == query.Category);
        if (!string.IsNullOrEmpty(query.Audience))
            products = products.Where(p => p.DoiTuong == query.Audience);

        // One matching active SKU must satisfy ALL variant filters, before COUNT and paging.
        products = products.Where(p => p.Variants.Any(v => v.TrangThai &&
            (query.Size == null || query.Size == "" || v.KichCo == query.Size) &&
            (query.Color == null || query.Color == "" || v.MauSac == query.Color) &&
            (!query.InStock || v.SoLuongTonKho > 0) &&
            (!query.MinPrice.HasValue || v.DonGia >= query.MinPrice.Value) &&
            (!query.MaxPrice.HasValue || v.DonGia <= query.MaxPrice.Value)));
        return products;
    }

    // Kept as a query so SQL translation can be verified without a running database.
    internal IQueryable<ProductSummaryResponse> CreateSearchQuery(ProductQuery query)
    {
        var summaries = FilterProducts(query).Select(p => new ProductSummaryResponse
        {
            Id = p.MaSP, Name = p.TenSanPham,
            Category = new CategoryResponse(p.MaLoai, p.Loai.TenLoai),
            Audience = p.DoiTuong, CreatedAt = p.NgayTao,
            Price = p.Variants.Where(v => v.TrangThai).Min(v => v.DonGia),
            Stock = p.Variants.Where(v => v.TrangThai).Sum(v => (long)v.SoLuongTonKho),
            ImageUrl = p.Images.Where(i => i.MaSKU == null || p.Variants.Any(v => v.TrangThai && v.MaSKU == i.MaSKU))
                .OrderBy(i => i.MaSKU == null ? 0 : 1).ThenByDescending(i => i.AnhChinh)
                .ThenBy(i => i.ThuTu).ThenBy(i => i.MaHinh).Select(i => i.DuongDan).FirstOrDefault()
        });
        var ordered = query.Sort switch
        {
            "price_asc" => summaries.OrderBy(p => p.Price).ThenBy(p => p.Id),
            "price_desc" => summaries.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            "name" => summaries.OrderBy(p => p.Name).ThenBy(p => p.Id),
            _ => summaries.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };
        return ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
    }

    public async Task<ProductListResponse> SearchAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var total = await FilterProducts(query).CountAsync(cancellationToken);
        var items = await CreateSearchQuery(query).ToListAsync(cancellationToken);
        return new ProductListResponse(items, total, query.Page, query.PageSize);
    }

    public Task<List<CategoryResponse>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        db.Categories.Where(c => c.TrangThai).OrderBy(c => c.TenLoai).ThenBy(c => c.MaLoai)
            .Select(c => new CategoryResponse(c.MaLoai, c.TenLoai)).ToListAsync(cancellationToken);

    internal IQueryable<SanPham> CreateDetailQuery(string id) => VisibleProducts().Include(p => p.Loai)
        .Include(p => p.Variants.Where(v => v.TrangThai)).Include(p => p.Images)
        .AsSplitQuery().Where(p => p.MaSP == id);

    public async Task<ProductDetailResponse?> GetDetailAsync(string id, CancellationToken cancellationToken)
    {
        var product = await CreateDetailQuery(id).SingleOrDefaultAsync(cancellationToken);
        if (product is null) return null;
        var skuIds = product.Variants.Select(v => v.MaSKU).ToHashSet(StringComparer.Ordinal);
        var images = product.Images.Where(i => i.MaSKU == null || skuIds.Contains(i.MaSKU))
            .OrderBy(i => i.MaSKU == null ? 0 : 1).ThenByDescending(i => i.AnhChinh)
            .ThenBy(i => i.ThuTu).ThenBy(i => i.MaHinh).ToList();
        return new ProductDetailResponse
        {
            Id = product.MaSP, Name = product.TenSanPham, Description = product.MoTa,
            Category = new CategoryResponse(product.MaLoai, product.Loai.TenLoai),
            Audience = product.DoiTuong, CreatedAt = product.NgayTao,
            Price = product.Variants.Min(v => v.DonGia),
            Stock = product.Variants.Sum(v => (long)v.SoLuongTonKho),
            ImageUrl = images.FirstOrDefault()?.DuongDan,
            Images = images.Select(i => new ProductImageResponse(i.MaSKU, i.DuongDan)).ToList(),
            Variants = product.Variants.OrderBy(v => v.MaSKU).Select(v =>
                new VariantResponse(v.MaSKU, v.KichCo, v.MauSac, v.DonGia, v.SoLuongTonKho)).ToList()
        };
    }

    public async Task<List<VariantResponse>?> GetVariantsAsync(string id, CancellationToken cancellationToken)
    {
        if (!await VisibleProducts().AnyAsync(p => p.MaSP == id, cancellationToken)) return null;
        return await db.Variants.Where(v => v.MaSP == id && v.TrangThai).OrderBy(v => v.MaSKU)
            .Select(v => new VariantResponse(v.MaSKU, v.KichCo, v.MauSac, v.DonGia, v.SoLuongTonKho))
            .ToListAsync(cancellationToken);
    }
}
