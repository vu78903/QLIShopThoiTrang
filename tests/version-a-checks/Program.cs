using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using QLIShopThoiTrang.Api.Data;
using QLIShopThoiTrang.Api.Models;
using QLIShopThoiTrang.Api.Services;

var checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
var services = new ServiceCollection().AddLogging().AddOptions().AddSingleton<IStaffSessionValidator,UnitStaffValidator>().BuildServiceProvider();
var sessions = new StaffSessions();
async Task<DefaultHttpContext> Authorize(string method, string path, string? role = null, string? body = null, string? token = null)
{
    var context = new DefaultHttpContext { RequestServices = services };
    context.Request.Method = method; context.Request.Path = path;
    context.Request.ContentType = "application/json";
    context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body ?? "{}"));
    context.Response.Body = new MemoryStream();
    if (token != null) context.Request.Headers.Authorization = "Bearer " + token;
    else if (role != null) context.Request.Headers.Authorization = "Bearer " + sessions.Create(new("NV-TEST", "Test", role));
    await new StaffAuthorizationMiddleware(c => { c.Response.StatusCode = 204; return Task.CompletedTask; }).InvokeAsync(context, sessions);
    return context;
}
foreach (var path in new[] { "/api/app-state", "/api/hoa-don", "/API/HOA-DON/", "/api/loai-san-pham", "/api/doi-tra", "/api/kiem-nhan-kho", "/api/doi-tac/x/y/tai-khoan", "/api/unknown" })
    Check((await Authorize("GET", path)).Response.StatusCode == 401, "Anonymous read: " + path);
Check((await Authorize("HEAD", "/api/app-state")).Response.StatusCode == 401, "HEAD must authenticate");
foreach (var path in new[] { "/api/hoa-don", "/api/doi-tra", "/api/thanh-toan-qr/1", "/api/san-pham/quan-ly" })
    Check((await Authorize("GET", path, "NhanVienKho")).Response.StatusCode == 403, "Warehouse privacy: " + path);
Check((await Authorize("GET", "/api/app-state", "unknown")).Response.StatusCode == 403, "Unknown role");
Check((await Authorize("GET", "/api/app-state", "ThuNgan")).Response.StatusCode == 204, "Cashier reads app state");
Check((await Authorize("GET", "/api/kiem-nhan-kho", "NhanVienKho")).Response.StatusCode == 204, "Warehouse reads stock review");
Check((await Authorize("POST", "/api/san-pham", "ThuNgan")).Response.StatusCode == 403, "Cashier cannot save catalog");
foreach (var path in new[] { "/api/dang-nhap", "/api/thanh-toan-qr/webhook" })
    Check((await Authorize("POST", path)).Response.StatusCode == 204, "Public POST entry");
Check((await Authorize("GET", "/api/dang-nhap")).Response.StatusCode == 401, "Login exemption is POST only");
var stamped = await Authorize("POST", "/api/hoa-don", "ThuNgan", "{\"employeeId\":\"FORGED\",\"EmployeeId\":\"FORGED AGAIN\"}");
Check(stamped.Response.StatusCode == 204 && await new StreamReader(stamped.Request.Body).ReadToEndAsync() is var text && text.Contains("NV-TEST") && !text.Contains("FORGED"), "Stamp actor even without Content-Length");
Check((await Authorize("POST", "/api/hoa-don", "ThuNgan", "{")).Response.StatusCode == 400, "Malformed JSON");
Check((await Authorize("POST", "/api/doi-tac", "ThuNgan", "{\"category\":\"Nhân viên\"}")).Response.StatusCode == 403, "Quick-add restricted to customers");
var fakeValidator=(UnitStaffValidator)services.GetRequiredService<IStaffSessionValidator>();
fakeValidator.DuringCheck=()=>sessions.RemoveEmployee("NV-TEST");
Check((await Authorize("GET","/api/app-state","ThuNgan")).Response.StatusCode==401,"Revocation during database validation cannot authorize an old cached user");
fakeValidator.DuringCheck=null;
var staleToken=sessions.Create(new("NV-TEST","Test","ThuNgan",SessionVersion:"old"));
var freshToken=sessions.Create(new("NV-TEST","Test","ThuNgan",SessionVersion:"new"));
fakeValidator.Accept=user=>user.SessionVersion!="old";
Check((await Authorize("GET","/api/app-state",token:staleToken)).Response.StatusCode==401 && sessions.Get(staleToken) is null,"Stale token alone is removed on validation failure");
Check((await Authorize("GET","/api/app-state",token:freshToken)).Response.StatusCode==204,"Late request from old token preserves newly logged-in session");
fakeValidator.Accept=null;
string? concurrentFreshToken=null;
fakeValidator.DuringCheck=()=>{sessions.RemoveEmployee("NV-TEST");concurrentFreshToken=sessions.Create(new("NV-TEST","Test","ThuNgan"));};
Check((await Authorize("GET","/api/app-state","ThuNgan")).Response.StatusCode==401,"Token revoked during validation is denied");
fakeValidator.DuringCheck=null;
Check(concurrentFreshToken is not null && (await Authorize("GET","/api/app-state",token:concurrentFreshToken)).Response.StatusCode==204,"Concurrent login survives old request finishing after revocation");
var versionOld=sessions.Create(new("VERSION-TEST","Test","ThuNgan",SessionVersion:"old"));
var versionNew=sessions.Create(new("VERSION-TEST","Test","ThuNgan",SessionVersion:"new"));
sessions.RemoveEmployeeVersion(" VERSION-TEST ","old");
Check(sessions.Get(versionOld) is null && sessions.Get(versionNew) is not null,"Version-scoped revocation preserves concurrently issued current token");
Check((await Authorize("GET","/api/bao-cao/ban-chay")).Response.StatusCode==401,"Best sellers requires login");
Check((await Authorize("GET","/api/bao-cao/ban-chay","ThuNgan")).Response.StatusCode==403,"Cashier cannot read best sellers report");
Check((await Authorize("GET","/api/bao-cao/ban-chay","TruongCa")).Response.StatusCode==204,"Shift leader can read best sellers report");
Console.WriteLine($"PASS {checks} authorization checks.");

