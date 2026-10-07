targets = {}

# Retired C++ build configurations. The C++ tree lives in ProteoWizard/pwiz-cpp now and these
# configurations build from that repo; nothing in this repo may trigger them. Kept here,
# commented out, so the ids stay findable when the TeamCity side is re-pointed. bt83, bt17 and
# bt209 are not among them: they took over the .NET builds below.
#targets['CoreWindows'] = {'master': {"bt36": "Core Windows x86", "bt143": "Core Windows x86_64 (no vendor DLLs)"}}
#targets['CoreWindowsDebug'] = {'master': {"bt84": "Core Windows x86_64 debug", "bt75": "Core Windows debug"}}
#targets['SkylineRelease'] = {'master': {"ProteoWizard_WindowsX8664msvcProfessionalSkylineResharperChecks": "Skyline code inspection",
#                                        "ProteoWizard_SkylineMasterAndPRsTestConnectedTests": "Skyline master and PRs TestConnected tests"},
#                             'release': {"ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional": "Skyline Release Branch x86_64",
#                                         "ProteoWizard_SkylineReleaseBranchCodeInspection": "Skyline release code inspection",
#                                         "ProteoWizard_SkylineReleaseTestConnectedTests": "Skyline release TestConnected tests"}}
#targets['SkylineDebug'] = {'master': {"bt210": "Skyline master and PRs (Windows x86_64 debug)"}}
#targets['ContainerWine'] = {'master': {"ProteoWizardAndSkylineDockerContainerWineX8664": "ProteoWizard and Skyline Docker container (Wine x86_64)"},
#                            'release': {"ProteoWizard_ProteoWizardAndSkylineReleaseBranchDockerContainerWineX8664": "ProteoWizard and Skyline (release branch) Docker container (Wine x86_64)"}}
#targets['Bumbershoot'] = {'master': {"Bumbershoot_Windows_X86_64": "Bumbershoot Windows x86_64",
#                                     "ProteoWizard_Bumbershoot_Windows_X86": "Bumbershoot Windows x86",
#                                     "ProteoWizard_Bumbershoot_Linux_x86_64": "Bumbershoot Linux x86_64"}}

# The pwiz library + tools (Pwiz.sln, built by build.bat on Windows and build.sh on Linux:
# dotnet restore + build + test). Both platforms build the same C# sources from the same
# tree, so any change that warrants a Windows .NET build warrants the Linux one too —
# otherwise a cross-platform regression (a hardcoded 7za.exe, a backslash path, a
# Windows-only vendor reference) only surfaces on the next unrelated Linux trigger. These are the
# historic Core x86_64 configs, which took over the steps of the temporary Core Windows/Linux
# .NET configs in Versioned Configs.
targets['CoreWindowsNet'] = {'master': {"bt83": "Core Windows x86_64"}}
targets['CoreLinuxNet'] = {'master': {"bt17": "Core Linux x86_64"}}
targets['CoreNet'] = merge(targets['CoreWindowsNet'], targets['CoreLinuxNet'])

# Skyline builds and tests run via pwiz_tools/Skyline/build.bat (dotnet build + TestRunner), in
# bt209, which took over the steps of the temporary Skyline Windows .NET config.
# Both inspections run in-build too: the custom CodeInspectionTest as a test inside
# Test.csproj, and ReSharper as the tcinspect.ps1 build step that replaces the standalone
# "Skyline Code Inspection" config. Skyline release branches that predate the .NET port
# (skyline_26_1 and older) live in ProteoWizard/pwiz-cpp, so there is no 'release' target
# here yet: add one when the first .NET-based release branch is cut.
targets['SkylineWindowsNet'] = {'master': {"bt209": "Skyline master and PRs (Windows x86_64)"}}
targets['Skyline'] = targets['SkylineWindowsNet']

# Configs that report more than one GitHub status, keyed by config id. bt209
# runs ReSharper as a build step and publishes that verdict under the context the standalone
# inspection config publishes, so when this script skips the build it has to report the paired
# context as well. Otherwise the check is simply absent on commits that do not rebuild Skyline,
# and a PR sits with one green check and one that never arrives.
#
# This does nothing YET. bt209 is nested under 'master' above, and a
# target reachable only through a merge()'d matchPaths entry never enters
# notBuildingDueToChangedFiles at all - it is dropped silently, so today the build status and
# the inspection status are consistently absent together. Un-nesting it starts reporting the
# build's skip status, and this is what keeps the inspection one alongside it rather than
# leaving a check that never arrives.
#
# These are WHOLE context strings, posted verbatim - unlike the targets above, which get a
# "teamcity - " prefix. TeamCity's commit status publisher posts this particular check as a
# bare "Skyline code inspection" with no prefix, so prefixing it here would create a second,
# permanently stale check rather than keeping the existing one in sync. (That mismatch is why
# the standalone config's skip status and its real status have always been two different
# checks on GitHub; nothing here changes that for the standalone config.)
extraStatuses = \
{
    "bt209": ["Skyline code inspection"]
}

