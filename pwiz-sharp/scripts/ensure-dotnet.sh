#!/usr/bin/env bash
# ------------------------------------------------------------------------
# ensure-dotnet.sh - shared dotnet resolution + SDK provisioning for the Linux
# entry points (build.sh, tcbuild.sh). SOURCE this file; do not execute it.
#
# Two separate problems, deliberately kept apart:
#
#   resolve_dotnet     dotnet IS installed but is not on PATH. A TeamCity agent
#                      can launch the build with a minimal PATH that omits the
#                      install location, and a dangling /usr/bin/dotnet symlink
#                      looks identical to "not installed" unless we say so.
#
#   ensure_dotnet_sdk  dotnet is on PATH but no installed SDK satisfies
#                      global.json. This is what broke ProteoWizard_CoreLinuxNet
#                      on the net10 retarget: the agent image ships 8.0.423 only,
#                      the tree pins 10.0.100, and rollForward=latestFeature does
#                      NOT cross majors - so 8.x can never serve a net10 pin and
#                      `dotnet --version` dies with
#                      "A compatible .NET SDK was not found".
#
# The SDK is installed with Microsoft's dotnet-install.sh into $HOME/.dotnet
# (override with DOTNET_INSTALL_DIR). Deliberately OUTSIDE the repo: tcbuild.sh
# ends with `git status --porcelain` and fails the build on stray files, so an
# in-tree install would trip its own hygiene check.
#
# Set PWIZ_NO_DOTNET_INSTALL=1 to make an unsatisfied global.json a hard error
# instead - for agents whose SDK is owned by the image, where a silent per-build
# install would mask image drift rather than surface it.
# ------------------------------------------------------------------------

# Locate an existing dotnet and put it on PATH. Returns 1 if none is found;
# callers report that, so this stays quiet apart from genuine oddities.
resolve_dotnet() {
    command -v dotnet >/dev/null 2>&1 && return 0
    local cand
    for cand in /usr/bin/dotnet /usr/local/bin/dotnet /usr/share/dotnet/dotnet \
                /usr/lib/dotnet/dotnet "${DOTNET_ROOT:-}/dotnet" "$HOME/.dotnet/dotnet"; do
        [ -n "$cand" ] || continue
        if [ -x "$cand" ]; then
            export PATH="$(dirname "$cand"):$PATH"
            echo "##teamcity[message text='dotnet was not on PATH; using $cand']"
            return 0
        fi
        if [ -e "$cand" ] || [ -L "$cand" ]; then
            echo "##teamcity[message text='$cand exists but is not executable (dangling symlink?)' status='WARNING']"
        fi
    done
    return 1
}

# Channel (major.minor) of the global.json that governs $1: 10.0.100 -> 10.0.
# Read rather than hard-coded so bumping the pin needs no edit here.
#
# Resolved the way dotnet itself resolves it: start in the directory and walk UP.
# Deliberately not one fixed path - where global.json sits is NOT stable across
# the branches this config builds. The net10 retarget moved it from pwiz-sharp/
# to the repo root, so branches cut before that still carry pwiz-sharp/global.json
# while branches after it carry the root one. Walking up is correct for both.
pwiz_required_sdk_channel() {
    local dir gj v
    dir="$(cd "$1" 2>/dev/null && pwd)" || return 1
    while : ; do
        gj="$dir/global.json"
        if [ -f "$gj" ]; then
            v="$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([0-9][^"]*\)".*/\1/p' "$gj" | head -1)"
            [ -n "$v" ] || return 1
            printf '%s\n' "${v%.*}"
            return 0
        fi
        [ "$dir" = "/" ] && return 1
        dir="$(dirname "$dir")"
    done
}

# Ensure SOME installed SDK satisfies global.json, installing one if not.
#
# $1 = the directory the build will run dotnet FROM - that directory, not the
# repo root. global.json resolution walks UP, so probing a PARENT of the real
# working directory cannot see a global.json sitting below it: dotnet answers
# from the parent, where nothing governs, and reports the newest installed SDK.
# The probe then returns "satisfied" for a build that is about to fail.
#
# That is exactly how ProteoWizard_CoreLinuxNet broke on every branch carrying
# pwiz-sharp/global.json (e.g. PR 4587, builds #143/#144/#147/#148/#154): the
# probe ran in the repo root and got 8.0.423 back, provisioning was skipped, and
# the very next line - dotnet --version from pwiz-sharp/ - died on the 10.0.100
# pin the probe never looked at. The guard and the command it guards must run in
# the same directory or the guard is measuring something else.
ensure_dotnet_sdk() {
    local governed_dir="$1"

    # global.json resolution is what actually decides, so ask dotnet itself from
    # the governed directory rather than pattern-matching `dotnet --list-sdks`.
    if ( cd "$governed_dir" && dotnet --version >/dev/null 2>&1 ); then
        return 0
    fi

    local channel
    if ! channel="$(pwiz_required_sdk_channel "$governed_dir")"; then
        echo "##teamcity[message text='dotnet --version failed in $governed_dir and no global.json governs it, so the pin is not the problem - the dotnet install is' status='ERROR']"
        return 1
    fi

    local installed
    installed="$(dotnet --list-sdks 2>/dev/null | tr '\n' ' ')"
    [ -n "$installed" ] || installed="(none)"
    echo "##teamcity[message text='No installed SDK satisfies global.json; need .NET $channel, have: $installed' status='WARNING']"

    if [ "${PWIZ_NO_DOTNET_INSTALL:-0}" = "1" ]; then
        echo "##teamcity[message text='PWIZ_NO_DOTNET_INSTALL=1; refusing to install. Provision .NET $channel on the agent.' status='ERROR']"
        return 1
    fi

    local install_dir="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
    local script rc
    script="$(mktemp)" || return 1

    echo "##teamcity[progressMessage 'Installing the .NET $channel SDK into $install_dir']"
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$script"
        rc=$?
    elif command -v wget >/dev/null 2>&1; then
        wget -qO "$script" https://dot.net/v1/dotnet-install.sh
        rc=$?
    else
        rm -f "$script"
        echo "##teamcity[message text='Neither curl nor wget is available to fetch dotnet-install.sh' status='ERROR']"
        return 1
    fi
    if [ $rc -ne 0 ]; then
        rm -f "$script"
        echo "##teamcity[message text='Could not download dotnet-install.sh (exit $rc)' status='ERROR']"
        return 1
    fi

    # --no-path: PATH is managed below; the installer must not edit shell profiles.
    bash "$script" --channel "$channel" --install-dir "$install_dir" --no-path
    rc=$?
    rm -f "$script"
    if [ $rc -ne 0 ]; then
        echo "##teamcity[message text='dotnet-install.sh failed (exit $rc)' status='ERROR']"
        return 1
    fi

    # Prepend so this SDK outranks an older one already on PATH (the 8.0.423
    # agent-image case), and export DOTNET_ROOT so child builds agree.
    export DOTNET_ROOT="$install_dir"
    export PATH="$install_dir:$PATH"

    if ! ( cd "$governed_dir" && dotnet --version >/dev/null 2>&1 ); then
        echo "##teamcity[message text='Installed .NET $channel but global.json is still unsatisfied' status='ERROR']"
        return 1
    fi
    echo "##teamcity[message text='Using freshly installed SDK $( cd "$governed_dir" && dotnet --version )']"
    return 0
}
