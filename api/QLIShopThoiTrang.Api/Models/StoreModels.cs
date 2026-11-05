namespace QLIShopThoiTrang.Api.Models;

public sealed record AppStateDto(
    List<ProductDto> ProductsData,
    List<ProductTypeDto> ProductTypesData,
    List<PartnerDto> PartnersData,
    List<PromotionDto> PromotionsData,
    List<ImportRequestDto> ImportRequests,
    List<SaleHistoryDto> SalesHistory,
    List<DeliveryOrderDto> DeliveryOrders,
    List<CodOrderDto> CodOrders,
    List<DeliveryOrderDto> CancelledOrders);

public sealed record ProductDto(
    string ProductId,
    string Sku,
    string Name,
    string Type,
    string? Audience,
    string? SupplierId,
    string? SupplierName,
    string Color,
    string Size,
    string Unit,
    decimal Price,
    int Stock,
    string? Image,
    List<string> Images,
    bool Active = true,
    decimal? SalePrice = null,
    decimal SalePercent = 0,
    string? SaleCode = null);

public sealed record ProductUpsertDto(
    string? ProductId,
    string? Sku,
    string Name,
    string Type,
    string? Audience,
    string? SupplierId,
    decimal Price,
    int Stock,
    string? Color = null,
    string? Size = null,
    string? Image = null,
    List<string>? Images = null,
    bool? Active = null);

public sealed record ProductTypeDto(string Id, string Name);
public sealed record ProductVariantCreateDto(string ProductId, string Color, string Size, decimal Price, List<string>? Images = null);
public sealed record ProductGroupUpdateDto(string Name, string Type, string Audience, string SupplierId, bool? Active = null);
public sealed record ProductStatusUpdateDto(bool Active);

public sealed record PartnerDto(
    string Category,
    string Id,
    string Name,
    string Phone,
    string Email,
    string Address,
    string Role,
    int? Points,
    decimal? TotalSpent,
    string? Gender = null,
    string? BirthDate = null);

public sealed record PromotionProductDto(
    string Sku,
    string ProductId,
    string ProductName,
    string Color,
    string Size,
    decimal OriginalPrice,
    decimal Percent,
    decimal SalePrice);

public sealed record CurrentPromotionProductDto(
    string MaKhuyenMai,
    string MaSanPham,
    string MaSku,
    string TenSanPham,
    string MauSac,
    string KichCo,
    decimal GiaGoc,
    decimal PhanTramGiam,
    decimal GiaKhuyenMai,
    string NgayBatDau,
    string NgayKetThuc,
    string? HinhAnh);

public sealed record PromotionDto(
    string Code,
    decimal Percent,
    decimal MinOrder,
    string StartDate,
    string EndDate,
    List<PromotionProductDto> Products,
    string Kind = "PHAN_TRAM");

public sealed record ImportRequestDto(
    string Id,
    string SupplierId,
    string SupplierName,
    List<ImportRequestItemDto> Items,
    int TotalQty,
    decimal TotalAmount,
    string CreatedBy,
    string CreatedAt,
    string Status,
    string Note);

public sealed record ImportRequestItemDto(
    string Sku,
    string ProductName,
    int Qty,
    decimal UnitCost,
    decimal Total);

public sealed record ProductTypeUpsertDto(string Id, string Name, string? Description, bool Active = true);

public sealed record ImportRequestItemCreateDto(string Sku, int Qty, decimal UnitCost);

public sealed record ImportRequestCreateDto(
    string SupplierId,
    List<ImportRequestItemCreateDto> Items,
    string? Note,
    string? EmployeeId,
    string? ClientRequestId = null)
{
    public const int MinimumQuantity = 50;
}

public sealed record PartnerUpsertDto(
    string Category,
    string Id,
    string Name,
    string Phone,
    string? Email,
    string? Address,
    string? Role,
    string? Username = null,
    string? Password = null,
    string? Gender = null,
    string? BirthDate = null);

public sealed record PromotionProductUpsertDto(string Sku, decimal Percent);

