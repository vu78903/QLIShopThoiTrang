using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FashionStore.Web.Models
{
    public sealed class CheckoutViewModel
    {
        [Required(ErrorMessage="Vui lòng nhập {0}."), StringLength(150, MinimumLength = 2, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."), Display(Name = "Người nhận")]
        public string Recipient { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}."), RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage="{0} không đúng định dạng."), Display(Name = "Số điện thoại")]
        public string Phone { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}."), StringLength(300, MinimumLength = 10, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."), Display(Name = "Địa chỉ giao hàng")]
        public string Address { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}."), RegularExpression("^(COD|CHUYEN_KHOAN)$", ErrorMessage="{0} không đúng định dạng."), Display(Name = "Phương thức thanh toán")]
        public string PaymentMethod { get; set; } = "COD";
        [StringLength(500, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."), Display(Name = "Ghi chú")]
        public string Note { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}.")] public Guid RequestId { get; set; }
        [StringLength(20, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."),Display(Name="Mã ưu đãi")] public string Voucher {get;set;}
        public decimal? QuotedDiscount {get;set;}
        public decimal? QuotedTotal {get;set;}
        public CustomerCart Cart { get; set; }
    }
    public sealed class PlacedOrder
    {
        public string OrderId { get; set; }
        public string Status { get; set; }
        public decimal Total { get; set; }
        public string PaymentMethod { get; set; }
    }
    public sealed class OrderSummaryModel
    {
        public string OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public string ShippingStatus { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentMethod { get; set; }
        public decimal Total { get; set; }
        public string Recipient { get; set; }
    }
    public sealed class OrderLineModel
    {
        public string Sku { get; set; }
        public string Name { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }
    public sealed class OrderDetailsModel
    {
        public bool SupportsHistory { get; set; } = true;
        public bool SupportsReturns { get; set; } = true;
        public string OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public string ShippingStatus { get; set; }
        public string PaymentStatus { get; set; }
        public string PaymentMethod { get; set; }
        public decimal Total { get; set; }
        public string Recipient { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Note { get; set; }
        public decimal Subtotal {get;set;}
        public decimal Discount {get;set;}
        public string Voucher {get;set;}
        public List<OrderLineModel> Items { get; set; } = new List<OrderLineModel>();
    }
    public sealed class OrderActionModel
    {
        public string OrderId { get; set; }
        public string Status { get; set; }
        public string Result { get; set; }
    }
}
