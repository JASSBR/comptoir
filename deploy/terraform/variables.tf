variable "subscription_id" {
  type = string
}

variable "location" {
  type        = string
  default     = "italynorth"
  description = "Azure region (Azure for Students only allows a few)."
}

variable "name" {
  type    = string
  default = "comptoir"
}

variable "operator_ip" {
  type        = string
  default     = null
  description = "Public IP allowed to reach Azure SQL (to run the DbMigrator from a workstation). Null: none."
}

variable "image_registry" {
  type        = string
  default     = "ghcr.io/jassbr"
  description = "Public registry holding comptoir-api and comptoir-facade, built by .github/workflows/images.yml."
}

variable "existing_environment" {
  type = object({
    name           = string
    resource_group = string
  })
  description = "Container Apps environment (WorkloadProfiles mode) to run the facade and the API in. Azure for Students allows one per region."
}

variable "image_tag" {
  type        = string
  default     = null
  description = "Tag of the images to run. Null: the new side is not deployed (legacy only)."
}
