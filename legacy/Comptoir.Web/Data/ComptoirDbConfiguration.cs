using System;
using System.Data.Entity;
using System.Data.Entity.SqlServer;

namespace Comptoir.Web.Data
{
    // Ajoute en 2026 avec le passage sur Azure SQL : une base serverless se met en pause et repond 40613 le temps de
    // reprendre. EF6 rejoue alors la requete au lieu d'afficher une erreur. (Decouvert en production, ADR 0002.)
    public class ComptoirDbConfiguration : DbConfiguration
    {
        public ComptoirDbConfiguration()
        {
            SetExecutionStrategy("System.Data.SqlClient", () => new SqlAzureExecutionStrategy(8, TimeSpan.FromSeconds(15)));
        }
    }
}
