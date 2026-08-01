# Garbus Cloudflare bootstrap

This OpenTofu root owns the shared Garbus Cloudflare account and the storage primitives for hosted
chart distribution:

- account-level two-factor authentication enforcement;
- maintainer invitations;
- an R2 bucket for immutable chart package revisions;
- a D1 database for catalog and publication metadata.

The Worker catalog API, database schema, package publisher, and game downloader live in follow-up
changes. This root does not create leaderboard or player-account infrastructure.

## Authenticate

The provider reads Cloudflare credentials from the environment. Prefer `CLOUDFLARE_API_TOKEN` for
routine work. Initial account creation can instead use `CLOUDFLARE_EMAIL` and
`CLOUDFLARE_API_KEY` when the operator only has a legacy Global API Key.

Never put credentials in `.tf`, `.tfvars`, plan, or committed state files.

## Create and import the account

Cloudflare may disallow account-container creation through the API for a standard user. Create the
`Garbus` account once in the Cloudflare dashboard, copy its account ID, then hand ownership to
OpenTofu:

```sh
cd infra/cloudflare
cp terraform.tfvars.example terraform.tfvars
tofu init
tofu import cloudflare_account.garbus ACCOUNT_ID
tofu fmt -check
tofu validate
tofu plan -out=bootstrap.tfplan
tofu apply bootstrap.tfplan
```

Replace the example maintainer address in the ignored `terraform.tfvars` before planning. A new
member receives a pending Cloudflare invitation and becomes a Super Administrator after accepting.
The import is required before the first plan when the dashboard created the account; otherwise the
provider attempts an API account create that Cloudflare may reject.

The root intentionally uses local state during bootstrap. Keep that state with one operator, back it
up through an encrypted channel, and never commit it. Move the state to a shared encrypted backend
before multiple maintainers begin applying changes.

## Import existing child resources

If any child resource already exists, import it instead of creating a duplicate:

```sh
tofu import cloudflare_r2_bucket.chart_packages ACCOUNT_ID/BUCKET_NAME/default
tofu import cloudflare_d1_database.chart_catalog ACCOUNT_ID/DATABASE_ID
tofu import 'cloudflare_account_member.administrators["maintainer@example.com"]' ACCOUNT_ID/MEMBER_ID
```

Review `tofu plan` after every import. The account, bucket, and database use `prevent_destroy`; an
intentional removal requires a reviewed configuration change before OpenTofu can destroy them.
