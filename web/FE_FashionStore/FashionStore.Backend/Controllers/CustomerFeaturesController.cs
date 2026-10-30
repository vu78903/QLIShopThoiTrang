using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FashionStore.Backend.Controllers;
[ApiController,Route("api/customer"),Authorize(Roles="Customer"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class CustomerFeaturesController(CustomerFeaturesService service):ControllerBase
{
    private string Id=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [AllowAnonymous,HttpGet("promotions")] public Task<List<Dictionary<string,object?>>> Promotions(CancellationToken ct)=>service.Promotions(ct);
    [HttpGet("points")] public Task<List<Dictionary<string,object?>>> Points(CancellationToken ct,[Range(1,100000)]int page=1)=>service.Points(Id,page,ct);
    [HttpGet("orders/{id}/history")] public Task<List<Dictionary<string,object?>>> History(string id,CancellationToken ct)=>service.History(Id,id,ct);
    [AllowAnonymous,HttpGet("reviews/{id}")] public Task<List<Dictionary<string,object?>>> Reviews(string id,CancellationToken ct,[Range(1,100000)]int page=1)=>service.Reviews(id,page,ct);
    [HttpPost("reviews")] public async Task<IActionResult> Review(ReviewInput input,CancellationToken ct){await service.Review(Id,input,ct);return NoContent();}
    [HttpGet("returns")] public Task<List<Dictionary<string,object?>>> Returns(CancellationToken ct,[Range(1,100000)]int page=1)=>service.Returns(Id,page,ct);
    [HttpPost("returns")] public async Task<IActionResult> Return(ReturnInput input,CancellationToken ct){await service.CreateReturn(Id,input,ct);return NoContent();}
    [HttpGet("wishlist")] public Task<List<Dictionary<string,object?>>> Wishlist(CancellationToken ct)=>service.Wishlist(Id,ct);
    [HttpPut("wishlist/{id}")] public async Task<IActionResult> SaveWishlist([StringLength(20,MinimumLength=1)]string id,CancellationToken ct){await service.SaveWishlist(Id,id,false,ct);return NoContent();}
    [HttpDelete("wishlist/{id}")] public async Task<IActionResult> RemoveWishlist([StringLength(20,MinimumLength=1)]string id,CancellationToken ct){await service.SaveWishlist(Id,id,true,ct);return NoContent();}
    [HttpGet("addresses")] public Task<List<Dictionary<string,object?>>> Addresses(CancellationToken ct)=>service.Addresses(Id,ct);
    [HttpPost("addresses")] public async Task<IActionResult> Address(AddressInput input,CancellationToken ct){await service.SaveAddress(Id,input,ct);return NoContent();}
    [HttpDelete("addresses/{id:long}")] public async Task<IActionResult> RemoveAddress(long id,CancellationToken ct){await service.RemoveAddress(Id,id,ct);return NoContent();}
}
