using FashionStore.Backend.Data;
using FashionStore.Backend.DTOs;
using FashionStore.Backend.Models;
using FashionStore.Backend.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FashionStore.Backend.Services;

public sealed class AccountService(FashionStoreDbContext db, IPasswordHasher<TaiKhoanKhachHang> hasher,
    CustomerSessions sessions, TimeProvider clock, CustomerPasswordVerifier verifier)
{
    public async Task<CustomerResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var name = request.UserName.Trim().ToLowerInvariant();
        var email = Optional(request.Email);
        if (await db.CustomerAccounts.AnyAsync(a => a.TenDangNhap == name, ct) ||
            await db.Customers.AnyAsync(c => c.SoDienThoai == request.Phone || (email != null && c.Email == email), ct))
            throw new CustomerRequestException(409, "Tên đăng nhập, số điện thoại hoặc email đã được sử dụng.");
        var now = clock.GetLocalNow().DateTime;
        var customer = new KhachHang { MaKH = "WEB" + Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(),
            HoTen = request.FullName.Trim(), SoDienThoai = request.Phone, Email = email,
            DiaChi = Optional(request.Address), NgayTao = now };
        var account = new TaiKhoanKhachHang { MaKH = customer.MaKH, TenDangNhap = name,
            Customer = customer, NgayTao = now };
        account.MatKhauHash = hasher.HashPassword(account, request.Password);
        db.CustomerAccounts.Add(account);
        // One SaveChanges transaction creates the customer, account and unique cart together.
        db.Carts.Add(new GioHang { MaKH = customer.MaKH, Customer = customer, NgayTao = now, NgayCapNhat = now });
        await db.SaveChangesAsync(ct);
        return Map(customer, name);
    }
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var name = request.UserName.Trim().ToLowerInvariant();
        var account = await db.CustomerAccounts.AsTracking().Include(a => a.Customer)
            .SingleOrDefaultAsync(a => a.TenDangNhap == name, ct);
        var verified = verifier.Verify(account, request.Password);
        if (account == null || !account.TrangThai || !account.Customer.TrangThai || verified == PasswordVerificationResult.Failed)
            throw new CustomerRequestException(401, "Tên đăng nhập hoặc mật khẩu không đúng.");
        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
            account.MatKhauHash = hasher.HashPassword(account, request.Password);
        account.DangNhapCuoi = clock.GetLocalNow().DateTime;
        await db.SaveChangesAsync(ct);
        var token = sessions.Create(account.MaKH, account.MatKhauHash);
        return new LoginResponse(token.Token, "Bearer", token.ExpiresAtUtc);
    }
    public async Task<CustomerResponse> GetAsync(string id, CancellationToken ct)
    {
        var account = await db.CustomerAccounts.Include(a => a.Customer).SingleAsync(a => a.MaKH == id, ct);
        return Map(account.Customer, account.TenDangNhap);
    }
    public async Task<CustomerResponse> UpdateAsync(string id, ProfileRequest request, CancellationToken ct)
    {
        byte[] version;
        try { version = Convert.FromBase64String(request.RowVersion); }
        catch (FormatException) { throw new CustomerRequestException(400, "Phiên bản hồ sơ không hợp lệ."); }
        if (version.Length != 8) throw new CustomerRequestException(400, "Phiên bản hồ sơ không hợp lệ.");
        var customer = await db.Customers.AsTracking().SingleAsync(c => c.MaKH == id, ct);
        if (db.Database.IsNpgsql())
        {
            var pgVersion = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(version);
            if (pgVersion < 0 || pgVersion > uint.MaxValue) throw new CustomerRequestException(400, "Phiên bản hồ sơ không hợp lệ.");
            db.Entry(customer).Property(c => c.PgVersion).OriginalValue = (uint)pgVersion;
        }
        else db.Entry(customer).Property(c => c.RowVer).OriginalValue = version;
        customer.HoTen = request.FullName.Trim(); customer.SoDienThoai = request.Phone;
        customer.Email = Optional(request.Email); customer.DiaChi = Optional(request.Address);
        customer.NhanKhuyenMai = request.ReceivePromotions;
        await db.SaveChangesAsync(ct);
        var account = await db.CustomerAccounts.SingleAsync(a => a.MaKH == id, ct);
        return Map(customer, account.TenDangNhap);
    }
    public async Task ChangePasswordAsync(string id, ChangePasswordRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.khachhang WHERE makh={id} FOR UPDATE", ct);
        var customers = db.Database.IsNpgsql()
            ? await db.Customers.AsNoTracking().Where(c => c.MaKH == id).ToListAsync(ct)
            : await db.Customers.FromSqlInterpolated($"SELECT * FROM dbo.KhachHang WITH (UPDLOCK,HOLDLOCK) WHERE MaKH={id}").AsNoTracking().ToListAsync(ct);
        if (customers.Count != 1 || !customers[0].TrangThai) throw new CustomerRequestException(401, "Vui lòng đăng nhập lại.");
        var account = await db.CustomerAccounts.AsTracking().SingleAsync(a => a.MaKH == id, ct);
        if (!account.TrangThai) throw new CustomerRequestException(401, "Vui lòng đăng nhập lại.");
        if (verifier.Verify(account, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new CustomerRequestException(400, "Mật khẩu hiện tại không đúng.");
        account.MatKhauHash = hasher.HashPassword(account, request.NewPassword);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // Authentication compares password version on every request: all old tokens become invalid.
    }
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private CustomerResponse Map(KhachHang c, string name)
    {
        var version = c.RowVer;
        if (db.Database.IsNpgsql())
        {
            version = new byte[8];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(version, c.PgVersion);
        }
        return new(c.MaKH, name, c.HoTen, c.SoDienThoai, c.Email, c.DiaChi,
            c.NhanKhuyenMai, c.DiemTichLuy, c.HangThe, Convert.ToBase64String(version));
    }
}
