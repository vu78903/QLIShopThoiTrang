using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FashionStore.Backend.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AdminLoginRequest
{
    [Required, StringLength(20)] public string EmployeeId { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}
public sealed record AdminLoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAtUtc,
    string EmployeeId, string FullName, string Role);
public sealed record AdminMeResponse(string EmployeeId, string FullName, string Role);
public sealed record AdminRecentOrder(string OrderId, DateTime CreatedAt, string Status);
public sealed record AdminDashboardResponse(int ActiveProducts, int ActiveSkus, int LowStockSkus,
    int Customers, int Orders, int PendingOrders, IReadOnlyList<AdminRecentOrder> RecentOrders);
