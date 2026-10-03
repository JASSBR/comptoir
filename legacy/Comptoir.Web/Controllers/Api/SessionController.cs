using System.Web;
using System.Web.Http;
using System.Web.Security;

namespace Comptoir.Web.Controllers.Api
{
    // Ajoute en 2026 pour la migration : la facade traduit la session Forms en jeton pour la nouvelle application.
    // Seule modification de comportement apportee a l'application historique.
    [RoutePrefix("api/session")]
    public class SessionController : ApiController
    {
        [Route("")]
        public IHttpActionResult Get()
        {
            var identity = User.Identity as FormsIdentity;
            var parts = (identity != null ? identity.Ticket.UserData ?? "" : "").Split('|');
            return Ok(new
            {
                login = User.Identity.Name,
                role = parts.Length > 0 ? parts[0] : "",
                displayName = parts.Length > 1 ? parts[1] : User.Identity.Name
            });
        }
    }
}
