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
    public sealed class CustomerApiClient
    {
        private static readonly HttpClient SharedHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        private readonly HttpClient http;
        private readonly Uri baseUri;
        public CustomerApiClient() : this(SharedHttp, ConfigurationManager.AppSettings["FashionStoreApiBaseUrl"]) { }
        internal CustomerApiClient(HttpClient client, string baseUrl)
        {
            Uri parsed;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != "http" && parsed.Scheme != "https") || !String.IsNullOrEmpty(parsed.Query) || !String.IsNullOrEmpty(parsed.Fragment))
                throw new InvalidOperationException("FashionStoreApiBaseUrl must be an absolute HTTP(S) base URL.");
            http = client; baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
        }
        public Task<CustomerProfile> RegisterAsync(RegisterViewModel model) => SendAsync<CustomerProfile>(HttpMethod.Post, "api/account/register", null,
            new { model.UserName, model.Password, model.FullName, model.Phone, Email=Optional(model.Email), Address=Optional(model.Address) });
        public Task<CustomerLoginResult> LoginAsync(LoginViewModel model) => SendAsync<CustomerLoginResult>(HttpMethod.Post, "api/account/login", null,
            new { model.UserName, model.Password });
        public Task<CustomerProfile> ProfileAsync(string token) => SendAsync<CustomerProfile>(HttpMethod.Get, "api/account/me", token);
        public Task<CustomerProfile> UpdateAsync(string token, CustomerProfile model) => SendAsync<CustomerProfile>(HttpMethod.Put, "api/account/me", token,
            new { model.FullName, model.Phone, Email=Optional(model.Email), Address=Optional(model.Address), model.ReceivePromotions, model.RowVersion });
        public Task<object> PasswordAsync(string token, ChangePasswordViewModel model) => SendAsync<object>(HttpMethod.Post, "api/account/password", token,
            new { model.CurrentPassword, model.NewPassword });
        public Task<object> LogoutAsync(string token) => SendAsync<object>(HttpMethod.Post, "api/account/logout", token);
        public Task<CustomerCart> CartAsync(string token) => SendAsync<CustomerCart>(HttpMethod.Get, "api/cart", token);
        public Task<CustomerCart> AddAsync(string token, string sku, int quantity) => SendAsync<CustomerCart>(HttpMethod.Post, "api/cart/items", token, new { Sku=sku, Quantity=quantity });
        public Task<CustomerCart> SetAsync(string token, string sku, int quantity) => SendAsync<CustomerCart>(HttpMethod.Put, "api/cart/items/"+Uri.EscapeDataString(sku), token, new { Quantity=quantity });
        public Task<CustomerCart> DeleteAsync(string token, string sku) => SendAsync<CustomerCart>(HttpMethod.Delete, "api/cart/items/"+Uri.EscapeDataString(sku), token);
        public Task<CustomerCart> ClearAsync(string token) => SendAsync<CustomerCart>(HttpMethod.Delete, "api/cart", token);
        public Task<PlacedOrder> PlaceOrderAsync(string token, CheckoutViewModel model) =>
            SendAsync<PlacedOrder>(HttpMethod.Post, "api/orders", token,
                new { model.Recipient, model.Phone, model.Address, model.PaymentMethod, model.Note, model.RequestId, model.Voucher });
        public Task<System.Collections.Generic.List<OrderSummaryModel>> OrdersAsync(string token,string q=null,string status=null,int page=1) =>
            SendAsync<System.Collections.Generic.List<OrderSummaryModel>>(HttpMethod.Get, "api/orders?q="+Uri.EscapeDataString(q??"")+"&status="+Uri.EscapeDataString(status??"")+"&page="+page, token);
        public Task<OrderDetailsModel> OrderAsync(string token, string id) =>
            SendAsync<OrderDetailsModel>(HttpMethod.Get, "api/orders/"+Uri.EscapeDataString(id), token);

        public Task<JArray> FeatureListAsync(string token,string path) => SendAsync<JArray>(HttpMethod.Get,"api/customer/"+path,token);
        public Task<JObject> QuoteAsync(string token,string voucher) => SendAsync<JObject>(HttpMethod.Get,"api/orders/quote?voucher="+Uri.EscapeDataString(voucher??""),token);
        public Task<object> FeatureAsync(string token,string path,object body=null,string method="POST")=>SendAsync<object>(new HttpMethod(method),"api/customer/"+path,token,body);
        public Task<object> CancelAsync(string token,string id)=>SendAsync<object>(HttpMethod.Post,"api/orders/"+Uri.EscapeDataString(id)+"/cancel",token);
        public Task<object> RecoveryAsync(string email)=>SendAsync<object>(HttpMethod.Post,"api/account/recovery",null,new{Email=email});
        public Task<object> ResetAsync(string token,string password)=>SendAsync<object>(HttpMethod.Post,"api/account/recovery/reset",null,new{Token=token,Password=password});
        private async Task<T> SendAsync<T>(HttpMethod method, string path, string token, object body=null) where T:class
        {
            using (var request = new HttpRequestMessage(method, new Uri(baseUri,path)))
            {
                // Authorization belongs to this request, never SharedHttp.DefaultRequestHeaders.
                if (!String.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",token);
                if (body!=null) request.Content = new StringContent(JsonConvert.SerializeObject(body),Encoding.UTF8,"application/json");
                using (var response = await http.SendAsync(request).ConfigureAwait(false))
                {
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        string title = "Chưa thể xử lý yêu cầu. Vui lòng thử lại.";
                        try { title = (string)JObject.Parse(json)["title"] ?? title; } catch (JsonException) { }
                        if(title=="One or more validation errors occurred.")title="Thông tin chưa hợp lệ. Vui lòng kiểm tra độ dài, số lượng và các trường bắt buộc.";
                        if(title=="An unexpected error occurred." || title=="Internal Server Error")
                            title="Hệ thống gặp lỗi khi xử lý. Vui lòng thử lại hoặc liên hệ cửa hàng.";
                        throw new CustomerApiException((int)response.StatusCode,title);
                    }
                    if (response.StatusCode==System.Net.HttpStatusCode.NoContent) return null;
                    try {
                        var result=JsonConvert.DeserializeObject<T>(json);
                        if(result==null)throw new CustomerApiException(502,"Phản hồi từ dịch vụ không hợp lệ.");
                        var cart=result as CustomerCart;
                        if(cart!=null){
                            var document=JObject.Parse(json);
                            if(!(document.GetValue("items",StringComparison.OrdinalIgnoreCase) is JArray) || cart.Items==null ||
                                document.GetValue("totalQuantity",StringComparison.OrdinalIgnoreCase)==null ||
                                document.GetValue("subtotal",StringComparison.OrdinalIgnoreCase)==null ||
                                document.GetValue("canCheckout",StringComparison.OrdinalIgnoreCase)==null)
                                throw new CustomerApiException(502,"Phản hồi giỏ hàng không hợp lệ.");
                        }
                        var profile=result as CustomerProfile;
                        if(profile!=null && (String.IsNullOrEmpty(profile.Id) || String.IsNullOrEmpty(profile.UserName) || String.IsNullOrEmpty(profile.RowVersion)))
                            throw new CustomerApiException(502,"Phản hồi hồ sơ không hợp lệ.");
                        return result;
                    }
                    catch (JsonException) { throw new CustomerApiException(502,"Phản hồi từ dịch vụ không hợp lệ."); }
                }
            }
        }
        private static string Optional(string value) => String.IsNullOrWhiteSpace(value)?null:value.Trim();
    }
    public sealed class CustomerApiException:Exception
    {
        public int StatusCode {get;private set;}
        public CustomerApiException(int status,string message):base(message){StatusCode=status;}
    }
}
