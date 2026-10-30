using System.Collections.Concurrent;
using System.Security.Cryptography;
using QLIShopThoiTrang.Api.Models;
namespace QLIShopThoiTrang.Api.Services;

public sealed class StaffSessions
{
    private readonly ConcurrentDictionary<string,(LoginResultDto User, DateTimeOffset Expiry)> sessions = new();
    public string Create(LoginResultDto user) { var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); sessions[token]=(user,DateTimeOffset.UtcNow.AddHours(12)); return token; }
    public LoginResultDto? Get(string token) { if(sessions.TryGetValue(token,out var entry) && entry.Expiry>DateTimeOffset.UtcNow) return entry.User; sessions.TryRemove(token,out _); return null; }
    public void Remove(string token) => sessions.TryRemove(token,out _);
    public static bool CanWrite(string role,string path,string method)
    {
        if(path=="/api/dang-xuat") return true;
        if(role=="CuaHangTruong") return true;
        if(path.StartsWith("/api/phieu-nhap-kho")) return !path.EndsWith("/duyet") && !path.EndsWith("/tu-choi") && role is "NhanVienKho" or "TruongCa";
        if(path.StartsWith("/api/hoa-don") || path.StartsWith("/api/doi-tra") || path.StartsWith("/api/thanh-toan-qr")) return role is "ThuNgan" or "TruongCa";
        // Thu ngân được thêm nhanh khách hàng, không được quản trị đối tác khác.
        return false;
    }
}
