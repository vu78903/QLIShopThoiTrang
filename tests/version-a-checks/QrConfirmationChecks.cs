using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PayOS.Models.Webhooks;
using QLIShopThoiTrang.Api.Data;
using QLIShopThoiTrang.Api.Models;
using QLIShopThoiTrang.Api.Services;

internal static class QrConfirmationChecks
{
    public static async Task RunAsync(string connection,Action<bool,string> check)
    {
        var target=new NpgsqlConnectionStringBuilder(connection);
        if(target.Host is not ("127.0.0.1" or "localhost" or "::1")||!target.Database!.StartsWith("fashionstore_form_test_"))throw new Exception("QR tests refuse non-local fixture databases.");
        await using var c=new NpgsqlConnection(connection);await c.OpenAsync();
        async Task Exec(string sql){await using var cmd=new NpgsqlCommand(sql,c);await cmd.ExecuteNonQueryAsync();}
        async Task<T> Value<T>(string sql){await using var cmd=new NpgsqlCommand(sql,c);return (T)(await cmd.ExecuteScalarAsync())!;}
        await Exec("""
            INSERT INTO nhanvien(manv,tendangnhap,matkhau,hoten,sodienthoai,maquyen,trangthai) VALUES('QR-C','qr-test','test-only','QR Cashier','0900000554','TN',true);
            INSERT INTO khachhang(makh,hoten,sodienthoai,hangthe,tongchitieu,diemtichluy) VALUES('QR-K','QR Test Customer','0900000555','Bạc',0,0);
            INSERT INTO sanpham(masp,masku,maloai,tensanpham,kichco,mausac,dongia,soluong,soluongtonkho,danhmuc,mansx) VALUES('QR-P','QR-SKU','AO','QR Test Product','S','Đen',100000,50,50,'NAM','NCC-001');
            """);
        const string key="fixture-checksum-not-a-real-merchant-key";
        IConfiguration Config(bool configured)=>new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["ConnectionStrings:YodyStoreDb"]=connection,["PayOS:ClientId"]=configured?"fixture-client":"",["PayOS:ApiKey"]=configured?"fixture-key":"",["PayOS:ChecksumKey"]=configured?key:""}).Build();
        var config=Config(true);var db=new SqlServerConnectionFactory(config);var store=new StoreRepository(db);var payments=new PayOsPaymentService(config,db,store);
        InvoiceCreateDto Invoice(string id)=>new(id,new("QR Test Customer","0900000555"),[new("QR-SKU","QR Test Product",1,100000,100000)],100000,0,8000,108000,"Quet ma QR",108000,null,false,null,null,null,"QR-C",Source:"POS");
        async Task Seed(long code,string id){await using var cmd=new NpgsqlCommand("INSERT INTO giaodichthanhtoanqr(magd,sohd,nguondon,sotien,malienket,duongdanthanhtoan,manganhang,sotaikhoan,tentaikhoan,noidung,trangthai,dulieuhoadon,ngaytao) VALUES(@code,@id,'POS',108000,@link,'https://example.test/qr','TEST','0000000000','FIXTURE','QR TEST','PENDING',CAST(@payload AS jsonb),LOCALTIMESTAMP)",c);cmd.Parameters.AddWithValue("code",code);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("link","fixture-"+code);cmd.Parameters.AddWithValue("payload",JsonSerializer.Serialize(Invoice(id)));await cmd.ExecuteNonQueryAsync();}
        Webhook Hook(long code,long amount=108000,bool goodSignature=true,bool success=true){
            var data=new{orderCode=code,amount,description="QR TEST",accountNumber="0000000000",reference="FIXTURE-"+code,transactionDateTime="2026-11-05 15:00:00",currency="VND",paymentLinkId="fixture-"+code,code=success?"00":"01",desc=success?"success":"failed",counterAccountBankId="",counterAccountBankName="",counterAccountName="",counterAccountNumber="",virtualAccountName="",virtualAccountNumber=""};
            var json=JsonSerializer.SerializeToElement(data);var text=string.Join('&',json.EnumerateObject().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>p.Name+"="+(p.Value.ValueKind==JsonValueKind.String?p.Value.GetString():p.Value.GetRawText())));
            var signature=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            return JsonSerializer.Deserialize<Webhook>(JsonSerializer.Serialize(new{code=success?"00":"01",desc=success?"success":"failed",success,data,signature=goodSignature?signature:new string('0',64)}),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        async Task<bool> Rejected(Webhook hook){try{await payments.HandleWebhookAsync(hook,default);return false;}catch(Exception){return true;}}
        var unavailable=new PayOsPaymentService(Config(false),db,store);
        check(!unavailable.IsConfigured,"QR without merchant keys is not configured");
        var blocked=false;try{await unavailable.CreateAsync(Invoice("QR-NO-CONFIG"),default);}catch(InvalidOperationException e){blocked=e.Message.Contains("PAYOS_CLIENT_ID");}
        check(blocked,"Creating QR without merchant keys is blocked with configuration message");
        check(await Value<long>("SELECT count(*) FROM hoadon WHERE sohd='QR-NO-CONFIG'")==0,"Missing QR configuration does not create a paid invoice");
        check(await Value<int>("SELECT soluongtonkho FROM sanpham WHERE masku='QR-SKU'")==50,"Missing QR configuration does not deduct stock");
        await Seed(900001,"QR-PAID");
        check(await Value<long>("SELECT count(*) FROM hoadon WHERE sohd='QR-PAID'")==0,"Pending QR session is not a paid invoice");
        check(await Rejected(Hook(900001,goodSignature:false)),"Forged webhook signature is rejected");
        check(await Value<long>("SELECT count(*) FROM hoadon WHERE sohd='QR-PAID'")==0&&await Value<int>("SELECT soluongtonkho FROM sanpham WHERE masku='QR-SKU'")==50,"Forged webhook cannot create invoice or deduct stock");
        check(await Rejected(Hook(900001,amount:107999)),"Signed webhook for insufficient amount is rejected");
        check(await Rejected(Hook(900001,amount:108001)),"Signed webhook for a different amount is rejected");
        await payments.HandleWebhookAsync(Hook(900001),default);
        check(await Value<string>("SELECT trangthai FROM giaodichthanhtoanqr WHERE magd=900001")=="PAID","Valid signed webhook confirms QR session as PAID");
        check(await Value<string>("SELECT trangthaithanhtoan FROM hoadon WHERE sohd='QR-PAID'")=="DA_THANH_TOAN","Valid webhook records paid invoice");
        check(await Value<int>("SELECT soluongtonkho FROM sanpham WHERE masku='QR-SKU'")==49&&await Value<int>("SELECT soluong FROM sanpham WHERE masku='QR-SKU'")==49,"Confirmed QR deducts one unit from both stock columns");
        check(await Value<decimal>("SELECT tongchitieu FROM khachhang WHERE makh='QR-K'")==108000,"Confirmed QR records customer spending once");
        await Task.WhenAll(Enumerable.Range(0,5).Select(_=>payments.HandleWebhookAsync(Hook(900001),default)));
        check(await Value<long>("SELECT count(*) FROM hoadon WHERE sohd='QR-PAID'")==1&&await Value<int>("SELECT soluongtonkho FROM sanpham WHERE masku='QR-SKU'")==49,"Five duplicate confirmations do not duplicate invoice or stock deduction");
        check(await Value<decimal>("SELECT tongchitieu FROM khachhang WHERE makh='QR-K'")==108000,"Duplicate confirmations do not duplicate loyalty spending");
        await Seed(900002,"QR-FAILED-PROVIDER");
        var rejectedFailure=await Rejected(Hook(900002,success:false));
        var failedInvoiceCount=await Value<long>("SELECT count(*) FROM hoadon WHERE sohd='QR-FAILED-PROVIDER'");
        var report=new{LocalFixtureOnly=true,LivePaymentTested=false,SuccessPathConfirmed=true,InvalidSignatureRejected=true,MismatchedAmountRejected=true,DuplicateConfirmationsSafe=true,FailureWebhook=new{ReportedSuccess=false,ReportedCode="01",HandlerRejected=rejectedFailure,PaidInvoiceCount=failedInvoiceCount,SessionStatus=await Value<string>("SELECT trangthai FROM giaodichthanhtoanqr WHERE magd=900002"),StockAfterFailure=await Value<int>("SELECT soluongtonkho FROM sanpham WHERE masku='QR-SKU'"),StockExpectedWithoutFailure=49},PublicTableCount=await Value<long>("SELECT count(*) FROM pg_tables WHERE schemaname='public'")};
        if(Environment.GetEnvironmentVariable("QR_TEST_REPORT") is {Length:>0} reportPath){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));}
        Console.WriteLine(JsonSerializer.Serialize(report));
        check(report.PublicTableCount==18,"QR confirmation tests use the same 18 fixture tables");
        check(rejectedFailure&&failedInvoiceCount==0,"Provider failure webhook must not record successful payment");
        Console.WriteLine("PASS QR confirmation, signature, amount, stock and duplicate checks on local PostgreSQL.");
    }
}
