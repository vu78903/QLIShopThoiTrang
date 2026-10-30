using System.Net;
using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class SupabaseCatalogTests
{
    [Fact]
    public async Task MultipleRowsPerProductAreGroupedBeforeCountAndPagingAndRetainAllVisibleSkus()
    {
        var name = Guid.NewGuid().ToString();
        var root = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
        await using var db = new SupabaseCatalogDbContext(new DbContextOptionsBuilder<SupabaseCatalogDbContext>()
            .UseInMemoryDatabase(name, root).Options);
        // Seed a separate writable test context; the application context stays read-only.
        await using (var seed = new DbContext(new DbContextOptionsBuilder()
            .UseInMemoryDatabase(name, root).UseModel(db.Model).Options))
        {
            seed.AddRange(new SupabaseCategory { maloai = "AO", tenloai = "Áo", trangthai = true },
                new SupabaseCategory { maloai = "HIDDEN", tenloai = "Ẩn", trangthai = false });
            seed.AddRange(
                Row("P1", "A", "M", "Đen", 100, 3),
                Row("P1", "B", "L", "Đỏ", 200, 5),
                Row("P1", "C", "M", "Đỏ", 1, 99, active: false),
                Row("P2", "D", "S", "Đen", 150, 0),
                Row("P3", "E", "S", "Trắng", 50, 1),
                Row("P4", "F", "M", "Đen", 10, 1, category: "HIDDEN"));
            await seed.SaveChangesAsync();
        }
        var service = new SupabaseCatalogService(db);
        var page1 = await service.SearchAsync(new ProductQuery { PageSize = 2 }, default);
        var page2 = await service.SearchAsync(new ProductQuery { PageSize = 2, Page = 2 }, default);
        Assert.Equal(3, page1.Total);
        Assert.Equal(new[] { "P1", "P2" }, page1.Items.Select(p => p.Id));
        Assert.Equal("P3", Assert.Single(page2.Items).Id);
        Assert.Equal(8, page1.Items[0].Stock);
        Assert.Equal(100, page1.Items[0].Price);
        Assert.False(page1.SupportsNewest);
        var sorted = await service.SearchAsync(new ProductQuery { Sort = "price_asc" }, default);
        Assert.Equal(new[] { "P3", "P1", "P2" }, sorted.Items.Select(p => p.Id));
        // M and red exist on different visible SKUs: they must not match together.
        Assert.Empty((await service.SearchAsync(new ProductQuery { Size = "M", Color = "Đỏ" }, default)).Items);
        var matching = await service.SearchAsync(new ProductQuery { Size = "L", Color = "Đỏ", MinPrice = 180, InStock = true }, default);
        Assert.Equal("P1", Assert.Single(matching.Items).Id);
        Assert.Equal(8, matching.Items[0].Stock);
        Assert.Equal(100, matching.Items[0].Price);
        var detail = await service.GetDetailAsync("P1", default);
        Assert.NotNull(detail);
        Assert.Equal(new[] { "A", "B" }, detail!.Variants.Select(v => v.Sku));
        Assert.Equal(8, detail.Stock);
        Assert.Equal(2, detail.Images.Count);
        Assert.Equal(new[] { "A", "B" }, (await service.GetVariantsAsync("P1", default))!.Select(v => v.Sku));
        Assert.Equal(2, (await service.SearchAsync(new ProductQuery { Audience = "NAM" }, default)).Total);
        Assert.Null(await service.GetDetailAsync("P4", default));
        Assert.Null(await service.GetVariantsAsync("missing", default));
    }

    private static SupabaseProduct Row(string id, string sku, string size, string color, decimal price,
        int stock, bool active = true, string category = "AO") => new()
    {
        masp = id, masku = sku, maloai = category, tensanpham = id, danhmuc = id == "P3" ? "NU" : "NAM", kichco = size, mausac = color,
        dongia = price, soluongtonkho = stock, trangthai = active, hinhanh = "/" + sku + ".png"
    };

    private static SupabaseCatalogDbContext Context() => new(new DbContextOptionsBuilder<SupabaseCatalogDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=unused;Username=unused;Password=unused").Options);

    [Theory]
    [InlineData("new")]
    [InlineData("price_asc")]
    [InlineData("price_desc")]
    [InlineData("name")]
    public void QueryUsesPartnerTablesAndFiltersBeforePaging(string sort)
    {
        using var db = Context();
        var sql = new SupabaseCatalogService(db).Page(new ProductQuery
        {
            Q = "Áo %'", Category = "AO", Size = "M", Color = "Đen", InStock = true,
            MinPrice = 100, MaxPrice = 200, Page = 2, PageSize = 12, Sort = sort
        }).ToQueryString();
        Assert.Contains("public.sanpham", sql);
        Assert.Contains("public.loaisanpham", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("soluongtonkho", sql);
        Assert.Contains("kichco", sql);
        Assert.Contains("mausac", sql);
        Assert.DoesNotContain("BienTheSanPham", sql);
        Assert.DoesNotContain("ngaytao", sql);
    }

    [Fact]
    public void MapsOneRealSkuWithoutInventingMissingMetadata()
    {
        var result = SupabaseCatalogService.Map([new SupabaseProduct
        {
            masp = "P1", masku = "SKU1", maloai = "AO", tensanpham = "Áo",
            kichco = "M", mausac = "Đen", dongia = 120, soluongtonkho = 0,
            Category = new SupabaseCategory { maloai = "AO", tenloai = "Áo" }
        }]);
        Assert.Equal("P1", result.Id);
        Assert.Equal("SKU1", Assert.Single(result.Variants).Sku);
        Assert.Equal(120, result.Price);
        Assert.Equal(0, result.Stock);
        Assert.Null(result.CreatedAt);
        Assert.Null(result.Audience);
        Assert.Empty(result.Images);
    }

    [Fact]
    public void AudienceUsesDanhMucAndContextCannotWrite()
    {
        using var db = Context();
        var sql = new SupabaseCatalogService(db).Filter(new ProductQuery { Audience = "NAM" }).ToQueryString();
        Assert.Contains("danhmuc", sql);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void PartnerEnvironmentVariableIsSupportedWithoutUsingLegacySqlConnection()
    {
        var mode = new DatabaseMode(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Supabase", ["YODYSTORE_CONNECTION_STRING"] = "partner",
            ["ConnectionStrings:FashionStore"] = "legacy"
        }).Build());
        Assert.True(mode.IsSupabase);
        Assert.Equal("partner", mode.SupabaseConnection);
    }

    [Fact]
    public async Task SupabaseModeBlocksUnmigratedControllersWithoutOpeningEitherDatabase()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Supabase", ["ConnectionStrings:Supabase"] = "",
                ["YODYSTORE_CONNECTION_STRING"] = "", ["ConnectionStrings:FashionStore"] = "invalid legacy connection"
            }));
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/unknown")).StatusCode);
    }
}
