variable "account_name" {
  description = "Cloudflare account name owned by this configuration."
  type        = string
  default     = "Garbus"

  validation {
    condition     = length(trimspace(var.account_name)) > 0
    error_message = "account_name must not be empty."
  }
}

variable "administrator_emails" {
  description = "Cloudflare users invited as equal Garbus account administrators. Keep real addresses in an ignored tfvars file."
  type        = set(string)
  default     = []

  validation {
    condition = alltrue([
      for email in var.administrator_emails : can(regex("^[^@[:space:]]+@[^@[:space:]]+\\.[^@[:space:]]+$", email))
    ])
    error_message = "Every administrator email must be a valid email address."
  }
}

variable "chart_package_bucket_name" {
  description = "R2 bucket containing immutable chart package revisions."
  type        = string
  default     = "garbus-chart-packages"

  validation {
    condition     = can(regex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", var.chart_package_bucket_name))
    error_message = "chart_package_bucket_name must be a 3-63 character lowercase R2 bucket name."
  }
}

variable "chart_package_bucket_location" {
  description = "Best-effort R2 placement hint."
  type        = string
  default     = "enam"

  validation {
    condition     = contains(["apac", "eeur", "enam", "weur", "wnam", "oc"], var.chart_package_bucket_location)
    error_message = "chart_package_bucket_location must be a supported R2 location hint."
  }
}

variable "chart_catalog_database_name" {
  description = "D1 database containing package catalog and publication metadata."
  type        = string
  default     = "garbus-chart-catalog"

  validation {
    condition     = length(trimspace(var.chart_catalog_database_name)) > 0
    error_message = "chart_catalog_database_name must not be empty."
  }
}

variable "chart_catalog_primary_location" {
  description = "D1 primary location hint."
  type        = string
  default     = "enam"

  validation {
    condition     = contains(["wnam", "enam", "weur", "eeur", "apac", "oc"], var.chart_catalog_primary_location)
    error_message = "chart_catalog_primary_location must be a supported D1 location hint."
  }
}
