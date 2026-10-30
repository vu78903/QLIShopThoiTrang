using FashionStore.Backend.Data;
using FashionStore.Backend.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FashionStore.Backend.Middleware;

public sealed class AdminAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, AdminSessions sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AdminBearer";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)) return AuthenticateResult.NoResult();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Invalid bearer token.");
        var token = header[7..].Trim();
        var session = sessions.Find(token);
        if (session == null) return AuthenticateResult.Fail("Session expired or invalid.");
        var db = Context.RequestServices.GetRequiredService<FashionStoreDbContext>();
        var access = Context.RequestServices.GetRequiredService<AdminAccess>();
        var employee = await db.Employees.AsNoTracking().Include(e => e.Quyen)
            .SingleOrDefaultAsync(e => e.MaNV == session.EmployeeId, Context.RequestAborted);
        if (!access.Allowed(employee) || string.IsNullOrWhiteSpace(employee!.MatKhauHash) ||
            AdminSessions.Version(employee.MatKhauHash) != session.PasswordVersion)
        {
            sessions.Revoke(token);
            return AuthenticateResult.Fail("Session expired or invalid.");
        }
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, employee.MaNV),
            new Claim(ClaimTypes.Name, employee.HoTen), new Claim(ClaimTypes.Role, "Admin") };
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
    }
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        await Results.Problem(statusCode: 401, title: "Vui lòng đăng nhập quản trị.").ExecuteAsync(Context);
    }
}
