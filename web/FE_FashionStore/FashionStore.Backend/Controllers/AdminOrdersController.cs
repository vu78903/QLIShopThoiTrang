using System.Security.Claims;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionStore.Backend.Controllers;

[ApiController, Route("api/admin/orders"),
 Authorize(AuthenticationSchemes = AdminAuthentication.SchemeName, Roles = "Admin"),
 ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOrdersController(OrderService orders) : ControllerBase
{
    private string EmployeeId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet] public Task<IReadOnlyList<OrderSummary>> List(CancellationToken ct) => orders.ListAdminAsync(ct);
    [HttpGet("{id}")] public Task<OrderDetails> Detail(string id, CancellationToken ct) => orders.DetailAsync(id, null, ct);
    [HttpPost("{id}/confirm")] public Task<OrderActionResponse> Confirm(string id, CancellationToken ct) => orders.ChangeStatusAsync(id, EmployeeId, false, ct);
    [HttpPost("{id}/cancel")] public Task<OrderActionResponse> Cancel(string id, CancellationToken ct) => orders.ChangeStatusAsync(id, EmployeeId, true, ct);
}
