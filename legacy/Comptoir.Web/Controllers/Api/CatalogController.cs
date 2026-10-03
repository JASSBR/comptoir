using System.Linq;
using System.Web.Http;
using Comptoir.Web.Data;

namespace Comptoir.Web.Controllers.Api
{
    public class CatalogController : ApiController
    {
        [HttpGet]
        [Route("api/products")]
        public IHttpActionResult Products(string search = null, string category = null)
        {
            using (var db = new ComptoirContext())
            {
                var q = db.Products.Where(p => p.IsActive);
                if (!string.IsNullOrEmpty(search)) q = q.Where(p => p.Label.Contains(search) || p.Sku.Contains(search));
                if (!string.IsNullOrEmpty(category)) q = q.Where(p => p.Category == category);
                var list = q.OrderBy(p => p.Category).ThenBy(p => p.Label).ToList()
                    .Select(p => new
                    {
                        id = p.ProductId,
                        sku = p.Sku,
                        label = p.Label,
                        category = p.Category,
                        unitPrice = p.UnitPrice,
                        vatRate = p.VatCode == "N" ? 20m : p.VatCode == "I" ? 10m : p.VatCode == "R" ? 5.5m : 0m,
                        available = p.StockQty - p.ReservedQty
                    });
                return Ok(list);
            }
        }

        [HttpGet]
        [Route("api/customers")]
        public IHttpActionResult Customers()
        {
            using (var db = new ComptoirContext())
            {
                return Ok(db.Customers.Where(c => c.IsActive).OrderBy(c => c.Name)
                    .Select(c => new { id = c.CustomerId, code = c.Code, name = c.Name, city = c.City, tier = c.Tier }).ToList());
            }
        }
    }
}
