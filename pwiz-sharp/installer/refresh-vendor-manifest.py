"""Regenerate installer/vendor-manifest.json from the vendor 7z archives in the
pwiz repo.

For each vendor, finds the most-recent commit that touched its archive (`git log
-1 -- <path>`), constructs a raw.githubusercontent.com URL pinned to that commit,
and records the file's SHA-256. Run this whenever a vendor archive is updated.

Usage:
    python pwiz-sharp/installer/refresh-vendor-manifest.py

Pins are self-contained:
  - The commit SHA is in the URL → GitHub serves byte-immutable content forever.
  - The SHA-256 is recorded → VendorSdkLoader verifies + rejects on mismatch.
  - Cache dir name carries the short SHA → multiple pwiz-sharp versions can keep
    their distinct vendor SDK caches side-by-side.
"""

from __future__ import annotations
import hashlib
import json
import subprocess
import sys
from pathlib import Path

PWIZ_ROOT = Path("C:/dev/pwiz-msconvert-pr")
SHARP_ROOT = PWIZ_ROOT / "pwiz-sharp"
MANIFEST_PATH = SHARP_ROOT / "installer" / "vendor-manifest.json"

# (vendor name, repo-relative archive path, assembly-name prefixes that route to it)
# Most archives live under pwiz_aux/msrc/utility/. Thermo on the pwiz-sharp side has
# its own newer .NET-8-compatible copy under pwiz-sharp/vendor-archives/.
VENDORS: list[tuple[str, str, list[str]]] = [
    ("Thermo",  "pwiz-sharp/vendor-archives/vendor_api_Thermo.7z", [
        "ThermoFisher.", "OpenMcdf",
    ]),
    ("Bruker",  "pwiz_aux/msrc/utility/vendor_api_Bruker.7z", [
        "Bruker.", "BaseDataAccess", "BDal.", "CompassXtractMS",
        "EDAL", "Interop.EDAL", "ProtocolBuffers", "BaseError",
        "BaseCommon", "Compass",
    ]),
    ("Waters",  "pwiz_aux/msrc/utility/vendor_api_Waters.7z", [
        "MassLynxRaw", "cdt", "MassLynx",
    ]),
    ("Agilent", "pwiz_aux/msrc/utility/vendor_api_Agilent.7z", [
        "Agilent.", "BaseDataAccess", "MIDAC.", "Mhdac.", "MassHunter.",
    ]),
    ("ABI",     "pwiz_aux/msrc/utility/vendor_api_ABI.7z", [
        "Clearcore2.", "Sciex.", "SCIEX.", "SciexToolKit", "Interop.",
    ]),
    ("Shimadzu","pwiz_aux/msrc/utility/vendor_api_Shimadzu.7z", [
        "Shimadzu.", "QTFL", "GCMS", "GCMSProto", "QTFLProto",
        "DataReader", "Google.Protobuf", "IDQuantLSS", "PeakIDEA",
        "PeakItgLSS", "MassLibrarySearch", "MassCalcWrap",
        "MassStandardSpectrum", "DualProbeInterface",
    ]),
    ("Mobilion","pwiz_aux/msrc/utility/vendor_api_Mobilion.7z", [
        "Mobilion.", "MBISDK", "MobilionShim",
    ]),
]

GH_REPO = "ProteoWizard/pwiz"
RAW_URL_FORMAT = "https://raw.githubusercontent.com/{repo}/{commit}/{path}"


def last_commit_for(rel_path: str) -> str:
    """Most recent commit SHA that touched rel_path (full 40-char hex)."""
    result = subprocess.run(
        ["git", "log", "-1", "--format=%H", "--", rel_path],
        cwd=PWIZ_ROOT, capture_output=True, text=True, check=True,
    )
    sha = result.stdout.strip()
    if not sha:
        raise RuntimeError(f"no git history found for {rel_path}")
    return sha


def sha256_of(rel_path: str) -> str:
    """SHA-256 of the file's bytes, hex-encoded uppercase to match
    Convert.ToHexString in C#."""
    h = hashlib.sha256()
    with (PWIZ_ROOT / rel_path).open("rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def main() -> int:
    vendors_json = []
    for name, rel_path, prefixes in VENDORS:
        abs_path = PWIZ_ROOT / rel_path
        if not abs_path.is_file():
            print(f"warning: {rel_path} not found, skipping", file=sys.stderr)
            continue

        commit = last_commit_for(rel_path)
        sha256 = sha256_of(rel_path)
        url = RAW_URL_FORMAT.format(repo=GH_REPO, commit=commit, path=rel_path)

        vendors_json.append({
            "name": name,
            "version": commit[:12],  # short SHA — cache directory key
            "url": url,
            "sha256": sha256,
            "assemblyPrefixes": prefixes,
        })
        print(f"{name:<10}  {commit[:12]}  {sha256[:16]}…  {rel_path}")

    manifest = {
        "schema": 1,
        "_generator": "pwiz-sharp/installer/refresh-vendor-manifest.py",
        "_repo": GH_REPO,
        "vendors": vendors_json,
    }

    MANIFEST_PATH.write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"\nwrote {MANIFEST_PATH.relative_to(PWIZ_ROOT)}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
