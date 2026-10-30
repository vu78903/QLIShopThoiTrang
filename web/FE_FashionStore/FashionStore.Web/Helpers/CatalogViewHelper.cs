using System;
using System.Globalization;

namespace FashionStore.Web.Helpers
{
    public static class CatalogViewHelper
    {
        private const string Placeholder = "~/Content/images/product-placeholder.svg";

        public static string ImagePath(string raw)
        {
            if (String.IsNullOrWhiteSpace(raw)) return Placeholder;
            string value = raw.Trim().Replace('\\', '/');
            Uri uri;
            if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? value : Placeholder;
            if (value.Contains("..") || value.Contains(":") || value.StartsWith("//")) return Placeholder;
            if (value.StartsWith("~/") || value.StartsWith("/")) return value;
            return "~/" + value.TrimStart('/');
        }

        public static string Vnd(decimal price)
        {
            return price.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
        }

        public static string AudienceLabel(string value)
        {
            switch (value)
            {
                case "NAM": return "Nam";
                case "NU": return "Nữ";
                case "PHU_KIEN": return "Phụ kiện";
                default: return "Thời trang";
            }
        }
    }
}
