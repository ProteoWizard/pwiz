import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildSteps.script

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
 * 4. DSL VERSION. `version` below and teamcity.dsl.version in pom.xml must both
 *    match the server. If the server rejects this file, let TeamCity generate
 *    the pom.xml/settings.kts pair itself (enable Versioned Settings in "use
 *    settings from TeamCity" mode), then re-apply the buildType below.
 * =============================================================================
 */

version = "2025.03"

project {
    description = "Build configs defined in-repo via Kotlin DSL, rather than in the TeamCity UI."

    buildType(NativeShimsWindows)
}

/*
 * Builds the three native Windows binaries that cannot be cross-compiled, and
 * publishes them so a Linux/Wine container can assemble a Windows payload
 * without a Windows C++ toolchain.
 *
 * MascotShim.dll and MobilionShim.dll link vendor DLLs that export C++ classes
 * returning MSVC STL types by value (msparser: std::string/std::vector;
 * MBI_SDK: std::shared_ptr<std::vector<double>>), so they cross the MSVC C++
 * ABI and cannot be built by mingw-w64 - it would link cleanly and corrupt
 * memory. Hardklor.exe has no vendor SDK and is cross-compilable in principle;
 * it rides along so all three ship as one bundle.
 *
 * Both shim sources are effectively frozen (1 and 2 commits respectively), so
 * this config runs rarely: only a change under one of the three shim
 * directories triggers it.
 */
object NativeShimsWindows : BuildType({
    id = AbsoluteId("ProteoWizard_NativeShimsWindows")
    name = "Native shims (Windows x86_64)"
    description = "MascotShim.dll, MobilionShim.dll and Hardklor.exe, prebuilt for consumers that cannot compile them (see scripts/misc/tcbuild-native-shims.bat)"

    // Consumers take an artifact dependency on this and pass the unpacked
    // directory as -p:PwizPrebuiltNativeShimDir=<dir>. The vendor companions
    // (msparser.dll, MBI_SDK.dll) are in the bundle so a consumer needs no
    // vendor-archive extraction of its own.
    artifactRules = "artifacts/native-shims => native-shims.zip"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        script {
            name = "Build native shims"
            scriptContent = """scripts\misc\tcbuild-native-shims.bat Release"""
        }
    }

    requirements {
        // All three need the VC++ toolchain: cmake drives MSVC for MascotShim,
        // and the other two are .vcxproj builds. There is no agent property for
        // "has the C++ workload" specifically, so this narrows to a Windows
        // agent with VS 2022 build tools and lets the script make the exact
        // check - tcbuild-native-shims.bat runs vswhere with
        // -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 and fails
        // with a message naming the missing component.
        //
        // Worth replacing with whatever the existing Windows C++ configs
        // (bt83, bt209) require, once those are visible - they build the cpp
        // tree and so already encode the right answer for this agent pool.
        contains("teamcity.agent.jvm.os.name", "Windows")
        exists("MSBuildTools17.0_x64_Path")
    }

    // No triggers block - see note 3 in the file header.
})