public sealed record PromotionUpsertDto(
    string Code,
    decimal Percent,
    decimal MinOrder,
    string StartDate,
    string EndDate,
    List<PromotionProductUpsertDto>? Products = null);

public sealed record InvoiceItemDto(string Sku, string Name, int Qty, decimal Price, decimal Total);

public sealed record InvoiceCustomerDto(string? Name, string? Phone);

public sealed record InvoiceCreateDto(
    string Id,
    InvoiceCustomerDto Customer,
    List<InvoiceItemDto> Items,
    decimal Subtotal,
    decimal Discount,
    decimal VatAmount,
    decimal Total,
    string PaymentMethod,
    decimal Paid,
    string? PromoCode,
    bool IsCod,
    string? ReceiverName,
    string? ReceiverPhone,
    string? ReceiverAddress,
    string? EmployeeId,
    string? DeliveryMode = null,
    decimal ShippingFee = 0,
    string? Source = null,
    string? DeliveryNote = null);

public sealed record CodStatusUpdateDto(string Status, string? EmployeeId);

public sealed record QrPaymentSessionDto(
    long OrderCode, string InvoiceId, decimal Amount, string Description, string BankBin,
    string AccountNumber, string AccountName, string PaymentLinkId, string CheckoutUrl, string Status);

public sealed record QrPaymentStatusDto(
    long OrderCode, string Status, decimal Amount, decimal AmountPaid, bool Paid);

public sealed record SaleItemDto(string Sku, string Name, int Qty, decimal Total);

public sealed record SaleHistoryDto(
    string Id, string DateKey, string Time, string CustomerName, string PaymentMethod,
    string Source, int ItemCount, decimal Total, List<SaleItemDto> Items);

public sealed record ReceiptStateDto(
    string Id, InvoiceCustomerDto Customer, List<InvoiceItemDto> Items, decimal Subtotal,
    decimal Discount, decimal VatAmount, decimal Total, string PaymentMethod, decimal Paid, int EarnedPoints, bool IsCod);

public sealed record CodOrderDto(
    string Id,
    string Name,
    string Phone,
    string Address,
    string Note,
    decimal AmountToCollect,
    decimal ShippingFee,
    string Status,
    string Source,
    ReceiptStateDto Receipt);

public sealed record DeliveryOrderDto(
    string Id,
    string Name,
    string Phone,
    string Address,
    string Note,
    decimal ShippingFee,
    string Status,
    string Source,
    ReceiptStateDto Receipt);

public sealed record LoginRequestDto(string Username, string Password);
public sealed record LoginResultDto(string EmployeeId, string DisplayName, string Role, string? PermissionCode = null, string? SessionVersion = null);

public sealed record InvoiceSummaryDto(
    string Id,
    string CreatedAt,
    string CustomerName,
    string Phone,
    string Cashier,
    string PaymentMethod,
    int ItemCount,
    decimal Total,
    string PaymentStatus);

public sealed record InvoiceDetailItemDto(
    string Sku,
    string Name,
    string Color,
    string Size,
    string Unit,
    int Qty,
    decimal Price,
    decimal Total);

public sealed record InvoiceDetailDto(
    string Id,
    string PrintedAt,
    string Cashier,
    string CustomerName,
    string CustomerPhone,
    string CustomerRank,
    int CustomerPoints,
    List<InvoiceDetailItemDto> Items,
    int ItemCount,
    decimal Subtotal,
    decimal Discount,
    decimal VatRate,
    decimal VatAmount,
    decimal ShippingFee,
    decimal Total,
    string PaymentMethod,
    decimal Paid,
    int EarnedPoints,
    string PromoCode,
    bool IsCod,
    string DeliveryMode);

public sealed record ReturnInvoiceSummaryDto(
    string Id,
    string CreatedAt,
    string CustomerName,
    string Phone,
    string Source,
    string EligibilityDate,
    string Deadline,
    bool IsEligible,
    string EligibilityMessage);

