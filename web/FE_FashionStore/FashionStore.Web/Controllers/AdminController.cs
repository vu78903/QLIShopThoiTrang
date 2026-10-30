using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using FashionStore.Web.Helpers;
using FashionStore.Web.Models;
using FashionStore.Web.Services;

namespace FashionStore.Web.Controllers
{
    [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
    public sealed class AdminController : Controller
    {
        private readonly AdminApiClient api = new AdminApiClient();
        [HttpGet]
        public ActionResult Login() => RedirectToAction("Login", "Account");

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(AdminLoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            try
            {
                var login = await api.LoginAsync(model);
                if (!String.Equals(login.Role, "Admin", StringComparison.Ordinal) ||
                    String.IsNullOrEmpty(login.EmployeeId) || String.IsNullOrEmpty(login.FullName))
                    throw new AdminApiException(502, "Phiên quản trị không hợp lệ.");
                AdminSessionCookie.SignIn(HttpContext, login.AccessToken, login.ExpiresAtUtc);
                return RedirectToAction("Index");
            }
            catch (AdminApiException e) { ModelState.AddModelError("", e.Message); }
            catch (Exception) { ModelState.AddModelError("", "Không thể kết nối hệ thống quản trị. Vui lòng thử lại."); }
            return View(model);
        }
        [HttpGet]
        public async Task<ActionResult> Index()
        {
            var token = AdminSessionCookie.Token(Request);
            if (String.IsNullOrEmpty(token)) return RedirectToAction("Login", "Account");
            try
            {
                var identity = await api.MeAsync(token); // API verifies the current SQL permission on every request.
                if (identity == null || identity.Role != "Admin") return LogInAgain();
                var dashboard = await api.DashboardAsync(token);
                if (dashboard == null) throw new AdminApiException(502, "Dữ liệu dashboard không hợp lệ.");
                return View(new AdminHomeViewModel { Identity = identity, Dashboard = dashboard });
            }
            catch (AdminApiException e)
            {
                if (e.StatusCode == 401 || e.StatusCode == 403) return LogInAgain();
                return ErrorView();
            }
            catch (Exception) { return ErrorView(); }
        }
        [HttpGet]
        public async Task<ActionResult> Orders()
        {
            var token = AdminSessionCookie.Token(Request);
            if (String.IsNullOrEmpty(token)) return LogInAgain();
            try
            {
                await api.MeAsync(token);
                return View(await api.OrdersAsync(token));
            }
            catch (AdminApiException e) { if (e.StatusCode == 401 || e.StatusCode == 403) return LogInAgain(); return ErrorView(); }
            catch (Exception) { return ErrorView(); }
        }
        [HttpGet]
        public async Task<ActionResult> Order(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return HttpNotFound();
            var token = AdminSessionCookie.Token(Request);
            if (String.IsNullOrEmpty(token)) return LogInAgain();
            try
            {
                await api.MeAsync(token);
                return View(await api.OrderAsync(token,id));
            }
            catch (AdminApiException e)
            {
                if (e.StatusCode == 401 || e.StatusCode == 403) return LogInAgain();
                if (e.StatusCode == 404) return HttpNotFound();
                return ErrorView();
            }
            catch (Exception) { return ErrorView(); }
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeOrder(string id, string operation)
        {
            var token = AdminSessionCookie.Token(Request);
            if (String.IsNullOrEmpty(token)) return LogInAgain();
            if (String.IsNullOrWhiteSpace(id) || (operation != "confirm" && operation != "cancel")) return HttpNotFound();
            try
            {
                await api.MeAsync(token);
                var result = operation == "confirm" ? await api.ConfirmAsync(token,id) : await api.CancelAsync(token,id);
                TempData["OrderNotice"] = "Cập nhật thành công: " + result.Result;
            }
            catch (AdminApiException e)
            {
                if (e.StatusCode == 401 || e.StatusCode == 403) return LogInAgain();
                TempData["OrderError"] = e.Message;
            }
            catch (Exception) { TempData["OrderError"] = "Chưa xác định được trạng thái đơn. Vui lòng tải lại trước khi thao tác tiếp."; }
            return RedirectToAction("Order",new{id=id});
        }
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Logout()
        {
            var token = AdminSessionCookie.Token(Request);
            try { if (!String.IsNullOrEmpty(token)) await api.LogoutAsync(token); }
            catch (Exception) { /* Local logout still clears the cookie; token expires at the API. */ }
            finally { AdminSessionCookie.SignOut(HttpContext); }
            return RedirectToAction("Login", "Account");
        }
        private ActionResult LogInAgain()
        {
            AdminSessionCookie.SignOut(HttpContext);
            return RedirectToAction("Login", "Account");
        }
        private ActionResult ErrorView()
        {
            Response.StatusCode = 503;
            Response.TrySkipIisCustomErrors = true;
            return View("Unavailable");
        }
    }
}
