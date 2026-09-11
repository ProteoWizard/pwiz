#!/bin/bash
#
# Vendor reader conversion sweep.
#
# For every vendor test fixture, convert it with msconvert and compare the result against
# the reference mzML checked in beside it with msdiff. This is what the TeamCity "Run
# ProteoWizard tests" step of the container build runs inside the image. It lives in the
# repository rather than in the build configuration so that it can be reviewed, changed
# under version control, and run by hand against a local build.
#
# Usage:
#   scripts/container/tctest.sh [options]
#
#   --data-root DIR   root of the vendor reader test data
#                     (default: pwiz/data/vendor_readers under the repository root)
#   --vendor NAME     sweep only this vendor; repeatable
#   --fixture GLOB    sweep only fixtures whose filename matches GLOB; repeatable
#   --list            list the fixtures that would be swept, then stop
#   --teamcity        emit TeamCity service messages (automatic when TEAMCITY_VERSION is set)
#   --install-deps    apt-get the tools the container image lacks (bzip2)
#   -h, --help        this message
#
# Environment:
#   MSCONVERT, MSDIFF   commands to run, when the defaults are not wanted. Each may carry
#                       arguments ("mywine msconvert"). By default the container's mywine
#                       wrapper is preferred, then msconvert/msdiff on PATH, then the
#                       staged build under pwiz-sharp/installer/build/stage.
#
# Exit status is 0 when every fixture matched its reference. Under TeamCity it is always
# 0: the service messages already fail the build, and reporting the failure a second time
# through the exit code would also defeat muting an individual fixture.

set -u

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
ROOT=$(cd "$SCRIPT_DIR/../.." && pwd)

VENDORS=("ABI" "Agilent" "Bruker" "Mobilion" "Shimadzu" "Thermo" "UIMF" "Waters")
EXTENSIONS=(".d" ".lcd" ".mbi" ".raw" ".wiff" ".wiff2" ".uimf")

DATA_ROOT="$ROOT/pwiz/data/vendor_readers"
VENDOR_FILTER=()
FIXTURE_FILTER=()
LIST_ONLY=0
INSTALL_DEPS=0
TEAMCITY=0
if [ -n "${TEAMCITY_VERSION:-}" ]; then
    TEAMCITY=1
fi

PASSED=0
FAILED=0

usage()
{
    sed -n '3,28p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

die()
{
    echo "tctest: $*" >&2
    exit 2
}

while [ $# -gt 0 ]; do
    case "$1" in
        --data-root)
            [ $# -ge 2 ] || die "--data-root needs a directory"
            DATA_ROOT="$2"
            shift 2
            ;;
        --vendor)
            [ $# -ge 2 ] || die "--vendor needs a name"
            VENDOR_FILTER+=("$2")
            shift 2
            ;;
        --fixture)
            [ $# -ge 2 ] || die "--fixture needs a pattern"
            FIXTURE_FILTER+=("$2")
            shift 2
            ;;
        --list)
            LIST_ONLY=1
            shift
            ;;
        --teamcity)
            TEAMCITY=1
            shift
            ;;
        --install-deps)
            INSTALL_DEPS=1
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            die "unrecognized option '$1' (try --help)"
            ;;
    esac
done

DATA_ROOT=$(cd "$DATA_ROOT" 2>/dev/null && pwd) || die "no such data root: $DATA_ROOT"

