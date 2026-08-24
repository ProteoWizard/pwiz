#!/bin/bash

# ------------------------------------------------------------------------
# clean.sh - wipe pwiz-sharp build artifacts.
#
# Linux/macOS counterpart of clean.bat, and the pwiz-sharp analogue of cpp
# pwiz's clean.sh two levels up. Same flags and same behaviour as clean.bat;
# keep the two in step when either changes.
#
# Default: wipe build outputs but KEEP the two caches (.NET runtime download
# + extracted vendor SDK assemblies). This is the mode TC runs - tcbuild.bat
# calls clean.bat with no arguments before every build, so CI already gets a
# from-scratch compile on every commit.
#
# Pass --all (or -a) to clear the caches too. Measured cost of doing so:
#   - vendor-assemblies/ re-extract (171 MB across 7 vendors): ~2 s. Cheap,
#     because it's a local 7z unpack of checked-in archives.
#   - installer/cache/: a ~56 MB re-download of the .NET desktop runtime from
#     aka.ms, and only on builds that run the installer. That's the real cost
#     of --all, and it's a reliability cost as much as a time one - it puts an
#     external endpoint on the build's critical path.
# Both caches are content-addressed (vendor archives by SHA-256 via the pins
# table, the runtime by a fixed versioned URL), so neither can drift
# commit-to-commit. --all is a paranoia reset: better suited to a nightly
# than to every commit.
#
# The list below tracks pwiz-sharp/.gitignore: everything the build writes is
# gitignored, so that file is the spec for what belongs here. Two gitignored
# entries are deliberately kept (see "NOT touched" below).
#
# What gets removed (always):
#   - bin/ and obj/ under every project (dotnet build outputs, AOT publish
#     output)
#   - TestResults/ at every level - the top-level one plus the per-project
#     dirs `dotnet test` drops in pwiz/test/*/ (run logs + dotCover snapshots)
#   - installer/build/ (packaged output + the version.txt sidecar that
#     Installer.Tests reads) and installer/staging/ (payload staging tree)
#   - examples/**/build/ and Tools/BiblioSpec/native/**/build/ (cmake build
#     trees - the AOT example, MascotShim, etc.)
#   - pwiz/src/Vendor/Common/VendorSdkPins.generated.cs (regenerated on every
#     build from the vendor 7z archives' SHA-256 + git history)
#
# What gets removed only with --all:
#   - installer/cache/ (~56 MB .NET runtime installer; re-downloaded by the
#     installer build on the next run if missing)
#   - vendor-assemblies/ (DLLs extracted from the vendor 7z archives by the
#     .csproj ExtractVendorAssemblies targets - one top-level dir, per
#     $(PwizVendorAssembliesPath); re-extracted on the next dotnet build)
#
# What is NOT touched, ever:
#   - vendor-archives/ - looks like a cache, is NOT: those archives are
#     TRACKED IN GIT and are build inputs. Deleting them is unrecoverable
#     without a fresh checkout. Do not add it to the --all branch.
#   - build/ at the top level - also tracked source (MSBuild .targets and the
#     VendorPinsGenerator/AgilentPatcher projects). This is why the cmake
#     sweep below is scoped to named subtrees instead of walking for any dir
#     called "build".
#   - Directory.Build.user.props (per-user "I agreed to vendor licenses" flag;
#     wiping it would force the user to re-run i-agree-to-the-vendor-licenses)
#   - .vs/ and *.user/*.suo files (IDE local state; not build output)
#
# Usage:
#   ./clean.sh          Wipe build outputs, keep caches (default).
#   ./clean.sh --all    Wipe caches too (TC-equivalent full reset).
#   ./clean.sh -a       Short alias.
# ------------------------------------------------------------------------

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$script_dir" || exit 1

clean_cache=0
case "${1:-}" in
    --all|-a) clean_cache=1 ;;
    "")       ;;
    *)        echo "clean.sh: unknown argument '$1' (expected --all or -a)" >&2; exit 2 ;;
esac

echo "Cleaning pwiz-sharp build artifacts..."

# bin/, obj/ and TestResults/ under every project. -prune stops the walk from
# descending into a tree it is about to delete (which would otherwise make find
# complain about vanished paths). No tracked file lives under a dir with any of
# these names, so the sweep is safe.
find . -type d \( -name bin -o -name obj -o -name TestResults \) -prune -exec rm -rf {} +

# Top-level output trees.
rm -rf installer/build installer/staging

# CMake build trees. Scoped to these two subtrees on purpose: the top-level
# build/ is tracked source, so a bare "find . -name build" would delete it.
for subtree in examples Tools/BiblioSpec/native; do
    if [ -d "$subtree" ]; then
        find "$subtree" -type d -name build -prune -exec rm -rf {} +
    fi
done

# Vendor SDK pins are regenerated on every build (build/VendorPinsGenerator is
# invoked as a pre-CoreCompile target in Vendor.Common.csproj).
rm -f pwiz/src/Vendor/Common/VendorSdkPins.generated.cs

# Caches: preserved by default; wiped only with --all. NB vendor-assemblies is a
# single top-level dir ($(PwizVendorAssembliesPath)), not a per-project one, and
# vendor-archives/ next to it is tracked - do not add it here.
if [ "$clean_cache" -eq 1 ]; then
    rm -rf installer/cache vendor-assemblies
    echo "Clean complete (caches wiped too)."
else
    echo "Clean complete (caches preserved)."
fi
