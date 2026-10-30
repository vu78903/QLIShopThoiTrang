using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FashionStore.Backend.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace FashionStore.Backend.Tests;

public sealed class PostgresLocalFactAttribute : FactAttribute
{
    public PostgresLocalFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FASHIONSTORE_POSTGRES_TEST_CONNECTION")))
            Skip = "Requires a new, empty local fashionstore_test PostgreSQL database; never use Supabase.";
    }
}

public sealed class SupabaseCustomerIntegrationTests
{
    [PostgresLocalFact]
    public async Task RealPostgresAccountProfilePasswordAndOwnedCartFlow()
    {
        var connection = Environment.GetEnvironmentVariable("FASHIONSTORE_POSTGRES_TEST_CONNECTION")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        Assert.Contains(settings.Host, new[] { "127.0.0.1", "localhost", "::1" });
        Assert.Equal("fashionstore_test", settings.Database);
        await using var sql = new NpgsqlConnection(connection);
        await sql.OpenAsync();
        async Task Execute(string command) { await using var cmd = new NpgsqlCommand(command, sql); await cmd.ExecuteNonQueryAsync(); }
        // Deliberately CREATE without IF NOT EXISTS: refuse to overwrite any existing test data.
        await Execute("""
            CREATE TABLE public.khachhang (
                makh varchar(20) PRIMARY KEY, hoten varchar(150) NOT NULL, sodienthoai varchar(20) NOT NULL UNIQUE,
                email varchar(254), diachi varchar(300), diemtichluy integer NOT NULL DEFAULT 0,
                hangthe varchar(30) NOT NULL DEFAULT 'Bạc', trangthai boolean NOT NULL DEFAULT true,
                tongchitieu numeric(18,2) NOT NULL DEFAULT 0, ngaysinh date, gioitinh varchar(20));
            CREATE TABLE public.loaisanpham (maloai varchar(20) PRIMARY KEY, tenloai varchar(100) NOT NULL, trangthai boolean NOT NULL);
            CREATE TABLE public.sanpham (
                masku varchar(20) PRIMARY KEY, masp varchar(20) NOT NULL, maloai varchar(20) NOT NULL REFERENCES public.loaisanpham,
                tensanpham varchar(200) NOT NULL, kichco varchar(30) NOT NULL, mausac varchar(60) NOT NULL,
                dongia numeric(18,2) NOT NULL, soluongtonkho integer NOT NULL, trangthai boolean NOT NULL, hinhanh text);
            INSERT INTO public.loaisanpham VALUES ('AO','Áo',true);
            INSERT INTO public.sanpham VALUES
                ('SKU-M','P1','AO','Áo test','M','Đen',100,10,true,'/black.png'),
                ('SKU-L','P1','AO','Áo test','L','Đỏ',200,4,true,'/red.png');
            """);
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Supabase-Customer-Cart.sql"));
        await Execute(migration);
        await Execute(migration); // Safe re-run must not erase registrations or data.
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Supabase", ["Database:EnableSupabaseCustomerFeatures"] = "true",
                ["Database:EnableSupabaseOrders"] = "true", ["Orders:EmployeeId"] = "NV01",
                ["ConnectionStrings:Supabase"] = connection
            }));
        });
        using var alice = factory.CreateClient(); using var bob = factory.CreateClient();
        const string password = "Synthetic test password 123!";
        async Task Register(HttpClient client, string name, string phone)
        {
            var response = await client.PostAsJsonAsync("/api/account/register", new RegisterRequest
                { UserName = name, Password = password, FullName = name, Phone = phone, Email = name + "@example.test" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        async Task Login(HttpClient client, string name, string pass)
        {
            var response = await client.PostAsJsonAsync("/api/account/login", new LoginRequest { UserName = name, Password = pass });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken);
        }
        await Register(alice, "alice", "0900000001"); await Register(bob, "bob", "0900000002");
        await Login(alice, "alice", password); await Login(bob, "bob", password);
        var duplicate = await alice.PostAsJsonAsync("/api/account/register", new RegisterRequest
            { UserName = "ALICE", FullName = "Duplicate", Phone = "0900000003", Password = password });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var profile = (await alice.GetFromJsonAsync<CustomerResponse>("/api/account/me"))!;
        Assert.Equal(8, Convert.FromBase64String(profile.RowVersion).Length);
        var update = new ProfileRequest { FullName = "Alice Updated", Phone = profile.Phone, Email = profile.Email,
            RowVersion = profile.RowVersion, ReceivePromotions = true };
        var updatedResponse = await alice.PutAsJsonAsync("/api/account/me", update);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = (await updatedResponse.Content.ReadFromJsonAsync<CustomerResponse>())!;
        Assert.True(updated.ReceivePromotions); Assert.NotEqual(profile.RowVersion, updated.RowVersion);
        update.FullName = "Stale update";
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync("/api/account/me", update)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync("/api/cart/items", new CartItemRequest { Sku = "SKU-M", Quantity = 2 })).StatusCode);
        var cart = (await alice.GetFromJsonAsync<CartResponse>("/api/cart"))!;
        Assert.Equal(200, cart.Subtotal); Assert.Equal("SKU-M", Assert.Single(cart.Items).Sku);
        Assert.Empty((await bob.GetFromJsonAsync<CartResponse>("/api/cart"))!.Items);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PutAsJsonAsync("/api/cart/items/SKU-M", new CartQuantityRequest { Quantity = 11 })).StatusCode);
        await Execute("UPDATE public.sanpham SET dongia=120, soluongtonkho=1 WHERE masku='SKU-M'");
        cart = (await alice.GetFromJsonAsync<CartResponse>("/api/cart"))!;
        Assert.Equal(240, cart.Subtotal); Assert.False(cart.CanCheckout); Assert.False(cart.Items[0].Available);
        Assert.Equal(HttpStatusCode.OK, (await alice.PutAsJsonAsync("/api/cart/items/SKU-M", new CartQuantityRequest { Quantity = 1 })).StatusCode);
        await using (var cmd = new NpgsqlCommand("SELECT soluongtonkho FROM public.sanpham WHERE masku='SKU-M'", sql))
            Assert.Equal(1, Convert.ToInt32(await cmd.ExecuteScalarAsync())); // Cart never deducts stock.
        await using (var cmd = new NpgsqlCommand("SELECT matkhauhash FROM webstore.customer_account WHERE tendangnhap='alice'", sql))
            Assert.NotEqual(password, (string)(await cmd.ExecuteScalarAsync())!);
        await Execute("UPDATE public.sanpham SET trangthai=false WHERE masku='SKU-M'");
        Assert.False((await alice.GetFromJsonAsync<CartResponse>("/api/cart"))!.Items[0].Available);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PostAsJsonAsync("/api/cart/items", new CartItemRequest { Sku = "SKU-M", Quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.DeleteAsync("/api/cart")).StatusCode);
        Assert.Empty((await alice.GetFromJsonAsync<CartResponse>("/api/cart"))!.Items);
        var parallelAdds = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            alice.PostAsJsonAsync("/api/cart/items", new CartItemRequest { Sku = "SKU-L", Quantity = 1 })));
        foreach (var response in parallelAdds)
        {
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            if (response.StatusCode == HttpStatusCode.Conflict)
                Assert.Equal(HttpStatusCode.OK, (await alice.PostAsJsonAsync("/api/cart/items", new CartItemRequest { Sku = "SKU-L", Quantity = 1 })).StatusCode);
        }
        Assert.Equal(2, Assert.Single((await alice.GetFromJsonAsync<CartResponse>("/api/cart"))!.Items).Quantity);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => alice.PostAsJsonAsync("/api/account/register",
            new RegisterRequest { UserName = "concurrent", FullName = "Race", Phone = "0900000009", Password = password })));
        Assert.Single(concurrent, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(concurrent, r => r.StatusCode == HttpStatusCode.Conflict);
        await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM public.khachhang WHERE sodienthoai='0900000009'", sql))
            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        // Shared invoice shape uses the partner's column names, sizes and numeric types.
        await Execute("""
            CREATE TABLE public.nhanvien(manv varchar(20) PRIMARY KEY,trangthai boolean NOT NULL);
            INSERT INTO public.nhanvien VALUES('NV01',true);
            CREATE TABLE public.hoadon(
                sohd varchar(30) PRIMARY KEY,ngaylap timestamp NOT NULL,loaihoadon varchar(30),phuongthucthanhtoan varchar(50),
                tongcong numeric(18,2),chietkhau numeric(18,2),thuevat numeric(18,2),khachphaitra numeric(18,2),
                tienkhachdua numeric(18,2),tienthua numeric(18,2),sodienthoai_kh varchar(20),
                makh varchar(20) REFERENCES public.khachhang,tennguoinhan varchar(150),diachigiao varchar(400),
                donvivanchuyen varchar(150),trangthaigiaohang varchar(50),trangthaithanhtoan varchar(30),
                phiship numeric(18,2),tiencod numeric(18,2),manv varchar(20) REFERENCES public.nhanvien,
                nguondon varchar(20),ghichugiaohang varchar(500));
            CREATE TABLE public.chitiethoadon(sohd varchar(30) REFERENCES public.hoadon,masku varchar(20) REFERENCES public.sanpham,
                soluong integer NOT NULL,dongia numeric(18,2) NOT NULL,thanhtien numeric(18,2) NOT NULL,PRIMARY KEY(sohd,masku));
            """);
        var orderMigration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Supabase-Web-Orders.sql"));
        await Execute(orderMigration); await Execute(orderMigration);
        var checkout = new CheckoutRequest("Alice Test", "0900000001", "123 Test street", "COD", "Test only", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.PostAsJsonAsync("/api/orders", checkout with { PaymentMethod = "CHUYEN_KHOAN" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/orders", checkout with { Voucher = "TEST" })).StatusCode);
        await Execute("UPDATE public.nhanvien SET trangthai=false");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await alice.PostAsJsonAsync("/api/orders", checkout)).StatusCode);
        await Execute("UPDATE public.nhanvien SET trangthai=true; UPDATE public.sanpham SET soluongtonkho=1 WHERE masku='SKU-L'");
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/api/orders", checkout)).StatusCode);
        Assert.Equal(2, Assert.Single((await alice.GetFromJsonAsync<CartResponse>("/api/cart"))!.Items).Quantity);
        await Execute("UPDATE public.sanpham SET soluongtonkho=4,dongia=250 WHERE masku='SKU-L'");
        var submissions = await Task.WhenAll(alice.PostAsJsonAsync("/api/orders", checkout), alice.PostAsJsonAsync("/api/orders", checkout));
        foreach (var result in submissions) Assert.Contains(result.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        var placedResponse = await alice.PostAsJsonAsync("/api/orders", checkout);
        Assert.Equal(HttpStatusCode.OK, placedResponse.StatusCode);
        var placed = (await placedResponse.Content.ReadFromJsonAsync<PlaceOrderResponse>())!;
        Assert.Equal(500, placed.Total);
        Assert.Empty((await alice.GetFromJsonAsync<CartResponse>("/api/cart"))!.Items);
        Assert.Single((await alice.GetFromJsonAsync<List<OrderSummary>>("/api/orders"))!);
        Assert.Empty((await bob.GetFromJsonAsync<List<OrderSummary>>("/api/orders"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync("/api/orders/" + placed.OrderId)).StatusCode);
        await using (var cmd = new NpgsqlCommand("SELECT count(*) FROM public.hoadon WHERE nguondon='WEB' AND manv='NV01' AND trangthaithanhtoan='CHUA_THANH_TOAN'", sql))
            Assert.Equal(1L, (long)(await cmd.ExecuteScalarAsync())!);
        await using (var cmd = new NpgsqlCommand("SELECT soluongtonkho FROM public.sanpham WHERE masku='SKU-L'", sql))
            Assert.Equal(4, Convert.ToInt32(await cmd.ExecuteScalarAsync()));
        await Execute("UPDATE public.sanpham SET tensanpham='Renamed',dongia=900; UPDATE public.hoadon SET trangthaigiaohang='Hàng giao thành công',trangthaithanhtoan='DA_THANH_TOAN'");
        var order = (await alice.GetFromJsonAsync<OrderDetails>("/api/orders/" + placed.OrderId))!;
        Assert.Equal("HOAN_TAT", order.Status); Assert.Equal("DA_THANH_TOAN", order.PaymentStatus);
        Assert.Equal("Áo test", Assert.Single(order.Items).Name); Assert.Equal(250, order.Items[0].UnitPrice);
        Assert.False(order.SupportsHistory); Assert.False(order.SupportsReturns);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsync("/api/orders/" + placed.OrderId + "/cancel", null)).StatusCode);
        // Windows currently deletes pending COD invoices. A retained snapshot must not become a successful new retry.
        await Execute("DELETE FROM public.chitiethoadon; DELETE FROM public.hoadon");
        order = (await alice.GetFromJsonAsync<OrderDetails>("/api/orders/" + placed.OrderId))!;
        Assert.Equal("CAN_HO_TRO", order.Status);
        var retryRemoved = (await (await alice.PostAsJsonAsync("/api/orders", checkout)).Content.ReadFromJsonAsync<PlaceOrderResponse>())!;
        Assert.Equal("CAN_HO_TRO", retryRemoved.Status);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync("/api/account/password", new ChangePasswordRequest
            { CurrentPassword = password, NewPassword = password + "new" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/account/me")).StatusCode);
        await Login(alice, "alice", password + "new");
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync("/api/account/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.GetAsync("/api/cart")).StatusCode);
    }
}
