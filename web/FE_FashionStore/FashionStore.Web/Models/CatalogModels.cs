using System;
using System.Collections.Generic;

namespace FashionStore.Web.Models
{
    // Read-only storefront DTOs; map to the approved SQL Server v2 schema.
    public class ProductSummary
    {
        public string MaSP { get; set; }
        public string TenSanPham { get; set; }
        public string MaLoai { get; set; }
        public string TenLoai { get; set; }
        public string DoiTuong { get; set; }
        public string MoTa { get; set; }
        public decimal DonGia { get; set; }
        public long TonKho { get; set; }
        public string HinhAnh { get; set; }
        public bool ConHang { get { return TonKho > 0; } }
    }

    public class CategoryOption
    {
        public string MaLoai { get; set; }
        public string TenLoai { get; set; }
    }

    public class ProductVariant
    {
        public string MaSKU { get; set; }
        public string KichCo { get; set; }
        public string MauSac { get; set; }
        public decimal DonGia { get; set; }
        public int SoLuongTonKho { get; set; }
    }

    public class ProductImage
    {
        public string MaSKU { get; set; }
        public string DuongDan { get; set; }
    }

    public class HomeViewModel
    {
        public HomeViewModel() { FeaturedProducts = new List<ProductSummary>(); }
        public IList<ProductSummary> FeaturedProducts { get; set; }
        public string CatalogError { get; set; }
    }

    public class CatalogListViewModel
    {
        public bool SupportsAudience { get; set; } = true;
        public bool SupportsNewest { get; set; } = true;
        public CatalogListViewModel()
        {
            Products = new List<ProductSummary>();
            Categories = new List<CategoryOption>();
        }
        public IList<ProductSummary> Products { get; set; }
        public IList<CategoryOption> Categories { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public bool InStock { get; set; }
        public string Query { get; set; }
        public string Audience { get; set; }
        public string CategoryId { get; set; }
        public string Sort { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int TotalPages { get { return (Total + PageSize - 1) / PageSize; } }
        public string CatalogError { get; set; }
    }

    public class ProductDetailViewModel
    {
        public ProductDetailViewModel()
        {
            Variants = new List<ProductVariant>();
            Images = new List<ProductImage>();
        }
        public IList<ProductSummary> Related { get; set; } = new List<ProductSummary>();
        public ProductSummary Product { get; set; }
        public IList<ProductVariant> Variants { get; set; }
        public IList<ProductImage> Images { get; set; }
        public string CatalogError { get; set; }
    }
}
