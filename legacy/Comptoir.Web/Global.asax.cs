using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;

namespace Comptoir.Web
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            GlobalFilters.Filters.Add(new HandleErrorAttribute());
        }

        // Le role est stocke dans le ticket Forms (UserData) pour eviter une requete par appel.
        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var cookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (cookie == null) return;
            FormsAuthenticationTicket ticket;
            try { ticket = FormsAuthentication.Decrypt(cookie.Value); }
            catch { return; }
            if (ticket == null || ticket.Expired) return;
            var parts = (ticket.UserData ?? "").Split('|');
            var role = parts.Length > 0 ? parts[0] : "";
            Context.User = new GenericPrincipal(new FormsIdentity(ticket), new[] { role });
        }
    }
}
