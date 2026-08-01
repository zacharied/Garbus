#!/usr/bin/env bash
# === Garbus updater (Linux/macOS) ===
# Updates this install to the newest master build. Run: ./update.sh  (or ./update.sh --force)
set -euo pipefail

repo="zacharied/Garbus"
ua="User-Agent: Garbus-Updater"

force=0
[[ "${1:-}" == "--force" ]] && force=1

install_dir="$(cd "$(dirname "$0")" && pwd -P)"
install_parent="$(dirname "$install_dir")"
install_name="$(basename "$install_dir")"

platform="$(uname -s)"
architecture="$(uname -m)"

case "$platform" in
    Linux)
        runtime="linux-x64"
        asset="Garbus-linux-x64.zip"
        ;;
    Darwin)
        if [[ "$architecture" != "arm64" ]]; then
            echo "Unsupported macOS architecture '$architecture'; Garbus releases target Apple Silicon." >&2
            exit 1
        fi

        runtime="osx-arm64"
        asset="Garbus-osx-arm64.zip"
        ;;
    *)
        echo "Unsupported platform '$platform'." >&2
        exit 1
        ;;
esac

required_tools=(curl python3)
if [[ "$platform" == "Linux" ]]; then
    required_tools+=(unzip sha256sum cp)
else
    required_tools+=(ditto shasum codesign)
fi

for tool in "${required_tools[@]}"; do
    command -v "$tool" >/dev/null 2>&1 || { echo "Required tool '$tool' not found." >&2; exit 1; }
done

tmp="$(mktemp -d)"
staged_install=""
new_updater=""
backup_dir=""
preserve_backup=0

cleanup()
{
    rm -rf "$tmp"
    if [[ -n "$staged_install" && -d "$staged_install" ]]; then
        rm -rf "$staged_install"
    fi
    if [[ -n "$new_updater" && -f "$new_updater" ]]; then
        rm -f "$new_updater"
    fi
    if [[ $preserve_backup -eq 0 && -n "$backup_dir" && -d "$backup_dir" ]]; then
        rm -rf "$backup_dir"
    fi
}
trap cleanup EXIT

sha256_of()
{
    if [[ "$platform" == "Linux" ]]; then
        sha256sum "$1" | awk '{print $1}'
    else
        shasum -a 256 "$1" | awk '{print $1}'
    fi
}

extract_archive()
{
    local archive="$1"
    local destination="$2"

    mkdir -p "$destination"
    if [[ "$platform" == "Linux" ]]; then
        unzip -q -o "$archive" -d "$destination"
    else
        ditto -x -k "$archive" "$destination"
    fi
}

copy_payload()
{
    local source="$1"
    local destination="$2"

    if [[ "$platform" == "Linux" ]]; then
        cp -a "$source/." "$destination/"
    else
        ditto "$source" "$destination"
    fi
}

verify_payload()
{
    local payload="$1"

    [[ -f "$payload/BUILD-INFO.txt" ]] || { echo "Update is missing BUILD-INFO.txt." >&2; return 1; }
    [[ -f "$payload/update.sh" ]] || { echo "Update is missing update.sh." >&2; return 1; }
    [[ -f "$payload/Garbus" ]] || { echo "Update is missing the Garbus executable." >&2; return 1; }
    grep -Fx "Runtime: $runtime" "$payload/BUILD-INFO.txt" >/dev/null \
        || { echo "Update runtime does not match $runtime." >&2; return 1; }

    chmod +x "$payload/Garbus" "$payload/update.sh"
    "$BASH" -n "$payload/update.sh"

    if [[ "$platform" == "Darwin" ]]; then
        codesign --verify --strict --verbose=2 "$payload/Garbus"
        while IFS= read -r -d '' library; do
            codesign --verify --strict --verbose=2 "$library"
        done < <(find "$payload" -type f -name '*.dylib' -print0)
    fi
}

echo "Garbus updater - checking for the newest master build..."

# 1. Current commit from BUILD-INFO.txt
current_commit=""
if [[ -f "$install_dir/BUILD-INFO.txt" ]]; then
    current_commit="$(sed -n 's/^Commit:[[:space:]]*//p' "$install_dir/BUILD-INFO.txt" | head -n1)"
