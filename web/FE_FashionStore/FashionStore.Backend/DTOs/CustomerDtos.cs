using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FashionStore.Backend.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RegisterRequest
{
    [Required, RegularExpression(@"^[A-Za-z0-9._-]{3,100}$")] public string UserName { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; set; } = "";
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required, RegularExpression(@"^\+?[0-9]{9,15}$")] public string Phone { get; set; } = "";
    [StringLength(254), EmailAddress, RegularExpression(@"^[\x21-\x7E]+$")] public string? Email { get; set; }
    [StringLength(300)] public string? Address { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LoginRequest
{
    [Required, StringLength(100)] public string UserName { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ProfileRequest
{
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required, RegularExpression(@"^\+?[0-9]{9,15}$")] public string Phone { get; set; } = "";
    [StringLength(254), EmailAddress, RegularExpression(@"^[\x21-\x7E]+$")] public string? Email { get; set; }
    [StringLength(300)] public string? Address { get; set; }
    public bool ReceivePromotions { get; set; }
    [Required] public string RowVersion { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChangePasswordRequest
{
    [Required, StringLength(128)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string NewPassword { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CartItemRequest
{
    [Required, StringLength(40)] public string Sku { get; set; } = "";
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CartQuantityRequest
{
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}
public sealed record CustomerResponse(string Id, string UserName, string FullName, string Phone,
    string? Email, string? Address, bool ReceivePromotions, int Points, string Membership, string RowVersion);
public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc);
public sealed record CartLineResponse(string Sku, string ProductId, string ProductName, string Size, string Color,
    int Quantity, decimal UnitPrice, decimal LineTotal, int Stock, bool Available, string? Issue, string? ImageUrl=null);
public sealed record CartResponse(IReadOnlyList<CartLineResponse> Items, long TotalQuantity, decimal Subtotal, bool CanCheckout)
{
    public bool CodOnly { get; init; }
    public bool CheckoutEnabled { get; init; } = true;
}
