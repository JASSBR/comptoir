# The 2014 application, as it would have been hosted: IIS on Windows, .NET Framework 4.8 (App Service F1, free).
resource "azurerm_service_plan" "legacy" {
  name                = "${var.name}-legacy-plan"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  os_type             = "Windows"
  sku_name            = "F1"
}

resource "azurerm_windows_web_app" "legacy" {
  name                = "${var.name}-legacy-${random_string.suffix.result}"
  resource_group_name = azurerm_resource_group.this.name
  location            = azurerm_resource_group.this.location
  service_plan_id     = azurerm_service_plan.legacy.id
  https_only          = true

  site_config {
    # F1 limits: no always-on, 32-bit worker.
    always_on         = false
    use_32_bit_worker = true
    ftps_state        = "Disabled"
    application_stack {
      current_stack  = "dotnet"
      dotnet_version = "v4.0"
    }
  }

  # Overrides <connectionStrings><add name="Comptoir"> of web.config at runtime.
  connection_string {
    name  = "Comptoir"
    type  = "SQLAzure"
    value = local.sql_connection_string
  }
}
