using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace FashionStore.Backend.Services;

// Separate, opaque admin sessions. Tokens are never stored in plaintext server-side.
public sealed class AdminSessions(IMemoryCache cache, TimeProvider clock)
{
    public sealed record Session(string EmployeeId, string PasswordVersion, DateTimeOffset ExpiresAtUtc);
    public (string Token, DateTimeOffset ExpiresAtUtc) Create(string employeeId, string hash)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiry = clock.GetUtcNow().AddMinutes(60);
        cache.Set(Key(token), new Session(employeeId, Version(hash), expiry), expiry);
        return (token, expiry);
    }
    public Session? Find(string token)
    {
        if (token.Length != 43 || !cache.TryGetValue<Session>(Key(token), out var session) || session == null)
            return null;
        if (session.ExpiresAtUtc <= clock.GetUtcNow()) { Revoke(token); return null; }
        return session;
    }
    public void Revoke(string token) => cache.Remove(Key(token));
    public static string Version(string hash) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)));
    private static string Key(string token) => "admin-session:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
