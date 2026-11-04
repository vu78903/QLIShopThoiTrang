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
    public void RemoveEmployee(string employeeId)
    {
        foreach(var entry in sessions)
            if(entry.Value.User.EmployeeId==employeeId.Trim())sessions.TryRemove(entry.Key,out _);
    }
    // Chỉ gỡ thế hệ cũ; phiên được tạo sau cập nhật bảo mật vẫn được giữ.
    public void RemoveEmployeeVersion(string employeeId,string version)
    {
        foreach(var entry in sessions)
            if(entry.Value.User.EmployeeId==employeeId.Trim() && entry.Value.User.SessionVersion==version)
                sessions.TryRemove(entry.Key,out _);
    }
    private static bool Under(string path,string prefix) => path==prefix || path.StartsWith(prefix+"/",StringComparison.Ordinal);
    public static bool CanRead(string role,string path)
    {
        if(role=="CuaHangTruong")return true;
        if(role is not ("ThuNgan" or "TruongCa" or "NhanVienKho"))return false;
        if(path is "/api/app-state" or "/api/loai-san-pham" or "/api/san-pham-khuyen-mai")return true;
        if(Under(path,"/api/kiem-nhan-kho"))return role is "NhanVienKho" or "TruongCa";
        return role is "ThuNgan" or "TruongCa" && (Under(path,"/api/hoa-don") || Under(path,"/api/doi-tra") || Under(path,"/api/thanh-toan-qr"));
    }
    public static bool CanWrite(string role,string path,string method)
    {
        if(role is not ("CuaHangTruong" or "ThuNgan" or "TruongCa" or "NhanVienKho"))return false;
        if(path=="/api/dang-xuat") return true;
        if(role=="CuaHangTruong") return true;
        if(Under(path,"/api/kiem-nhan-kho")) return method=="GET" && role is "NhanVienKho" or "TruongCa";
        if(Under(path,"/api/phieu-nhap-kho")) return !path.EndsWith("/duyet") && !path.EndsWith("/tu-choi") && role is "NhanVienKho" or "TruongCa";
        if(Under(path,"/api/hoa-don") || Under(path,"/api/doi-tra") || Under(path,"/api/thanh-toan-qr")) return role is "ThuNgan" or "TruongCa";
        // Thu ngân được thêm nhanh khách hàng, không được quản trị đối tác khác.
        return false;
    }
}
