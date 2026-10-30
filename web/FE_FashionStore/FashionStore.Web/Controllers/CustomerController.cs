using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using FashionStore.Web.Helpers;
using FashionStore.Web.Services;
using Newtonsoft.Json.Linq;
namespace FashionStore.Web.Controllers
{
    [Authorize,OutputCache(NoStore=true,Duration=0,VaryByParam="*")]
    public sealed class CustomerController:Controller
    {
        private readonly CustomerApiClient api=new CustomerApiClient();
        private string Token {get{return CustomerAuthentication.Token(Request);}}
        public async Task<ActionResult> Index() {
            try{return View(await api.ProfileAsync(Token));}catch(Exception e){return Error(e);}
        }
        [AllowAnonymous] public Task<ActionResult> Promotions(){return List("Promotions","promotions",1);}
        public Task<ActionResult> Points(int page=1){return List("Points","points",page);}
        public Task<ActionResult> Wishlist(){return List("Wishlist","wishlist",1);}
        public Task<ActionResult> Addresses(){return List("Addresses","addresses",1);}
        public Task<ActionResult> Returns(int page=1){return List("Returns","returns",page);}
        [AllowAnonymous] public Task<ActionResult> Reviews(string id,int page=1){if(String.IsNullOrWhiteSpace(id)||id.Length>20)return Task.FromResult<ActionResult>(HttpNotFound());ViewBag.ProductId=id;return List("Reviews","reviews/"+Uri.EscapeDataString(id),page);}
        public async Task<ActionResult> Return(string id) {
            if(String.IsNullOrWhiteSpace(id)||id.Length>30)return HttpNotFound();
            try{ViewBag.RequestId=Guid.NewGuid();return View(await api.OrderAsync(Token,id));}catch(Exception e){return Error(e);}
        }
        [HttpPost,ValidateAntiForgeryToken] public Task<ActionResult> SaveWishlist(string id) {return Change("Wishlist",()=>api.FeatureAsync(Token,"wishlist/"+Uri.EscapeDataString(id??""),null,"PUT"));}
        [HttpPost,ValidateAntiForgeryToken] public Task<ActionResult> RemoveWishlist(string id){return Change("Wishlist",()=>api.FeatureAsync(Token,"wishlist/"+Uri.EscapeDataString(id??""),null,"DELETE"));}
        [HttpPost,ValidateAntiForgeryToken] public Task<ActionResult> SaveAddress(string recipient,string phone,string address){return Change("Addresses",()=>api.FeatureAsync(Token,"addresses",new{Recipient=recipient,Phone=phone,Address=address}));}
        [HttpPost,ValidateAntiForgeryToken] public Task<ActionResult> RemoveAddress(long id){return Change("Addresses",()=>api.FeatureAsync(Token,"addresses/"+id,null,"DELETE"));}
        [HttpPost,ValidateAntiForgeryToken] public async Task<ActionResult> Review(string productId,string orderId,int stars,string body){
            try{await api.FeatureAsync(Token,"reviews",new{ProductId=productId,OrderId=orderId,Stars=stars,Body=body});TempData["CustomerMessage"]="Đã gửi đánh giá, đang chờ cửa hàng duyệt.";}
            catch(Exception e){if(IsUnauthorized(e))return Error(e);TempData["CustomerError"]=Message(e);}
            return RedirectToAction("Reviews",new{id=productId});
        }
        [HttpPost,ValidateAntiForgeryToken] public async Task<ActionResult> SendReturn(string orderId,string sku,int quantity,string kind,string newSku,string reason,Guid requestId){
            try{await api.FeatureAsync(Token,"returns",new{OrderId=orderId,Sku=sku,Quantity=quantity,Kind=kind,NewSku=newSku,Reason=reason,RequestId=requestId});TempData["CustomerMessage"]="Đã gửi yêu cầu. Cửa hàng sẽ kiểm tra trước khi duyệt.";return RedirectToAction("Returns");}
            catch(Exception e){if(IsUnauthorized(e))return Error(e);TempData["CustomerError"]=Message(e);return RedirectToAction("Return",new{id=orderId});}
        }
        private async Task<ActionResult> List(string view,string path,int page){
            if(page<1||page>100000)return new HttpStatusCodeResult(400);
            ViewBag.Page=page;
            try{return View(view,await api.FeatureListAsync(Token,path+"?page="+page));}catch(Exception e){return Error(e);}
        }
        private async Task<ActionResult> Change(string action,Func<Task<object>> call){
            try{await call();TempData["CustomerMessage"]="Đã cập nhật.";}catch(Exception e){if(IsUnauthorized(e))return Error(e);TempData["CustomerError"]=Message(e);}return RedirectToAction(action);
        }
        private static bool IsUnauthorized(Exception e){var apiError=e as CustomerApiException;return apiError!=null&&apiError.StatusCode==401;}
        private static string Message(Exception e){return e is CustomerApiException?e.Message:"Chưa thể kết nối dịch vụ. Vui lòng thử lại.";}
        private ActionResult Error(Exception e){if(IsUnauthorized(e)){CustomerAuthentication.SignOut();return RedirectToAction("Login","Account");}Response.StatusCode=503;Response.TrySkipIisCustomErrors=true;ViewBag.CustomerError=Message(e);return View("~/Views/Account/CustomerError.cshtml");}
    }
}