targets['SkylineWithTestConnected'] = \
{
    'master':
    {
        # TestConnected tests are not triggered: their config
        # (ProteoWizard_SkylineMasterAndPRsTestConnectedTests, which depends on bt209) still has
        # its cpp/MSVC steps. Re-enable once it runs the .NET tests.
        #"ProteoWizard_SkylineMasterAndPRsTestConnectedTests": "Skyline master and PRs TestConnected tests"
        "bt209": "Skyline master and PRs (Windows x86_64)"
    }
}

targets['Container'] = \
{
    'master':
    {
        # The .NET container. It takes the payload from this chain: snapshot + artifact
        # dependencies on Core Windows .NET (ProteoWizard-WithVendorSdks-Setup*.exe) and
        # Skyline Windows .NET (SkylineTester.zip), so it validates the artifacts this repo
        # actually produces. Note the id carries the ProteoWizard_ prefix while the retired
        # cpp one above does not - both are as TeamCity has them, and smartBuildTrigger.py
        # POSTs the key verbatim as <buildType id="...">.
        "ProteoWizard_ProteoWizardAndSkylineDockerContainerNetWineX8664": "ProteoWizard and Skyline Docker container .NET (Wine x86_64)"
    }
}

targets['OspreyWindowsNet'] = {'master': {"ProteoWizard_OspreyWindowsNet": "Osprey Windows .NET"}}
targets['OspreyLinuxNet'] = {'master': {"ProteoWizard_VersionedConfigs_OspreyLinuxNet": "Osprey Linux .NET"}}
# Both platforms build the same net10.0 sources from the same tree, so a change that warrants
# the Windows Osprey build warrants the Linux one too - otherwise a portability regression (a
# backslash path literal, a Windows-only API) only surfaces on the next unrelated Linux
# trigger. Same reasoning as targets['CoreNet'] above.
targets['Osprey'] = merge(targets['OspreyWindowsNet'], targets['OspreyLinuxNet'])

# MascotShim.dll / MobilionShim.dll / Hardklor.exe are native Windows binaries COMPILED during
# the build rather than vendored, and the first two link vendor DLLs that export C++ classes
# returning MSVC STL types by value - so they cannot be cross-compiled, and a Linux/Wine
# container cannot produce them. This config builds them once and publishes them as artifacts;
# see scripts/misc/tcbuild-native-shims.bat. Config id must match .teamcity/settings.kts, which
# is what smartBuildTrigger POSTs to the build queue.
targets['NativeShims'] = {'master': {"ProteoWizard_VersionedConfigs_NativeShimsWindows": "Native shims (Windows x86_64)"}}

targets['All'] = merge(targets['CoreNet'], targets['SkylineWithTestConnected'], targets['OspreyWindowsNet'], targets['Container'])
targets['Windows'] = merge(targets['CoreWindowsNet'], targets['SkylineWithTestConnected'], targets['OspreyWindowsNet'], targets['Container'])
targets['Linux'] = targets['CoreLinuxNet']

