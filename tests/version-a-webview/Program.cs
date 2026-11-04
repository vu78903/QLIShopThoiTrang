using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using qlicuahang;

internal static class Program
{
    private static int checks;
    private static readonly string Today=DateTime.Now.ToString("yyyy-MM-dd");
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var compatibility = typeof(formTongQuan).Assembly.GetType("qlicuahang.ApiProcessManager")!
            .GetMethod("IsCompatibleHealth", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool Compatible(string json) { using var doc=JsonDocument.Parse(json); return (bool)compatibility.Invoke(null,new object[]{doc.RootElement})!; }
        Check(!Compatible("{\"ok\":true,\"qrPayment\":true}"),"Startup rejects legacy API that lacks staff security protocol");
        Check(Compatible("{\"ok\":true,\"qrPayment\":true,\"staffSecurityVersion\":1}"),"Startup accepts matching staff security API");
        Check(!Compatible("{\"ok\":true,\"qrPayment\":true,\"staffSecurityVersion\":0}"),"Startup rejects mismatched API protocol");
        Check(!Compatible("{\"ok\":false,\"qrPayment\":true,\"staffSecurityVersion\":1}"),"Startup rejects unhealthy API");

        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER",Path.Combine(AppContext.BaseDirectory,"profile-"+Guid.NewGuid().ToString("N")));
        using var api=new FakeApi();
        using var context=new ApplicationContext();
        var started=false;
        Application.Idle+=async (_,_)=>
        {
            if(started)return;started=true;
            try {await RunChecks(api);Console.WriteLine($"PASS {checks} real WinForms/WebView checks.");}
            catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
            finally {foreach(Form form in Application.OpenForms.Cast<Form>().ToArray())form.Close();context.ExitThread();}
        };
        Application.Run(context);
    }

    private static void SetIdentity(string id="C",string role="ThuNgan")
    {
        formDangNhap.VaiTro=role;formDangNhap.MaNhanVien=id;
        formDangNhap.TenHienThi=id=="M"?"Manager":id=="C2"?"Other":"Cashier";formDangNhap.ApiToken="TEST-A";
    }

    private static async Task<WebView2> Start(Form form)
    {
        // Form chính của thu ngân mở Báo cáo rồi chuyển sang POS. Không dùng ready của trang trung gian.
        var expectedEmployee=formDangNhap.MaNhanVien;
        var expectedPage=form is formTongQuan ? formDangNhap.VaiTro switch
        {
            "CuaHangTruong" or "TruongCa"=>"bao-cao.html",
            "NhanVienKho"=>"ton-kho.html",
            _=>"ban-hang.html"
        } : null;
        form.Opacity=0;form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);
        form.Show();
        var field=form.GetType().GetField("webView",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var view=(WebView2)field.GetValue(form)!;
        var installed=false;
        async void Install()
        {
            if(installed || view.CoreWebView2 is null)return;installed=true;
            if(Environment.GetEnvironmentVariable("FORM_TEST_TRACE")=="1")
            {
                view.CoreWebView2.NavigationStarting+=(_,e)=>Console.WriteLine("TRACE navigating "+new Uri(e.Uri).AbsolutePath);
                view.CoreWebView2.NavigationCompleted+=(_,e)=>Console.WriteLine("TRACE navigation-completed "+view.Source+" success="+e.IsSuccess);
            }
            view.CoreWebView2.AddWebResourceRequestedFilter("https://*",CoreWebView2WebResourceContext.All);
            view.CoreWebView2.WebResourceRequested+=(_,e)=>
            {
                // Không tải CDN hoặc tài nguyên Internet trong phép thử dữ liệu giả.
                e.Response=view.CoreWebView2.Environment.CreateWebResourceResponse(new MemoryStream(),200,"OK","Content-Type: application/javascript");
            };
            await view.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.lucide={createIcons(){}};window.Chart=class{destroy(){}};");
        }
        view.CoreWebView2InitializationCompleted+=(_,_)=>Install();Install();
        var ready="document.readyState==='complete' && window.staffApiDataReady===true && typeof STAFF_STORAGE_OWNER!=='undefined' && STAFF_STORAGE_OWNER==="+JsonSerializer.Serialize(expectedEmployee);
        if(expectedPage is not null)ready+=" && location.pathname.toLowerCase().endsWith("+JsonSerializer.Serialize("/"+expectedPage)+")";
        await Wait(view,ready);
        if(Environment.GetEnvironmentVariable("FORM_TEST_TRACE")=="1")Console.WriteLine("TRACE start-ready "+await Snapshot(view));
        return view;
    }

