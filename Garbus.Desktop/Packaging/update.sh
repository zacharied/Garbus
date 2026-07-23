#!/usr/bin/env bash
# === Garbus updater (Linux) ===
# Updates this install to the newest master build. Run: ./update.sh  (or ./update.sh --force)
set -euo pipefail

repo="zacharied/Garbus"
asset="Garbus-linux-x64.zip"
ua="User-Agent: Garbus-Updater"

force=0
[[ "${1:-}" == "--force" ]] && force=1

install_dir="$(cd "$(dirname "$0")" && pwd)"

echo "Garbus updater - checking for the newest master build..."

for tool in curl python3 unzip sha256sum; do
    command -v "$tool" >/dev/null 2>&1 || { echo "Required tool '$tool' not found." >&2; exit 1; }
done

# 1. Current commit from BUILD-INFO.txt
current_commit=""
if [[ -f "$install_dir/BUILD-INFO.txt" ]]; then
    current_commit="$(sed -n 's/^Commit:[[:space:]]*//p' "$install_dir/BUILD-INFO.txt" | head -n1)"
fi

# 2. Newest master release -> "<tag>\t<zip_url>\t<sums_url>"
info="$(curl -fsSL -H "$ua" "https://api.github.com/repos/$repo/releases?per_page=100" | python3 - "$asset" <<'PY'
import sys, json
asset = sys.argv[1]
data = json.load(sys.stdin)
masters = [r for r in data if (r.get("tag_name") or "").startswith("master-")]
masters.sort(key=lambda r: r.get("created_at", ""), reverse=True)
if not masters:
    sys.exit("No master builds found.")
r = masters[0]
def url(name):
    for a in r.get("assets", []):
        if a.get("name") == name:
            return a.get("browser_download_url") or ""
    return ""
z, s = url(asset), url("SHA256SUMS.txt")
if not z or not s:
    sys.exit("Release %s is missing required assets." % r.get("tag_name"))
print("%s\t%s\t%s" % (r.get("tag_name"), z, s))
PY
)"

tag="$(cut -f1 <<<"$info")"
zip_url="$(cut -f2 <<<"$info")"
sums_url="$(cut -f3 <<<"$info")"
short="${tag#master-}"

echo "Newest build:    $tag"
if [[ -n "$current_commit" ]]; then echo "Installed build: commit $current_commit"
else echo "Installed build: unknown (no BUILD-INFO.txt)"; fi

# 3. Up-to-date short-circuit
if [[ $force -eq 0 && -n "$current_commit" && "$current_commit" == "$short"* ]]; then
    echo "Already up to date."
    exit 0
fi

# 4. Refuse if the game is running
if pgrep -x Garbus >/dev/null 2>&1; then
    echo "Garbus is running. Close the game and run this updater again." >&2
    exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# 5-6. Download
echo "Downloading $asset..."
curl -fsSL -H "$ua" "$zip_url"  -o "$tmp/$asset"
curl -fsSL -H "$ua" "$sums_url" -o "$tmp/SHA256SUMS.txt"

# 7. Verify SHA-256
expected="$(grep -F "$asset" "$tmp/SHA256SUMS.txt" | awk '{print $1}' | head -n1)"
[[ -n "$expected" ]] || { echo "$asset not listed in SHA256SUMS.txt." >&2; exit 1; }
actual="$(sha256sum "$tmp/$asset" | awk '{print $1}')"
if [[ "$expected" != "$actual" ]]; then
    echo "Checksum mismatch for $asset (expected $expected, got $actual). Aborting; install untouched." >&2
    exit 1
fi
echo "Checksum verified."

# 8. Extract to staging
unzip -q -o "$tmp/$asset" -d "$tmp/stage"
[[ -d "$tmp/stage/Garbus" ]] || { echo "Unexpected archive layout: no top-level 'Garbus' folder." >&2; exit 1; }

# 9. Copy over install, skipping the running updater
rm -f "$tmp/stage/Garbus/update.sh"
echo "Installing update to $install_dir..."
cp -a "$tmp/stage/Garbus/." "$install_dir/"
chmod +x "$install_dir/Garbus" 2>/dev/null || true
echo "Updated to $tag."
