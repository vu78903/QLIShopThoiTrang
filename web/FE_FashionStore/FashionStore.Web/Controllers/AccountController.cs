using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using FashionStore.Web.Helpers;
using FashionStore.Web.Models;
using FashionStore.Web.Services;

namespace FashionStore.Web.Controllers
{
    [OutputCache(NoStore=true,Duration=0,VaryByParam="*")]
    public class AccountController:Controller
    {
        private readonly CustomerApiClient api = new CustomerApiClient();
        private readonly AdminApiClient adminApi = new AdminApiClient();

        // One public login form for Customer and Admin; authentication and sessions stay separate.
        [HttpGet]
        public ActionResult Login(string returnUrl)
        {
            if (!String.IsNullOrEmpty(AdminSessionCookie.Token(Request)))
                return RedirectToAction("Index", "Admin");
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            try
            {
                // Customer uses the existing Forms Authentication ticket and Customer API.
                var login = await api.LoginAsync(model);
                // Dùng họ tên từ hồ sơ đã xác thực để hiển thị trên header, không dùng tên đăng nhập.
                var profile = await api.ProfileAsync(login.AccessToken);
                CustomerAuthentication.SignIn(HttpContext, profile.FullName, login);
                // Prevent a browser from retaining both kinds of active cookies.
                AdminSessionCookie.SignOut(HttpContext);
                if (Url.IsLocalUrl(model.ReturnUrl) &&
                    !model.ReturnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) &&
                    !model.ReturnUrl.StartsWith("~/Admin", StringComparison.OrdinalIgnoreCase))
                    return Redirect(model.ReturnUrl);
                return RedirectToAction("Index","Customer");
            }
            catch (CustomerApiException e) when (e.StatusCode == 401)
            {
                // Only an explicit invalid-credentials result may fall back to Admin.
                // Never fall back for 429 / 500 / 503: those are not invalid credentials.
                // Employee IDs are limited to 20 characters by AdminLoginViewModel.
                var employeeId = model.UserName?.Trim();
                if (String.IsNullOrEmpty(employeeId) || employeeId.Length > 20)
                {
                    ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                    return View(model);
                }
                try
                {
                    var adminLogin = await adminApi.LoginAsync(new AdminLoginViewModel
                    {
                        EmployeeId = employeeId,
                        Password = model.Password
                    });
                    // The signed Admin session is issued ONLY after API verification.
                    if (adminLogin == null || adminLogin.Role != "Admin" ||
                        String.IsNullOrWhiteSpace(adminLogin.EmployeeId) ||
                        String.IsNullOrWhiteSpace(adminLogin.FullName))
                        throw new AdminApiException(502, "Phản hồi phiên quản trị không hợp lệ.");

                    AdminSessionCookie.SignIn(HttpContext, adminLogin.AccessToken, adminLogin.ExpiresAtUtc);
                    CustomerAuthentication.SignOut();
                    return RedirectToAction("Index", "Admin");
                }
                catch (AdminApiException adminError) when (
                    adminError.StatusCode == 400 || adminError.StatusCode == 401 || adminError.StatusCode == 403)
                {
                    // Deliberately do not reveal whether a Customer or Admin ID exists.
                    ModelState.AddModelError("", "Tên đăng nhập hoặc mật khẩu không đúng.");
                }
                catch (AdminApiException)
                {
                    ModelState.AddModelError("", "Dịch vụ đăng nhập hiện chưa sẵn sàng. Vui lòng thử lại.");
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Chưa thể kết nối hệ thống quản trị. Vui lòng thử lại.");
                }
            }
            catch (CustomerApiException e)
            {
                ModelState.AddModelError("", e.Message);
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "Chưa thể đăng nhập. Vui lòng thử lại.");
            }
            return View(model);
        }
        [HttpGet] public ActionResult Forgot(){return View();}
        [HttpPost,ValidateAntiForgeryToken] public async Task<ActionResult> Forgot(string email){
            if(String.IsNullOrWhiteSpace(email)||email.Length>254||!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)){ModelState.AddModelError("","Email không hợp lệ.");return View();}
            try{await api.RecoveryAsync(email);ViewBag.Message="Nếu email phù hợp với tài khoản đang hoạt động, yêu cầu khôi phục được tiếp nhận. Hãy kiểm tra hộp thư và thư rác; nếu chưa nhận được, liên hệ cửa hàng.";}
            catch(CustomerApiException e){ModelState.AddModelError("",e.Message);}catch(Exception){ModelState.AddModelError("","Dịch vụ khôi phục chưa sẵn sàng.");}return View();
        }
        [HttpGet] public ActionResult Reset(string token){Response.Headers["Referrer-Policy"]="no-referrer";ViewBag.Token=token;return View();}
        [HttpPost,ValidateAntiForgeryToken] public async Task<ActionResult> Reset(string token,string password,string confirmPassword){
            Response.Headers["Referrer-Policy"]="no-referrer";ViewBag.Token=token;
            if(String.IsNullOrEmpty(password)||password.Length<12||password.Length>128||password!=confirmPassword){ModelState.AddModelError("","Mật khẩu phải có 12–128 ký tự và nhập lại trùng khớp.");return View();}
            try{await api.ResetAsync(token,password);CustomerAuthentication.SignOut();TempData["AccountMessage"]="Đã đặt lại mật khẩu. Vui lòng đăng nhập.";return RedirectToAction("Login");}
            catch(CustomerApiException e){ModelState.AddModelError("",e.Message);}catch(Exception){ModelState.AddModelError("","Chưa thể đặt lại mật khẩu.");}return View();
        }
        [HttpGet]
        public async Task<ActionResult> Provinces()
        {
            try { return Json(await VietnamLocationsClient.ProvincesAsync(), JsonRequestBehavior.AllowGet); }
            catch (Exception) { Response.StatusCode = 503; return Json(new { error = "Chưa tải được tỉnh/thành phố. Vui lòng thử lại." }, JsonRequestBehavior.AllowGet); }
        }

        [HttpGet]
        public async Task<ActionResult> Wards(int provinceCode)
        {
            try { return Json(await VietnamLocationsClient.WardsAsync(provinceCode), JsonRequestBehavior.AllowGet); }
            catch (Exception) { Response.StatusCode = 503; return Json(new { error = "Chưa tải được phường/xã. Vui lòng thử lại." }, JsonRequestBehavior.AllowGet); }
        }

        [HttpGet] public ActionResult Register(){return View(new RegisterViewModel());}
        [HttpPost,ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if(!ModelState.IsValid)return View(model);
            try
            {
                model.Address = await VietnamLocationsClient.ValidatedAddressAsync(
                    model.StreetAddress, model.ProvinceCode.Value, model.WardCode.Value);
                if (String.IsNullOrWhiteSpace(model.Address))
                {
                    ModelState.AddModelError("WardCode", "Địa chỉ không hợp lệ hoặc vượt quá 300 ký tự. Vui lòng chọn lại.");
                    return View(model);
                }
                model.FullName = model.FullName.Trim();
                model.Phone = model.Phone.Trim();
                model.Email = model.Email.Trim();
                model.UserName = model.UserName.Trim();
                await api.RegisterAsync(model);
                TempData["AccountMessage"] = "Đăng ký thành công. Bạn có thể đăng nhập.";
                return RedirectToAction("Login");
            }
            catch(CustomerApiException e){ModelState.AddModelError("",e.Message);}
            catch(HttpRequestException){ModelState.AddModelError("", "Không tải được dữ liệu địa chỉ. Vui lòng thử lại khi có kết nối mạng.");}
            catch(Exception){ModelState.AddModelError("", "Chưa thể đăng ký. Vui lòng thử lại.");}
            return View(model);
        }
        [HttpGet,Authorize,ActionName("Profile")] public async Task<ActionResult> EditProfile()
        {
            try{return View("Profile",await api.ProfileAsync(CustomerAuthentication.Token(Request)));}
            catch(CustomerApiException e){if(e.StatusCode==401)return LoginAgain();return AccountError(e.Message);}
            catch(Exception){return AccountError("Chưa thể tải hồ sơ. Vui lòng thử lại.");}
        }
        [HttpPost,Authorize,ValidateAntiForgeryToken,ActionName("Profile")]
        public async Task<ActionResult> SaveProfile(CustomerProfile model)
        {
            if(ModelState.IsValid){
                try
                {
                    var updated = await api.UpdateAsync(CustomerAuthentication.Token(Request),model);
                    CustomerAuthentication.UpdateDisplayName(HttpContext, updated.FullName);
                    TempData["AccountMessage"]="Đã lưu hồ sơ.";
                    return RedirectToAction("Profile");
                }
                catch(CustomerApiException e){if(e.StatusCode==401)return LoginAgain();ModelState.AddModelError("",e.Message);}
                catch(Exception){ModelState.AddModelError("","Chưa thể lưu hồ sơ. Vui lòng thử lại.");}
            }
            // Display-only fields always come from the API, never the submitted form.
            try{var current=await api.ProfileAsync(CustomerAuthentication.Token(Request));model.UserName=current.UserName;model.Points=current.Points;model.Membership=current.Membership;}
            catch(Exception){model.UserName=User.Identity.Name;model.Membership=null;model.Points=0;}
            return View("Profile",model);
        }
        [HttpGet,Authorize] public ActionResult Password(){return View(new ChangePasswordViewModel());}
        [HttpPost,Authorize,ValidateAntiForgeryToken]
        public async Task<ActionResult> Password(ChangePasswordViewModel model)
        {
            if(!ModelState.IsValid)return View(model);
            try{await api.PasswordAsync(CustomerAuthentication.Token(Request),model);CustomerAuthentication.SignOut();TempData["AccountMessage"]="Đã đổi mật khẩu. Vui lòng đăng nhập lại.";return RedirectToAction("Login");}
            catch(CustomerApiException e){if(e.StatusCode==401)return LoginAgain();ModelState.AddModelError("",e.Message);}
            catch(Exception){ModelState.AddModelError("","Chưa thể đổi mật khẩu. Vui lòng thử lại.");}
            return View(model);
        }
        [HttpPost,ValidateAntiForgeryToken] public async Task<ActionResult> Logout()
        {
            var token=CustomerAuthentication.Token(Request);
            try{if(!String.IsNullOrEmpty(token))await api.LogoutAsync(token);}
            catch(Exception){/* Local sign-out still removes the protected cookie; API session expires within one hour. */}
            finally{CustomerAuthentication.SignOut();}
            return RedirectToAction("Login");
        }
        private ActionResult LoginAgain(){CustomerAuthentication.SignOut();return RedirectToAction("Login",new{returnUrl=Url.Action("Profile")});}
        private ActionResult AccountError(string message){Response.StatusCode=503;Response.TrySkipIisCustomErrors=true;ViewBag.CustomerError=message;return View("CustomerError");}
    }
}
