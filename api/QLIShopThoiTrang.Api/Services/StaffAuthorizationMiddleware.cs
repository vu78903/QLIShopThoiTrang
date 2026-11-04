using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QLIShopThoiTrang.Api.Models;

namespace QLIShopThoiTrang.Api.Services;

public sealed class StaffAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, StaffSessions sessions)
    {
        var path = (context.Request.Path.Value ?? "").TrimEnd('/').ToLowerInvariant();
        var method = context.Request.Method;
        if (path != "/api" && !path.StartsWith("/api/", StringComparison.Ordinal))
        {
            await next(context);
            return;
        }
        // Chỉ ba điểm vào công khai; CORS middleware xử lý preflight trước bước này.
        if ((path == "/api/health" && method is "GET" or "HEAD") ||
            (method == "POST" && path is "/api/dang-nhap" or "/api/thanh-toan-qr/webhook"))
        {
            await next(context);
            return;
        }
        context.Response.Headers.CacheControl = "no-store";
        var authorization = context.Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim() : "";
        var user = sessions.Get(token);
        if (user is null)
        {
            await Deny(context, 401, "Vui lòng đăng nhập lại.");
            return;
        }
        try
        {
            if(!await context.RequestServices.GetRequiredService<IStaffSessionValidator>().IsCurrentAsync(user) || sessions.Get(token)!=user)
            {
                // Request của token cũ không được thu hồi phiên mới của cùng nhân viên.
                sessions.Remove(token);
                await Deny(context,401,"Tài khoản hoặc quyền đã thay đổi. Vui lòng đăng nhập lại.");return;
            }
        }
        catch(Exception ex) when(ex is System.Data.Common.DbException or TimeoutException)
        {await Deny(context,503,"Chưa kiểm tra được phiên đăng nhập. Vui lòng thử lại.");return;}
        var reading = method is "GET" or "HEAD";
        if (!(reading ? StaffSessions.CanRead(user.Role, path) :
                StaffSessions.CanWrite(user.Role, path, method)) &&
            !(path == "/api/doi-tac" && method == "POST" && user.Role is "ThuNgan" or "TruongCa"))
        {
            await Deny(context, 403, "Bạn không có quyền thực hiện thao tác này.");
            return;
        }
        context.Items["StaffUser"] = user;
        if (path == "/api/doi-tac" && method == "POST" && user.Role != "CuaHangTruong")
        {
            context.Request.EnableBuffering();
            try
            {
                var input = await context.Request.ReadFromJsonAsync<PartnerUpsertDto>();
                if (input?.Category != "Khách hàng")
                {
                    await Deny(context, 403, "Chỉ được thêm khách hàng tại màn hình bán hàng.");
                    return;
                }
            }
            catch (JsonException)
            {
                await Deny(context, 400, "Dữ liệu gửi lên không hợp lệ.");
                return;
            }
            finally { context.Request.Body.Position = 0; }
        }
        if (method is "POST" or "PUT" &&
            (path.StartsWith("/api/hoa-don/", StringComparison.Ordinal) ||
             path is "/api/hoa-don" or "/api/phieu-nhap-kho" or "/api/doi-tra" or "/api/thanh-toan-qr"))
        {
            try
            {
                var body = await JsonNode.ParseAsync(context.Request.Body);
                if (body is not JsonObject obj)
                {
                    await Deny(context, 400, "Dữ liệu gửi lên không hợp lệ.");
                    return;
                }
                foreach (var key in obj.Select(p => p.Key).Where(k => k.Equals("employeeId", StringComparison.OrdinalIgnoreCase)).ToArray()) obj.Remove(key);
                obj["employeeId"] = user.EmployeeId;
                var bytes = Encoding.UTF8.GetBytes(obj.ToJsonString());
                context.Request.Body = new MemoryStream(bytes);
                context.Request.ContentLength = bytes.Length;
            }
            catch (JsonException)
            {
                await Deny(context, 400, "Dữ liệu gửi lên không hợp lệ.");
                return;
            }
        }
        await next(context);
    }

    private static async Task Deny(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message });
    }
}
