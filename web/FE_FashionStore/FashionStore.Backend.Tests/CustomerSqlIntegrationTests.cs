using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FashionStore.Backend.DTOs;
using Microsoft.Data.SqlClient;
using Xunit;

namespace FashionStore.Backend.Tests;

[Collection("SqlCatalog")]
public sealed class CustomerSqlIntegrationTests(SqlCatalogFixture data) : IAsyncLifetime
{
    private ApiFactory? factory;
    private HttpClient? client;
    private readonly List<string> names=[];
    private const string Password="SQL synthetic password 123!";
    public Task InitializeAsync()
    {
        if(!string.IsNullOrEmpty(data.Connection)){factory=new ApiFactory(data.Connection);client=factory.CreateClient();}
        return Task.CompletedTask;
    }
    public async Task DisposeAsync()
    {
        client?.Dispose();if(factory!=null)await factory.DisposeAsync();
        if(string.IsNullOrEmpty(data.Connection))return;
        await using var sql=new SqlConnection(data.Connection);await sql.OpenAsync();
        foreach(var name in names){
            await using var cmd=sql.CreateCommand();
            cmd.CommandText="""
                IF DB_NAME()<>N'FashionStoreDB_Dev' THROW 51000,'DEV copy required.',1;
                DECLARE @id varchar(20)=(SELECT MaKH FROM dbo.TaiKhoanKhachHang WHERE TenDangNhap=@name);
                BEGIN TRANSACTION;
                DELETE l FROM dbo.ChiTietGioHang l JOIN dbo.GioHang g ON g.MaGioHang=l.MaGioHang WHERE g.MaKH=@id;
                DELETE dbo.GioHang WHERE MaKH=@id;
                DELETE dbo.TaiKhoanKhachHang WHERE MaKH=@id;
                DELETE dbo.KhachHang WHERE MaKH=@id;
                COMMIT TRANSACTION;
                """;
            cmd.Parameters.AddWithValue("@name",name);await cmd.ExecuteNonQueryAsync();
        }
    }
    private async Task<(string Id,string Token,string Name)> Register()
    {
        var name="tacct"+Guid.NewGuid().ToString("N");names.Add(name);
        var response=await client!.PostAsJsonAsync("/api/account/register",new RegisterRequest{
            UserName=name,Password=Password,FullName="SQL account fixture",Phone="08"+RandomNumberGenerator.GetInt32(100000000,999999999),Email=name+"@example.invalid"});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var profile=(await response.Content.ReadFromJsonAsync<CustomerResponse>())!;
        var login=await client.PostAsJsonAsync("/api/account/login",new LoginRequest{UserName=name,Password=Password});
        Assert.Equal(HttpStatusCode.OK,login.StatusCode);
        return(profile.Id,(await login.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken,name);
    }
    private Task<HttpResponseMessage> Send(HttpMethod method,string path,string token,object? body=null)
    {
        var request=new HttpRequestMessage(method,path);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        if(body!=null)request.Content=JsonContent.Create(body);
        return client!.SendAsync(request);
    }
    private async Task<CartResponse> ReadCart(string token)
    {
        var r=await Send(HttpMethod.Get,"/api/cart",token);Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        return(await r.Content.ReadFromJsonAsync<CartResponse>())!;
    }
    [SqlServerFact] public async Task RegistrationIsAtomicAndDuplicateUserOrPhoneDoesNotCreatePartialRows()
    {
        var a=await Register();
        var r=await client!.PostAsJsonAsync("/api/account/register",new RegisterRequest{
            UserName=a.Name,Password=Password,FullName="Duplicate",Phone="089999999999"});
        Assert.Equal(HttpStatusCode.Conflict,r.StatusCode);
        await using var sql=new SqlConnection(data.Connection);await sql.OpenAsync();
        await using var cmd=sql.CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM dbo.KhachHang c JOIN dbo.TaiKhoanKhachHang a ON a.MaKH=c.MaKH JOIN dbo.GioHang g ON g.MaKH=c.MaKH WHERE c.MaKH=@id";
        cmd.Parameters.AddWithValue("@id",a.Id);Assert.Equal(1,(int)(await cmd.ExecuteScalarAsync())!);
    }
    [SqlServerFact] public async Task ProfileUsesActualRowVersionAndDoesNotAllowLoyaltyOverposting()
    {
        var a=await Register();
        var original=(await(await Send(HttpMethod.Get,"/api/account/me",a.Token)).Content.ReadFromJsonAsync<CustomerResponse>())!;
        var update=new ProfileRequest{FullName="Updated profile",Phone=original.Phone,Email=original.Email,
            Address="Địa chỉ DEV",RowVersion=original.RowVersion,ReceivePromotions=true};
        var saved=await Send(HttpMethod.Put,"/api/account/me",a.Token,update);
        Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        var current=(await saved.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.Equal("Địa chỉ DEV",current.Address);Assert.NotEqual(original.RowVersion,current.RowVersion);
        update.FullName="Stale update";
        Assert.Equal(HttpStatusCode.Conflict,(await Send(HttpMethod.Put,"/api/account/me",a.Token,update)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(HttpMethod.Put,"/api/account/me",a.Token,new{fullName="Escalate",phone=original.Phone,rowVersion=current.RowVersion,points=999999})).StatusCode);
    }
    [SqlServerFact] public async Task CartIsOwnedByAuthenticatedCustomerAndPutDeleteAreIdempotent()
    {
        var a=await Register();var b=await Register();var sku=data.Product(1)+"-M";
        Assert.Empty((await ReadCart(a.Token)).Items);
        Assert.Equal(HttpStatusCode.OK,(await Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku,quantity=1})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku=sku.ToLowerInvariant(),quantity=1})).StatusCode);
        var cart=await ReadCart(a.Token);Assert.Equal(sku,Assert.Single(cart.Items).Sku);Assert.Equal(2,Assert.Single(cart.Items).Quantity);Assert.Equal(200000m,cart.Subtotal);
        Assert.Empty((await ReadCart(b.Token)).Items);
        foreach(var i in new[]{1,2})Assert.Equal(HttpStatusCode.OK,(await Send(HttpMethod.Put,"/api/cart/items/"+sku,a.Token,new{quantity=1})).StatusCode);
        Assert.Equal(1,Assert.Single((await ReadCart(a.Token)).Items).Quantity);
        foreach(var i in new[]{1,2})Assert.Equal(HttpStatusCode.OK,(await Send(HttpMethod.Delete,"/api/cart/items/"+sku.ToLowerInvariant(),a.Token)).StatusCode);
        Assert.Empty((await ReadCart(a.Token)).Items);
    }
    [SqlServerFact] public async Task InvalidStockAndDisabledVariantsAreRejectedWithoutChangingInventory()
    {
        var a=await Register();
        Assert.Equal(HttpStatusCode.Conflict,(await Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku=data.Product(1)+"-M",quantity=3})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku=data.Product(1)+"-L",quantity=1})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku=data.Product(1)+"-X",quantity=1})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku=data.Product(1)+"-M",quantity=0})).StatusCode);
        Assert.Empty((await ReadCart(a.Token)).Items);
        var detail=(await client!.GetFromJsonAsync<ProductDetailResponse>("/api/products/"+data.Product(1)))!;
        Assert.Equal(2,detail.Variants.Single(v=>v.Size=="M").Stock);
    }
    [SqlServerFact] public async Task ConcurrentAddsDoNotLoseUpdatesOrDuplicateCartLines()
    {
        var a=await Register();var sku=data.Product(1)+"-M";
        var replies=await Task.WhenAll(Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku,quantity=1}),Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku,quantity=1}));
        Assert.All(replies,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
        Assert.Equal(2,Assert.Single((await ReadCart(a.Token)).Items).Quantity);
    }
    [SqlServerFact] public async Task CartRecalculatesCurrentSqlPriceAndFlagsStockChanges()
    {
        var a=await Register();var sku=data.Product(1)+"-M";
        Assert.Equal(HttpStatusCode.OK,(await Send(HttpMethod.Post,"/api/cart/items",a.Token,new{sku,quantity=2})).StatusCode);
        await using var sql=new SqlConnection(data.Connection);await sql.OpenAsync();
        async Task Set(decimal price,int stock){await using var cmd=sql.CreateCommand();cmd.CommandText="UPDATE dbo.BienTheSanPham SET DonGia=@p,SoLuongTonKho=@s WHERE MaSKU=@sku";cmd.Parameters.AddWithValue("@p",price);cmd.Parameters.AddWithValue("@s",stock);cmd.Parameters.AddWithValue("@sku",sku);await cmd.ExecuteNonQueryAsync();}
        try{await Set(120000,1);var cart=await ReadCart(a.Token);Assert.Equal(240000m,cart.Subtotal);Assert.False(cart.CanCheckout);Assert.False(Assert.Single(cart.Items).Available);}
        finally{await Set(100000,2);}
    }
    [SqlServerFact] public async Task ConcurrentRegistrationCreatesExactlyOneAccountForDuplicateRequest()
    {
        var name="tacct"+Guid.NewGuid().ToString("N");names.Add(name);
        var request=new RegisterRequest{UserName=name,Password=Password,FullName="Concurrent signup",Phone="08"+RandomNumberGenerator.GetInt32(100000000,999999999)};
        var results=await Task.WhenAll(client!.PostAsJsonAsync("/api/account/register",request),client.PostAsJsonAsync("/api/account/register",request));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);
    }
    [SqlServerFact] public async Task PasswordChangeChecksCurrentPasswordAndInvalidatesAllExistingSessions()
    {
        var a=await Register();
        var second=await client!.PostAsJsonAsync("/api/account/login",new LoginRequest{UserName=a.Name,Password=Password});
        var b=(await second.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(HttpMethod.Post,"/api/account/password",a.Token,new{currentPassword="incorrect",newPassword="Changed SQL password!"})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await Send(HttpMethod.Post,"/api/account/password",a.Token,new{currentPassword=Password,newPassword="Changed SQL password!"})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Send(HttpMethod.Get,"/api/account/me",a.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Send(HttpMethod.Get,"/api/account/me",b)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/account/login",new LoginRequest{UserName=a.Name,Password=Password})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/account/login",new LoginRequest{UserName=a.Name,Password="Changed SQL password!"})).StatusCode);
    }

}
