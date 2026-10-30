using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FashionStore.Backend.Tests;

// Uses the actual SQL Server provider's query translator. No SQL connection is
// opened; successful translation is not evidence of successful SQL execution.
public sealed class SqlTranslationTests
{
    private static FashionStoreDbContext CreateContext() => new(new DbContextOptionsBuilder<FashionStoreDbContext>()
        .UseSqlServer("Server=127.0.0.1,1;Database=FashionStoreDB_Dev;Integrated Security=True").Options);

    [Theory]
    [InlineData("new")]
    [InlineData("price_asc")]
    [InlineData("price_desc")]
    [InlineData("name")]
    public void ProductQueriesTranslateWithFiltersAndServerSidePagination(string sort)
    {
        using var db = CreateContext();
        var query = new CatalogService(db).CreateSearchQuery(new ProductQuery
        {
            Q = "Áo %'", Audience = "NAM", Category = "CAT", Sort = sort, Page = 3, PageSize = 12
        });
        var sql = query.ToQueryString();
        Assert.Contains("OFFSET", sql);
        Assert.Contains("FETCH NEXT", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("[DoiTuong]", sql);
        Assert.Contains("[SoLuongTonKho]", sql);
    }

    [Fact]
    public void DetailIncludesTranslateUsingActualTableAndKeyMapping()
    {
        using var db = CreateContext();
        var sql = new CatalogService(db).CreateDetailQuery("SP-TEST").ToQueryString();
        Assert.Contains("[dbo].[SanPham]", sql);
        Assert.Contains("[dbo].[LoaiSanPham]", sql);
        Assert.Contains("[dbo].[BienTheSanPham]", sql);
    }
}
