using System.ComponentModel.DataAnnotations;
using System.Net;
using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace FashionStore.Backend.Tests;
public sealed class CustomerTask3Tests
{
    [Fact] public void ReversedPriceRangeIsRejected() {
        var q=new ProductQuery{MinPrice=200,MaxPrice=100};var errors=new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(q,new ValidationContext(q),errors,true));
    }
    [Fact] public void FiltersTranslateBeforePaging() {
        using var db=new FashionStoreDbContext(new DbContextOptionsBuilder<FashionStoreDbContext>().UseSqlServer("Server=localhost;Database=FashionStoreDB_Dev;Integrated Security=true").Options);
        var sql=new CatalogService(db).CreateSearchQuery(new ProductQuery{Size="M",Color="Đen",MinPrice=100,MaxPrice=200,InStock=true,Page=2}).ToQueryString();
        Assert.Contains("[KichCo]",sql);Assert.Contains("[MauSac]",sql);Assert.Contains("OFFSET",sql);
    }
    [Theory]
    [InlineData(1000,"PHAN_TRAM",15,150)]
    [InlineData(1000,"SO_TIEN",1500,1000)]
    [InlineData(199,"PHAN_TRAM",10,19.9)]
    public void VoucherCannotExceedMerchandise(decimal subtotal,string kind,decimal value,decimal expected)=>Assert.Equal(expected,OrderService.DiscountAmount(subtotal,kind,value));
    [Theory]
    [InlineData("/api/customer/points")]
    [InlineData("/api/customer/returns")]
    [InlineData("/api/customer/wishlist")]
    [InlineData("/api/customer/addresses")]
    [InlineData("/api/customer/orders/OTHER/history")]
    public async Task PrivateFeaturesRejectAnonymous(string route){await using var f=new ApiFactory();using var client=f.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(route)).StatusCode);}
}