    private static async Task Wait(WebView2 view,string expression)
    {
        for(var n=0;n<1200;n++)
        {
            if(view.CoreWebView2 is not null && !view.IsDisposed)
            {
                try {if(await view.CoreWebView2.ExecuteScriptAsync(expression)=="true")return;}
                catch(InvalidOperationException){}
            }
            await Task.Delay(50);
        }
        throw new TimeoutException(expression+"; state="+await Snapshot(view));
    }
    private static async Task Reload(WebView2 view)
    {
        var loaded=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Complete(object? sender,CoreWebView2NavigationCompletedEventArgs e){if(e.IsSuccess)loaded.TrySetResult(true);}
        view.CoreWebView2.NavigationCompleted+=Complete;
        try {view.CoreWebView2.Reload();await loaded.Task.WaitAsync(TimeSpan.FromSeconds(20));}
        finally {view.CoreWebView2.NavigationCompleted-=Complete;}
    }
    private static Task<string> Js(WebView2 view,string expression)=>view.CoreWebView2 is { } core && !view.IsDisposed ? core.ExecuteScriptAsync(expression) : Task.FromResult("WebView unavailable; disposed="+view.IsDisposed);
    private static async Task CheckState(WebView2 view,string expression,string message)
    {
        var valid=await Js(view,expression)=="true";
        Check(valid,message+(valid?"":"; state="+await Snapshot(view)));
    }
    private static Task<string> Snapshot(WebView2 view)=>Js(view,"JSON.stringify({href:location.href,ready:window.staffApiDataReady,owner:typeof STAFF_STORAGE_OWNER==='undefined'?null:STAFF_STORAGE_OWNER,employee:window.currentEmployee?.id,role:window.userRole,live:typeof cart==='undefined'?null:{qty:cart.map(i=>i.qty),reports:shiftReports.map(r=>r.totalRevenue),checkout:checkoutAttempt?.id,returns:returnAttempt?.id},saved:Object.keys(localStorage).filter(k=>k.includes(':account:')).map(k=>{try{let v=JSON.parse(localStorage.getItem(k));return {key:k,owner:v.staffOwnerId,qty:v.cart?.map(i=>i.qty),reports:v.shiftReports?.map(r=>r.totalRevenue),checkout:v.checkoutAttempt?.id,returns:v.returnAttempt?.id}}catch{return {key:k,invalid:true}}})})");

