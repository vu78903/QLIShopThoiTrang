using FashionStore.Backend.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace FashionStore.Backend.Services;

// Creates one dummy hash at startup instead of a costly PBKDF2 hash for every dashboard request.
public sealed class AdminPasswordVerifier
{
    private readonly IPasswordHasher<NhanVien> hasher;
    private readonly NhanVien dummy = new();
    public AdminPasswordVerifier(IPasswordHasher<NhanVien> hasher)
    {
        this.hasher = hasher;
        dummy.MatKhauHash = hasher.HashPassword(dummy, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }
    public PasswordVerificationResult Verify(NhanVien? employee, string password)
    {
        var selected = employee ?? dummy;
        var hash = string.IsNullOrWhiteSpace(selected.MatKhauHash) ? dummy.MatKhauHash! : selected.MatKhauHash;
        try { return hasher.VerifyHashedPassword(selected, hash, password); }
        catch (Exception e) when (e is FormatException or ArgumentException or InvalidOperationException)
        { return PasswordVerificationResult.Failed; }
    }
}
