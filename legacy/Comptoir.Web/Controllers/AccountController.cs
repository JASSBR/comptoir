using System;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Security;
using Comptoir.Web.Data;

namespace Comptoir.Web.Controllers
{
    public class AccountController : Controller
    {
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string login, string password, string returnUrl)
        {
            using (var db = new ComptoirContext())
            {
                var user = db.Users.FirstOrDefault(u => u.Login == login);
                if (user == null || !Crypto.VerifyHashedPassword(user.PasswordHash, password ?? ""))
                {
                    ViewBag.Error = "Identifiant ou mot de passe incorrect.";
                    ViewBag.ReturnUrl = returnUrl;
                    return View();
                }

                var ticket = new FormsAuthenticationTicket(1, user.Login, DateTime.Now, DateTime.Now.AddHours(8), false,
                    user.Role + "|" + user.DisplayName);
                var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
                {
                    HttpOnly = true,
                    Secure = FormsAuthentication.RequireSSL,
                    Path = FormsAuthentication.FormsCookiePath
                };
                Response.Cookies.Add(cookie);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login");
        }
    }
}
