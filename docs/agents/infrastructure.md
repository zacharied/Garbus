# Infrastructure

## Purpose and scope

Garbus uses Cloudflare for hosted chart distribution. OpenTofu under `infra/cloudflare/` owns the
shared Cloudflare account, maintainer membership, chart-package storage, and chart-catalog database.
The game remains fully playable offline after a package is installed.

Leaderboard, player identity, public chart submission, and score verification are outside this
infrastructure domain.

## Resource model

- **Cloudflare account:** named `Garbus`, with two-factor authentication required for every member.
- **R2 bucket:** stores complete chart packages. Objects are immutable revisions addressed by their
  SHA-256 digest, for example `packages/{packageId}/{sha256}.garbuspack`.
- **D1 database:** stores searchable package metadata, revision pointers, publication state, and
  rights declarations. Binary chart content does not belong in D1.
- **Account members:** each maintainer uses an individual Cloudflare login. Shared passwords and
  Global API Keys are not the collaboration model.

The R2 bucket and D1 database are protected with OpenTofu `prevent_destroy` lifecycle rules.

## Package and catalog invariants

- A package contains the `.garbus` song file plus every referenced audio and artwork asset.
- `SongId` and `ChartId` provide stable author-facing identity; the archive SHA-256 identifies an
  exact immutable revision.
- Publishing a revision creates a new R2 object. Existing revision objects are never overwritten.
- The catalog returns the download URL and expected SHA-256. Clients do not construct storage keys.
- The client downloads to temporary storage, verifies the digest and archive paths, extracts the
  complete song folder, installs it atomically under the local `charts/` library, then rescans Song
  Select.
- Gameplay reads installed local assets. Audio and charts are not streamed from Cloudflare.

Publishing is maintainer-only. Catalog records carry a publication state and rights declaration;
public uploads require a separate moderation design.

## OpenTofu operation

Provider credentials come from Cloudflare environment variables and never from committed files.
`terraform.tfvars` and all local state artifacts are Git-ignored. The committed example contains no
real member addresses or credentials.

The Cloudflare account container is created once in the dashboard when the account-creation API is
unavailable, then imported as `cloudflare_account.garbus`; every subsequent setting and child
resource remains OpenTofu-managed. Bootstrap state is local and single-operator. Before multiple
maintainers apply infrastructure, move state to a shared encrypted backend with locking. CI performs
formatting, provider initialization, and configuration validation without Cloudflare credentials;
plans and applies are deliberate operator actions.

See [`infra/cloudflare/README.md`](../../infra/cloudflare/README.md) for commands and import syntax.

## Gotchas

- A logical `SongId` or `ChartId` does not prove exact content identity; always retain the package
  SHA-256 with catalog and installation records.
- Never overwrite an R2 revision object. Mutable URLs make cache contents and installed files
  ambiguous.
- Validate compressed size, extracted size, file count, file types, relative paths, and symlinks
  before publishing or installing a package.
- Removing an object is not enough for moderation: retain catalog state and the rights/takedown
  record after a revision becomes unavailable.