public sealed record ReturnInvoiceItemDto(
    string Sku,
    string Name,
    string Color,
    string Size,
    string Unit,
    int PurchasedQty,
    int ReturnedQty,
    int RemainingQty,
    decimal Price,
    decimal? PaidTotal = null,
    decimal ReturnedValue = 0);

public sealed record ReturnInvoiceDetailDto(
    string Id,
    string CreatedAt,
    string CustomerName,
    string Phone,
    string Source,
    string EligibilityDate,
    string Deadline,
    bool IsEligible,
    string EligibilityMessage,
    List<ReturnInvoiceItemDto> Items);

public sealed record ReturnCreateDto(
    string InvoiceId,
    string ReturnSku,
    int Qty,
    string Reason,
    string Resolution,
    string? ReplacementSku,
    string? EmployeeId,
    string? ClientRequestId = null);

public sealed record ReturnSummaryDto(
    string Id,
    string CreatedAt,
    string InvoiceId,
    string CustomerName,
    string Phone,
    string Resolution,
    string ReturnProduct,
    int Qty,
    decimal RefundAmount,
    decimal ExtraAmount);

public sealed record ReturnReceiptDto(
    string Id,
    string CreatedAt,
    string InvoiceId,
    string OriginalPurchaseDate,
    string CustomerName,
    string Phone,
    string EmployeeName,
    string Source,
    string Reason,
    string Resolution,
    string ReturnSku,
    string ReturnProduct,
    string ReturnColor,
    string ReturnSize,
    int Qty,
    decimal ReturnUnitPrice,
    decimal ReturnValue,
    string? ReplacementSku,
    string? ReplacementProduct,
    string? ReplacementColor,
    string? ReplacementSize,
    decimal ReplacementUnitPrice,
    decimal ReplacementValue,
    decimal RefundAmount,
    decimal ExtraAmount,
    List<ReturnReceiptLineDto>? Items = null);

public sealed record ReturnReceiptLineDto(string Sku, string Name, string Color, string Size, int Qty,
    decimal ReturnValue, string? ReplacementSku, string? ReplacementName, decimal ReplacementValue, string Reason, int ReplacementQty = 0);
public sealed record ReturnRequestedLineDto(string Sku, string Name, string Color, string Size, int Qty,
    string Reason, string Resolution, string? ReplacementSku, string? ReplacementName, int ReplacementQty = 0,
    string? ReplacementColor=null,string? ReplacementSize=null);
public sealed record ReturnRequestDto(string Id, string InvoiceId, string CustomerName, string Phone,
    string CreatedAt, string Status, string? RejectionReason, List<string> Images, List<ReturnRequestedLineDto> Items,
    string? Recipient=null,string? ExchangePhone=null,string? ExchangeAddress=null,string? AccountHolder=null,
    string? BankName=null,string? BankAccount=null,decimal RefundAmount=0,decimal ExtraAmount=0);
public sealed record ReturnRequestActionDto(string? EmployeeId, string? Reason = null);

public sealed record StockReviewDto(string Source, string DocumentId, string InvoiceId, string Sku,
    string Name, string Color, string Size, int Qty, string Status, string Reason, string? ReviewedBy, string? ReviewedAt,
    int ReceivedQty = 0, int RestockedQty = 0, string? PaymentStatus = null, decimal RefundAmount = 0,
    decimal ExtraAmount = 0, List<StockReviewEventDto>? History = null);
public sealed record StockReviewEventDto(Guid ReviewId, int ReceivedQty, int RestockQty, string Note,
    string EmployeeId, DateTime ReviewedAt);
public sealed record StockReviewActionDto(bool Restock, string? Note, bool Received = false,
    int? ReceivedQty = null, int? RestockQty = null, Guid? ReviewId = null, int? ExpectedReceivedQty = null);
public sealed record StockReconcileDto(bool AlreadyDeducted);
public sealed record StockUnconfirmedDto(string InvoiceId, string Type, string Status, bool? Deducted, bool KnownUnreserved);
public sealed record CancelShipmentDto(string? Reason);

public sealed record BestSellingProductDto(string ProductId,string Name,long Quantity,long OrderCount);
