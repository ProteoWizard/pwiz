import jetbrains.buildServer.configs.kotlin.*
import buildTypes.*

/*
 * TeamCity Kotlin DSL for the "VersionedConfigs" SUBPROJECT.
 *
 * ============================ READ BEFORE EDITING ============================
 *
 * 1. SCOPE. This file describes ONE subproject, not the ProteoWizard project.
 *    .teamcity/ holds two independent sets of versioned settings:
 *      - This Kotlin DSL (settings.kts, pom.xml, the .kt files in buildTypes,
 *        and the .kts patch scripts in patches/buildTypes that TeamCity writes
 *        when one of this subproject's configs is edited in the UI) is the
 *        VersionedConfigs subproject. Never write a slash followed by an
 *        asterisk anywhere in this comment, a path glob included: Kotlin block
 *        comments nest, so it opens a comment that never closes, everything
 *        below - project() included - is commented out, and every
 *        VersionedConfigs build fails with "project definition is not found".
 *      - The parent ProteoWizard project and its other subprojects - bt83,
 *        bt209 and the rest of scripts/misc/vcs_trigger_and_paths_config.py -
 *        are versioned in XML format, one file per config under
 *        .teamcity/ProteoWizard/buildTypes/ (project-level parameters in
 *        .teamcity/ProteoWizard/project-config.xml), and none of them appear
 *        here.
 *    The parent's versioned settings prefer settings from VCS (buildSettings
 *    PREFER_VCS in project-config.xml), so its builds - PR builds included -
 *    run with the settings on their own branch: change a config in the PR that
 *    needs the change, not in the UI.
 *
 *    The footgun: TeamCity reads Kotlin settings from settings.kts at the root
 *    of `.teamcity/`. If anyone switches the PARENT ProteoWizard project's
 *    versioned settings from XML to Kotlin format against this same repo,
 *    TeamCity will read this file as the whole project definition - i.e. as a
 *    project containing this subproject's configs and nothing else. Migrating
 *    the parent means exporting the live config first ("Download settings in
 *    Kotlin format") and merging this in, never pointing the parent at this
 *    file as-is.
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
    // itself generates. This file stays a manifest: a config change then shows up as a
    // diff to that config alone rather than to a file every other config also touches.
    //
    // Those files are .kt, NOT .kts, and that is load-bearing. A .kts script gets an
    // implicit class named after the file, so buildTypes/OspreyLinuxNet.kts would declare
    // a CLASS OspreyLinuxNet colliding with the object inside it; `import buildTypes.*`
    // then binds the script class and this line fails to compile with "Classifier
    // 'OspreyLinuxNet' does not have a companion object, and thus must be initialized
    // here". settings.kts is the script; everything it references is a source file.
    buildType(NativeShimsWindows)
    buildType(OspreyLinuxNet)
}
