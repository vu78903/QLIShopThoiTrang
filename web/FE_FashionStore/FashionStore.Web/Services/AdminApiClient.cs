using System;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FashionStore.Web.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FashionStore.Web.Services
{
    public sealed class AdminApiClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        private readonly Uri root;
        public AdminApiClient()
        {
            var configured = ConfigurationManager.AppSettings["FashionStoreApiBaseUrl"];
            Uri address;
            if (!Uri.TryCreate(configured, UriKind.Absolute, out address) ||
                (address.Scheme != "http" && address.Scheme != "https") ||
                !String.IsNullOrEmpty(address.Query) || !String.IsNullOrEmpty(address.Fragment))
                throw new InvalidOperationException("Invalid API base URL.");
            root = new Uri(configured.TrimEnd('/') + "/");
        }
        public Task<AdminLoginResult> LoginAsync(AdminLoginViewModel model) => SendAsync<AdminLoginResult>(HttpMethod.Post,
            "api/admin/auth/login", null, new { model.EmployeeId, model.Password });
        public Task<AdminIdentity> MeAsync(string token) => SendAsync<AdminIdentity>(HttpMethod.Get, "api/admin/auth/me", token);
        public Task<AdminDashboard> DashboardAsync(string token) => SendAsync<AdminDashboard>(HttpMethod.Get, "api/admin/dashboard", token);
        public Task<object> LogoutAsync(string token) => SendAsync<object>(HttpMethod.Post, "api/admin/auth/logout", token);
        public Task<System.Collections.Generic.List<OrderSummaryModel>> OrdersAsync(string token) =>
            SendAsync<System.Collections.Generic.List<OrderSummaryModel>>(HttpMethod.Get,"api/admin/orders",token);
        public Task<OrderDetailsModel> OrderAsync(string token, string id) =>
            SendAsync<OrderDetailsModel>(HttpMethod.Get,"api/admin/orders/"+Uri.EscapeDataString(id),token);
        public Task<OrderActionModel> ConfirmAsync(string token, string id) =>
            SendAsync<OrderActionModel>(HttpMethod.Post,"api/admin/orders/"+Uri.EscapeDataString(id)+"/confirm",token);
        public Task<OrderActionModel> CancelAsync(string token, string id) =>
            SendAsync<OrderActionModel>(HttpMethod.Post,"api/admin/orders/"+Uri.EscapeDataString(id)+"/cancel",token);

        private async Task<T> SendAsync<T>(HttpMethod method, string path, string token, object body = null) where T : class
        {
            using (var request = new HttpRequestMessage(method, new Uri(root, path)))
            {
                if (!String.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (body != null) request.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        string message = "Dịch vụ quản trị chưa thể xử lý yêu cầu.";
                        try { message = (string)JObject.Parse(json)["title"] ?? message; } catch (JsonException) { }
                        if (message == "An unexpected error occurred." || message == "Internal Server Error")
                            message = "Hệ thống quản trị gặp lỗi. Vui lòng thử lại.";
                        throw new AdminApiException((int)response.StatusCode, message);
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
                    try
                    {
                        var item = JsonConvert.DeserializeObject<T>(json);
                        if (item == null) throw new AdminApiException(502, "Phản hồi API không hợp lệ.");
                        return item;
                    }
                    catch (JsonException) { throw new AdminApiException(502, "Phản hồi API không hợp lệ."); }
                }
            }
        }
    }
    public sealed class AdminApiException : Exception
    {
        public int StatusCode { get; private set; }
        public AdminApiException(int status, string message) : base(message) { StatusCode = status; }
    }
}
