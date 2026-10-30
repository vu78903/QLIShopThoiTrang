using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionStore.Backend.Controllers;

[ApiController, Route("api/admin/dashboard"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None),
 Authorize(AuthenticationSchemes = AdminAuthentication.SchemeName, Roles = "Admin")]
public sealed class AdminDashboardController(AdminService admin) : ControllerBase
{
    [HttpGet]
    public Task<AdminDashboardResponse> Get(CancellationToken ct) => admin.DashboardAsync(ct);
}
