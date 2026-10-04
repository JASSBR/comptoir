# The new side of the migration: the facade (public, serves the Angular app) and the API (internal only).

data "azurerm_container_app_environment" "this" {
  name                = var.existing_environment.name
  resource_group_name = var.existing_environment.resource_group
}

# Shared by the facade (signs) and the API (validates): the browser never sees it.
resource "random_password" "signing_key" {
  length  = 64
  special = false
}

locals {
  modern   = var.image_tag == null ? 0 : 1
  registry = var.image_registry
}

resource "azurerm_container_app" "api" {
  count                        = local.modern
  name                         = "${var.name}-api"
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = data.azurerm_container_app_environment.this.id
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"

  secret {
    name  = "database"
    value = "${local.sql_connection_string};Max Pool Size=10"
  }

  secret {
    name  = "signing-key"
    value = random_password.signing_key.result
  }

  # Internal ingress: only the facade, in the same environment, can reach the API.
  ingress {
    external_enabled = false
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = 0
    max_replicas = 2

    container {
      name   = "api"
      image  = "${local.registry}/comptoir-api:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"
      env {
        name        = "ConnectionStrings__comptoir"
        secret_name = "database"
      }
      env {
        name        = "Facade__SigningKey"
        secret_name = "signing-key"
      }
      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
      }
    }
  }

}

resource "azurerm_container_app" "facade" {
  count                        = local.modern
  name                         = "${var.name}-facade"
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = data.azurerm_container_app_environment.this.id
  revision_mode                = "Single"
  workload_profile_name        = "Consumption"

  secret {
    name  = "signing-key"
    value = random_password.signing_key.result
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = 0
    # One replica: the shadow comparisons are kept in memory, and the dashboard reads them there.
    max_replicas = 1

    http_scale_rule {
      name                = "http"
      concurrent_requests = "50"
    }

    container {
      name   = "facade"
      image  = "${local.registry}/comptoir-facade:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"
      env {
        name  = "Legacy__BaseUrl"
        value = "https://${azurerm_windows_web_app.legacy.default_hostname}"
      }
      env {
        # Apps of one environment reach each other by name.
        name  = "Api__BaseUrl"
        value = "http://${var.name}-api"
      }
      env {
        name        = "Facade__SigningKey"
        secret_name = "signing-key"
      }
      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health"
      }
    }
  }

}
