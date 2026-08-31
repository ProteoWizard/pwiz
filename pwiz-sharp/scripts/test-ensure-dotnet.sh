#!/usr/bin/env bash
# ------------------------------------------------------------------------
# test-ensure-dotnet.sh - guards the contract of ensure-dotnet.sh.
#
# Runs in well under a second, needs no network and no real .NET SDK: a stub
# dotnet on PATH mimics the agent image (8.0.423 only, so any 10.x pin fails).
#
# What it protects. ensure_dotnet_sdk decides whether to provision an SDK by
# running `dotnet --version` in a directory. global.json resolution walks UP, so
# that directory has to be the one the build actually runs in. It used to be
# handed the repo root while the build ran in pwiz-sharp/ - on a tree whose pin
# lives in pwiz-sharp/global.json, nothing governed the root, dotnet returned the
# newest installed SDK, and the guard reported "satisfied" for a build that then
# died on the pin. ProteoWizard_CoreLinuxNet #143/#144/#147/#148/#154.
#
# The two layouts below are both real: the net10 retarget moved global.json from
# pwiz-sharp/ to the repo root, so branches exist on either side of that move and
# this config builds both.
#
# Usage: bash test-ensure-dotnet.sh [path/to/ensure-dotnet.sh]
# ------------------------------------------------------------------------
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="${1:-$SCRIPT_DIR/ensure-dotnet.sh}"
[ -f "$SRC" ] || { echo "ensure-dotnet.sh not found at $SRC" >&2; exit 1; }

pass=0; fail=0
ok()   { echo "  PASS $1"; pass=$((pass+1)); }
bad()  { echo "  FAIL $1"; fail=$((fail+1)); }
check(){ [ "$2" = "$3" ] && ok "$1" || bad "$1 : expected [$2] got [$3]"; }

root="$(mktemp -d)" || exit 1
trap 'rm -rf "$root"' EXIT

pin='{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}'
# $1 = layout name, $2 = dir (relative to the layout root) that holds global.json
mklayout() {
    rm -rf "${root:?}/$1"; mkdir -p "$root/$1/pwiz-sharp"
    printf '%s\n' "$pin" > "$root/$1/$2/global.json"
}

# A stub dotnet that resolves global.json exactly as the real one does - walk up
# from cwd - and refuses any 10.x pin, which is the agent image's behaviour.
bin="$root/bin"; mkdir -p "$bin"
cat > "$bin/dotnet" <<'STUB'
#!/usr/bin/env bash
d="$PWD"
while : ; do
  if [ -f "$d/global.json" ]; then
    if grep -q '"version"[[:space:]]*:[[:space:]]*"10\.' "$d/global.json"; then
      echo "A compatible .NET SDK was not found. Requested SDK version: 10.0.100" >&2
      exit 1
    fi
    break
  fi
  [ "$d" = "/" ] && break
  d="$(dirname "$d")"
done
if [ "${1:-}" = "--list-sdks" ]; then echo "8.0.423 [/usr/share/dotnet/sdk]"; else echo "8.0.423"; fi
exit 0
STUB
chmod +x "$bin/dotnet"
export PATH="$bin:$PATH"
export PWIZ_NO_DOTNET_INSTALL=1   # stop at detection; never download an SDK here

# shellcheck disable=SC1090
. "$SRC"

echo "pwiz_required_sdk_channel - resolves the global.json that GOVERNS a dir"

mklayout root_pin "."
check "root global.json found by walking up from pwiz-sharp/" \
      "10.0" "$(pwiz_required_sdk_channel "$root/root_pin/pwiz-sharp" 2>/dev/null)"

mklayout nested_pin "pwiz-sharp"
check "pwiz-sharp/global.json found in place" \
      "10.0" "$(pwiz_required_sdk_channel "$root/nested_pin/pwiz-sharp" 2>/dev/null)"

# Documents WHY the repo root is the wrong thing to probe: resolution never
# descends, so a pin below the probed directory is invisible.
pwiz_required_sdk_channel "$root/nested_pin" >/dev/null 2>&1
check "probing the repo root cannot see pwiz-sharp/global.json" "1" "$?"

mkdir -p "$root/no_pin/pwiz-sharp"
pwiz_required_sdk_channel "$root/no_pin/pwiz-sharp" >/dev/null 2>&1
check "absent global.json reports not-found" "1" "$?"

echo "ensure_dotnet_sdk - detects an unsatisfied pin in both layouts"

ensure_dotnet_sdk "$root/nested_pin/pwiz-sharp" >/dev/null 2>&1
[ $? -ne 0 ] && ok "nested pin detected (the regression case)" \
             || bad "nested pin missed - a build would fail after the guard passed"

ensure_dotnet_sdk "$root/root_pin/pwiz-sharp" >/dev/null 2>&1
[ $? -ne 0 ] && ok "root pin detected" \
             || bad "root pin missed"

# A satisfied pin must NOT trigger provisioning: 8.x pin, 8.0.423 stub installed.
rm -rf "$root/ok_pin"; mkdir -p "$root/ok_pin/pwiz-sharp"
printf '%s\n' '{"sdk":{"version":"8.0.100","rollForward":"latestFeature"}}' > "$root/ok_pin/global.json"
ensure_dotnet_sdk "$root/ok_pin/pwiz-sharp" >/dev/null 2>&1
[ $? -eq 0 ] && ok "satisfied pin returns success without provisioning" \
             || bad "satisfied pin was treated as unsatisfied"

echo "passed=$pass failed=$fail"
[ "$fail" -eq 0 ]
