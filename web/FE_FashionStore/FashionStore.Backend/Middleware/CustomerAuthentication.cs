using System.Security.Claims;
using System.Text.Encodings.Web;
using FashionStore.Backend.Data;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;

namespace FashionStore.Backend.Middleware;

public sealed class CustomerAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, CustomerSessions sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CustomerBearer";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>().Count is not > 0)
            return AuthenticateResult.NoResult();
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header)) return AuthenticateResult.NoResult();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.Fail("Invalid authentication.");
        var token = header[7..].Trim();
        var session = sessions.Find(token);
        if (session == null) return AuthenticateResult.Fail("Session expired or invalid.");
        // Resolve SQL only after a protected request presents an existing session.
        var db = Context.RequestServices.GetRequiredService<FashionStoreDbContext>();
        var account = await db.CustomerAccounts.AsNoTracking().Include(a => a.Customer)
            .SingleOrDefaultAsync(a => a.MaKH == session.CustomerId, Context.RequestAborted);
        if (account == null || !account.TrangThai || !account.Customer.TrangThai ||
            CustomerSessions.Version(account.MatKhauHash) != session.PasswordVersion)
        {
            sessions.Revoke(token);
            return AuthenticateResult.Fail("Session expired or invalid.");
        }
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, account.MaKH),
            new Claim(ClaimTypes.Name, account.TenDangNhap), new Claim(ClaimTypes.Role, "Customer") };
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
    }
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        await Results.Problem(statusCode: 401, title: "Vui lòng đăng nhập lại.").ExecuteAsync(Context);
    }
}
