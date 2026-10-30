using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FashionStore.Web.Models;
using Newtonsoft.Json;

namespace FashionStore.Web.Services
{
    // MVC reads only the public Product API. SQL access and business rules live in the backend.
    public class CatalogRepository
    {
        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        private readonly HttpClient http;
        private readonly Uri baseUri;

        public CatalogRepository() : this(SharedHttp, ConfigurationManager.AppSettings["FashionStoreApiBaseUrl"]) { }

        internal CatalogRepository(HttpClient client, string baseUrl)
        {
            Uri parsed;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
                !String.IsNullOrEmpty(parsed.Query) || !String.IsNullOrEmpty(parsed.Fragment))
                throw new InvalidOperationException("FashionStoreApiBaseUrl must be an absolute HTTP(S) base URL.");
            http = client;
            baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
        }

        public async Task<IList<ProductSummary>> GetFeaturedAsync(int limit)
        {
            var result = await SearchAsync(null, null, null, "new", 1, Math.Max(1, Math.Min(12, limit))).ConfigureAwait(false);
            return result.Products;
        }

        public async Task<IList<CategoryOption>> GetCategoriesAsync()
        {
            var categories = await GetAsync<List<CategoryDto>>("api/categories", false).ConfigureAwait(false);
            return categories.Select(c => new CategoryOption { MaLoai = c.Id, TenLoai = c.Name }).ToList();
        }

        public async Task<CatalogListViewModel> SearchAsync(string query, string audience, string categoryId, string sort, int page, int pageSize, decimal? minPrice = null, decimal? maxPrice = null, string size = null, string color = null, bool inStock = false)
        {
            var parts = new List<string>
            {
                "sort=" + Uri.EscapeDataString(String.IsNullOrWhiteSpace(sort) ? "new" : sort),
                "page=" + page.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "pageSize=" + pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            if (!String.IsNullOrWhiteSpace(query)) parts.Add("q=" + Uri.EscapeDataString(query.Trim()));
            if (!String.IsNullOrWhiteSpace(audience)) parts.Add("audience=" + Uri.EscapeDataString(audience));
            if (!String.IsNullOrWhiteSpace(categoryId)) parts.Add("category=" + Uri.EscapeDataString(categoryId));
            if(minPrice.HasValue) parts.Add("minPrice=" + minPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(maxPrice.HasValue) parts.Add("maxPrice=" + maxPrice.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(!String.IsNullOrWhiteSpace(size)) parts.Add("size=" + Uri.EscapeDataString(size.Trim()));
            if(!String.IsNullOrWhiteSpace(color)) parts.Add("color=" + Uri.EscapeDataString(color.Trim()));
            if(inStock) parts.Add("inStock=true");
            var result = await GetAsync<ProductListDto>("api/products?" + String.Join("&", parts), false).ConfigureAwait(false);
            if (result.Items == null || result.Total < 0 || result.Page != page || result.PageSize != pageSize)
                throw new CatalogApiException(502, "Product API returned an invalid paging contract.");
            return new CatalogListViewModel
            {
                MinPrice=minPrice, MaxPrice=maxPrice, Size=size, Color=color, InStock=inStock,
                Query = (query ?? "").Trim(), Audience = audience ?? "", CategoryId = categoryId ?? "",
                Sort = String.IsNullOrWhiteSpace(sort) ? "new" : sort,
                Page = result.Page, PageSize = result.PageSize, Total = result.Total,
                SupportsAudience = result.SupportsAudience, SupportsNewest = result.SupportsNewest,
                Products = result.Items.Select(MapProduct).ToList()
            };
        }

        public async Task<ProductDetailViewModel> GetDetailAsync(string id)
        {
            var dto = await GetAsync<ProductDetailDto>("api/products/" + Uri.EscapeDataString(id), true).ConfigureAwait(false);
            if (dto == null) return new ProductDetailViewModel();
            if (dto.Variants == null || dto.Images == null)
                throw new CatalogApiException(502, "Product API returned an invalid detail contract.");
            return new ProductDetailViewModel
            {
                Product = MapProduct(dto),
                Variants = dto.Variants.Select(v => new ProductVariant
                {
                    MaSKU = v.Sku, KichCo = v.Size, MauSac = v.Color,
                    DonGia = v.Price, SoLuongTonKho = v.Stock
                }).ToList(),
                Images = dto.Images.Select(i => new ProductImage { MaSKU = i.Sku, DuongDan = i.Url }).ToList()
            };
        }

        private static ProductSummary MapProduct(ProductDto product)
        {
            if (product == null || String.IsNullOrEmpty(product.Id) || product.Category == null)
                throw new CatalogApiException(502, "Product API returned an invalid product contract.");
            return new ProductSummary
            {
                MaSP = product.Id, TenSanPham = product.Name, MaLoai = product.Category.Id,
                TenLoai = product.Category.Name, DoiTuong = product.Audience, MoTa = product.Description,
                DonGia = product.Price, TonKho = product.Stock, HinhAnh = product.ImageUrl
            };
        }

        private async Task<T> GetAsync<T>(string path, bool allowNotFound) where T : class
        {
            using (var response = await http.GetAsync(new Uri(baseUri, path)).ConfigureAwait(false))
            {
                if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return null;
                if (!response.IsSuccessStatusCode)
                    throw new CatalogApiException((int)response.StatusCode, "Product API request failed.");
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                try
                {
                    var result = JsonConvert.DeserializeObject<T>(json);
                    if (result == null) throw new CatalogApiException(502, "Product API returned null JSON.");
                    return result;
                }
                catch (JsonException error)
                {
                    throw new CatalogApiException(502, "Product API returned invalid JSON.", error);
                }
            }
        }

        private class ProductListDto
        {
            public bool SupportsAudience { get; set; } = true;
            public bool SupportsNewest { get; set; } = true;
            public List<ProductDto> Items { get; set; }
            public int Total { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
        }
        private class CategoryDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
        private class ProductDto
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public CategoryDto Category { get; set; }
            public string Audience { get; set; }
            public decimal Price { get; set; }
            public long Stock { get; set; }
            public string ImageUrl { get; set; }
        }
        private class ProductDetailDto : ProductDto
        {
            public List<VariantDto> Variants { get; set; }
            public List<ImageDto> Images { get; set; }
        }
        private class VariantDto
        {
            public string Sku { get; set; }
            public string Size { get; set; }
            public string Color { get; set; }
            public decimal Price { get; set; }
            public int Stock { get; set; }
        }
        private class ImageDto
        {
            public string Sku { get; set; }
            public string Url { get; set; }
        }
    }

    public sealed class CatalogApiException : Exception
    {
        public int StatusCode { get; private set; }
        public CatalogApiException(int statusCode, string message, Exception innerException = null)
            : base(message, innerException) { StatusCode = statusCode; }
    }
}
