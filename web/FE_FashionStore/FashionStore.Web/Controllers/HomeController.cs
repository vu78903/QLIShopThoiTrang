using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Mvc;
using FashionStore.Web.Models;
using FashionStore.Web.Services;

namespace FashionStore.Web.Controllers
{
    public class HomeController : Controller
    {
        public async Task<ActionResult> Index()
        {
            var model = new HomeViewModel();
            try { model.FeaturedProducts = await new CatalogRepository().GetFeaturedAsync(8); }
            catch (Exception error)
            {
                Trace.TraceError("Catalog home: " + error);
                Response.StatusCode = 503;
                Response.TrySkipIisCustomErrors = true;
                model.CatalogError = "Chưa thể tải danh sách sản phẩm. Vui lòng thử lại sau.";
            }
            return View(model);
        }

        public ActionResult Policy(string id) {
            var allowed=new[]{"shopping","shipping","payment","returns","privacy","terms","faq"};
            if(Array.IndexOf(allowed,id)<0)return HttpNotFound();ViewBag.Policy=id;return View();
        }
        public ActionResult About() { return View(); }
        public ActionResult Contact() { return View(); }
    }
}
