using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Models;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FashionStore.Backend.Tests;

// EF InMemory here validates HTTP/authentication behavior only, not SQL transactions/constraints.
public sealed class CustomerMemoryFactory : WebApplicationFactory<Program>
{
    public sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    public TestClock Clock { get; } = new();
    private readonly string database = Guid.NewGuid().ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<FashionStoreDbContext>>();
            services.RemoveAll<FashionStoreDbContext>();
            services.AddDbContext<FashionStoreDbContext>(options => options.UseInMemoryDatabase(database));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}

public sealed class CustomerSecurityTests : IAsyncLifetime
{
    private readonly CustomerMemoryFactory factory = new();
    private HttpClient client = null!;
    private const string Password = "Synthetic password 123!";
    public async Task InitializeAsync()
    {
        client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FashionStoreDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<TaiKhoanKhachHang>>();
        foreach (var suffix in new[] { "a", "b" })
        {
            var c = new KhachHang { MaKH = "UNIT-" + suffix, HoTen = "Synthetic " + suffix,
                SoDienThoai = suffix == "a" ? "0900000001" : "0900000002", RowVer = new byte[] {1,2,3,4,5,6,7,8} };
            var a = new TaiKhoanKhachHang { MaKH = c.MaKH, Customer = c, TenDangNhap = "unit-"+suffix };
            a.MatKhauHash = hasher.HashPassword(a, Password);
            db.CustomerAccounts.Add(a);
        }
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task<string> Login(string user="unit-a")
    {
        var response = await client.PostAsJsonAsync("/api/account/login", new LoginRequest { UserName=user, Password=Password });
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
    }
    private Task<HttpResponseMessage> Get(string path,string? token=null)
    {
        var request=new HttpRequestMessage(HttpMethod.Get,path);
        if(token!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        return client.SendAsync(request);
    }
    private Task<HttpResponseMessage> Post(string path,object body,string token)
    {
        var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        return client.SendAsync(request);
    }
    [Theory]
    [InlineData("/api/account/me")]
    [InlineData("/api/cart")]
    public async Task AnonymousAndForgedTokensCannotReadCustomerData(string path)
    {
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get(path,new string('x',43))).StatusCode);
    }
    [Fact] public async Task LoginCreatesSeparateSessionsAndProfileDoesNotExposeHash()
    {
        var a=await Login();var b=await Login("unit-b");
        Assert.NotEqual(a,b);
        var response=await Get("/api/account/me",a);
        var json=await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("UNIT-a",json);Assert.DoesNotContain("UNIT-b",json);
        Assert.DoesNotContain("MatKhau",json,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password",json,StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task LogoutRevokesOnlyThatSessionAndRepeatedLogoutCannotReuseToken()
    {
        var first=await Login();var second=await Login();
        Assert.Equal(HttpStatusCode.NoContent,(await Post("/api/account/logout",new{},first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/account/me",first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Post("/api/account/logout",new{},first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Get("/api/account/me",second)).StatusCode);
    }
    [Fact] public async Task SessionExpiresWithoutSleeping()
    {
        var token=await Login();factory.Clock.Now=factory.Clock.Now.AddHours(1).AddSeconds(1);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/account/me",token)).StatusCode);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task DisablingAccountOrCustomerInvalidatesExistingToken(bool account)
    {
        var token=await Login();
        using(var scope=factory.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<FashionStoreDbContext>();
            if(account)(await db.CustomerAccounts.AsTracking().SingleAsync(a=>a.MaKH=="UNIT-a")).TrangThai=false;
            else(await db.Customers.AsTracking().SingleAsync(a=>a.MaKH=="UNIT-a")).TrangThai=false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/account/me",token)).StatusCode);
    }
    [Fact] public async Task StoredPasswordVersionChangeInvalidatesAllOldTokens()
    {
        var a=await Login();var b=await Login();
        using(var scope=factory.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<FashionStoreDbContext>();
            var hasher=scope.ServiceProvider.GetRequiredService<IPasswordHasher<TaiKhoanKhachHang>>();
            var account=await db.CustomerAccounts.AsTracking().SingleAsync(x=>x.MaKH=="UNIT-a");
            account.MatKhauHash=hasher.HashPassword(account,"Changed synthetic password!");
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/account/me",a)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/account/me",b)).StatusCode);
    }
    [Fact] public async Task AuthenticatedCartReadDoesNotExposeOtherCustomers()
    {
        var token=await Login();var response=await Get("/api/cart",token);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<CartResponse>())!.Items);
    }
    [Fact] public async Task OverpostedPriceOrOwnerIsRejectedBeforeCartMutation()
    {
        var token=await Login();
        Assert.Equal(HttpStatusCode.BadRequest,(await Post("/api/cart/items",new{sku="SKU",quantity=1,price=1,maKH="UNIT-b"},token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Post("/api/cart/items",new{sku="SKU",quantity=0},token)).StatusCode);
    }
    [Fact] public async Task RegistrationHashesPasswordAndDoesNotClaimExistingCustomerByPhone()
    {
        var payload=new RegisterRequest {UserName="new-user",Password=Password,FullName="New user",Phone="0900000099"};
        var registered=await client.PostAsJsonAsync("/api/account/register",payload);
        Assert.Equal(HttpStatusCode.Created,registered.StatusCode);
        var profile=(await registered.Content.ReadFromJsonAsync<CustomerResponse>())!;
        using var scope=factory.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<FashionStoreDbContext>();
        var account=await db.CustomerAccounts.SingleAsync(a=>a.MaKH==profile.Id);
        Assert.NotEqual(Password,account.MatKhauHash);
        Assert.Equal(1,await db.Carts.CountAsync(c=>c.MaKH==profile.Id));
        payload.UserName="another-name";
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/account/register",payload)).StatusCode);
    }
    [Fact] public async Task AuthenticationFailuresUseSamePublicMessageAndRateLimit()
    {
        string? message=null;
        for(var i=0;i<20;i++){
            var response=await client.PostAsJsonAsync("/api/account/login",new{userName=i%2==0?"missing-user":"unit-a",password="incorrect password"});
            Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
            var body=await response.Content.ReadAsStringAsync();
            Assert.Contains("401",body);
            if(message==null)message=System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("title").GetString();
            else Assert.Equal(message,System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("title").GetString());
        }
        Assert.Equal(HttpStatusCode.TooManyRequests,(await client.PostAsJsonAsync("/api/account/login",new{userName="unit-a",password=Password})).StatusCode);
    }
}
