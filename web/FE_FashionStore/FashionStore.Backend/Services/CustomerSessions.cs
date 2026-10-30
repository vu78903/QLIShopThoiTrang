using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace FashionStore.Backend.Services;

// Single-backend DEV session store. No database schema change or signing secret required.
// Only SHA-256 digests of random 256-bit tokens are retained. Restart signs all clients out.
public sealed class CustomerSessions(IMemoryCache cache, TimeProvider clock)
{
    public sealed record Session(string CustomerId, string PasswordVersion, DateTimeOffset ExpiresAtUtc);
    public (string Token, DateTimeOffset ExpiresAtUtc) Create(string customerId, string passwordHash)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiry = clock.GetUtcNow().AddHours(1);
        cache.Set(Key(token), new Session(customerId, Version(passwordHash), expiry), expiry);
        return (token, expiry);
    }
    public Session? Find(string token)
    {
        if (token.Length != 43 || !cache.TryGetValue<Session>(Key(token), out var session) || session == null) return null;
        if (session.ExpiresAtUtc <= clock.GetUtcNow()) { Revoke(token); return null; }
        return session;
    }
    public void Revoke(string token) => cache.Remove(Key(token));
    public static string Version(string hash) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)));
    private static string Key(string token) => "customer-session:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
