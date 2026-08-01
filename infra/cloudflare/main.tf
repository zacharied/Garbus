locals {
  # Cloudflare documents this as the global role ID for "Super Administrator - All Privileges".
  super_administrator_role_id = "33666b9c79b9a5273fc7344ff42f953d"
}

resource "cloudflare_account" "garbus" {
  name = var.account_name

  settings = {
    enforce_twofactor = true
  }

  lifecycle {
    prevent_destroy = true
  }
}

resource "cloudflare_account_member" "administrators" {
  for_each = var.administrator_emails

  account_id = cloudflare_account.garbus.id
  email      = each.value
  roles      = [local.super_administrator_role_id]
}

resource "cloudflare_r2_bucket" "chart_packages" {
  account_id    = cloudflare_account.garbus.id
  name          = var.chart_package_bucket_name
  location      = var.chart_package_bucket_location
  storage_class = "Standard"

  lifecycle {
    prevent_destroy = true
  }
}

resource "cloudflare_d1_database" "chart_catalog" {
  account_id            = cloudflare_account.garbus.id
  name                  = var.chart_catalog_database_name
  primary_location_hint = var.chart_catalog_primary_location

  lifecycle {
    prevent_destroy = true
  }
}
