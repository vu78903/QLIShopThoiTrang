using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using FashionStore.Web.Helpers;
using FashionStore.Web.Models;
using FashionStore.Web.Services;

namespace FashionStore.Web.Controllers
{
    [CustomerCartAuthorize,OutputCache(NoStore=true,Duration=0,VaryByParam="*")]
    public class CartController:Controller
    {
        private readonly CustomerApiClient api=new CustomerApiClient();
        [HttpGet] public async Task<ActionResult> Count() {
            try {var cart=await api.CartAsync(CustomerAuthentication.Token(Request));return Json(new{count=cart.TotalQuantity},JsonRequestBehavior.AllowGet);}
            catch(Exception){return new HttpStatusCodeResult(503);}
        }
        [HttpGet] public async Task<ActionResult> Index()
        {
            try{return View(await api.CartAsync(CustomerAuthentication.Token(Request)));}
            catch(CustomerApiException e){if(e.StatusCode==401)return LoginAgain();Response.StatusCode=503;Response.TrySkipIisCustomErrors=true;return View(new CustomerCart{Error=e.Message});}
            catch(Exception){Response.StatusCode=503;Response.TrySkipIisCustomErrors=true;return View(new CustomerCart{Error="Chưa thể tải giỏ hàng. Vui lòng thử lại."});}
        }
        [HttpPost,AllowAnonymous,ValidateAntiForgeryToken]
        public async Task<ActionResult> Add([Bind(Prefix="selected-sku")]string sku,int quantity=1,string productId=null)
        {
            if(!User.Identity.IsAuthenticated)return RedirectToAction("Login","Account",new{returnUrl=Url.Action("Details","Product",new{id=productId})});
            if(!ModelState.IsValid){TempData["CartError"]="Số lượng không hợp lệ.";return RedirectToAction("Details","Product",new{id=productId});}
            if(String.IsNullOrWhiteSpace(sku)){TempData["CartError"]="Vui lòng chọn kích cỡ và màu sắc.";return RedirectToAction("Details","Product",new{id=productId});}
            try{await api.AddAsync(CustomerAuthentication.Token(Request),sku,quantity);TempData["CustomerMessage"]="Đã thêm sản phẩm vào giỏ hàng.";return RedirectToAction("Index");}
            catch(CustomerApiException e){if(e.StatusCode==401)return LoginAgain();TempData["CartError"]=e.Message;}
            catch(Exception){TempData["CartError"]="Chưa thể thêm vào giỏ. Vui lòng kiểm tra giỏ hàng trước khi thử lại.";}
            return RedirectToAction("Details","Product",new{id=productId});
        }
        [HttpPost,ValidateAntiForgeryToken] public Task<ActionResult> Update(string sku,int quantity){return Change(()=>api.SetAsync(CustomerAuthentication.Token(Request),sku,quantity));}
        [HttpPost,ValidateAntiForgeryToken] public Task<ActionResult> Remove(string sku){return Change(()=>api.DeleteAsync(CustomerAuthentication.Token(Request),sku));}
        [HttpPost,ValidateAntiForgeryToken] public Task<ActionResult> Clear(){return Change(()=>api.ClearAsync(CustomerAuthentication.Token(Request)));}
        private async Task<ActionResult> Change(Func<Task<CustomerCart>> action)
        {
            try{await action();}
            catch(CustomerApiException e){if(e.StatusCode==401)return LoginAgain();TempData["CartError"]=e.Message;}
            catch(Exception){TempData["CartError"]="Chưa thể cập nhật giỏ hàng. Vui lòng tải lại để kiểm tra.";}
            return RedirectToAction("Index");
        }
        private ActionResult LoginAgain(){CustomerAuthentication.SignOut();return RedirectToAction("Login","Account",new{returnUrl=Url.Action("Index")});}
    }
}
