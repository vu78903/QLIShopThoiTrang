using FashionStore.Backend.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace FashionStore.Backend.Services;

public sealed class CustomerPasswordVerifier
{
    private readonly IPasswordHasher<TaiKhoanKhachHang> hasher;
    private readonly TaiKhoanKhachHang dummy = new();
    public CustomerPasswordVerifier(IPasswordHasher<TaiKhoanKhachHang> hasher)
    {
        this.hasher = hasher;
        dummy.MatKhauHash = hasher.HashPassword(dummy, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }
    public PasswordVerificationResult Verify(TaiKhoanKhachHang? account, string password)
    {
        var selected = account ?? dummy;
        try { return hasher.VerifyHashedPassword(selected, selected.MatKhauHash, password); }
        catch (Exception e) when (e is FormatException or ArgumentException) { return PasswordVerificationResult.Failed; }
    }
}
