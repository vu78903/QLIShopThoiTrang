using System;
using System.Globalization;
using System.Text;
using System.Web;
using System.Web.Security;

namespace FashionStore.Web.Helpers
{
    // Independent from the Customer Forms Authentication ticket/cookie.
    public static class AdminSessionCookie
    {
        public const string Name = "FashionStore.Admin";
        public static void SignIn(HttpContextBase context, string accessToken, DateTimeOffset expiresUtc)
        {
            if (String.IsNullOrWhiteSpace(accessToken) || accessToken.Length != 43 || expiresUtc <= DateTimeOffset.UtcNow)
                throw new ArgumentException("Invalid admin session.");
            // Never issue a remotely accessible admin cookie over plain HTTP.
            if (!context.Request.IsSecureConnection && !context.Request.IsLocal)
                throw new InvalidOperationException("HTTPS is required for administrator login.");
            var payload = accessToken + "|" + expiresUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture);
            var encrypted = MachineKey.Protect(Encoding.UTF8.GetBytes(payload), "FashionStore", "AdminCookie", "v1");
            var cookie = new HttpCookie(Name, WebSafeEncode(encrypted))
            {
                HttpOnly = true, Secure = context.Request.IsSecureConnection, SameSite = SameSiteMode.Strict, Path = "/"
            };
            context.Response.Cookies.Add(cookie);
            context.Response.Cache.SetNoStore();
        }
        public static string Token(HttpRequestBase request)
        {
            var value = request.Cookies[Name]?.Value;
            if (String.IsNullOrEmpty(value)) return null;
            try
            {
                var bytes = MachineKey.Unprotect(WebSafeDecode(value), "FashionStore", "AdminCookie", "v1");
                if (bytes == null) return null;
                var fields = Encoding.UTF8.GetString(bytes).Split('|');
                if (fields.Length != 2 || fields[0].Length != 43) return null;
                long ticks;
                if (!long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out ticks)) return null;
                if (new DateTime(ticks, DateTimeKind.Utc) <= DateTime.UtcNow) return null;
                return fields[0];
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }
        public static void SignOut(HttpContextBase context)
        {
            context.Response.Cookies.Add(new HttpCookie(Name, "")
            {
                Expires = DateTime.UtcNow.AddDays(-1), HttpOnly = true,
                Secure = context.Request.IsSecureConnection, SameSite = SameSiteMode.Strict, Path = "/"
            });
        }
        private static string WebSafeEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static byte[] WebSafeDecode(string encoded)
        {
            var b64 = encoded.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(b64.PadRight((b64.Length + 3) / 4 * 4, '='));
        }
    }
}
