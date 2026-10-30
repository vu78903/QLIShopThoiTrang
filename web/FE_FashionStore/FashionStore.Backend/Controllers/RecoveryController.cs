using System.ComponentModel.DataAnnotations;
using FashionStore.Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace FashionStore.Backend.Controllers;
[ApiController,Route("api/account/recovery"),EnableRateLimiting("account"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class RecoveryController(PasswordRecoveryService recovery):ControllerBase
{
    [HttpPost] public async Task<IActionResult> Request(RecoveryInput input,CancellationToken ct){await recovery.Request(input.Email,ct);return NoContent();}
    [HttpPost("reset")] public async Task<IActionResult> Reset(ResetInput input,CancellationToken ct){await recovery.Reset(input.Token,input.Password,ct);return NoContent();}
}
public sealed class RecoveryInput { [Required,EmailAddress,StringLength(254)] public string Email{get;set;}=""; }
public sealed class ResetInput {
 [Required,StringLength(43,MinimumLength=43)] public string Token{get;set;}="";
 [Required,StringLength(128,MinimumLength=12)] public string Password{get;set;}="";
}
