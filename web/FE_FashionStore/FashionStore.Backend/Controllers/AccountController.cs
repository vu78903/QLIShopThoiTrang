using System.Security.Claims;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FashionStore.Backend.Controllers;

[ApiController, Route("api/account"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(AccountService accounts, CustomerSessions sessions) : ControllerBase
{
    [HttpPost("register"), EnableRateLimiting("account")]
    public async Task<ActionResult<CustomerResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var customer = await accounts.RegisterAsync(request, ct);
        return Created("/api/account/me", customer);
    }
    [HttpPost("login"), EnableRateLimiting("account")]
    public Task<LoginResponse> Login(LoginRequest request, CancellationToken ct) => accounts.LoginAsync(request, ct);
    [HttpGet("me"), Authorize(Roles = "Customer")]
    public Task<CustomerResponse> Me(CancellationToken ct) => accounts.GetAsync(CustomerId, ct);
    [HttpPut("me"), Authorize(Roles = "Customer")]
    public Task<CustomerResponse> Update(ProfileRequest request, CancellationToken ct) => accounts.UpdateAsync(CustomerId, request, ct);
    [HttpPost("password"), Authorize(Roles = "Customer"), EnableRateLimiting("account")]
    public async Task<IActionResult> Password(ChangePasswordRequest request, CancellationToken ct)
    {
        await accounts.ChangePasswordAsync(CustomerId, request, ct);
        return NoContent();
    }
    [HttpPost("logout"), Authorize(Roles = "Customer")]
    public IActionResult Logout()
    {
        sessions.Revoke(Request.Headers.Authorization.ToString()[7..].Trim());
        return NoContent();
    }
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