# Patterns are processed in order. If a path matches multiple patterns, only the first pattern will trigger. For example,
# "pwiz_tools/BiblioSpec/src/BlibBuild/BlibBuild.csproj" matches both "pwiz_tools/BiblioSpec/.*" and "pwiz_tools/.*", but
# will only trigger the "pwiz_tools/BiblioSpec/.*" targets.
matchPaths = [
    (".*/smartBuildTrigger.py", {}),
    (".*/vcs_trigger_and_paths_config.py", {}),
    (".*/ai/.*", {}),
    # TeamCity versioned settings: every UI edit to a config in the Versioned Configs
    # subproject is committed back to this branch by TeamCity itself, and those commits carry
    # nothing a build could test. Without this they fall through to no pattern at all, which
    # happens to be a no-op today but only by accident - anything added below with a broad
    # enough pattern would start rebuilding the world on every settings tweak. Say it out loud
    # instead, next to the other build-plumbing no-ops.
    #
    # Not hypothetical: the run of moves into that subproject produced six commits in eight
    # minutes, superseding builds mid-flight and leaving Core Linux .NET and Skyline Windows
    # .NET reporting cancellations and an "Error while applying patch" against a commit whose
    # only delta was a generated .kts patch file.
    (".*\\.teamcity/.*", {}),
    # Native shims: these three dirs hold C++ that only a Windows agent with the VC++ toolchain
    # can build, so they get their own config. Each entry ALSO triggers the config that actually
    # tests the result, so a shim change still runs BiblioSpec's Mascot tests / Mobilion.Tests /
    # Skyline's Hardklor-Bullseye tests rather than only producing a binary nobody exercised.
    # These must stay ABOVE the broader pwiz/ and pwiz_tools/ patterns below: first match wins,
    # so a later-but-broader pattern would swallow them.
    ("pwiz_tools/BiblioSpec/native/MascotShim/.*", merge(targets['NativeShims'], targets['CoreNet'])),
    ("pwiz/data/vendor_readers/Mobilion/MobilionShim/.*", merge(targets['NativeShims'], targets['CoreNet'])),
    ("scripts/misc/tcbuild-native-shims.bat", targets['NativeShims']),
    # pwiz library: the modules under pwiz/ + the data fixtures beside them. Skyline consumes
    # the library through ProjectReferences, but (as before the hoist) library-only edits
    # trigger only the Core builds and the container; the Skyline run happens on the next
    # Skyline-side change. The container IS included because its payload is the pwiz
    # installer, so a library change is exactly what needs validating there.
    ("pwiz/.*", merge(targets['CoreNet'], targets['Container'])),
    # MSBuild targets shared by pwiz and Skyline (PwizVersion, ExtractTestData, vendor SDK pins).
    ("build/.*", merge(targets['CoreNet'], targets['Skyline'], targets['Container'])),
    # build/vendor-sdk-pins.json points VendorSdkLoader at these archives, so an archive swap
    # changes what every .NET install resolves at run time.
    ("vendor-archives/.*", merge(targets['CoreNet'], targets['Container'])),
    # 7za/bsdtar/msparser/zlib/expat: used by the pwiz build and by Skyline's Hardklor build.
    ("libraries/.*", merge(targets['CoreNet'], targets['Skyline'], targets['Container'])),
    ("examples/.*", targets['CoreNet']),
    ("example_data/.*", targets['CoreNet']),
    ("scripts/installer/.*", merge(targets['CoreNet'], targets['Container'])),
    # The container's msconvert sweep, which nothing else runs or builds. Must stay above the
    # generic scripts/ pattern: that one is targets['All'], so without this a shell-script edit
    # rebuilds everything to exercise one container step.
    ("scripts/container/.*", targets['Container']),
    ("scripts/.*", targets['All']),
    ("Pwiz.sln", merge(targets['CoreNet'], targets['Container'])),
    ("Directory.Build.*", merge(targets['CoreNet'], targets['Container'])),
    ("global.json", targets['All']),
    # pwiz tools that Skyline bundles (BlibBuild/BlibFilter, msconvert, bullseye-sharp).
    ("pwiz_tools/BiblioSpec/.*", merge(targets['CoreNet'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Commandline/.*", merge(targets['CoreNet'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/BullseyeSharp/.*", merge(targets['CoreNet'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/MSConvertGUI/.*", merge(targets['CoreNet'], targets['Container'])),
    ("pwiz_tools/SeeMS/.*", merge(targets['CoreNet'], targets['Container'])),
    ("pwiz_tools/Skyline/TestConnected/.*", merge(targets['SkylineWithTestConnected'], targets['Container'])),
    ("pwiz_tools/Skyline/.*Ardia.*", merge(targets['SkylineWithTestConnected'], targets['Container'])),
    ("pwiz_tools/Skyline/.*Koina.*", merge(targets['SkylineWithTestConnected'], targets['Container'])),
    ("pwiz_tools/Skyline/.*Panorama.*", merge(targets['SkylineWithTestConnected'], targets['Container'])),
    ("pwiz_tools/Skyline/.*Unifi.*", merge(targets['SkylineWithTestConnected'], targets['Container'])),
    ("pwiz_tools/Skyline/.*WatersConnect.*", merge(targets['SkylineWithTestConnected'], targets['Container'])),
    ("pwiz_tools/Skyline/.*DataSource.*", merge(targets['SkylineWithTestConnected'], targets['Container'])),
    ("pwiz_tools/Skyline/Executables/Hardklor/.*", merge(targets['NativeShims'], targets['Skyline'])),
    ("pwiz_tools/Skyline/.*", merge(targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Shared/CommonMsData/RemoteApi/.*", merge(targets['SkylineWithTestConnected'], targets['Container'])),
    # pwiz compiles Shared/zedgraph + Shared/MSGraph in place and links Shared/Lib binaries.
    ("pwiz_tools/Shared/.*", merge(targets['Skyline'], targets['CoreNet'], targets['Container'])),
    ("pwiz_tools/Osprey/.*", targets['Osprey']),
    ("pwiz_tools/.*", targets['All']),
    (".*\\.bat", targets['Windows']),
    (".*\\.sh", targets['Linux'])
]
