output "account_id" {
  description = "Garbus Cloudflare account identifier."
  value       = cloudflare_account.garbus.id
}

output "chart_package_bucket_name" {
  description = "R2 bucket used for immutable chart packages."
  value       = cloudflare_r2_bucket.chart_packages.name
}

output "chart_catalog_database_id" {
  description = "D1 database identifier used by the chart catalog API."
  value       = cloudflare_d1_database.chart_catalog.id
}
