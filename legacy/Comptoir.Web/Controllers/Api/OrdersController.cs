using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web.Http;
using Comptoir.Web.Data;
using Comptoir.Web.Models;

namespace Comptoir.Web.Controllers.Api
{
    public class OrderLineInput
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderInput
    {
        public int CustomerId { get; set; }
        public string Comment { get; set; }
        public List<OrderLineInput> Lines { get; set; }
    }

    [RoutePrefix("api/orders")]
    public class OrdersController : ApiController
    {
        [Route("")]
        public IHttpActionResult Get(int? status = null)
        {
            using (var db = new ComptoirContext())
            {
                var q = from o in db.Orders
                        join c in db.Customers on o.CustomerId equals c.CustomerId
                        select new { o, c };
                if (status.HasValue) q = q.Where(x => x.o.Status == status.Value);
                var list = q.OrderByDescending(x => x.o.OrderId).Take(200).ToList()
                    .Select(x => new
                    {
                        id = x.o.OrderId,
                        number = x.o.OrderNumber,
                        customer = x.c.Name,
                        status = x.o.Status,
                        statusLabel = StatusLabel(x.o.Status),
                        createdOn = x.o.CreatedOn,
                        totalTTC = x.o.TotalTTC
                    });
                return Ok(list);
            }
        }

        [Route("{id:int}")]
        public IHttpActionResult Get(int id)
        {
            using (var db = new ComptoirContext())
            {
                var o = db.Orders.Include("Lines").FirstOrDefault(x => x.OrderId == id);
                if (o == null) return NotFound();
                var customer = db.Customers.Find(o.CustomerId);
                var productIds = o.Lines.Select(l => l.ProductId).ToList();
                var products = db.Products.Where(p => productIds.Contains(p.ProductId)).ToDictionary(p => p.ProductId);
                var invoice = db.Invoices.FirstOrDefault(i => i.OrderId == id);
                return Ok(new
                {
                    id = o.OrderId,
                    number = o.OrderNumber,
                    customer = new { id = customer.CustomerId, name = customer.Name, tier = customer.Tier },
                    status = o.Status,
                    statusLabel = StatusLabel(o.Status),
                    createdOn = o.CreatedOn,
                    createdBy = o.CreatedBy,
                    comment = o.Comment,
                    lines = o.Lines.OrderBy(l => l.OrderLineId).Select(l => new
                    {
                        productId = l.ProductId,
                        sku = products[l.ProductId].Sku,
                        label = products[l.ProductId].Label,
                        quantity = l.Quantity,
                        unitPrice = l.UnitPrice,
                        discountPct = l.DiscountPct,
                        lineHT = l.LineHT,
                        lineVAT = l.LineVAT
                    }),
                    totalHT = o.TotalHT,
                    shippingHT = o.ShippingHT,
                    totalVAT = o.TotalVAT,
                    totalTTC = o.TotalTTC,
                    invoiceNumber = invoice != null ? invoice.InvoiceNumber : null
                });
            }
        }

        [HttpPost]
        [Route("")]
        public IHttpActionResult Create(OrderInput input)
        {
            if (input == null || input.Lines == null || input.Lines.Count == 0) return BadRequest("Commande vide.");
            if (input.Lines.Any(l => l.Quantity <= 0)) return BadRequest("Quantite invalide.");
            using (var db = new ComptoirContext())
            {
                if (db.Customers.Find(input.CustomerId) == null) return BadRequest("Client inconnu.");
                var order = new Order { CustomerId = input.CustomerId, Comment = input.Comment, CreatedBy = User.Identity.Name, CreatedOn = DateTime.Now };
                foreach (var l in input.Lines) order.Lines.Add(new OrderLine { ProductId = l.ProductId, Quantity = l.Quantity });
                db.Orders.Add(order);
                db.SaveChanges();
                db.Database.ExecuteSqlCommand("EXEC dbo.usp_PriceOrder @p0", order.OrderId);
                return Created("api/orders/" + order.OrderId, new { id = order.OrderId });
            }
        }

        [HttpPut]
        [Route("{id:int}/lines")]
        public IHttpActionResult ReplaceLines(int id, List<OrderLineInput> lines)
        {
            if (lines == null || lines.Count == 0) return BadRequest("Commande vide.");
            using (var db = new ComptoirContext())
            {
                var order = db.Orders.Include("Lines").FirstOrDefault(o => o.OrderId == id);
                if (order == null) return NotFound();
                if (order.Status != OrderStatus.Brouillon) return Content(HttpStatusCode.Conflict, "Seul un brouillon est modifiable.");
                db.OrderLines.RemoveRange(order.Lines.ToList());
                foreach (var l in lines) db.OrderLines.Add(new OrderLine { OrderId = id, ProductId = l.ProductId, Quantity = l.Quantity });
                db.SaveChanges();
                db.Database.ExecuteSqlCommand("EXEC dbo.usp_PriceOrder @p0", id);
                return Ok();
            }
        }

        [HttpPost]
        [Route("{id:int}/{transition:regex(^(confirm|ship|invoice|cancel)$)}")]
        public IHttpActionResult Transition(int id, string transition)
        {
            var procedure = transition == "confirm" ? "usp_ConfirmOrder"
                : transition == "ship" ? "usp_ShipOrder"
                : transition == "invoice" ? "usp_InvoiceOrder"
                : "usp_CancelOrder";
            using (var db = new ComptoirContext())
            {
                try
                {
                    // Les procedures gerent leur propre transaction : EF ne doit pas en ouvrir une autour (sinon
                    // "Transaction count after EXECUTE..." s'ajoute au message metier).
                    db.Database.ExecuteSqlCommand(TransactionalBehavior.DoNotEnsureTransaction, "EXEC dbo." + procedure + " @p0", id);
                }
                catch (SqlException ex) when (ex.Number == 50000)
                {
                    // Les procedures remontent des messages metier : on les renvoie tels quels a l'ecran.
                    return Content(HttpStatusCode.Conflict, ex.Message);
                }
                return Ok();
            }
        }

        private static string StatusLabel(byte status)
        {
            switch (status)
            {
                case OrderStatus.Brouillon: return "Brouillon";
                case OrderStatus.Confirmee: return "Confirmée";
                case OrderStatus.Expediee: return "Expédiée";
                case OrderStatus.Facturee: return "Facturée";
                case OrderStatus.Annulee: return "Annulée";
                default: return "?";
            }
        }
    }
}
