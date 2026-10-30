using FashionStore.Backend.Models;
using Microsoft.Extensions.Options;

namespace FashionStore.Backend.Services;

public sealed class AdminRoleOptions
{
    // Explicit grant only. Do not grant rights based on display name or "non-Customer" status.
    public string RoleCode { get; set; } = "";
}
public sealed class AdminAccess(IOptions<AdminRoleOptions> options)
{
    public bool Allowed(NhanVien? employee) => employee != null && employee.TrangThai &&
        employee.Quyen != null && employee.Quyen.TrangThai &&
        !string.IsNullOrWhiteSpace(options.Value.RoleCode) &&
        string.Equals(employee.MaQuyen, options.Value.RoleCode.Trim(), StringComparison.OrdinalIgnoreCase);
}
