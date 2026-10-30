using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Middleware;
using FashionStore.Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Services;

public sealed class AdminService(FashionStoreDbContext db, AdminPasswordVerifier verifier,
    AdminSessions sessions, AdminAccess access)
{
    public async Task<AdminLoginResponse> LoginAsync(AdminLoginRequest request, CancellationToken ct)
    {
        var id = request.EmployeeId.Trim();
        var employee = await db.Employees.AsNoTracking().Include(e => e.Quyen)
            .SingleOrDefaultAsync(e => e.MaNV == id, ct);
        var verified = verifier.Verify(employee, request.Password);
        if (!access.Allowed(employee) || verified == PasswordVerificationResult.Failed ||
            string.IsNullOrWhiteSpace(employee!.MatKhauHash))
            throw new CustomerRequestException(401, "Tài khoản hoặc mật khẩu không đúng, hoặc không có quyền quản trị.");
        // Avoid silent hash migration on a read-only login. Password changes are managed by the DEV setup utility.
        var token = sessions.Create(employee.MaNV, employee.MatKhauHash);
        return new AdminLoginResponse(token.Token, "Bearer", token.ExpiresAtUtc, employee.MaNV, employee.HoTen, "Admin");
    }
    public async Task<AdminMeResponse> MeAsync(string employeeId, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().Include(e => e.Quyen)
            .SingleOrDefaultAsync(e => e.MaNV == employeeId, ct);
        if (!access.Allowed(employee)) throw new CustomerRequestException(401, "Phiên quản trị không còn hợp lệ.");
        return new AdminMeResponse(employee!.MaNV, employee.HoTen, "Admin");
    }
    public async Task<AdminDashboardResponse> DashboardAsync(CancellationToken ct)
    {
        var products = await db.Products.CountAsync(p => p.TrangThai, ct);
        var skus = await db.Variants.CountAsync(v => v.TrangThai, ct);
        var low = await db.Variants.CountAsync(v => v.TrangThai && v.SoLuongTonKho <= 5, ct);
        var customers = await db.Customers.CountAsync(ct);
        var orders = await db.AdminOrders.CountAsync(o => o.LoaiHoaDon == "ONLINE", ct);
        var pending = await db.AdminOrders.CountAsync(o => o.LoaiHoaDon == "ONLINE" && o.TrangThaiHoaDon == "MOI", ct);
        var recent = await db.AdminOrders.AsNoTracking().Where(o => o.LoaiHoaDon == "ONLINE")
            .OrderByDescending(o => o.NgayLap).ThenByDescending(o => o.SoHD).Take(8)
            .Select(o => new AdminRecentOrder(o.SoHD, o.NgayLap, o.TrangThaiHoaDon)).ToListAsync(ct);
        return new AdminDashboardResponse(products, skus, low, customers, orders, pending, recent);
    }
}