if [ ${#VENDOR_FILTER[@]} -gt 0 ]; then
    VENDORS=("${VENDOR_FILTER[@]}")
fi

if [ "$INSTALL_DEPS" -eq 1 ]; then
    # bzip2 is what tar shells out to for the .tar.bz2 fixtures; the image does not carry it.
    export DEBIAN_FRONTEND="noninteractive"
    apt-get -qq update
    apt-get -qq install bzip2
fi

# ---------------------------------------------------------------------------
# Tools
# ---------------------------------------------------------------------------

MSCONVERT_CMD=()
MSDIFF_CMD=()

resolve_tool()  # <tool-name> <override>; echoes the command words
{
    local tool="$1" override="$2"
    if [ -n "$override" ]; then
        printf '%s\n' "$override"
    elif command -v mywine > /dev/null 2>&1; then
        printf 'mywine %s\n' "$tool"
    elif command -v "$tool" > /dev/null 2>&1; then
        printf '%s\n' "$tool"
    elif [ -x "$ROOT/pwiz-sharp/installer/build/stage/$tool.exe" ]; then
        printf '%s\n' "$ROOT/pwiz-sharp/installer/build/stage/$tool.exe"
    else
        return 1
    fi
}

read -ra MSCONVERT_CMD <<< "$(resolve_tool msconvert "${MSCONVERT:-}")"
read -ra MSDIFF_CMD <<< "$(resolve_tool msdiff "${MSDIFF:-}")"
[ ${#MSCONVERT_CMD[@]} -gt 0 ] || die "cannot find msconvert; set MSCONVERT to the command to run"
[ ${#MSDIFF_CMD[@]} -gt 0 ] || die "cannot find msdiff; set MSDIFF to the command to run"

msconvert_()
{
    "${MSCONVERT_CMD[@]}" "$@"
}

msdiff_()
{
    "${MSDIFF_CMD[@]}" "$@"
}

# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------

# TeamCity reads a line as a service message however much whitespace precedes it, so the
# leading space costs nothing and keeps the emitted lines identical to the ones the inline
# build step used to produce.
TC=" ##teamcity"

tc_escape()
{
    local s="$1"
    s=${s//|/||}
    s=${s//\'/|\'}
    s=${s//$'\n'/|n}
    s=${s//\[/|[}
    s=${s//]/|]}
    printf '%s' "$s"
}

suite_started()
{
    if [ "$TEAMCITY" -eq 1 ]; then
        echo "${TC}[testSuiteStarted name='$(tc_escape "$1")']"
    fi
}

suite_finished()
{
    if [ "$TEAMCITY" -eq 1 ]; then
        echo "${TC}[testSuiteFinished name='$(tc_escape "$1")']"
    fi
}

test_started()
{
    if [ "$TEAMCITY" -eq 1 ]; then
        echo "${TC}[testStarted name='$(tc_escape "$1")' captureStandardOutput='true']"
    fi
}

test_failed()  # <test name> <message>
{
    if [ "$TEAMCITY" -eq 1 ]; then
        echo "${TC}[testFailed name='$(tc_escape "$1")' message='$(tc_escape "$2")']"
    else
        echo "FAIL  $1: $2"
    fi
}

test_finished()  # <test name> <duration ms> <failed?>
{
    if [ "$TEAMCITY" -eq 1 ]; then
        echo "${TC}[testFinished name='$(tc_escape "$1")' duration='$2']"
    elif [ "$3" -eq 0 ]; then
        echo "ok    $1 ($2 ms)"
    fi
}

now_ms()
{
    local t
    t=$(date +%s%3N 2>/dev/null)
    case "$t" in
        '' | *[!0-9]*) echo $(( $(date +%s) * 1000 )) ;;
        *) echo "$t" ;;
    esac
}

# ---------------------------------------------------------------------------
# Comparison
# ---------------------------------------------------------------------------

# Reads the index range a reference mzML covers. A handful of references hold only a slice
# of their source file, and the vendor reader test harness records the slice's bounds in
# the dataProcessing section when it writes one:
#
#   <processingMethod order="1" softwareRef="pwiz_Reader_Waters">
#     <cvParam cvRef="MS" accession="MS:1001486" name="data filtering" value="" />
#     <userParam name="index filter" value="0-9" />
#   </processingMethod>
#
# MS:1001486 "data filtering" is the nearest standard term; no CV accession describes an
# index subset, so the range itself rides in the userParam. Prints "a-b", or nothing for a
# reference that covers its whole source file.
reference_index_range()
{
    sed -n \
        -e 's/.*<userParam name="index filter" value="\([0-9][0-9]*-[0-9][0-9]*\)".*/\1/p' \
        -e '/<\/dataProcessingList>/q' \
        "$1" 2>/dev/null | head -1
}

# Narrows a converted mzML to the index range its reference covers, so the two can be
# compared spectrum for spectrum, and prints the path to the narrowed copy.
#
# The narrowing happens here, on the file msconvert already wrote, rather than by adding
# --filter "index [a,b]" to the conversion itself. Both select the same spectra, but
# filtering during the conversion would stop the reader ever touching the rest of the
# file - and reading the whole file is most of what this sweep exists to prove.
narrow_to_range()  # <produced mzML> <a-b> <destination dir>
{
    local produced="$1" range="$2" dest="$3"
    local outfile
    outfile=$(basename "$produced")
    mkdir -p "$dest" || return 1
    msconvert_ -z "$produced" --noindex -o "$dest" --outfile "$outfile" \
        --filter "index [${range%-*},${range#*-}]" > /dev/null 2>&1 || return 1
    printf '%s\n' "$dest/$outfile"
}

# Compares one converted mzML against one reference, appending msdiff's report to the
# fixture's diff file. Returns 0 when the two hold the same spectra and chromatograms.
#
# The verdict comes from msdiff's summary lines rather than its exit status, because a
# reference and a fresh conversion always differ somewhere in their metadata - which is
# what msdiff's non-zero status reports. The grep is anchored on the parenthesis: cpp
# msdiff renders both the matching and the differing case as "(N spectra)", so an
# unanchored "0 spectra" also matches the tail of "19570 spectra" and a file that matched
# nothing passes. msdiff-sharp renders the differing case as "(N differing spectra)" so
# that it cannot be misread either way, but the anchor is what makes the check sound.
#
# Each pair is judged on its own report before that report joins the fixture's diff file.
# A wiff holding several samples produces several pairs, and grepping the accumulated file
# would let the first sample that matched vouch for every sample after it.
compare_to_reference()  # <reference> <produced> <precision|""> <diff file>
{
    local reference="$1" produced="$2" precision="$3" difffile="$4"
    local range narrowed report status

    report="$difffile.pair"
    : > "$report"

    range=$(reference_index_range "$reference")
    if [ -n "$range" ]; then
        narrowed=$(narrow_to_range "$produced" "$range" "$(dirname "$difffile")/narrowed")
        if [ -z "$narrowed" ]; then
            echo "could not narrow $produced to index $range" > "$report"
            cat "$report" >> "$difffile"
            rm -f "$report"
            return 1
        fi
        produced="$narrowed"
    fi

    if [ -n "$precision" ]; then
        msdiff_ -p "$precision" "$reference" "$produced" > "$report" 2>&1
    else
        msdiff_ "$reference" "$produced" > "$report" 2>&1
    fi

    status=1
    if grep -q "(0 spectra)" "$report" && grep -q "(0 chromatograms)" "$report"; then
        status=0
    fi

    cat "$report" >> "$difffile"
    rm -f "$report"
    return "$status"
}

# ---------------------------------------------------------------------------
# Sweep
# ---------------------------------------------------------------------------

wanted_fixture()
{
    local name="$1" pattern
    if [ ${#FIXTURE_FILTER[@]} -eq 0 ]; then
        return 0
    fi
    for pattern in "${FIXTURE_FILTER[@]}"; do
        case "$name" in
            $pattern) return 0 ;;
        esac
    done
    return 1
}

sweep_fixture()  # <fixture filename> <extension>
{
    local fixture="$1" extension="$2"
    local name="${fixture%"$extension"}"
    local testname="Convert $fixture"
    local outdir="output/$name"
    local difffile="$outdir/$name.diff"
    local started duration failed=0 compared=0
    local mzml reference

    test_started "$testname"
    rm -f "$difffile"
    started=$(now_ms)

    # An Analysis.yep fixture is a Bruker format the sweep does not convert, and diaPASEF
    # is only tested through a filtered configuration the reference does not describe.
    if ! [ -e "$fixture/Analysis.yep" ] && [ "$fixture" != "diaPASEF.d" ]; then
        if [ "$name" = "swath.api" ]; then
            msconvert_ -z "$fixture" --noindex -o "$outdir" \
                --outfile "swath.api-sample-centroid.mzML" --filter "peakPicking true 1-"
        else
            msconvert_ -z "$fixture" --noindex -o "$outdir"
        fi
    fi

    duration=$(( $(now_ms) - started ))

    if [ -e "$outdir/$name.mzML" ]; then
        compared=1
        compare_to_reference "$name.mzML" "$outdir/$name.mzML" 1e-5 "$difffile" || failed=1
    elif [ "$extension" = ".wiff" ] || [ "$extension" = ".wiff2" ]; then
        # One wiff holds several samples, so msconvert writes one mzML per sample and the
        # pairing runs the other way round: each produced file looks for a reference of its
        # own name. A produced sample with no reference is not a failure - some fixtures
        # deliberately carry references for only part of their contents. Each pairing gets
        # its own verdict, so a fixture passes only when every sample that has a reference
        # matched it.
        for mzml in "$outdir"/*.mzML; do
            [ -e "$mzml" ] || continue
            reference=$(basename "$mzml")
            if [ ! -e "$reference" ]; then
                reference="$(basename "$mzml" .mzML)-centroid.mzML"
            fi
            [ -e "$reference" ] || continue
            compared=1
            compare_to_reference "$reference" "$mzml" "" "$difffile" || failed=1
        done
    elif [ "$fixture" = "diaPASEF.d" ]; then
        echo " Skipping diaPASEF test because it is not tested in non-filtered mode (diaPASEF.mzML)."
    else
        failed=1
        test_failed "$testname" "Expected output file '$outdir/$name.mzML' does not exist."
    fi

    if [ "$failed" -eq 1 ] && [ "$compared" -eq 1 ]; then
        test_failed "$testname" "$(summarize_diff "$difffile")"
    fi

    if [ "$failed" -eq 0 ]; then
        PASSED=$(( PASSED + 1 ))
        rm -f "$difffile"
    else
        FAILED=$(( FAILED + 1 ))
    fi
    rm -rf "$outdir/narrowed"

    test_finished "$testname" "$duration" "$failed"
}

summarize_diff()
{
    grep -E '^(spectrumList|chromatogramList) \(' "$1" 2>/dev/null \
        | grep -v '(0 ' \
        | paste -sd '; ' - \
        | sed 's/^$/msdiff reported no summary/'
}

sweep_vendor()
{
    local vendor="$1"
    local dir="$DATA_ROOT/$vendor"
    local data="$dir/Reader_${vendor}_Test.data"
    local extension fixture

    [ -d "$dir" ] || return 0

    if [ -e "$dir/Reader_${vendor}_Test.data.tar.bz2" ]; then
        # -k keeps existing files, so a reference committed to the repository always wins
        # over the copy inside the tarball. The "Cannot open: File exists" it prints for
        # every one of them, and the failure status it exits with afterwards, are the
        # mechanism working rather than a broken extraction; anything else tar has to say
        # still comes through.
        ( cd "$dir" && tar xkjf "Reader_${vendor}_Test.data.tar.bz2" ) 2>&1 \
            | grep -vE 'Cannot open: File exists|Exiting with failure status'
    fi

    [ -d "$data" ] || return 0
    cd "$data" || return 1

    for extension in "${EXTENSIONS[@]}"; do
        for fixture in *"$extension"; do
            [ -e "$fixture" ] || continue
            wanted_fixture "$fixture" || continue
            if [ "$LIST_ONLY" -eq 1 ]; then
                echo "$vendor/$fixture"
            else
                sweep_fixture "$fixture" "$extension"
            fi
        done
    done
}

suite_started "ProteoWizard msconvert"
for vendor in "${VENDORS[@]}"; do
    sweep_vendor "$vendor"
done
suite_finished "ProteoWizard msconvert"

if [ "$LIST_ONLY" -eq 1 ]; then
    exit 0
fi

echo "tctest: $PASSED passed, $FAILED failed"

if [ "$FAILED" -gt 0 ] && [ "$TEAMCITY" -eq 0 ]; then
    exit 1
fi
exit 0
