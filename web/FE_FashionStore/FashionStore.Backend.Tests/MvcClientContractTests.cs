using System.Net;
using System.Text;
using FashionStore.Web.Services;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace FashionStore.Backend.Tests;

// Isolated HTTP contract tests. These fixtures are never backend runtime data.
public sealed class MvcClientContractTests
{
    private const string Summary = """
        {"id":"SP-TEST","name":"Áo sơ mi","category":{"id":"CAT-TEST","name":"Áo"},
        "audience":"NAM","price":150000,"stock":2147483650,"imageUrl":"/images/product.jpg"}
        """;

    [Fact]
    public async Task SearchSendsFiltersToProductApiAndKeepsServerPagination()
    {
        var handler = new ResponseHandler(HttpStatusCode.OK,
            "{\"items\":[" + Summary + "],\"total\":40,\"page\":2,\"pageSize\":12}");
        var repository = new CatalogRepository(new HttpClient(handler), "http://localhost:5000");
        var model = await repository.SearchAsync("Áo & quần", "NAM", "CAT-TEST", "price_asc", 2, 12);
        Assert.Single(handler.Requests);
        Assert.Equal("/api/products", handler.Requests[0].AbsolutePath);
        var query = QueryHelpers.ParseQuery(handler.Requests[0].Query);
        Assert.Equal("Áo & quần", query["q"].ToString());
        Assert.Equal("NAM", query["audience"].ToString());
        Assert.Equal("CAT-TEST", query["category"].ToString());
        Assert.Equal("price_asc", query["sort"].ToString());
        Assert.Equal("2", query["page"].ToString());
        Assert.Equal(40, model.Total);
        Assert.Equal(2, model.Page);
        Assert.Single(model.Products); // The client must not Skip(12) a second time.
        var product = model.Products[0];
        Assert.Equal("SP-TEST", product.MaSP);
        Assert.Equal("CAT-TEST", product.MaLoai);
        Assert.Equal("NAM", product.DoiTuong);
        Assert.Equal(2147483650L, product.TonKho);
        Assert.Equal("/images/product.jpg", product.HinhAnh);
    }

    [Fact]
    public async Task DetailMapsActualSkuSizeColorDescriptionAndImages()
    {
        var detail = Summary.TrimEnd().TrimEnd('}') + """
            ,"description":"Mô tả thật từ response",
            "variants":[{"sku":"SKU-DIFFERENT-FROM-PRODUCT","size":"XL","color":"Đỏ","price":175000,"stock":3}],
            "images":[{"sku":null,"url":"/images/main.jpg"},{"sku":"SKU-DIFFERENT-FROM-PRODUCT","url":"/images/red.jpg"}]}
            """;
        var repository = new CatalogRepository(new HttpClient(new ResponseHandler(HttpStatusCode.OK, detail)), "http://localhost:5000");
        var model = await repository.GetDetailAsync("SP-TEST");
        Assert.Equal("Mô tả thật từ response", model.Product.MoTa);
        var sku = Assert.Single(model.Variants);
        Assert.Equal("SKU-DIFFERENT-FROM-PRODUCT", sku.MaSKU);
        Assert.Equal("XL", sku.KichCo);
        Assert.Equal("Đỏ", sku.MauSac);
        Assert.Equal(175000m, sku.DonGia);
        Assert.Equal(3, sku.SoLuongTonKho);
        Assert.Equal(2, model.Images.Count);
    }

    [Fact]
    public async Task FeaturedUsesNewestProductEndpointOnce()
    {
        var handler = new ResponseHandler(HttpStatusCode.OK, "{\"items\":[],\"total\":0,\"page\":1,\"pageSize\":8}");
        var repository = new CatalogRepository(new HttpClient(handler), "http://localhost:5000");
        Assert.Empty(await repository.GetFeaturedAsync(8));
        Assert.Single(handler.Requests);
        Assert.Contains("sort=new", handler.Requests[0].Query);
        Assert.DoesNotContain("app-state", handler.Requests[0].ToString());
    }

    [Fact]
    public async Task CategoriesUseDedicatedEndpoint()
    {
        var handler = new ResponseHandler(HttpStatusCode.OK, "[{\"id\":\"C1\",\"name\":\"Áo\"}]");
        var repository = new CatalogRepository(new HttpClient(handler), "http://localhost:5000");
        Assert.Equal("C1", Assert.Single(await repository.GetCategoriesAsync()).MaLoai);
        Assert.Equal("/api/categories", Assert.Single(handler.Requests).AbsolutePath);
    }

    [Fact]
    public async Task Detail404IsMissingProductBut503IsAnError()
    {
        var missing = new CatalogRepository(new HttpClient(new ResponseHandler(HttpStatusCode.NotFound, "")), "http://localhost");
        Assert.Null((await missing.GetDetailAsync("missing")).Product);
        var unavailable = new CatalogRepository(new HttpClient(new ResponseHandler(HttpStatusCode.ServiceUnavailable, "")), "http://localhost");
        var error = await Assert.ThrowsAsync<CatalogApiException>(() => unavailable.GetDetailAsync("SP-TEST"));
        Assert.Equal(503, error.StatusCode);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("not-json")]
    [InlineData("{\"total\":0,\"page\":1,\"pageSize\":12}")]
    public async Task BrokenApiResponseMustNotLookLikeAnEmptyCatalog(string response)
    {
        var repository = new CatalogRepository(new HttpClient(new ResponseHandler(HttpStatusCode.OK, response)), "http://localhost");
        var error = await Assert.ThrowsAsync<CatalogApiException>(() => repository.SearchAsync(null, null, null, "new", 1, 12));
        Assert.Equal(502, error.StatusCode);
    }

    private sealed class ResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