var connectionString = Environment.GetEnvironmentVariable("FORM_TEST_POSTGRES");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("SKIP database/HTTP checks: set FORM_TEST_POSTGRES to a local PostgreSQL admin connection.");
    return;
}
var settings = new NpgsqlConnectionStringBuilder(connectionString);
if (settings.Host is not ("127.0.0.1" or "localhost" or "::1") || settings.Database != "fashionstore_test")
    throw new Exception("Refusing non-local or non-test database.");
var database = "fashionstore_form_test_" + Guid.NewGuid().ToString("N");
settings.Database = "postgres"; settings.Pooling = false;
await using var admin = new NpgsqlConnection(settings.ConnectionString); await admin.OpenAsync();
await using (var create = new NpgsqlCommand("CREATE DATABASE " + database, admin)) await create.ExecuteNonQueryAsync();
Process? api = null;
try
{
    settings.Database = database;
    await using var sql = new NpgsqlConnection(settings.ConnectionString); await sql.OpenAsync();
    async Task Exec(string query) { await using var c = new NpgsqlCommand(query, sql) { CommandTimeout = 60 }; await c.ExecuteNonQueryAsync(); }
    async Task<T> Scalar<T>(string query) { await using var c = new NpgsqlCommand(query, sql); return (T)(await c.ExecuteScalarAsync())!; }
    static string Q(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixture-schema.json")));
    using var contract=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Data","SchemaContract.json")));
    foreach (var table in fixture.RootElement.GetProperty("tables").EnumerateArray())
    {
        var baseColumns=table.GetProperty("columns").EnumerateArray().ToArray();
        var columns = baseColumns.Select(col =>
        {
            var type = col.GetProperty("type").GetString()!;
            var fallback = type == "boolean" ? "true" : type == "jsonb" ? "'[]'::jsonb" : type.StartsWith("timestamp") ? "CURRENT_TIMESTAMP" : type == "date" ? "CURRENT_DATE" : type is "integer" or "bigint" || type.StartsWith("numeric") ? "0" : "''";
            return Q(col.GetProperty("name").GetString()!) + " " + type + (col.GetProperty("not_null").GetBoolean() ? " NOT NULL DEFAULT " + fallback : "");
        });
        var extras=contract.RootElement.GetProperty("tables").EnumerateArray().Single(t=>t.GetProperty("name").GetString()==table.GetProperty("name").GetString()).GetProperty("columns").EnumerateArray()
            .Where(col=>col.GetProperty("name").GetString()!="phienbanbaomat" && !baseColumns.Any(b=>b.GetProperty("name").GetString()==col.GetProperty("name").GetString()))
            .Select(col=>Q(col.GetProperty("name").GetString()!)+" "+col.GetProperty("type").GetString());
        await Exec("CREATE TABLE public." + Q(table.GetProperty("name").GetString()!) + "(" + string.Join(',', columns.Concat(extras)) + ")");
    }
    foreach (var c in fixture.RootElement.GetProperty("constraints").EnumerateArray().OrderBy(c => c.GetProperty("type").GetString() == "f" ? 1 : 0))
        await Exec("ALTER TABLE public." + Q(c.GetProperty("table").GetString()!) + " ADD CONSTRAINT " + Q(c.GetProperty("name").GetString()!) + " " + c.GetProperty("definition").GetString());
    foreach(var view in fixture.RootElement.GetProperty("views").EnumerateArray())await Exec("CREATE VIEW public."+Q(view.GetProperty("name").GetString()!)+" AS "+view.GetProperty("definition").GetString());
    await Exec("ALTER TABLE hoadon ALTER COLUMN datrutonkho SET DEFAULT FALSE; ALTER TABLE hoadon ALTER COLUMN daghinhanthanhtoan SET DEFAULT FALSE;");
    await Exec("""
        INSERT INTO phanquyen(maquyen,tenquyen) VALUES('CHT','Manager'),('TN','Cashier'),('TC','Shift'),('KHO','Warehouse');
        INSERT INTO nhanvien(manv,tendangnhap,matkhau,hoten,sodienthoai,maquyen) VALUES
          ('M','manager','test123','Manager','0900000001','CHT'),('C','cashier','test123','Cashier','0900000002','TN'),
          ('C2','other','test123','Other','0900000003','TN'),('S','shift','test123','Shift','0900000004','TC'),('W','warehouse','test123','Warehouse','0900000005','KHO');
        INSERT INTO nhasanxuat(mansx,tennsx,sodienthoai) VALUES('NCC-001','Chợ An Đông','0900000006');
        INSERT INTO loaisanpham(maloai,tenloai) VALUES('AO','Áo thun nam');
        INSERT INTO sanpham(masp,masku,maloai,tensanpham,kichco,mausac,dongia,soluong,soluongtonkho,danhmuc,mansx)
          VALUES('SP-900','TEST-M','AO','Test','M','Đen',100000,20,20,'NAM','NCC-001');
        INSERT INTO khachhang(makh,hoten,sodienthoai,email,diachi,hangthe,tendangnhap,matkhau)
          VALUES('KH','Customer','0900000007','private@example.test','Test address','Bạc','customer','TEST HASH MUST NOT LEAK');
        INSERT INTO hoadon(sohd,manv,makh,loaihoadon,nguondon,phuongthucthanhtoan,trangthaithanhtoan,dauvantayyeucau)
          VALUES('OWN','C','KH','TAI_QUAY','POS','TIEN_MAT','DA_THANH_TOAN','test'),
          ('OTHER','C2','KH','TAI_QUAY','POS','TIEN_MAT','DA_THANH_TOAN','test'),
          ('WEB','M','KH','COD','WEB','COD','CHUA_THANH_TOAN','test');
        INSERT INTO chitiethoadon(sohd,masku,soluong,dongia,thanhtien) VALUES('OWN','TEST-M',1,100000,100000),('OTHER','TEST-M',1,100000,100000);
        """);
    await Exec("UPDATE hoadon SET datrutonkho=NULL,daghinhanthanhtoan=NULL");
    Check(await Scalar<long>("SELECT count(*) FROM pg_tables WHERE schemaname='public'")==18,"Fixture uses 18 tables");
    // Override only this test process so the unmodified factory cannot pick a real connection.
    Environment.SetEnvironmentVariable("YODYSTORE_CONNECTION_STRING",settings.ConnectionString);
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:YodyStoreDb"] = settings.ConnectionString }).Build();
    var db = new SqlServerConnectionFactory(config); var repository = new StoreRepository(db);
    var initializer = new SqlServerDatabaseInitializer(db);
    await Exec("""
        INSERT INTO sanpham(masp,masku,maloai,tensanpham,kichco,mausac,dongia,soluong,soluongtonkho,danhmuc,mansx)
        VALUES('SP-900','BEST-S','AO','Test','S','Đen',100000,10,10,'NAM','NCC-001'),
              ('SP-901','BEST-OTHER','AO','Other product','M','Đen',100000,10,10,'NAM','NCC-001');
        INSERT INTO hoadon(sohd,manv,loaihoadon,phuongthucthanhtoan,trangthaithanhtoan,trangthaigiaohang,ngaylap)
        VALUES('BEST-1','M','TAI_QUAY','TIEN_MAT','DA_THANH_TOAN',NULL,'2020-01-01 23:59:59'),
              ('BEST-2','M','TAI_QUAY','TIEN_MAT','DA_THANH_TOAN',NULL,'2020-01-02 00:00:00'),
              ('BEST-CANCEL','M','COD','COD','DA_THANH_TOAN','Đã hủy','2020-01-01'),
              ('BEST-UNPAID','M','COD','COD','CHUA_THANH_TOAN',NULL,'2020-01-01'),
              ('BEST-OTHER','M','TAI_QUAY','TIEN_MAT','DA_THANH_TOAN',NULL,'2020-01-01'),
              ('BEST-TYPE','M','WEB_HUY','COD','DA_THANH_TOAN',NULL,'2020-01-01');
        INSERT INTO chitiethoadon(sohd,masku,soluong,dongia,thanhtien)
        VALUES('BEST-1','TEST-M',3,100000,300000),('BEST-1','BEST-S',2,100000,200000),
              ('BEST-2','TEST-M',4,100000,400000),('BEST-CANCEL','TEST-M',99,100000,9900000),
              ('BEST-UNPAID','TEST-M',88,100000,8800000),('BEST-OTHER','BEST-OTHER',6,100000,600000),
              ('BEST-TYPE','TEST-M',77,100000,7700000);
        """);
    var bestDay=await repository.GetBestSellingProductsAsync(new DateOnly(2020,1,1),new DateOnly(2020,1,1));
    Check(bestDay.Count==2 && bestDay[0].ProductId=="SP-901" && bestDay[0].Quantity==6,"Best sellers ranks by units, not number of variants");
    Check(bestDay[1].Quantity==5 && bestDay[1].OrderCount==1,"Best sellers merges SKUs and counts distinct paid non-cancelled invoices");
    var bestRange=await repository.GetBestSellingProductsAsync(new DateOnly(2020,1,1),new DateOnly(2020,1,2));
    Check(bestRange[0].ProductId=="SP-900" && bestRange[0].Quantity==9 && bestRange[0].OrderCount==2,"Best sellers includes whole end date and preserves next day boundary");
    Check((await repository.GetBestSellingProductsAsync(new DateOnly(2019,1,1),new DateOnly(2019,1,1))).Count==0,"Best sellers empty range");
    await Exec("DELETE FROM chitiethoadon WHERE sohd LIKE 'BEST-%';DELETE FROM hoadon WHERE sohd LIKE 'BEST-%';DELETE FROM sanpham WHERE masku IN ('BEST-S','BEST-OTHER');");
    var securityMigration=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"BoSung-PhienBanBaoMat.sql"));
    var missingSecurityRejected=false;
    try{await initializer.InitializeAsync();}catch(InvalidOperationException ex){missingSecurityRejected=ex.Message.Contains("phienbanbaomat");}
    Check(missingSecurityRejected,"Startup fails clearly before security migration; it never applies DDL itself");
    const string profiles="SELECT jsonb_build_object('staff',(SELECT jsonb_agg(to_jsonb(n)-'phienbanbaomat' ORDER BY manv) FROM nhanvien n),'roles',(SELECT jsonb_agg(to_jsonb(p)-'phienbanbaomat' ORDER BY maquyen) FROM phanquyen p))::text";
    var profilesBefore=await Scalar<string>(profiles);
    await Exec(securityMigration);
    Check(profilesBefore==await Scalar<string>(profiles),"Security migration preserves existing profiles, credentials and permissions");
    Check(await Scalar<long>("SELECT count(*) FROM pg_tables WHERE schemaname='public'")==18,"Security migration retains exactly 18 tables");
    var versionsBefore=await Scalar<string>("SELECT jsonb_build_object('staff',(SELECT jsonb_agg(phienbanbaomat ORDER BY manv) FROM nhanvien),'roles',(SELECT jsonb_agg(phienbanbaomat ORDER BY maquyen) FROM phanquyen))::text");
    await Exec(securityMigration);
    Check(versionsBefore==await Scalar<string>("SELECT jsonb_build_object('staff',(SELECT jsonb_agg(phienbanbaomat ORDER BY manv) FROM nhanvien),'roles',(SELECT jsonb_agg(phienbanbaomat ORDER BY maquyen) FROM phanquyen))::text"),"Reapplying migration preserves established security versions");
    await Exec("ALTER TABLE nhanvien DISABLE TRIGGER trg_phienbanbaomat_nhanvien");
    var disabledTriggerRejected=false;
    try{await initializer.InitializeAsync();}catch(InvalidOperationException ex){disabledTriggerRejected=ex.Message.Contains("trigger phiên bản bảo mật");}
    Check(disabledTriggerRejected,"Startup refuses disabled security trigger");
    await Exec("ALTER TABLE nhanvien ENABLE TRIGGER trg_phienbanbaomat_nhanvien");
    var before = await Scalar<string>("SELECT jsonb_agg(to_jsonb(h) ORDER BY sohd)::text FROM hoadon h");
    await initializer.InitializeAsync(); await initializer.InitializeAsync();
    Check(before == await Scalar<string>("SELECT jsonb_agg(to_jsonb(h) ORDER BY sohd)::text FROM hoadon h"), "Startup preserves data");
    // default_transaction_read_only proves startup works without write transactions.
    var readOnly = new NpgsqlConnectionStringBuilder(settings.ConnectionString) { Options = "-c default_transaction_read_only=on" };
    Environment.SetEnvironmentVariable("YODYSTORE_CONNECTION_STRING",readOnly.ConnectionString);
    await new SqlServerDatabaseInitializer(new SqlServerConnectionFactory(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:YodyStoreDb"] = readOnly.ConnectionString }).Build())).InitializeAsync();
    checks++;
    Environment.SetEnvironmentVariable("YODYSTORE_CONNECTION_STRING",settings.ConnectionString);
    Check(await Scalar<long>("SELECT count(*) FROM hoadon WHERE datrutonkho IS NULL AND daghinhanthanhtoan IS NULL")==3,"Startup does not guess historical flags");
    await Exec("ALTER TABLE hoadon RENAME COLUMN datrutonkho TO datrutonkho_missing");
    try { await initializer.InitializeAsync(); throw new Exception("Missing schema must fail"); }
    catch (InvalidOperationException ex) { Check(ex.Message.Contains("hoadon.datrutonkho"), "Missing schema reported"); }
    Check(await Scalar<long>("SELECT count(*) FROM information_schema.columns WHERE table_name='hoadon' AND column_name='datrutonkho'") == 0, "Startup does not repair schema");
    await Exec("ALTER TABLE hoadon RENAME COLUMN datrutonkho_missing TO datrutonkho");
    var stale = new ProductUpsertDto("SP-900","TEST-M","Changed name","AO","NAM","NCC-001",100000,20,"Đen","M");
    await Exec("UPDATE sanpham SET soluong=17,soluongtonkho=17 WHERE masku='TEST-M'");
    await repository.SaveProductAsync(stale);
    Check(await Scalar<int>("SELECT soluongtonkho FROM sanpham WHERE masku='TEST-M'") == 17 && await Scalar<int>("SELECT soluong FROM sanpham WHERE masku='TEST-M'") == 17, "Metadata save cannot restore stale stock");
    // Hold the sale row lock until the metadata save is actually waiting for it.
    await using (var blocker = new NpgsqlConnection(settings.ConnectionString))
    {
        await blocker.OpenAsync(); await using var transaction = await blocker.BeginTransactionAsync();
        await using (var sale = new NpgsqlCommand("UPDATE sanpham SET soluong=14,soluongtonkho=14 WHERE masku='TEST-M'",blocker,transaction)) await sale.ExecuteNonQueryAsync();
        var saving = repository.SaveProductAsync(stale);
        var waiting = false;
        for (var n=0;n<100;n++) { if (await Scalar<long>("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock'") > 0) { waiting=true; break; } await Task.Delay(20); }
        Check(waiting,"Concurrent metadata save waits for sale row lock");
        await transaction.CommitAsync(); await saving.WaitAsync(TimeSpan.FromSeconds(10));
    }
    Check(await Scalar<int>("SELECT soluongtonkho FROM sanpham WHERE masku='TEST-M'") == 14, "Concurrent sale stock survives metadata save");
    await repository.SaveProductAsync(stale with { Sku="TEST-L", Size="L", Stock=5 });
    Check(await Scalar<int>("SELECT soluongtonkho FROM sanpham WHERE masku='TEST-L'") == 5,"New SKU can initialize stock");
    var warehouse = await repository.GetWarehouseAppStateAsync("W");
    Check(warehouse.PartnersData.All(p=>p.Category=="Nhà cung cấp" || p.Category=="Nhân viên" && p.Id=="W") && warehouse.SalesHistory.Count==0 && warehouse.CodOrders.Count==0 && warehouse.DeliveryOrders.Count==0 && warehouse.CancelledOrders.Count==0,"Warehouse data scope");
    var cashier=await repository.GetAppStateAsync();
    Check(cashier.SalesHistory.Any(s=>s.Id=="OWN") && cashier.SalesHistory.Any(s=>s.Id=="OTHER"),"Cashier sales history is not restricted by creator");
    var invoices=await repository.GetInvoicesAsync(null);
    Check(new[]{"OWN","OTHER","WEB"}.All(id=>invoices.Any(i=>i.Id==id)),"Invoice lookup includes other cashiers and web");

    await Exec("""
        INSERT INTO hoadon(sohd,manv,loaihoadon,nguondon,phuongthucthanhtoan,trangthaigiaohang,trangthaithanhtoan,datrutonkho)
        VALUES('IN-TRANSIT','M','GIAO_HANG','POS','TIEN_MAT','Hàng đang được vận chuyển','DA_THANH_TOAN',TRUE),
              ('CANCELLED-REAL','M','GIAO_HANG','POS','TIEN_MAT','Đã hủy','DA_THANH_TOAN',TRUE),
              ('CANCELLED-ALT','M','COD','WEB','COD','Đã huỷ','CHUA_THANH_TOAN',TRUE);
        """);
    var shipments=await repository.GetAppStateAsync();
    Check(shipments.DeliveryOrders.Any(o=>o.Id=="IN-TRANSIT") && !shipments.CancelledOrders.Any(o=>o.Id=="IN-TRANSIT"),"In-transit text is never classified as cancellation");
    Check(new[]{"CANCELLED-REAL","CANCELLED-ALT"}.All(id=>shipments.CancelledOrders.Any(o=>o.Id==id)),"Actual cancellation spellings remain cancelled");
    await repository.CancelShipmentAsync("IN-TRANSIT","Test actual cancellation");
    Check(await Scalar<string>("SELECT trangthaigiaohang FROM hoadon WHERE sohd='IN-TRANSIT'")=="Đã hủy","Cancel shipment does not mistake CHUYEN for HUY and skip the update");

    var apiDll = args.FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(apiDll))
    {
        using var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("FORM_TEST_DOTNET") ?? "dotnet") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(apiDll))! };
        start.ArgumentList.Add(Path.GetFullPath(apiDll));start.ArgumentList.Add("--urls");start.ArgumentList.Add($"http://127.0.0.1:{port}");
        start.Environment["YODYSTORE_CONNECTION_STRING"]=settings.ConnectionString;
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Production";
        api=Process.Start(start)!;var log=api.StandardOutput.ReadToEndAsync();var error=api.StandardError.ReadToEndAsync();
        using var client=new HttpClient(new HttpClientHandler { UseProxy=false }) {BaseAddress=new Uri($"http://127.0.0.1:{port}"),Timeout=TimeSpan.FromSeconds(30)};
        var ready=false;
        for(var n=0;n<100;n++){if(api.HasExited)throw new Exception("Test API failed: "+await log+await error);try{if((await client.GetAsync("/api/health")).IsSuccessStatusCode){ready=true;break;}}catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException){} await Task.Delay(100);}
        Check(ready,"Test API startup");
        foreach(var route in new[]{"/api/app-state","/api/hoa-don","/API/HOA-DON/"})Check((await client.GetAsync(route)).StatusCode==HttpStatusCode.Unauthorized,"HTTP anonymous "+route);
        using(var health=await client.GetAsync("/api/health")){var data=await health.Content.ReadAsStringAsync();Check(!data.Contains("database"),"Public health hides connection details");}
        using(var login=await client.PostAsJsonAsync("/api/dang-nhap",new{username="cashier",password="test123"}))
        {login.EnsureSuccessStatusCode();using var data=JsonDocument.Parse(await login.Content.ReadAsStringAsync());client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",data.RootElement.GetProperty("token").GetString());}
        foreach(var invoiceId in new[]{"OWN","OTHER","WEB"})Check((await client.GetAsync("/api/hoa-don/"+invoiceId)).StatusCode==HttpStatusCode.OK,"HTTP cashier reads invoice "+invoiceId);
        using(var list=JsonDocument.Parse(await client.GetStringAsync("/api/hoa-don")))Check(new[]{"OWN","OTHER","WEB"}.All(id=>list.RootElement.EnumerateArray().Any(i=>i.GetProperty("id").GetString()==id)),"HTTP cashier lists all operational invoices");
        using(var login=await client.PostAsJsonAsync("/api/dang-nhap",new{username="manager",password="test123"}))
        {login.EnsureSuccessStatusCode();using var data=JsonDocument.Parse(await login.Content.ReadAsStringAsync());client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",data.RootElement.GetProperty("token").GetString());}
        Check((await client.GetAsync("/api/bao-cao/ban-chay")).IsSuccessStatusCode,"HTTP manager reads best sellers report");
        Check((await client.GetAsync("/api/bao-cao/ban-chay?from=2020-01-02&to=2020-01-01")).StatusCode==HttpStatusCode.BadRequest,"HTTP best sellers rejects reversed dates");
        using(var saved=await client.PostAsJsonAsync("/api/san-pham",stale))
        {saved.EnsureSuccessStatusCode();using var data=JsonDocument.Parse(await saved.Content.ReadAsStringAsync());Check(data.RootElement.GetProperty("stock").GetInt32()==14,"HTTP metadata save returns current stock, not stale input");}
        async Task LoginAs(string name,string password="test123")
        {
            using var login=await client.PostAsJsonAsync("/api/dang-nhap",new{username=name,password});login.EnsureSuccessStatusCode();
            using var data=JsonDocument.Parse(await login.Content.ReadAsStringAsync());client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",data.RootElement.GetProperty("token").GetString());
        }
        await LoginAs("cashier");
        // Đổi hồ sơ qua SQL/API không được thu hồi phiên; bảo mật đổi rồi phục hồi vẫn thu hồi.
        var profileToken=client.DefaultRequestHeaders.Authorization!;
        var originalSecurity=(await repository.GetActiveStaffAsync("C"))!.SessionVersion;
        await Exec("UPDATE nhanvien SET hoten='Updated name',sodienthoai='0900000022',email='profile@example.com',diachi='Updated address',chucvu='Updated title' WHERE manv='C'; UPDATE phanquyen SET tenquyen='Updated display name' WHERE maquyen='TN'");
        Check((await client.GetAsync("/api/app-state")).IsSuccessStatusCode,"SQL profile and role display-name edits preserve active session");
        Check((await repository.GetActiveStaffAsync("C"))!.SessionVersion==originalSecurity,"Only nonsecurity fields changed: security version stays unchanged");
        await LoginAs("manager");
        var profileUpdate=new PartnerUpsertDto("Nhân viên","C","Cashier renamed","0900000023","profile@example.com","Address changed","Nhân viên thu ngân",Username:"cashier",Password:"test123");
        Check((await client.PostAsJsonAsync("/api/doi-tac",profileUpdate)).IsSuccessStatusCode,"Manager edits profile with unchanged credentials filled by form");
        client.DefaultRequestHeaders.Authorization=profileToken;
        Check((await client.GetAsync("/api/app-state")).IsSuccessStatusCode,"API profile edit with unchanged credential fields preserves old token");
        await LoginAs("manager");
        Check((await client.PostAsJsonAsync("/api/doi-tac",new PartnerUpsertDto("Nhân viên","M","Manager renamed","0900000024","manager@example.com","Address changed","Cửa hàng trưởng",Username:"manager",Password:"test123"))).IsSuccessStatusCode,"Manager can edit own name/phone");
        Check((await client.GetAsync("/api/app-state")).IsSuccessStatusCode,"Own profile edit does not log manager out");
        client.DefaultRequestHeaders.Authorization=profileToken;
        await Exec("UPDATE nhanvien SET matkhau='changed123' WHERE manv='C'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Changed password immediately invalidates old token");
        await LoginAs("cashier","changed123");
        Check((await client.GetAsync("/api/app-state")).IsSuccessStatusCode,"New password works without format change");
        await Exec("UPDATE nhanvien SET matkhau='test123' WHERE manv='C'; UPDATE nhanvien SET tendangnhap='temporary_login' WHERE manv='C'; UPDATE nhanvien SET tendangnhap='cashier' WHERE manv='C'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Restoring username/password cannot resurrect old token");
        await LoginAs("cashier");
        await Exec("UPDATE phanquyen SET trangthai=FALSE WHERE maquyen='TN'; UPDATE phanquyen SET trangthai=TRUE WHERE maquyen='TN'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Permission group disable/re-enable between requests invalidates old token");
        await LoginAs("cashier");
        var oldCustomer=await Scalar<string>("SELECT to_jsonb(k)::text FROM khachhang k WHERE makh='KH'");
        var quick=new PartnerUpsertDto("Khách hàng","KH","Forged customer","0900099991","forged@example.com","Changed address",null);
        Check((await client.PostAsJsonAsync("/api/doi-tac",quick)).StatusCode==HttpStatusCode.Conflict,"Cashier cannot update existing customer by ID");
        Check(oldCustomer==await Scalar<string>("SELECT to_jsonb(k)::text FROM khachhang k WHERE makh='KH'"),"Rejected quick edit preserves entire customer record");
        var raced=await Task.WhenAll(client.PostAsJsonAsync("/api/doi-tac",quick with{Id="NEW-QUICK",Name="First",Phone="0900099992"}),client.PostAsJsonAsync("/api/doi-tac",quick with{Id="NEW-QUICK",Name="Second",Phone="0900099993"}));
        Check(raced.Count(r=>r.IsSuccessStatusCode)==1 && raced.Count(r=>r.StatusCode==HttpStatusCode.Conflict)==1,"Concurrent quick-add creates once without updating winner");foreach(var r in raced)r.Dispose();
        var inactiveToken=client.DefaultRequestHeaders.Authorization!;
        await Exec("UPDATE nhanvien SET trangthai=FALSE WHERE manv='C'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Direct database disable invalidates token on next request");
        await Exec("UPDATE nhanvien SET trangthai=TRUE WHERE manv='C'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Re-enabled employee cannot reuse revoked token");
        await LoginAs("cashier");
        var delayedToken=client.DefaultRequestHeaders.Authorization!;
        await Exec("UPDATE nhanvien SET trangthai=FALSE WHERE manv='C'; UPDATE nhanvien SET trangthai=TRUE WHERE manv='C'");
        await LoginAs("cashier");var currentToken=client.DefaultRequestHeaders.Authorization!;
        client.DefaultRequestHeaders.Authorization=delayedToken;
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"HTTP rejects delayed stale request after fresh login");
        client.DefaultRequestHeaders.Authorization=currentToken;
        Check((await client.GetAsync("/api/app-state")).IsSuccessStatusCode,"HTTP stale request cannot invalidate newly issued employee token");
        await Exec("UPDATE nhanvien SET trangthai=FALSE WHERE manv='C'; UPDATE nhanvien SET trangthai=TRUE WHERE manv='C'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Database disable/re-enable between requests still revokes old token using security version");
        await LoginAs("cashier");
        await Exec("UPDATE nhanvien SET maquyen='TC' WHERE manv='C'; UPDATE nhanvien SET maquyen='TN' WHERE manv='C'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Database role change/restore between requests still revokes old token");
        await LoginAs("cashier");var cashierToken=client.DefaultRequestHeaders.Authorization!;
        await LoginAs("manager");
        var staffUpdate=new PartnerUpsertDto("Nhân viên"," C ","Cashier","0900000002","cashier@example.com","Address","Trưởng ca");
        Check((await client.PostAsJsonAsync("/api/doi-tac",staffUpdate)).IsSuccessStatusCode,"Manager changes staff role");
        Check((await client.PostAsJsonAsync("/api/doi-tac",staffUpdate with{Role="Nhân viên thu ngân"})).IsSuccessStatusCode,"Manager restores role before old token is used");
        var managerToken=client.DefaultRequestHeaders.Authorization!;client.DefaultRequestHeaders.Authorization=cashierToken;
        Check((await client.GetAsync("/api/hoa-don")).StatusCode==HttpStatusCode.Unauthorized,"Role change with padded ID and restore do not resurrect old sessions");
        await LoginAs("cashier");cashierToken=client.DefaultRequestHeaders.Authorization!;client.DefaultRequestHeaders.Authorization=managerToken;
        Check((await client.DeleteAsync("/api/doi-tac/"+Uri.EscapeDataString("Nhân viên")+"/C")).IsSuccessStatusCode,"Manager disables employee through API");
        Check((await client.PostAsJsonAsync("/api/doi-tac",staffUpdate with{Role="Nhân viên thu ngân"})).IsSuccessStatusCode,"Manager re-enables before any old-token request");
        client.DefaultRequestHeaders.Authorization=cashierToken;
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"API disable/re-enable cannot restore previous-generation sessions");
        await LoginAs("other");
        await Exec("UPDATE phanquyen SET trangthai=FALSE WHERE maquyen='TN'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Disabled permission group invalidates employee token");
        await Exec("UPDATE phanquyen SET trangthai=TRUE WHERE maquyen='TN'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Permission-group re-enable does not restore revoked session");
        await LoginAs("other");
        await Exec("INSERT INTO phanquyen(maquyen,tenquyen) VALUES('OTHER-TN','Other cashier'); UPDATE nhanvien SET maquyen='OTHER-TN' WHERE manv='C2'");
        Check((await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Changed permission code revokes token even when mapped UI role is same");
        await Exec("UPDATE nhanvien SET maquyen='TN' WHERE manv='C2'");
        await LoginAs("manager");
        var provider=new PartnerUpsertDto("Nhà cung cấp","NCC-900","Imported supplier","0900099900","supplier@example.com","Address","");
        var importedStaff=new PartnerUpsertDto("Nhân viên","NV-900","Imported staff","0900099901","staff@example.com","Address","Nhân viên kho",Username:"csv_staff",Password:"test123");
        Check((await client.PostAsJsonAsync("/api/doi-tac/nhap-file",new[]{provider,importedStaff})).IsSuccessStatusCode,"Partner file persists suppliers and staff through API");
        Check(await Scalar<long>("SELECT count(*) FROM nhasanxuat WHERE mansx='NCC-900'")==1 && await Scalar<long>("SELECT count(*) FROM nhanvien WHERE manv='NV-900'")==1,"Imported partners exist in database");
        using(var reloaded=JsonDocument.Parse(await client.GetStringAsync("/api/app-state")))Check(new[]{"NCC-900","NV-900"}.All(id=>reloaded.RootElement.GetProperty("partnersData").EnumerateArray().Any(p=>p.GetProperty("id").GetString()==id)),"Reloaded app-state retains imported partners");
        await LoginAs("csv_staff");
        Check((await client.GetAsync("/api/app-state")).IsSuccessStatusCode,"Imported employee logs in with the explicit CSV credentials");
        await LoginAs("manager");
        var credentialErrors=new[]{importedStaff with{Username=null},importedStaff with{Password=null},importedStaff with{Username=" ",Password=" "}};
        for(var i=0;i<credentialErrors.Length;i++)
        {
            using var missing=await client.PostAsJsonAsync("/api/doi-tac/nhap-file",new[]{provider with{Id="NCC-MISSING-"+i,Phone="090008880"+i},credentialErrors[i] with{Id="NV-MISSING-"+i,Phone="090008881"+i}});
            Check(missing.StatusCode==HttpStatusCode.Conflict && (await missing.Content.ReadAsStringAsync()).Contains("Dòng 3"),"Missing CSV credentials are rejected with row number: "+i);
            Check(await Scalar<long>("SELECT count(*) FROM nhanvien WHERE manv='NV-MISSING-"+i+"'")==0 && await Scalar<long>("SELECT count(*) FROM nhasanxuat WHERE mansx='NCC-MISSING-"+i+"'")==0,"Missing credentials roll back the whole CSV batch: "+i);
        }
        var rejected=await client.PostAsJsonAsync("/api/doi-tac/nhap-file",new[]{provider with{Id="NCC-901",Phone="0900099902"},provider with{Id="NCC-902",Phone="bad"}});
        Check(rejected.StatusCode==HttpStatusCode.Conflict && await Scalar<long>("SELECT count(*) FROM nhasanxuat WHERE mansx IN ('NCC-901','NCC-902')")==0,"Invalid later CSV row rolls back earlier row");rejected.Dispose();
        var supplierBefore=await Scalar<string>("SELECT to_jsonb(s)::text FROM nhasanxuat s WHERE mansx='NCC-001'");
        Check((await client.PostAsJsonAsync("/api/doi-tac/nhap-file",new[]{provider with{Id="NCC-001",Name="Overwrite attempt",Phone="0900099903"}})).StatusCode==HttpStatusCode.Conflict,"Import refuses to overwrite an existing partner");
        Check(supplierBefore==await Scalar<string>("SELECT to_jsonb(s)::text FROM nhasanxuat s WHERE mansx='NCC-001'"),"Duplicate import preserves original partner");
        await LoginAs("cashier");
        Check((await client.PostAsJsonAsync("/api/doi-tac/nhap-file",new[]{provider with{Id="NCC-FORBIDDEN"}})).StatusCode==HttpStatusCode.Forbidden,"Cashier cannot use import endpoint");

        using(var login=await client.PostAsJsonAsync("/api/dang-nhap",new{username="warehouse",password="test123"}))
        {login.EnsureSuccessStatusCode();using var data=JsonDocument.Parse(await login.Content.ReadAsStringAsync());client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("bearer",data.RootElement.GetProperty("token").GetString());}
        Check((await client.GetAsync("/api/hoa-don")).StatusCode==HttpStatusCode.Forbidden,"HTTP warehouse invoice denied");
        var state=await client.GetStringAsync("/api/app-state");Check(!state.Contains("private@example.test")&&!state.Contains("Customer")&&!state.Contains("TEST HASH MUST NOT LEAK"),"HTTP warehouse sensitive data excluded");
        Check((await client.PostAsync("/api/dang-xuat",null)).IsSuccessStatusCode && (await client.GetAsync("/api/app-state")).StatusCode==HttpStatusCode.Unauthorized,"Logout revokes bearer token");
        api.Kill(true);await api.WaitForExitAsync();api.Dispose();api=null;
    }
    Console.WriteLine($"PASS {checks} checks including database, concurrency and real HTTP.");
}
catch(Exception ex)
{
    Console.Error.WriteLine("Test body failed before cleanup: " + ex);
    throw;
}
finally
{
    if(api is {HasExited:false}) {api.Kill(true);await api.WaitForExitAsync();}api?.Dispose();
    await using var drop=new NpgsqlCommand("DROP DATABASE "+database+" WITH (FORCE)",admin){CommandTimeout=120};await drop.ExecuteNonQueryAsync();
}

public sealed class UnitStaffValidator : IStaffSessionValidator
{ public Action? DuringCheck; public Func<LoginResultDto,bool>? Accept; public Task<bool> IsCurrentAsync(LoginResultDto user){DuringCheck?.Invoke();return Task.FromResult(Accept?.Invoke(user)??true);} }
