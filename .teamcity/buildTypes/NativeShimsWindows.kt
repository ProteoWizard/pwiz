package buildTypes

import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildSteps.script

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
    // TeamCity requires a build config id to be prefixed with its parent project id,
    // hence the ProteoWizard_VersionedConfigs_ prefix. Spelled out rather than left as a
    // relative id so the value the trigger map has to match is greppable from here.
    id = AbsoluteId("ProteoWizard_VersionedConfigs_NativeShimsWindows")
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
