using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FashionStore.Web.Services
{
    // Dữ liệu đơn vị hành chính hai cấp (sau 07/2025): https://provinces.open-api.vn/api/v2/
    // Các lựa chọn phải được xác minh ở máy chủ; không tin tên tỉnh/phường do client gửi lên.
    public sealed class VietnamLocation
    {
        public int Code { get; set; }
        public string Name { get; set; }
    }

    public static class VietnamLocationsClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        private static readonly object Sync = new object();
        private static readonly Dictionary<int, Tuple<DateTime, IList<VietnamLocation>>> WardCache =
            new Dictionary<int, Tuple<DateTime, IList<VietnamLocation>>>();
        private static Tuple<DateTime, IList<VietnamLocation>> provinceCache;
        private const string BaseUrl = "https://provinces.open-api.vn/api/v2/";

        public static async Task<IList<VietnamLocation>> ProvincesAsync()
        {
            lock (Sync)
            {
                if (provinceCache != null && provinceCache.Item1 > DateTime.UtcNow)
                    return provinceCache.Item2;
            }
            var json = await Http.GetStringAsync(BaseUrl).ConfigureAwait(false);
            var locations = ReadArray(JArray.Parse(json));
            if (locations.Count < 30) throw new InvalidOperationException("Danh sách tỉnh/thành phố không đầy đủ.");
            lock (Sync) { provinceCache = Tuple.Create(DateTime.UtcNow.AddHours(12), locations); }
            return locations;
        }

        public static async Task<IList<VietnamLocation>> WardsAsync(int provinceCode)
        {
            if (provinceCode < 1 || provinceCode > 999) throw new ArgumentOutOfRangeException("provinceCode");
            lock (Sync)
            {
                Tuple<DateTime, IList<VietnamLocation>> hit;
                if (WardCache.TryGetValue(provinceCode, out hit) && hit.Item1 > DateTime.UtcNow)
                    return hit.Item2;
            }
            // Chỉ gọi endpoint đã định dạng từ mã số nguyên, không nhận URL từ người dùng.
            var json = await Http.GetStringAsync(BaseUrl + "p/" + provinceCode + "?depth=2").ConfigureAwait(false);
            var province = JObject.Parse(json);
            var wards = province["wards"] as JArray;
            if (wards == null || wards.Count == 0) throw new InvalidOperationException("Không có dữ liệu phường/xã.");
            var locations = ReadArray(wards);
            lock (Sync) { WardCache[provinceCode] = Tuple.Create(DateTime.UtcNow.AddHours(12), locations); }
            return locations;
        }

        public static async Task<string> ValidatedAddressAsync(string street, int provinceCode, int wardCode)
        {
            var provinces = await ProvincesAsync().ConfigureAwait(false);
            var province = provinces.FirstOrDefault(p => p.Code == provinceCode);
            if (province == null) return null;
            var wards = await WardsAsync(provinceCode).ConfigureAwait(false);
            var ward = wards.FirstOrDefault(w => w.Code == wardCode);
            if (ward == null) return null;
            var address = (street ?? "").Trim() + ", " + ward.Name + ", " + province.Name;
            return address.Length <= 300 ? address : null;
        }

        private static IList<VietnamLocation> ReadArray(JArray items)
        {
            return items.Select(item => new VietnamLocation {
                Code = (int)item["code"], Name = (string)item["name"]
            }).Where(x => x.Code > 0 && !String.IsNullOrWhiteSpace(x.Name))
              .OrderBy(x => x.Name, StringComparer.CurrentCulture).ToList();
        }
    }
}
