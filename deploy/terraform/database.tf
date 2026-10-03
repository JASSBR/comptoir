resource "random_string" "suffix" {
  length  = 6
  special = false
  upper   = false
}

resource "random_password" "sql" {
  length           = 32
  special          = true
  override_special = "-_"
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${var.name}"
  location = var.location
}

resource "azurerm_mssql_server" "this" {
  name                          = "${var.name}-sql-${random_string.suffix.result}"
  resource_group_name           = azurerm_resource_group.this.name
  location                      = azurerm_resource_group.this.location
  version                       = "12.0"
  administrator_login           = "comptoiradmin"
  administrator_login_password  = random_password.sql.result
  minimum_tls_version           = "1.2"
  public_network_access_enabled = true
}

resource "azurerm_mssql_firewall_rule" "azure_services" {
  # 0.0.0.0 is Azure's convention for "Azure services only".
  name             = "allow-azure-services"
  server_id        = azurerm_mssql_server.this.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_mssql_firewall_rule" "operator" {
  count            = var.operator_ip == null ? 0 : 1
  name             = "operator"
  server_id        = azurerm_mssql_server.this.id
  start_ip_address = var.operator_ip
  end_ip_address   = var.operator_ip
}

# Azure SQL free offer (serverless, 100 000 vCore-seconds a month): declared through the ARM API, which exposes
# useFreeLimit; it pauses instead of billing when the monthly allowance is spent.
resource "azapi_resource" "database" {
  type      = "Microsoft.Sql/servers/databases@2023-08-01"
  name      = "Comptoir"
  parent_id = azurerm_mssql_server.this.id
  location  = azurerm_resource_group.this.location
  body = {
    sku = { name = "GP_S_Gen5_2", tier = "GeneralPurpose", family = "Gen5", capacity = 2 }
    properties = {
      useFreeLimit                = true
      freeLimitExhaustionBehavior = "AutoPause"
      autoPauseDelay              = 60
      minCapacity                 = 0.5
      maxSizeBytes                = 34359738368
      zoneRedundant               = false
    }
  }
}

locals {
  sql_connection_string = "Server=tcp:${azurerm_mssql_server.this.fully_qualified_domain_name},1433;Database=Comptoir;User ID=${azurerm_mssql_server.this.administrator_login};Password=${random_password.sql.result};Encrypt=True;Connect Timeout=60"
}
