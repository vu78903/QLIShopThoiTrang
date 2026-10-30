using System.Security.Claims;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionStore.Backend.Controllers;

[ApiController, Route("api/orders"), Authorize(Roles = "Customer"),
 ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpPost] public Task<PlaceOrderResponse> Place(CheckoutRequest request, CancellationToken ct) => orders.PlaceAsync(CustomerId, request, ct);
    [HttpGet] public Task<IReadOnlyList<OrderSummary>> List(CancellationToken ct,[System.ComponentModel.DataAnnotations.StringLength(30)]string? q=null,[System.ComponentModel.DataAnnotations.RegularExpression("^(MOI|XAC_NHAN|DANG_XU_LY|HOAN_TAT|HUY)$")]string? status=null,[System.ComponentModel.DataAnnotations.Range(1,100000)]int page=1) => orders.ListCustomerAsync(CustomerId, ct,q,status,page);
    [HttpGet("quote")] public Task<CheckoutQuote> Quote(CancellationToken ct,[System.ComponentModel.DataAnnotations.StringLength(20)]string? voucher=null)=>orders.QuoteAsync(CustomerId,voucher,ct);
    [HttpPost("{id}/cancel")] public async Task<IActionResult> Cancel([System.ComponentModel.DataAnnotations.StringLength(30,MinimumLength=1)]string id,CancellationToken ct){await orders.CancelCustomerAsync(CustomerId,id,ct);return NoContent();}
    [HttpGet("{id}")] public Task<OrderDetails> Details(string id, CancellationToken ct) => orders.DetailAsync(id, CustomerId, ct);
}
