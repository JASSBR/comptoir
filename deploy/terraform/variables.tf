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
