using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FashionStore.Web.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage="Vui lòng nhập tên đăng nhập."), RegularExpression(@"^[A-Za-z0-9._-]{3,100}$", ErrorMessage="Tên đăng nhập gồm 3–100 ký tự không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang."), Display(Name="Tên đăng nhập")] public string UserName { get; set; }
        [Required(ErrorMessage="Vui lòng nhập mật khẩu."), StringLength(128, ErrorMessage="Mật khẩu không được vượt quá 128 ký tự."), DataType(DataType.Password), Display(Name="Mật khẩu")] public string Password { get; set; }
        public string ReturnUrl { get; set; }
    }
    public class RegisterViewModel
    {
        [Required(ErrorMessage="Vui lòng nhập tên đăng nhập."), RegularExpression(@"^[A-Za-z0-9._-]{3,100}$", ErrorMessage="Tên đăng nhập gồm 3–100 ký tự không dấu, số, dấu chấm, gạch dưới hoặc gạch ngang."), Display(Name="Tên đăng nhập")]
        public string UserName { get; set; }
        [Required(ErrorMessage="Vui lòng nhập mật khẩu."), StringLength(128, MinimumLength=12, ErrorMessage="Mật khẩu phải có từ 12 đến 128 ký tự."), RegularExpression(@"^(?=.*[A-Za-z])(?=.*[0-9]).{12,128}$", ErrorMessage="Mật khẩu cần có ít nhất một chữ cái và một chữ số."), DataType(DataType.Password), Display(Name="Mật khẩu")]
        public string Password { get; set; }
        [Required(ErrorMessage="Vui lòng nhập lại mật khẩu."), Compare("Password",ErrorMessage="Mật khẩu nhập lại chưa trùng khớp."), DataType(DataType.Password), Display(Name="Nhập lại mật khẩu")]
        public string ConfirmPassword { get; set; }
        [Required(ErrorMessage="Vui lòng nhập họ và tên."), StringLength(150, MinimumLength=2, ErrorMessage="Họ và tên phải có từ 2 đến 150 ký tự."), Display(Name="Họ và tên")]
        public string FullName { get; set; }
        [Required(ErrorMessage="Vui lòng nhập số điện thoại."), RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage="Số điện thoại phải có từ 9 đến 15 chữ số, có thể bắt đầu bằng +."), Display(Name="Số điện thoại")]
        public string Phone { get; set; }
        [Required(ErrorMessage="Vui lòng nhập email."), StringLength(254, ErrorMessage="Email tối đa 254 ký tự."), EmailAddress(ErrorMessage="Email không hợp lệ."), Display(Name="Email")]
        public string Email { get; set; }
        [Required(ErrorMessage="Vui lòng nhập số nhà và tên đường."), StringLength(180, MinimumLength=5, ErrorMessage="Số nhà và tên đường phải có từ 5 đến 180 ký tự."), Display(Name="Số nhà, tên đường")]
        public string StreetAddress { get; set; }
        [Required(ErrorMessage="Vui lòng chọn tỉnh hoặc thành phố."), Range(1, 999, ErrorMessage="Tỉnh hoặc thành phố không hợp lệ."), Display(Name="Tỉnh/Thành phố")]
        public int? ProvinceCode { get; set; }
        [Required(ErrorMessage="Vui lòng chọn phường hoặc xã."), Range(1, 99999, ErrorMessage="Phường hoặc xã không hợp lệ."), Display(Name="Phường/Xã")]
        public int? WardCode { get; set; }
        // Không gửi trực tiếp từ client: MVC xây dựng từ lựa chọn hợp lệ được xác minh trên máy chủ.
        public string Address { get; set; }
    }
    public class CustomerProfile
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}."), StringLength(150, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."), Display(Name="Họ tên")] public string FullName { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}."), RegularExpression(@"^\+?[0-9]{9,15}$", ErrorMessage="{0} không đúng định dạng."), Display(Name="Số điện thoại")] public string Phone { get; set; }
        [StringLength(254, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."), EmailAddress(ErrorMessage="Email không hợp lệ."), Display(Name="Email")] public string Email { get; set; }
        [StringLength(300, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."), Display(Name="Địa chỉ mặc định")] public string Address { get; set; }
        [Display(Name="Nhận thông tin khuyến mãi")] public bool ReceivePromotions { get; set; }
        public int Points { get; set; }
        public string Membership { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}.")] public string RowVersion { get; set; }
    }
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage="Vui lòng nhập {0}."), StringLength(128, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."), DataType(DataType.Password), Display(Name="Mật khẩu hiện tại")] public string CurrentPassword { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}."), StringLength(128, MinimumLength=12, ErrorMessage="{0} vượt giới hạn hoặc chưa đủ độ dài yêu cầu."), DataType(DataType.Password), Display(Name="Mật khẩu mới (ít nhất 12 ký tự)")] public string NewPassword { get; set; }
        [Required(ErrorMessage="Vui lòng nhập {0}."), Compare("NewPassword",ErrorMessage="Mật khẩu nhập lại chưa trùng khớp."), DataType(DataType.Password), Display(Name="Nhập lại mật khẩu mới")] public string ConfirmPassword { get; set; }
    }
    public class CustomerLoginResult
    {
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
    public class CustomerCart
    {
        public List<CustomerCartLine> Items { get; set; } = new List<CustomerCartLine>();
        public long TotalQuantity { get; set; }
        public decimal Subtotal { get; set; }
        public bool CanCheckout { get; set; }
        public bool CodOnly { get; set; }
        public bool CheckoutEnabled { get; set; } = true;
        public string Error { get; set; }
    }
    public class CustomerCartLine
    {
        public string Sku { get; set; }
        public string ProductId { get; set; }
        public string ImageUrl {get;set;}
        public string ProductName { get; set; }
        public string Size { get; set; }
        public string Color { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public int Stock { get; set; }
        public bool Available { get; set; }
        public string Issue { get; set; }
    }
}
