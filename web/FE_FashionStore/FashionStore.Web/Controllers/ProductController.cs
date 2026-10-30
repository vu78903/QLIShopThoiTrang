using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Mvc;
using FashionStore.Web.Models;
using FashionStore.Web.Services;

namespace FashionStore.Web.Controllers
{
    public class ProductController : Controller
    {
        private const int PageSize = 12;

        [HttpGet]
        public async Task<ActionResult> Index(string q, string audience, string category, string sort, int? page, decimal? minPrice = null, decimal? maxPrice = null, string size = null, string color = null, bool inStock = false)
        {
            int pageNumber = Math.Max(1, Math.Min(page ?? 1, 100000));
            var model = EmptyModel(q, audience, category, sort, pageNumber);
            if(!ModelState.IsValid){model.CatalogError="Giá hoặc số trang không hợp lệ.";SetErrorStatus(400);return View(model);}
            try
            {
                var catalog = new CatalogRepository();
                model = await catalog.SearchAsync(q, audience, category, sort, pageNumber, PageSize, minPrice, maxPrice, size, color, inStock);
                model.Categories = await catalog.GetCategoriesAsync();
            }
            catch (CatalogApiException error)
            {
                Trace.TraceError("Catalog list API: " + error);
                SetErrorStatus(error.StatusCode == 400 ? 400 : 503);
                model.CatalogError = error.StatusCode == 400
                    ? "Bộ lọc không hợp lệ. Vui lòng kiểm tra lại tìm kiếm và lựa chọn."
                    : "Chưa thể tải danh sách sản phẩm. Vui lòng thử lại sau.";
            }
            catch (Exception error)
            {
                Trace.TraceError("Catalog list: " + error);
                SetErrorStatus(503);
                model.CatalogError = "Chưa thể tải danh sách sản phẩm. Vui lòng thử lại sau.";
            }
            return View(model);
        }

        [HttpGet]
        public async Task<ActionResult> Details(string id)
        {
            if (String.IsNullOrWhiteSpace(id) || id.Length > 20) return HttpNotFound();
            try
            {
                var model = await new CatalogRepository().GetDetailAsync(id);
                if (model.Product == null) return HttpNotFound();
                try {
                    var related = await new CatalogRepository().SearchAsync(null,null,model.Product.MaLoai,"new",1,5);
                    model.Related = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Take(System.Linq.Enumerable.Where(related.Products, p => p.MaSP != id),4));
                } catch(Exception) { /* Related content must not hide the primary product. */ }
                return View(model);
            }
            catch (Exception error)
            {
                Trace.TraceError("Catalog detail: " + error);
                SetErrorStatus(503);
                return View(new ProductDetailViewModel
                {
                    CatalogError = "Chưa thể tải sản phẩm. Vui lòng thử lại sau."
                });
            }
        }

        private void SetErrorStatus(int status)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
        }

        private static CatalogListViewModel EmptyModel(string q, string audience, string category, string sort, int page)
        {
            return new CatalogListViewModel
            {
                Query = q ?? "", Audience = audience ?? "", CategoryId = category ?? "",
                Sort = String.IsNullOrWhiteSpace(sort) ? "new" : sort, Page = page, PageSize = PageSize
            };
        }
    }
}
