using System;
using System.Web;
using System.Web.Security;
using FashionStore.Web.Models;
using System.Web.Mvc;
using System.Web.Routing;

namespace FashionStore.Web.Helpers
{
    public sealed class CustomerCartAuthorizeAttribute : AuthorizeAttribute
    {
        protected override void HandleUnauthorizedRequest(AuthorizationContext context)
        {
            var urls=new UrlHelper(context.RequestContext);
            context.Result=new RedirectToRouteResult(new RouteValueDictionary(new {
                controller="Account",action="Login",returnUrl=urls.Action("Index","Cart") }));
        }
    }
    public static class CustomerAuthentication
    {
        public static string Token(HttpRequestBase request)
        {
            var cookie=request.Cookies[FormsAuthentication.FormsCookieName];
            if(cookie==null || String.IsNullOrEmpty(cookie.Value)) return null;
            try { var ticket=FormsAuthentication.Decrypt(cookie.Value); return ticket==null || ticket.Expired?null:ticket.UserData; }
            catch(System.Security.Cryptography.CryptographicException){return null;}
            catch(ArgumentException){return null;}
        }
        public static void SignIn(HttpContextBase context,string userName,CustomerLoginResult login)
        {
            if(String.IsNullOrEmpty(login.AccessToken) || login.ExpiresAtUtc<=DateTimeOffset.UtcNow)
                throw new InvalidOperationException("Invalid login response.");
            var expiry=login.ExpiresAtUtc.LocalDateTime;
            var ticket=new FormsAuthenticationTicket(2,userName,DateTime.Now,expiry,false,login.AccessToken,FormsAuthentication.FormsCookiePath);
            context.Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName,FormsAuthentication.Encrypt(ticket))
            {HttpOnly=true,Secure=context.Request.IsSecureConnection,SameSite=SameSiteMode.Lax,Path=FormsAuthentication.FormsCookiePath});
        }
        public static void UpdateDisplayName(HttpContextBase context, string fullName)
        {
            if (String.IsNullOrWhiteSpace(fullName)) return;
            var existing = context.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (existing == null || String.IsNullOrWhiteSpace(existing.Value)) return;
            FormsAuthenticationTicket ticket;
            try { ticket = FormsAuthentication.Decrypt(existing.Value); }
            catch (System.Security.Cryptography.CryptographicException) { return; }
            catch (ArgumentException) { return; }
            if (ticket == null || ticket.Expired) return;
            var updated = new FormsAuthenticationTicket(ticket.Version, fullName.Trim(),
                ticket.IssueDate, ticket.Expiration, ticket.IsPersistent, ticket.UserData, ticket.CookiePath);
            context.Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(updated))
            { HttpOnly = true, Secure = context.Request.IsSecureConnection, SameSite = SameSiteMode.Lax, Path = ticket.CookiePath });
        }
        public static void SignOut(){FormsAuthentication.SignOut();}
    }
}
