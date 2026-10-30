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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class AdminMemoryFactory : WebApplicationFactory<Program>
{
    private readonly string dbName = Guid.NewGuid().ToString("N");
    public sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    public FakeClock Clock { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
        { ["Admin:RoleCode"] = "ADMIN" }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<FashionStoreDbContext>>();
            services.RemoveAll<FashionStoreDbContext>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddDbContext<FashionStoreDbContext>(options => options.UseInMemoryDatabase(dbName));
        });
    }
}
public sealed class AdminSecurityTests : IAsyncLifetime
{
    private readonly AdminMemoryFactory factory = new();
    private HttpClient client = null!;
    private const string Secret = "Example Strong Admin Password 42!";
    public async Task InitializeAsync()
    {
        client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FashionStoreDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<NhanVien>>();
        db.Permissions.AddRange(
            new PhanQuyen { MaQuyen = "ADMIN", TenQuyen = "Admin", TrangThai = true },
            new PhanQuyen { MaQuyen = "STAFF", TenQuyen = "Staff", TrangThai = true });
        foreach (var (id, role, active) in new[] { ("AD001", "ADMIN", true), ("AD002", "ADMIN", false), ("ST001", "STAFF", true) })
        {
            var employee = new NhanVien { MaNV = id, MaQuyen = role, HoTen = "Synthetic " + id, TrangThai = active };
            employee.MatKhauHash = hasher.HashPassword(employee, Secret);
            db.Employees.Add(employee);
        }
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task<string> Login(string id = "AD001")
    {
        var res = await client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest { EmployeeId=id, Password=Secret });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<AdminLoginResponse>())!.AccessToken;
    }
    private async Task<HttpResponseMessage> Get(string url, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
    [Fact] public async Task AdminEndpointRejectsAnonymousAndForgedSessions()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/admin/auth/me", new string('x',43))).StatusCode);
    }
    [Fact] public async Task AdminLoginIsSeparateAndDashboardIsReadOnly()
    {
        var token = await Login();
        Assert.Equal(HttpStatusCode.OK,(await Get("/api/admin/auth/me",token)).StatusCode);
        var response=await Get("/api/admin/dashboard",token);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.NotNull((await response.Content.ReadFromJsonAsync<AdminDashboardResponse>())!.RecentOrders);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/account/me",token)).StatusCode);
    }
    [Theory]
    [InlineData("ST001")][InlineData("AD002")][InlineData("UNKNOWN")]
    public async Task OnlyActiveWhitelistedPermissionCanSignIn(string id)
    {
        var result=await client.PostAsJsonAsync("/api/admin/auth/login",new AdminLoginRequest{ EmployeeId=id, Password=Secret });
        Assert.Equal(HttpStatusCode.Unauthorized,result.StatusCode);
    }
    [Fact] public async Task RevokingRoleDisablesAlreadyIssuedToken()
    {
        var token=await Login();
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FashionStoreDbContext>();
            var employee=await db.Employees.AsTracking().SingleAsync(e=>e.MaNV=="AD001");
            employee.MaQuyen="STAFF";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/admin/dashboard",token)).StatusCode);
    }
    [Fact] public async Task NewHashInvalidatesExistingSessionsAndLogoutRevokes()
    {
        var token=await Login();
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FashionStoreDbContext>();
            var hasher=scope.ServiceProvider.GetRequiredService<IPasswordHasher<NhanVien>>();
            var employee=await db.Employees.AsTracking().SingleAsync(e=>e.MaNV=="AD001");
            employee.MatKhauHash=hasher.HashPassword(employee,"New!Password!With!12!Chars");
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/admin/auth/me",token)).StatusCode);
    }
    [Fact] public async Task AdminSessionExpiresAfterOneHourWithoutSleeping()
    {
        var token=await Login();
        factory.Clock.Now=factory.Clock.Now.AddHours(1).AddSeconds(1);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/admin/auth/me",token)).StatusCode);
    }
    [Fact] public async Task DisabledPermissionRevokesSession()
    {
        var token=await Login();
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FashionStoreDbContext>();
            var permission=await db.Permissions.AsTracking().SingleAsync(p=>p.MaQuyen=="ADMIN");
            permission.TrangThai=false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/admin/dashboard",token)).StatusCode);
    }
    [Fact] public async Task LogoutRevokesAdminToken()
    {
        var token=await Login();
        using var req=new HttpRequestMessage(HttpMethod.Post,"/api/admin/auth/logout");
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        Assert.Equal(HttpStatusCode.NoContent,(await client.SendAsync(req)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Get("/api/admin/dashboard",token)).StatusCode);
    }
}
