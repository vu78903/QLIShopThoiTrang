using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace qlicuahang;

internal static class WebViewStaffContext
{
    public static Task<string> ConfigureAsync(CoreWebView2 core)
    {
        var role = formDangNhap.VaiTro switch
        {
            "CuaHangTruong" => "admin",
            "TruongCa" => "shiftLeader",
            "NhanVienKho" => "warehouse",
            _ => "cashier"
        };
        var roleJson = JsonSerializer.Serialize(role);
        var tokenJson = JsonSerializer.Serialize(formDangNhap.ApiToken);
        return core.AddScriptToExecuteOnDocumentCreatedAsync($@"
            (()=>{{window.userRole={roleJson};const token={tokenJson};const original=window.fetch.bind(window);
            window.fetch=(resource,init={{}})=>{{const url=typeof resource==='string'?resource:resource.url;
            if(new URL(url,location.href).origin==='http://127.0.0.1:5001'){{const headers=new Headers(init.headers || (resource instanceof Request ? resource.headers : undefined));headers.set('Authorization','Bearer '+token);init={{...init,headers}};}}
            return original(resource,init);}};}})();");
    }
}
