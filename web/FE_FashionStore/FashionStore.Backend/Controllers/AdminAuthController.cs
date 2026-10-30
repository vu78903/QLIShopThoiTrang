using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using FashionStore.Backend.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FashionStore.Backend.Controllers;

[ApiController, Route("api/admin/auth"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAuthController(AdminService admin, AdminSessions sessions) : ControllerBase
{
    [HttpPost("login"), EnableRateLimiting("admin-login")]
    public Task<AdminLoginResponse> Login(AdminLoginRequest request, CancellationToken ct) => admin.LoginAsync(request, ct);
    [HttpGet("me"), Authorize(AuthenticationSchemes = AdminAuthentication.SchemeName, Roles = "Admin")]
    public Task<AdminMeResponse> Me(CancellationToken ct) => admin.MeAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct);
    [HttpPost("logout"), Authorize(AuthenticationSchemes = AdminAuthentication.SchemeName, Roles = "Admin")]
    public IActionResult Logout()
    {
        sessions.Revoke(Request.Headers.Authorization.ToString()[7..].Trim());
        return NoContent();
    }
}
