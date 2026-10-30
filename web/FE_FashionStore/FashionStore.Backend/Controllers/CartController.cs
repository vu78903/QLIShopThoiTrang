using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FashionStore.Backend.Controllers;

[ApiController, Route("api/cart"), Authorize(Roles = "Customer"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CartController(CartService carts) : ControllerBase
{
    [HttpGet] public Task<CartResponse> Get(CancellationToken ct) => carts.GetAsync(CustomerId, ct);
    [HttpPost("items")] public Task<CartResponse> Add(CartItemRequest request, CancellationToken ct) =>
        carts.SetAsync(CustomerId, request.Sku, request.Quantity, true, ct);
    [HttpPut("items/{sku}")] public Task<CartResponse> Set([StringLength(40, MinimumLength = 1)] string sku, CartQuantityRequest request, CancellationToken ct) =>
        carts.SetAsync(CustomerId, sku, request.Quantity, false, ct);
    [HttpDelete("items/{sku}")] public Task<CartResponse> Delete([StringLength(40, MinimumLength = 1)] string sku, CancellationToken ct) =>
        carts.DeleteAsync(CustomerId, sku, ct);
    [HttpDelete] public Task<CartResponse> Clear(CancellationToken ct) => carts.DeleteAsync(CustomerId, null, ct);
    private string CustomerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