fi

# 2. Newest master release -> "<tag>\t<zip_url>\t<sums_url>"
curl -fsSL -H "$ua" "https://api.github.com/repos/$repo/releases?per_page=100" \
    -o "$tmp/releases.json"
info="$(python3 - "$asset" "$tmp/releases.json" <<'PY'
import json
import sys

asset, releases_path = sys.argv[1:]
with open(releases_path, encoding="utf-8") as releases_file:
    data = json.load(releases_file)

masters = [release for release in data if (release.get("tag_name") or "").startswith("master-")]
masters.sort(key=lambda release: release.get("created_at", ""), reverse=True)
if not masters:
    sys.exit("No master builds found.")

release = masters[0]

def asset_url(name):
    for candidate in release.get("assets", []):
        if candidate.get("name") == name:
            return candidate.get("browser_download_url") or ""
    return ""

zip_url = asset_url(asset)
sums_url = asset_url("SHA256SUMS.txt")
if not zip_url or not sums_url:
    sys.exit("Release %s is missing required assets." % release.get("tag_name"))

print("%s\t%s\t%s" % (release.get("tag_name"), zip_url, sums_url))
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

# 5-6. Download
echo "Downloading $asset..."
curl -fsSL -H "$ua" "$zip_url" -o "$tmp/$asset"
curl -fsSL -H "$ua" "$sums_url" -o "$tmp/SHA256SUMS.txt"

# 7. Verify SHA-256
expected="$(grep -F "$asset" "$tmp/SHA256SUMS.txt" | awk '{print $1}' | head -n1)"
[[ -n "$expected" ]] || { echo "$asset not listed in SHA256SUMS.txt." >&2; exit 1; }
actual="$(sha256_of "$tmp/$asset")"
if [[ "$expected" != "$actual" ]]; then
    echo "Checksum mismatch for $asset (expected $expected, got $actual). Aborting; install untouched." >&2
    exit 1
fi
echo "Checksum verified."

# 8. Extract and validate before touching the installation
extract_archive "$tmp/$asset" "$tmp/extracted"
payload="$tmp/extracted/Garbus"
[[ -d "$payload" ]] || { echo "Unexpected archive layout: no top-level 'Garbus' folder." >&2; exit 1; }
verify_payload "$payload"

# 9. Stage beside the current install so the updater's final rename stays on one filesystem.
staged_install="$(mktemp -d "$install_parent/.${install_name}.update.XXXXXX")"
copy_payload "$payload" "$staged_install"
verify_payload "$staged_install"

# 10. Copy from the verified stage with rollback available. Keep the install directory itself in
# place so a caller running the updater from inside it retains a valid working directory.
backup_dir="$(mktemp -d "$install_parent/.${install_name}.backup.XXXXXX")"
copy_payload "$install_dir" "$backup_dir"

# Do not truncate the updater while Bash is still reading it. Copy the other files first, then
# replace update.sh with a same-filesystem rename after the new payload verifies.
new_updater="$(mktemp "$install_parent/.${install_name}.updater.XXXXXX")"
cp "$staged_install/update.sh" "$new_updater"
chmod +x "$new_updater"
rm "$staged_install/update.sh"

rollback()
{
    local displaced_updater
    displaced_updater="$(mktemp "$install_parent/.${install_name}.displaced-updater.XXXXXX")"
    rm "$displaced_updater"
    mv "$install_dir/update.sh" "$displaced_updater"
    copy_payload "$backup_dir" "$install_dir"
    rm -f "$displaced_updater"
    rm -rf "$backup_dir"
    backup_dir=""
    preserve_backup=0
}

preserve_backup=1
if ! copy_payload "$staged_install" "$install_dir" || ! verify_payload "$install_dir"; then
    rollback
    echo "Could not install the update; the previous installation was restored." >&2
    exit 1
fi

if ! mv -f "$new_updater" "$install_dir/update.sh" || ! verify_payload "$install_dir"; then
    rollback
    echo "Installed files failed verification; the previous installation was restored." >&2
    exit 1
fi
new_updater=""

rm -rf "$backup_dir"
backup_dir=""
preserve_backup=0
rm -rf "$staged_install"
staged_install=""
echo "Updated to $tag."
