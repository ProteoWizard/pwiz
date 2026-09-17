import jetbrains.buildServer.configs.kotlin.*
import buildTypes.*

/*
 * TeamCity Kotlin DSL for the "VersionedConfigs" SUBPROJECT.
 *
 * ============================ READ BEFORE EDITING ============================
 *
 * 1. SCOPE. This file describes ONE subproject, not the ProteoWizard project.
 *    Every other build config on teamcity.labkey.org - the ~26 in
 *    scripts/misc/vcs_trigger_and_paths_config.py and more besides - is still
 *    UI-managed, and none of them appear here.
 *
 *    The footgun: TeamCity reads versioned settings from `.teamcity/` at the
 *    root of the settings VCS root. If anyone later enables versioned settings
 *    on the PARENT ProteoWizard project against this same repo, TeamCity will
 *    read this file as the whole project definition - i.e. as a project
 *    containing one build config and nothing else. Migrating the parent means
 *    exporting the live config first ("Download settings in Kotlin format")
 *    and merging this in, never pointing the parent at this file as-is.
 *
 * 2. THE CONFIG ID IS AN API. smartBuildTrigger.py queues builds by POSTing
 *    <build branchName="..."><buildType id="..."/></build> to
 *    /app/rest/buildQueue, so the AbsoluteId below must stay in lockstep with
 *    the key in scripts/misc/vcs_trigger_and_paths_config.py. Change it in one
 *    place only and the config silently stops being triggered - no error, the
 *    build just never runs.
 *
 *    The `name` is load-bearing too: the trigger script reports skipped builds
 *    to GitHub as "teamcity - <name>", so it has to match the map's value or
 *    PR checks will not line up.
 *
 * 3. NO VCS TRIGGER, deliberately. Everything in this project is queued by
 *    smartBuildTrigger.py (the "zSmart build trigger" config) based on which
 *    files a commit touched. Adding a `triggers { vcs { } }` block here would
 *    rebuild the shims on every commit, which is the exact cost this config
 *    exists to avoid.
 *
 * 4. DSL VERSION. `version` below and teamcity.dsl.version in pom.xml both track the
 *    server, currently 2026.1; bump them together when it is upgraded. The one
 *    value here NOT taken from the server is kotlin.version in pom.xml - if the
 *    DSL fails to compile with a Kotlin error rather than a TeamCity one, that is
 *    the thing to check. If the server rejects this descriptor outright, let
 *    TeamCity generate the pom.xml/settings.kts pair itself (enable Versioned
 *    Settings in "use settings from TeamCity" mode), then re-apply the buildType
 *    below on top of what it produces.
 * =============================================================================
 */

version = "2026.1"

project {
    description = "Build configs defined in-repo via Kotlin DSL, rather than in the TeamCity UI."

    // One file per build configuration under buildTypes/, which is the layout TeamCity
    // itself generates (and the one its patch scripts already use). This file stays a
    // manifest: a config change then shows up as a diff to that config alone rather than
    // to a file every other config also touches.
    buildType(NativeShimsWindows)
    buildType(OspreyLinuxNet)
}
