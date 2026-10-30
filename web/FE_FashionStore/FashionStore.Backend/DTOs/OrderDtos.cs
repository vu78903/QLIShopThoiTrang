using System.ComponentModel.DataAnnotations;

namespace FashionStore.Backend.DTOs;

public sealed record CheckoutRequest(
    [param: Required, StringLength(150, MinimumLength = 2)] string Recipient,
    [param: Required, RegularExpression(@"^\+?[0-9]{9,15}$")] string Phone,
    [param: Required, StringLength(300, MinimumLength = 10)] string Address,
    [param: Required, RegularExpression("^(COD|CHUYEN_KHOAN)$")] string PaymentMethod,
    [param: StringLength(500)] string? Note,
    [param: Required] Guid RequestId,
    [param: StringLength(20)] string? Voucher = null);
public sealed record CheckoutQuote(decimal Subtotal, decimal Discount, decimal Total);

public sealed record PlaceOrderResponse(string OrderId, string Status, decimal Total, string PaymentMethod);
public sealed record OrderSummary(string OrderId, DateTime CreatedAt, string Status, string ShippingStatus,
    string PaymentStatus, string PaymentMethod, decimal Total, string Recipient);
public sealed record OrderLine(string Sku, string Name, string Size, string Color, int Quantity, decimal UnitPrice, decimal Total);
public sealed record OrderDetails(string OrderId, DateTime CreatedAt, string Status, string ShippingStatus,
    string PaymentStatus, string PaymentMethod, decimal Total, string Recipient, string Phone, string Address,
    string? Note, IReadOnlyList<OrderLine> Items, decimal Subtotal=0, decimal Discount=0, string? Voucher=null)
{
    public bool SupportsHistory { get; init; } = true;
    public bool SupportsReturns { get; init; } = true;
}
public sealed record OrderActionResponse(string OrderId, string Status, string Result);