    private static async Task RunChecks(FakeApi api)
    {
        SetIdentity();
        using var main=new formTongQuan();var view=await Start(main);
        Check(view.Source.IsFile,"Keep file origin");
        Check(api.LastAuthorization=="Bearer TEST-A","Main form sends bearer token");
        var legacy=new
        {
            productsData=Array.Empty<object>(),productTypesData=Array.Empty<object>(),partnersData=Array.Empty<object>(),promotionsData=Array.Empty<object>(),deliveryOrders=Array.Empty<object>(),codOrders=Array.Empty<object>(),cancelledOrders=Array.Empty<object>(),importRequests=Array.Empty<object>(),salesHistory=Array.Empty<object>(),
            cart=new[]{new{sku="TEST-M",name="Demo",qty=2,price=100000,originalPrice=100000,stock=20,color="Đen",size="M"}},
            checkoutAttempt=new{signature="legacy-signature",id="LEGACY-REQUEST"},returnAttempt=new{signature="legacy-return",id="LEGACY-RETURN"},
            shiftReports=new[]{new{shiftId="LEGACY-CA",dateKey=DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"),cashier="Cashier",totalRevenue=123456,closedAt="20:00"}},appliedPromotion=(object?)null
        };
        var seed=JsonSerializer.Serialize(JsonSerializer.Serialize(legacy));
        await Js(view,"window.removeEventListener('beforeunload',saveAppState);localStorage.setItem('y_fashion_erp_state_v2',"+seed+");");
        await Reload(view);await Wait(view,"window.staffApiDataReady === true && cart.length===0 && !!document.getElementById('legacyStorageNotice')");
        Check(await Js(view,"cart.length===0 && shiftReports.length===0 && checkoutAttempt===null && returnAttempt===null")=="true","Unowned legacy state is never assigned to first cashier");
        Check(await Js(view,"!!document.getElementById('legacyStorageNotice') && !document.querySelector('#legacyStorageNotice button')")=="true","Cashier sees recovery notice without access to assign legacy records");
        var legacyBefore=await Js(view,"localStorage.getItem(LEGACY_STORAGE_KEY)");main.Close();await Task.Delay(100);
        SetIdentity("M","CuaHangTruong");
        using(var recovery=new formTongQuan())
        {
            var recoveryView=await Start(recovery);await Js(recoveryView,"openLegacyRecovery()");await Wait(recoveryView,"!!document.getElementById('legacyCartOwner')");
            await Js(recoveryView,"document.getElementById('legacyCartOwner').value='C';document.getElementById('legacyReturnOwner').value='C';document.getElementById('legacyReportOwner_0').value='C';applyLegacyRecovery()");
            await Wait(recoveryView,"!document.getElementById('legacyRecoveryModal') && !!JSON.parse(localStorage.getItem(staffStorageKey('C'))).cart.length");
            Check(await Js(recoveryView,"cart.length===0 && shiftReports.length===0")=="true","Manager does not inherit records assigned to cashier");
            Check(await Js(recoveryView,"localStorage.getItem(LEGACY_STORAGE_KEY)")==legacyBefore,"Legacy archive remains byte-for-byte unchanged after explicit recovery");recovery.Close();
        }

        SetIdentity();using var reopened=new formTongQuan();view=await Start(reopened);
        await CheckState(view,"cart.length===1 && cart[0].qty===2 && shiftReports[0].totalRevenue===123456","Close/reopen preserves cart and shift report");
        Check(await Js(view,"checkoutAttempt.id==='LEGACY-REQUEST'")=="true","Close/reopen preserves retry request ID");

        await Js(view,"updateCartQuantity(0,3)");
        Check(await Js(view,"JSON.parse(localStorage.getItem(APP_STORAGE_KEY)).cart[0].qty===3")=="true","Cart edit persists immediately before window close");
        await Js(view,"updateCartQuantity(0,2)");
        SetIdentity("C2");
        using(var other=new formTongQuan())
        {
            var otherView=await Start(other);
            await CheckState(otherView,"cart.length===0 && shiftReports.length===0 && checkoutAttempt===null && returnAttempt===null","Other account never inherits cart, shift reports or retry IDs");other.Close();
        }
        SetIdentity();
        api.Status=503;await Js(view,"loadAppState()");await Wait(view,"document.getElementById('staffApiNotice')!==null && window.staffApiDataReady===false");
        Check(await Js(view,"productsData.length===1 && cart.length===1 && shiftReports.length===1")=="true","503 retains loaded rows and drafts");
        Check(await Js(view,"document.querySelector('main').style.display!=='none' && document.getElementById('staffApiRetry').textContent==='Tải lại'")=="true","503 shows retry beside retained data");
        var logoutWrites=api.Writes;await Js(view,"fetch(API_BASE_URL+'/api/dang-xuat/',{method:'POST'}).then(()=>window.testOfflineLogoutSent=true)");
        await Wait(view,"window.testOfflineLogoutSent===true");Check(api.Writes==logoutWrites+1,"Both fetch layers allow logout while data is unavailable");
        var writes=api.Writes;
        await Js(view,"fetch(API_BASE_URL+'/api/hoa-don',{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'}).catch(e=>window.testWriteBlocked=true)");
        await Wait(view,"window.testWriteBlocked===true");Check(api.Writes==writes,"Offline write never reaches API");
        api.Status=200;await Js(view,"document.getElementById('staffApiRetry').click()");await Wait(view,"window.staffApiDataReady===true && !document.getElementById('staffApiNotice')");checks++;

        foreach(var failure in new[]{500,-1,-2})
        {
            api.Status=failure;await Js(view,"loadAppState()");
            await Wait(view,"!!document.getElementById('staffApiNotice') && document.getElementById('staffApiNoticeText').textContent.includes('kết nối')");
            Check(await Js(view,"cart.length===1 && shiftReports.length===1 && productsData.length===1")=="true","Failure retains data: "+failure);
            api.Status=200;await Js(view,"document.getElementById('staffApiRetry').click()");await Wait(view,"window.staffApiDataReady===true");
        }
        api.Status=403;await Js(view,"loadAppState()");await Wait(view,"document.getElementById('staffApiNoticeText')?.textContent.includes('không có quyền')");
        Check(!reopened.IsDisposed && !reopened.YeuCauDangXuat,"403 keeps authenticated session");
        api.Status=200;await Js(view,"document.getElementById('staffApiRetry').click()");await Wait(view,"window.staffApiDataReady===true");

        using(var cod=new formCOD())
        {
            var codView=await Start(cod);
            await Wait(codView,"document.getElementById('shipmentTabCod')?.classList.contains('active')");
            Check(codView.Source.LocalPath.EndsWith("giao-hang.html"),"Native COD form keeps delivery/COD navigation");
            Check(api.LastAuthorization=="Bearer TEST-A","COD child form sends token");cod.Close();
        }

        foreach(var child in new Form[]{new formBanHang(),new formGiaoHang(),new formNhapHang(),new formTonKho(),new formQLSanPham(),new formKhuyenMai(),new formQLDoiTac()})
        {
            using(child)
            {
                formDangNhap.VaiTro=child is formNhapHang or formTonKho?"NhanVienKho":child is formQLSanPham or formKhuyenMai or formQLDoiTac?"CuaHangTruong":"ThuNgan";
                var childView=await Start(child);Check(api.LastAuthorization=="Bearer TEST-A",child.GetType().Name+" sends token");child.Close();
            }
        }
        SetIdentity();

        // Chưa tải được dữ liệu ở trang mới phải hiện lỗi, không hiện danh sách trống như dữ liệu thật.
        api.Status=503;
        await Js(view,"window.removeEventListener('beforeunload',saveAppState);localStorage.setItem(APP_STORAGE_KEY,JSON.stringify({staffOwnerId:STAFF_STORAGE_OWNER,cart,shiftReports,checkoutAttempt,returnAttempt,productsData:[],partnersData:[]}));");
        await Reload(view);await Wait(view,"document.getElementById('staffApiNotice')!==null");
        Check(await Js(view,"document.querySelector('main').style.display==='none' && cart.length===1 && shiftReports.length===1")=="true","First-load error hides unloaded content but keeps legacy drafts");
        api.Status=200;await Js(view,"document.getElementById('staffApiRetry').click()");await Wait(view,"window.staffApiDataReady===true");

        await Js(view,"Storage.prototype.setItem=function(){throw new DOMException('test quota','QuotaExceededError')}");
        api.Status=401;await Js(view,"loadAppState()");
        for(var n=0;n<100&&!reopened.YeuCauDangXuat;n++)await Task.Delay(50);
        Check(reopened.YeuCauDangXuat && formDangNhap.ApiToken=="","401 returns to native login even when local save fails");
        await Task.Delay(200);api.Status=200;SetIdentity();
        using var afterLogin=new formTongQuan();var finalView=await Start(afterLogin);
        Check(await Js(finalView,"shiftReports[0].shiftId==='LEGACY-CA' && cart[0].qty===2")=="true","401/relogin preserves report and cart");afterLogin.Close();

        SetIdentity("M","CuaHangTruong");
        using(var reporting=new formTongQuan())
        {
            var reportView=await Start(reporting);await Wait(reportView,"document.body.dataset.view==='report' && window.staffApiDataReady===true");
            await Js(reportView,"openEditProduct(0)");
            Check(await Js(reportView,"document.getElementById('f_stock').readOnly && document.getElementById('f_stock').value==='20' && !!document.getElementById('f_stock_help')")=="true","Existing SKU stock is displayed as read-only with adjustment guidance");
            if(Environment.GetEnvironmentVariable("FORM_TEST_EVIDENCE") is {Length:>0} evidence)
            {
                Directory.CreateDirectory(evidence);
                await Js(reportView,"document.getElementById('f_stock').scrollIntoView({block:'center'})");await Task.Delay(200);
                await using var image=File.Create(Path.Combine(evidence,"sku-stock-readonly-after.png"));
                await reportView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
            }
            await Js(reportView,"openAddProduct()");
            Check(await Js(reportView,"!document.getElementById('f_stock').readOnly")=="true","New SKU can still set initial stock");
            await Js(reportView,"document.getElementById('crudModalBg').classList.remove('open');window.testAlerts=[];window.alert=m=>testAlerts.push(m)");
            var imports=api.PartnerImports;
            await Js(reportView,"importPartnerRows([{nhom:'Nhân viên',ma:'NV-911',ten:'CSV employee',sdt:'0900099911',email:'employee@example.com',chucvu:'Nhân viên kho'}])");
            Check(await Js(reportView,"testAlerts.some(m=>m.includes('Tài khoản') && m.includes('Mật khẩu'))")=="true" && api.PartnerImports==imports,"Employee CSV missing credentials is blocked before sending API request");
            await Js(reportView,"importPartnerRows(readCsvRows('Nhóm,Mã,Tên,Số điện thoại,Email,Chức vụ,Tài khoản,Mật khẩu\\nNhân viên,NV-912,CSV employee,0900099912,employee@example.com,Nhân viên kho,csv_employee,TestCsv123'))");
            await Wait(reportView,"partnersData.some(p=>p.id==='NV-912')");
            Check(api.PartnerImports==imports+1 && api.LastImportedUsername=="csv_employee" && api.LastImportedPassword=="TestCsv123","Vietnamese CSV credential columns reach the server request");
            imports=api.PartnerImports;
            await Js(reportView,"importPartnerRows([{nhom:'Nhà cung cấp',ma:'NCC-910',ten:'CSV supplier',sdt:'0900099910',email:'csv@example.com',diachi:'Test address'}])");
            await Wait(reportView,"partnersData.some(p=>p.id==='NCC-910')");Check(api.PartnerImports==imports+1,"CSV UI sends a server import request");
            await Reload(reportView);await Wait(reportView,"window.staffApiDataReady===true && partnersData.some(p=>p.id==='NCC-910')");checks++;
            await Js(reportView,"openShiftReportModal(); window.print=()=>window.testPrinted=true;printShiftReport()");
            Check(await Js(reportView,"window.testPrinted===true && shiftReports.some(r=>r.dateKey==='"+Today+"')")=="true","New shift close can render and request printing");reporting.Close();
        }
        using(var reportAgain=new formTongQuan())
        {
            var reportView=await Start(reportAgain);
            Check(await Js(reportView,"shiftReports.some(r=>r.dateKey==='"+Today+"') && !shiftReports.some(r=>r.shiftId==='LEGACY-CA')")=="true","Manager report survives reopen without inheriting cashier report");
            var saved=JsonSerializer.Deserialize<string>(await Js(reportView,"localStorage.getItem(APP_STORAGE_KEY)"))!;
            await Js(reportView,"window.removeEventListener('beforeunload',saveAppState);localStorage.setItem(APP_STORAGE_KEY,'{bad-cache');");
            await Reload(reportView);await Wait(reportView,"window.staffApiDataReady===true && document.getElementById('staffApiNoticeText')?.textContent.includes('Không đọc được')");
            Check(await Js(reportView,"localStorage.getItem(APP_STORAGE_KEY)==='{bad-cache'")=="true","Unreadable account cache is never overwritten");
            await Js(reportView,"localStorage.setItem(APP_STORAGE_KEY,"+JsonSerializer.Serialize(saved)+")");reportAgain.Close();
        }
        SetIdentity();using(var obsolete=new formTongQuan())
        {
            var obsoleteView=await Start(obsolete);SetIdentity("C2");using var current=new formTongQuan();var currentView=await Start(current);
            api.Status=401;await Js(obsoleteView,"loadAppState()");await Wait(obsoleteView,"staffExpired===true");
            Check(!current.YeuCauDangXuat && formDangNhap.MaNhanVien=="C2","Expired message from previous account cannot close the current account");
            api.Status=-1;await Js(currentView,"loadAppState()");await Wait(currentView,"window.staffApiDataReady===false && !!document.getElementById('staffApiNotice')");
            await Js(currentView,"window.confirm=()=>true;dangXuat()");
            for(var n=0;n<100&&!current.YeuCauDangXuat;n++)await Task.Delay(50);
            Check(current.YeuCauDangXuat && formDangNhap.ApiToken=="","Actual logout button works while API is unavailable");api.Status=200;obsolete.Close();
        }
    }

    private sealed class FakeApi:IDisposable
    {
        private readonly HttpListener listener=new();private readonly Task loop;
        private int status=200,writes,partnerImports;private readonly List<string> importedPartners=[];public int PartnerImports=>Volatile.Read(ref partnerImports);public string LastAuthorization="",LastImportedUsername="",LastImportedPassword="";
        public int Status{get=>Volatile.Read(ref status);set=>Volatile.Write(ref status,value);}public int Writes=>Volatile.Read(ref writes);
        private readonly string data=JsonSerializer.Serialize(new
        {
            productsData=new[]{new{productId="SP-900",sku="TEST-M",name="Demo",type="Áo thun nam",audience="NAM",supplierId="NCC-001",supplierName="Supplier",color="Đen",size="M",unit="Cái",price=100000,stock=20,image=(string?)null,images=Array.Empty<string>(),active=true,salePrice=(decimal?)null,salePercent=0}},
            productTypesData=new[]{new{id="AO",name="Áo thun nam"}},
            partnersData=new[]{new{category="Nhân viên",id="C",name="Cashier",phone="",email="",address="",role="Nhân viên thu ngân",points=(int?)null,totalSpent=(decimal?)null},new{category="Nhân viên",id="C2",name="Other",phone="",email="",address="",role="Nhân viên thu ngân",points=(int?)null,totalSpent=(decimal?)null},new{category="Nhân viên",id="M",name="Manager",phone="",email="",address="",role="Cửa hàng trưởng",points=(int?)null,totalSpent=(decimal?)null}},
            promotionsData=Array.Empty<object>(),importRequests=Array.Empty<object>(),salesHistory=Array.Empty<object>(),deliveryOrders=Array.Empty<object>(),codOrders=Array.Empty<object>(),cancelledOrders=Array.Empty<object>()
        });
        public FakeApi()
        {
            using(var probe=new TcpListener(IPAddress.Loopback,5001)){probe.Start();probe.Stop();}
            listener.Prefixes.Add("http://127.0.0.1:5001/");listener.Start();loop=Serve();
        }
        private async Task Serve()
        {
            try
            {
                while(listener.IsListening)
                {
                    var c=await listener.GetContextAsync();c.Response.Headers["Access-Control-Allow-Origin"]="*";
                    c.Response.Headers["Access-Control-Allow-Headers"]="Authorization,Content-Type,X-Request-ID";c.Response.Headers["Access-Control-Allow-Methods"]="GET,POST,PUT,DELETE,HEAD";
                    if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=204;c.Response.Close();continue;}
                    LastAuthorization=c.Request.Headers["Authorization"]??"";
                    if(c.Request.HttpMethod is not ("GET" or "HEAD"))Interlocked.Increment(ref writes);
                    var responseStatus=Status;
                    if(c.Request.Url!.AbsolutePath=="/api/app-state" && int.TryParse(Environment.GetEnvironmentVariable("FORM_TEST_APPSTATE_DELAY_MS"),out var delay))await Task.Delay(Math.Clamp(delay,0,1000));
                    if(responseStatus==-1){c.Response.Abort();continue;}
                    if(responseStatus==-2){await Task.Delay(13000);responseStatus=200;}
                    c.Response.StatusCode=responseStatus;c.Response.ContentType="application/json";
                    var body="[]";
                    if(c.Request.Url!.AbsolutePath=="/api/app-state")
                    {
                        var state=System.Text.Json.Nodes.JsonNode.Parse(data)!;
                        foreach(var partner in importedPartners)state["partnersData"]!.AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse(partner));
                        body=state.ToJsonString();
                    }
                    if(c.Request.Url.AbsolutePath=="/api/doi-tac/nhap-file" && c.Request.HttpMethod=="POST")
                    {
                        using var payload=await JsonDocument.ParseAsync(c.Request.InputStream);
                        foreach(var partner in payload.RootElement.EnumerateArray())
                        {
                            LastImportedUsername=partner.TryGetProperty("username",out var username)?username.GetString()??"":"";
                            LastImportedPassword=partner.TryGetProperty("password",out var password)?password.GetString()??"":"";
                            var publicPartner=System.Text.Json.Nodes.JsonNode.Parse(partner.GetRawText())!.AsObject();
                            publicPartner.Remove("username");publicPartner.Remove("password");importedPartners.Add(publicPartner.ToJsonString());
                        }
                        Interlocked.Increment(ref partnerImports);body=JsonSerializer.Serialize(new{count=payload.RootElement.GetArrayLength()});
                    }
                    try {await c.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(body));c.Response.Close();}
                    catch(Exception ex) when(ex is HttpListenerException or IOException or ObjectDisposedException){}
                }
            }catch(HttpListenerException){}catch(ObjectDisposedException){}
        }
        public void Dispose(){listener.Stop();listener.Close();loop.GetAwaiter().GetResult();}
    }
}
