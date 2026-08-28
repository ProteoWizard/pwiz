#!/bin/bash
# Build the pwiz-sharp Linux packages. Linux counterpart of installer/build.ps1.
#
# Mirrors the Windows pipeline and its two-variant split:
#
#   Windows                                         Linux
#   ProteoWizard-Sharp-Setup-<ver>.exe              ProteoWizard-Sharp-linux-x64-<ver>.tar.gz
#     bundles the .NET desktop runtime                self-contained: the runtime is IN the payload
#   ProteoWizard-Sharp-NoNetRuntime-Setup-<ver>.exe ProteoWizard-Sharp-NoNetRuntime-linux-x64-<ver>.tar.gz
#     needs .NET 8 already installed                  framework-dependent: same requirement
#
# "Setup" is dropped for the runtime identifier because a tarball is not an installer; the
# NoNetRuntime discriminator is kept verbatim, since it means exactly the same thing on both
# platforms. Version, output directory, installer-version.txt and the closing size/SHA-256
# report all follow build.ps1 so release tooling can treat the two identically.
#
# Only msconvert ships: MSConvertGUI and SeeMS are net10.0-windows + WinForms.
#
# MUST run on Linux. The vendor csprojs condition their native staging on $(OS), which is the
# BUILD HOST rather than the target RID, so a linux-x64 publish from Windows would stage Windows
# DLLs.
#
# Usage: bash installer/build-linux.sh
set -uo pipefail

INSTALLER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PWIZ_SHARP="$(cd "$INSTALLER_DIR/.." && pwd)"
OUT_DIR="$INSTALLER_DIR/build"
PROJ="$PWIZ_SHARP/Tools/Commandline/MsConvert/src/MsConvert.csproj"
RID=linux-x64

mkdir -p "$OUT_DIR"

# Version: 4.0.YYDOY-gitsha, identical derivation to build.ps1 (date +%y%j gives YYDOY already,
# zero-padded, so lexical sort matches chronological). Falls back to 4.0.0-dev without git, so a
# source-tarball build still produces a versioned artifact.
GITSHA="$(git -C "$PWIZ_SHARP" rev-parse --short=7 HEAD 2>/dev/null || true)"
if [ -z "$GITSHA" ]; then
    APP_VERSION="4.0.0-dev"
else
    APP_VERSION="4.0.$(date +%y%j)-$GITSHA"
fi
echo "==> Stamping version: $APP_VERSION"

# Vendor binaries are STRIPPED from the payload, exactly as build.ps1 does, and fetched at
# runtime by VendorSdkLoader instead. Note the build itself runs WITH vendor licences: the
# managed readers must be compiled without NO_VENDOR_SUPPORT or Read() throws no matter what the
# resolver later downloads. Licences on, binaries stripped - not the other way round.
read -r -d '' PREFIX_PY <<'PY' || true
import json, sys
with open(sys.argv[1], encoding='utf-8') as fh:
    d = json.load(fh)
print('\n'.join(sorted({p for v in d['vendors'] for p in v['prefixes']})))
PY
PINS="$PWIZ_SHARP/build/vendor-sdk-pins.json"
if command -v python3 >/dev/null 2>&1; then
    mapfile -t VENDOR_PREFIXES < <(python3 -c "$PREFIX_PY" "$PINS")
else
    echo "    WARNING: python3 not found; cannot read vendor prefixes, refusing to package" >&2
    exit 1
fi

# Strip one published tree in place. Mirrors build.ps1's Should-Skip, adjusted for Linux: keep
# the linux-x64 runtimes subtree rather than win-x64, and match the lib<name>.so spelling as well
# as <name>.so - the DllImport name is "timsdata" but the file is libtimsdata.so, so a bare
# prefix test would let the vendor binaries through, which is a licensing problem, not a size one.
strip_payload() {
    local dir="$1" removed=0
    while IFS= read -r -d '' f; do
        local leaf base
        leaf="$(basename "$f")"
        base="${leaf#lib}"
        case "$leaf" in
            *.pdb|*.xml) rm -f "$f"; removed=$((removed+1)); continue ;;
        esac
        case "$leaf" in
            *.dll|*.so|*.so.*|*.dylib)
                for p in "${VENDOR_PREFIXES[@]}"; do
                    if [[ "$leaf" == "$p"* || "$base" == "$p"* ]]; then
                        rm -f "$f"; removed=$((removed+1)); break
                    fi
                done ;;
        esac
    done < <(find "$dir" -type f -print0)

    # Non-linux RID payloads and BCL localisation satellites, as on Windows.
    find "$dir/runtimes" -mindepth 1 -maxdepth 1 -type d ! -name "linux*" -exec rm -rf {} + 2>/dev/null
    for lang in cs de es fr it ja ko pl pt-BR ru tr zh-Hans zh-Hant; do
        rm -rf "${dir:?}/$lang"
    done
    echo "    stripped $removed vendor/debug files"
}

package() {
    local variant="$1" self_contained="$2" name="$3"
    local stage="$OUT_DIR/stage-$variant"
    echo
    echo "==> publish $variant (self-contained=$self_contained)"
    rm -rf "$stage"
    if ! dotnet publish "$PROJ" -c Release -r "$RID" --self-contained "$self_contained" \
            -p:IAgreeToVendorLicenses=true --nologo -o "$stage" 2>&1 | grep -E " error |error [A-Z]+[0-9]+" ; then
        : # grep found no errors, which is the good path
    fi
    if [ ! -f "$stage/msconvert" ]; then
        echo "    FAILED: no msconvert apphost produced in $stage" >&2
        return 1
    fi

    strip_payload "$stage"

    # Refuse to ship a vendor binary. build.ps1 gets this from its staging filter; here it is an
    # explicit assertion, because the failure mode is redistributing a licensed SDK rather than
    # an oversized artifact.
    local leaked
    leaked="$(find "$stage" -type f \( -name '*.so' -o -name '*.so.*' -o -name '*.dll' \) -printf '%f\n' |
        while read -r leaf; do
            base="${leaf#lib}"
            for p in "${VENDOR_PREFIXES[@]}"; do
                if [[ "$leaf" == "$p"* || "$base" == "$p"* ]]; then echo "$leaf"; break; fi
            done
        done)"
    if [ -n "$leaked" ]; then
        echo "    ABORT: vendor binaries present in payload:" >&2
        echo "$leaked" | sed 's/^/      /' >&2
        return 1
    fi

    chmod +x "$stage/msconvert"
    local tarball="$OUT_DIR/$name.tar.gz"
    rm -f "$tarball"
    tar -czf "$tarball" -C "$stage" .
    rm -rf "$stage"

    printf 'Package: %s\n' "$tarball"
    printf 'Version: %s\n' "$APP_VERSION"
    printf 'Size:    %s MB\n' "$(awk "BEGIN{printf \"%.1f\", $(stat -c%s "$tarball")/1048576}")"
    printf 'SHA-256: %s\n' "$(sha256sum "$tarball" | cut -d' ' -f1 | tr 'a-f' 'A-F')"
    echo
}

RC=0
package self-contained      true  "ProteoWizard-Sharp-$RID-$APP_VERSION"              || RC=1
package framework-dependent false "ProteoWizard-Sharp-NoNetRuntime-$RID-$APP_VERSION" || RC=1

# Same side-car build.ps1 writes, so packaging tests can pin the version without re-deriving it
# from a filename.
printf '%s' "$APP_VERSION" > "$OUT_DIR/installer-version.txt"
exit $RC
