using System.Net;
using System.Net.Http.Json;
using FashionStore.Backend.DTOs;
using FashionStore.Web.Services;
using Xunit;

namespace FashionStore.Backend.Tests;

[Collection("SqlCatalog")]
public sealed class SqlCatalogIntegrationTests(SqlCatalogFixture data)
{
    private Task<ProductListResponse?> Search(string query = "") =>
        data.Client.GetFromJsonAsync<ProductListResponse>("/api/products?q=" + data.Tag + "&" + query);

    [SqlServerFact]
    public async Task FiltersAndVisibilityUseSqlData()
    {
        var all = (await Search())!;
        Assert.Equal(5, all.Total);
        Assert.DoesNotContain(all.Items, p => p.Id == data.Product(5) || p.Id == data.Product(6) || p.Id == data.Product(7) || p.Id == data.Product(8));
        Assert.Equal(3, (await Search("category=" + data.CategoryA))!.Total);
        foreach (var audience in new[] { "NAM", "NU", "TRE_EM", "UNISEX" })
        {
            var result = (await Search("audience=" + audience))!;
            Assert.Equal(audience, Assert.Single(result.Items).Audience);
        }
        Assert.Equal(0, (await Search("category=" + data.CategoryB + "&audience=NAM"))!.Total);
    }

    [SqlServerFact]
    public async Task PagingAndPriceSortAreExecutedBySql()
    {
        var first = (await Search("sort=price_asc&page=1&pageSize=2"))!;
        var second = (await Search("sort=price_asc&page=2&pageSize=2"))!;
        Assert.Equal(5, first.Total);
        Assert.Equal(new[] { data.Product(4), data.Product(1) }, first.Items.Select(p => p.Id));
        Assert.Equal(new[] { data.Product(3), data.Product(9) }, second.Items.Select(p => p.Id));
        Assert.Equal(data.Product(2), (await Search("sort=price_desc"))!.Items[0].Id);
        Assert.Empty((await Search("page=100"))!.Items);
    }

    [SqlServerFact]
    public async Task NewSortUsesActualCreatedAtAndSoldOutProductsRemainVisible()
    {
        var result = (await Search("sort=new"))!;
        Assert.Equal(data.Product(9), result.Items[0].Id);
        Assert.Equal(0, Assert.Single(result.Items, p => p.Id == data.Product(2)).Stock);
        Assert.Null(Assert.Single(result.Items, p => p.Id == data.Product(9)).Audience);
    }

    [SqlServerFact]
    public async Task DetailsReturnActualVariantsPricesStockAndVisibleImages()
    {
        var detail = (await data.Client.GetFromJsonAsync<ProductDetailResponse>("/api/products/" + data.Product(1)))!;
        Assert.Equal(100000m, detail.Price);
        Assert.Equal(2, detail.Stock);
        Assert.Equal("Mô tả tích hợp SQL", detail.Description);
        Assert.Equal(2, detail.Variants.Count);
        Assert.DoesNotContain(detail.Variants, v => v.Sku == detail.Id || v.Size == "Free size");
        Assert.Contains(detail.Variants, v => v.Size == "M" && v.Color == "Xanh" && v.Price == 100000 && v.Stock == 2);
        Assert.Contains(detail.Variants, v => v.Size == "L" && v.Price == 200000 && v.Stock == 0);
        Assert.Equal(2, detail.Images.Count);
        Assert.Equal("/images/integration-main.jpg", detail.ImageUrl);
        var variants = await data.Client.GetFromJsonAsync<List<VariantResponse>>("/api/products/" + data.Product(1) + "/variants");
        Assert.Equal(detail.Variants, variants);
    }

    [SqlServerFact]
    public async Task MissingOrInactiveProductIs404()
    {
        foreach (var id in new[] { data.Product(5), data.Product(6), data.Product(7), data.Product(8), "MISSING-API-TEST" })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await data.Client.GetAsync("/api/products/" + id)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await data.Client.GetAsync("/api/products/" + id + "/variants")).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task CategoriesAndUnicodeSearchUseDatabase()
    {
        var categories = (await data.Client.GetFromJsonAsync<List<CategoryResponse>>("/api/categories"))!;
        Assert.Contains(categories, c => c.Id == data.CategoryA);
        Assert.DoesNotContain(categories, c => c.Id == data.Tag + "C");
        var result = await data.Client.GetFromJsonAsync<ProductListResponse>("/api/products?q=" + Uri.EscapeDataString(data.Tag + " Áo sơ mi"));
        Assert.Equal(data.Product(1), Assert.Single(result!.Items).Id);
    }

    [SqlServerFact]
    public async Task MvcRepositoryMapsActualApiAndSqlResponses()
    {
        var repository = new CatalogRepository(data.Client, data.Client.BaseAddress!.ToString());
        var list = await repository.SearchAsync(data.Tag, "NAM", data.CategoryA, "price_asc", 1, 12);
        Assert.Equal(data.Product(1), Assert.Single(list.Products).MaSP);
        var detail = await repository.GetDetailAsync(data.Product(1));
        Assert.Equal(2, detail.Variants.Count);
        Assert.Equal(data.Product(1) + "-M", Assert.Single(detail.Variants, v => v.KichCo == "M").MaSKU);
        Assert.Equal("/images/integration-main.jpg", detail.Product.HinhAnh);
        Assert.Null((await repository.GetDetailAsync("MISSING-API-TEST")).Product);
    }
}
