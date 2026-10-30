using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FashionStore.Web.Models
{
    public sealed class AdminLoginViewModel
    {
        [Required, StringLength(20), Display(Name="Mã nhân viên")]
        public string EmployeeId { get; set; }
        [Required, StringLength(128), DataType(DataType.Password), Display(Name="Mật khẩu")]
        public string Password { get; set; }
    }
    public sealed class AdminLoginResult
    {
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public string EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
    }
    public sealed class AdminIdentity
    {
        public string EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
    }
    public sealed class AdminOrderSummary
    {
        public string OrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
    }
    public sealed class AdminDashboard
    {
        public int ActiveProducts { get; set; }
        public int ActiveSkus { get; set; }
        public int LowStockSkus { get; set; }
        public int Customers { get; set; }
        public int Orders { get; set; }
        public int PendingOrders { get; set; }
        public List<AdminOrderSummary> RecentOrders { get; set; } = new List<AdminOrderSummary>();
    }
    public sealed class AdminHomeViewModel
    {
        public AdminIdentity Identity { get; set; }
        public AdminDashboard Dashboard { get; set; }
    }
}
