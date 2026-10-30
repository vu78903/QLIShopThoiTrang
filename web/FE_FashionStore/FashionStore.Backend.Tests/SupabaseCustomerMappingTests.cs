using System.Net;
using FashionStore.Backend.Data;
using FashionStore.Backend.Models;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class SupabaseCustomerMappingTests
{
    internal static FashionStoreDbContext Create() => new(new DbContextOptionsBuilder<FashionStoreDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=fashionstore_test;Username=test;Password=test").Options);

    [Fact]
    public void CustomerQueryUsesExistingPublicColumnsAndPostgresConcurrency()
    {
        using var db = Create();
        var sql = db.CustomerAccounts.Include(a => a.Customer).Where(a => a.TenDangNhap == "alice").ToQueryString();
        Assert.Contains("webstore.customer_account", sql);
        Assert.Contains("public.khachhang", sql);
        Assert.Contains("xmin", sql);
        Assert.DoesNotContain("RowVer", sql);
        var customer = db.Model.FindEntityType(typeof(KhachHang))!;
        Assert.True(customer.FindProperty(nameof(KhachHang.PgVersion))!.IsConcurrencyToken);
        Assert.Null(customer.FindProperty(nameof(KhachHang.NgayTao)));
        Assert.Null(customer.FindProperty(nameof(KhachHang.RowVer)));
        Assert.Null(db.Model.FindEntityType(typeof(NhanVien)));
    }

    [Fact]
    public void CartQueryJoinsBySkuAndScopesToOwner()
    {
        using var db = Create();
        var sql = new CartService(db, TimeProvider.System).ReadQuery("WEB-OWNER").ToQueryString();
        Assert.Contains("webstore.cart_line", sql);
        Assert.Contains("webstore.cart", sql);
        Assert.Contains("public.sanpham", sql);
        Assert.Contains("masku", sql);
        Assert.Contains("WEB-OWNER", sql);
        Assert.Contains("dongia", sql);
        Assert.DoesNotContain("dbo", sql);
        Assert.DoesNotContain("BienTheSanPham", sql);
        Assert.Equal("masku", Assert.Single(db.Model.FindEntityType(typeof(SupabaseProduct))!.FindPrimaryKey()!.Properties).Name);
    }

    [Fact]
    public async Task EnabledCustomerModeStillRequiresAuthenticationAndBlocksCheckout()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Supabase", ["Database:EnableSupabaseCustomerFeatures"] = "true",
                ["ConnectionStrings:Supabase"] = "Host=127.0.0.1;Port=1;Database=fashionstore_test;Username=test;Password=test;Timeout=1"
            }));
        });
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cart")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/account/register", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        // Existing endpoints outside Account/Cart remain disabled even with customer mode on.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/customer/promotions")).StatusCode);
    }
}
