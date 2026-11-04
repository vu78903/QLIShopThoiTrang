using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace qlicuahang;

internal static class WebViewStaffContext
{
    public static event Action? SessionExpired;

    public static bool IsTrustedDocument(string source)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || !uri.IsFile || uri.IsUnc)
            return false;
        try
        {
            var views = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Views"));
            var file = Path.GetFullPath(uri.LocalPath);
            return string.Equals(Path.GetDirectoryName(file), views, StringComparison.OrdinalIgnoreCase)
                && Path.GetExtension(file).Equals(".html", StringComparison.OrdinalIgnoreCase)
                && File.Exists(file);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    public static Task<string> ConfigureAsync(CoreWebView2 core)
    {
        var sessionToken=formDangNhap.ApiToken;
        var sessionEmployee=formDangNhap.MaNhanVien;
        core.NavigationStarting += (_, args) =>
        {
            if (args.Uri != "about:blank" && !IsTrustedDocument(args.Uri)) args.Cancel = true;
        };
        core.WebMessageReceived += (_, args) =>
        {
            if (!IsTrustedDocument(args.Source)) return;
            string message;
            try { message = args.TryGetWebMessageAsString(); }
            catch (ArgumentException) { return; }
            if (message == "staff-session-expired" && sessionToken==formDangNhap.ApiToken && sessionEmployee==formDangNhap.MaNhanVien) SessionExpired?.Invoke();
        };
        var views = Path.Combine(AppContext.BaseDirectory, "Views");
        var pages = Directory.Exists(views)
            ? Directory.GetFiles(views, "*.html").Select(p => new Uri(p).AbsoluteUri).ToArray() : [];
        var pagesJson = JsonSerializer.Serialize(pages);
        var role = formDangNhap.VaiTro switch
        {
            "CuaHangTruong" => "admin", "TruongCa" => "shiftLeader", "NhanVienKho" => "warehouse", _ => "cashier"
        };
        var roleJson = JsonSerializer.Serialize(role);
        var tokenJson = JsonSerializer.Serialize(sessionToken);
        var employeeJson = JsonSerializer.Serialize(new { id = formDangNhap.MaNhanVien, name = formDangNhap.TenHienThi });
        return core.AddScriptToExecuteOnDocumentCreatedAsync($@"
            (()=>{{
            if(window.top!==window)return;
            const page=new URL(location.href);page.hash='';page.search='';
            if(!new Set({pagesJson}.map(p=>p.toLowerCase())).has(page.href.toLowerCase()))return;
            window.userRole={roleJson};window.currentEmployee={employeeJson};
            const token={tokenJson};window.staffAuthenticated=!!token;
            const original=window.fetch.bind(window);
            window.fetch=async(resource,init={{}})=>{{
                const url=new URL(resource instanceof Request?resource.url:String(resource),location.href);
                const apiPath=url.pathname.toLowerCase().replace(/\/+$/,'');
                const internal=url.origin==='http://127.0.0.1:5001' && (apiPath==='/api'||apiPath.startsWith('/api/'));
                const method=(init.method || (resource instanceof Request?resource.method:'GET')).toUpperCase();
                if(internal && !['GET','HEAD','OPTIONS'].includes(method) && apiPath!=='/api/dang-xuat' && window.staffApiDataReady===false)
                    throw new Error('Chưa xác minh được dữ liệu. Vui lòng tải lại trước khi ghi thay đổi.');
                if(internal){{
                    const headers=new Headers(init.headers || (resource instanceof Request?resource.headers:undefined));
                    if(token)headers.set('Authorization','Bearer '+token);init={{...init,headers}};
                }}
                try{{
                    const response=await original(resource,init);
                    if(internal && (response.status===401||response.status===403||response.status>=500))window.handleStaffApiFailure?.(response.status);
                    return response;
                }}catch(error){{if(internal)window.handleStaffApiFailure?.('network');throw error;}}
            }};
            }})();");
    }
}
