using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using FashionStore.Backend.Models;
using FashionStore.Backend.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
namespace FashionStore.Backend.Services;
public sealed class PasswordRecoveryService(CustomerFeaturesService data,IConfiguration config,IPasswordHasher<TaiKhoanKhachHang> hasher,ILogger<PasswordRecoveryService> logger)
{
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public async Task Request(string email,CancellationToken ct)
    {
        var host=config["Recovery:SmtpHost"];var from=config["Recovery:From"];var resetUrl=config["Recovery:ResetUrl"];
        if(string.IsNullOrWhiteSpace(host)||string.IsNullOrWhiteSpace(from)||!Uri.TryCreate(resetUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https")
            throw new CustomerRequestException(503,"Khôi phục mật khẩu chưa được bật. Cửa hàng cần cấu hình email và địa chỉ HTTPS nhận liên kết.");
        await using var c=data.Connection();await c.OpenAsync(ct);
        await using(var ready=CustomerFeaturesService.Command(c,null,"SELECT TOP (0) TokenHash FROM dbo.WebPasswordReset")) await ready.ExecuteNonQueryAsync(ct);
        var rows=await data.Query(@"SELECT a.MaKH,a.MatKhauHash FROM dbo.TaiKhoanKhachHang a JOIN dbo.KhachHang k ON k.MaKH=a.MaKH WHERE k.Email=@email AND k.TrangThai=1 AND a.TrangThai=1",ct,("@email",email.Trim()));
        // Deliberately never return a token, account identity or a delivery confirmation.
        if(rows.Count!=1)return;
        var row=rows[0];var token=WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await using(var cmd=CustomerFeaturesService.Command(c,null,"INSERT dbo.WebPasswordReset(TokenHash,MaKH,PasswordVersion,ExpiresAtUtc) VALUES(@token,@kh,@version,DATEADD(minute,20,SYSUTCDATETIME()))",("@token",Hash(token)),("@kh",row["MaKH"]),("@version",CustomerSessions.Version((string)row["MatKhauHash"]!)))) await cmd.ExecuteNonQueryAsync(ct);
        try {
            using var smtp=new SmtpClient(host,config.GetValue<int?>("Recovery:SmtpPort")??587) {EnableSsl=true};
            if(!string.IsNullOrEmpty(config["Recovery:SmtpUser"])) smtp.Credentials=new NetworkCredential(config["Recovery:SmtpUser"],config["Recovery:SmtpPassword"]);
            using var message=new MailMessage(from!,email.Trim(),"Mộc Nhiên — khôi phục mật khẩu","Liên kết có hiệu lực 20 phút, dùng một lần:\n"+resetUrl+"?token="+Uri.EscapeDataString(token)+"\nNếu bạn không yêu cầu, hãy bỏ qua email này.");
            await smtp.SendMailAsync(message,ct);
        } catch(SmtpException) { logger.LogWarning("Password recovery delivery failed; check SMTP configuration."); }
    }
    public async Task Reset(string token,string password,CancellationToken ct)
    {
        if(token.Length!=43)throw new CustomerRequestException(400,"Liên kết không hợp lệ hoặc đã hết hạn.");
        await using var c=data.Connection();await c.OpenAsync(ct);
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        string kh;string version;string current;
        await using(var cmd=CustomerFeaturesService.Command(c,tx,@"SELECT r.MaKH,r.PasswordVersion,a.MatKhauHash FROM dbo.WebPasswordReset r WITH(UPDLOCK,HOLDLOCK) JOIN dbo.TaiKhoanKhachHang a WITH(UPDLOCK,HOLDLOCK) ON a.MaKH=r.MaKH JOIN dbo.KhachHang k ON k.MaKH=a.MaKH WHERE r.TokenHash=@hash AND r.UsedAtUtc IS NULL AND r.ExpiresAtUtc>SYSUTCDATETIME() AND a.TrangThai=1 AND k.TrangThai=1",("@hash",Hash(token))))
        await using(var rd=await cmd.ExecuteReaderAsync(ct)) {
            if(!await rd.ReadAsync(ct))throw new CustomerRequestException(400,"Liên kết không hợp lệ hoặc đã hết hạn.");
            kh=rd.GetString(0);version=rd.GetString(1);current=rd.GetString(2);
        }
        if(CustomerSessions.Version(current)!=version)throw new CustomerRequestException(400,"Liên kết không còn hiệu lực.");
        var passwordHash=hasher.HashPassword(new TaiKhoanKhachHang(),password);
        await using(var cmd=CustomerFeaturesService.Command(c,tx,@"UPDATE dbo.TaiKhoanKhachHang SET MatKhauHash=@password WHERE MaKH=@kh;
UPDATE dbo.WebPasswordReset SET UsedAtUtc=SYSUTCDATETIME() WHERE MaKH=@kh AND UsedAtUtc IS NULL",("@password",passwordHash),("@kh",kh))) await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }
}
