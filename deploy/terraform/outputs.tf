output "legacy_url" {
  value = "https://${azurerm_windows_web_app.legacy.default_hostname}"
}

output "legacy_app_name" {
  value = azurerm_windows_web_app.legacy.name
}

output "sql_server" {
  value = azurerm_mssql_server.this.fully_qualified_domain_name
}

output "sql_connection_string" {
  value     = local.sql_connection_string
  sensitive = true
}

output "facade_url" {
  value = local.modern == 1 ? "https://${azurerm_container_app.facade[0].ingress[0].fqdn}" : null
}
