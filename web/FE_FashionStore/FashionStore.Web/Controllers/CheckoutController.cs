using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using FashionStore.Web.Helpers;
using FashionStore.Web.Models;
using FashionStore.Web.Services;

namespace FashionStore.Web.Controllers
{
    [CustomerCartAuthorize, OutputCache(NoStore=true,Duration=0,VaryByParam="*")]
    public sealed class CheckoutController : Controller
    {
        private readonly CustomerApiClient api = new CustomerApiClient();
        [HttpGet]
        public async Task<ActionResult> Index(long? addressId=null)
        {
            var token = CustomerAuthentication.Token(Request);
            if (String.IsNullOrEmpty(token)) return LoginAgain();
            try
            {
                var cart = await api.CartAsync(token);
                if (cart.Items.Count == 0 || !cart.CanCheckout || !cart.CheckoutEnabled) return RedirectToAction("Index", "Cart");
                var customer = await api.ProfileAsync(token);
                if(addressId.HasValue && !cart.CodOnly) {
                    var addresses=await api.FeatureListAsync(token,"addresses");
                    foreach(var address in addresses) if((long)address["Id"]==addressId.Value) {
                        customer.FullName=(string)address["Recipient"];customer.Phone=(string)address["Phone"];customer.Address=(string)address["Address"];break;
                    }
                }
                return View(new CheckoutViewModel {
                    Recipient = customer.FullName, Phone = customer.Phone, Address = customer.Address,
                    PaymentMethod = "COD", RequestId = Guid.NewGuid(), Cart = cart
                });
            }
            catch (CustomerApiException e) { if (e.StatusCode == 401) return LoginAgain(); return Unavailable(); }
            catch (Exception) { return Unavailable(); }
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(CheckoutViewModel model,string intent=null)
        {
            var token = CustomerAuthentication.Token(Request);
            if (String.IsNullOrEmpty(token)) return LoginAgain();
            if (model.RequestId == Guid.Empty) ModelState.AddModelError("", "Mã yêu cầu không hợp lệ. Vui lòng tải lại trang thanh toán.");
            if(intent=="quote") {
                try {model.Cart=await api.CartAsync(token);var quote=await api.QuoteAsync(token,model.Voucher);model.QuotedDiscount=(decimal)quote["discount"];model.QuotedTotal=(decimal)quote["total"];}
                catch(CustomerApiException e){if(e.StatusCode==401)return LoginAgain();ModelState.AddModelError("",e.Message);if(model.Cart==null)return Unavailable();}
                catch(Exception){return Unavailable();}
                return View(model);
            }
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await api.PlaceOrderAsync(token, model);
                    if (result.Status != "CAN_HO_TRO") TempData["OrderSuccess"]=result.OrderId;
                    else TempData["CustomerError"]="Đơn hàng cần cửa hàng kiểm tra. Yêu cầu này không tạo thêm đơn mới.";
                    return RedirectToAction("Details", "Orders", new { id = result.OrderId });
                }
                catch (CustomerApiException e)
                {
                    if (e.StatusCode == 401) return LoginAgain();
                    ModelState.AddModelError("", e.Message);
                }
                catch (Exception) { ModelState.AddModelError("", "Không rõ đơn hàng đã được tạo chưa. Vui lòng kiểm tra lịch sử trước khi thử lại."); }
            }
            try { model.Cart = await api.CartAsync(token); }
            catch (CustomerApiException e) { if (e.StatusCode == 401) return LoginAgain(); return Unavailable(); }
            catch (Exception) { return Unavailable(); }
            if (model.Cart.Items.Count == 0) return RedirectToAction("Index", "Orders");
            return View(model);
        }
        private ActionResult LoginAgain() { CustomerAuthentication.SignOut(); return RedirectToAction("Login","Account",new { returnUrl=Url.Action("Index") }); }
        private ActionResult Unavailable() { Response.StatusCode = 503; Response.TrySkipIisCustomErrors = true; ViewBag.CustomerError="Không thể tải dữ liệu thanh toán. Vui lòng thử lại sau."; return View("~/Views/Account/CustomerError.cshtml"); }
    }
}
