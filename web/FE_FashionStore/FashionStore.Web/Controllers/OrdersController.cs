using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using FashionStore.Web.Helpers;
using FashionStore.Web.Models;
using FashionStore.Web.Services;

namespace FashionStore.Web.Controllers
{
    [CustomerCartAuthorize, OutputCache(NoStore=true,Duration=0,VaryByParam="*")]
    public sealed class OrdersController : Controller
    {
        private readonly CustomerApiClient api = new CustomerApiClient();
        [HttpGet] public async Task<ActionResult> Index(string q=null,string status=null,int page=1)
        {
            if(page<1||page>100000)return new HttpStatusCodeResult(400);
            ViewBag.Query=q;ViewBag.Status=status;ViewBag.Page=page;
            try { return View(await api.OrdersAsync(CustomerAuthentication.Token(Request),q,status,page)); }
            catch (CustomerApiException e) { if (e.StatusCode == 401) return LoginAgain(); return Unavailable(); }
            catch (Exception) { return Unavailable(); }
        }
        [HttpGet] public async Task<ActionResult> Details(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return HttpNotFound();
            try {
                var model=await api.OrderAsync(CustomerAuthentication.Token(Request),id);
                if(model.SupportsHistory) try{ViewBag.History=await api.FeatureListAsync(CustomerAuthentication.Token(Request),"orders/"+Uri.EscapeDataString(id)+"/history");}catch(Exception){ViewBag.HistoryError="Chưa thể tải lịch sử xử lý.";}
                return View(model);
            }
            catch (CustomerApiException e)
            {
                if (e.StatusCode == 401) return LoginAgain();
                if (e.StatusCode == 404) return HttpNotFound();
                return Unavailable();
            }
            catch (Exception) { return Unavailable(); }
        }
        [HttpPost,ValidateAntiForgeryToken] public async Task<ActionResult> Cancel(string id) {
            if(String.IsNullOrWhiteSpace(id)||id.Length>30)return HttpNotFound();
            try{await api.CancelAsync(CustomerAuthentication.Token(Request),id);TempData["CustomerMessage"]="Đã hủy đơn hàng.";}
            catch(CustomerApiException e){if(e.StatusCode==401)return LoginAgain();TempData["CustomerError"]=e.Message;}
            catch(Exception){TempData["CustomerError"]="Chưa xác nhận được kết quả hủy. Vui lòng tải lại đơn hàng.";}
            return RedirectToAction("Details",new{id=id});
        }
        private ActionResult LoginAgain() { CustomerAuthentication.SignOut(); return RedirectToAction("Login","Account",new { returnUrl=Url.Action("Index") }); }
        private ActionResult Unavailable() { Response.StatusCode=503;Response.TrySkipIisCustomErrors=true;ViewBag.CustomerError="Không thể tải đơn hàng. Vui lòng thử lại sau.";return View("~/Views/Account/CustomerError.cshtml"); }
    }
}
