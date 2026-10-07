
targets = {}
targets['CoreWindowsRelease'] = \
{
    'master':
    {
        # NET8-PORT TEMP (restore before merge): don't trigger the cpp Core x86_64
        # builds during net8 iteration; they fan out from any pwiz_tools/ edit via All.
        # bt83 is not one of them any more: it runs the pwiz-sharp build and is
        # triggered through targets['CoreWindowsNet'] below.
        #"bt36": "Core Windows x86"
        #,"bt143": "Core Windows x86_64 (no vendor DLLs)"
    },
    'release':
    {
        # bt83 will be triggered by ProteoWizard_ProteoWizardAndSkylineReleaseBranchDockerContainerWineX8664
    }
}
#targets['CoreWindowsDebug'] = \
#{
#    "bt84": "Core Windows x86_64 debug"
#    ,"bt75": "Core Windows debug"
#}
#targets['CoreWindows'] = merge(targets['CoreWindowsRelease'], targets['CoreWindowsDebug'])
targets['CoreWindows'] = targets['CoreWindowsRelease']
# bt17 runs the pwiz-sharp build now and is triggered through targets['CoreLinuxNet'] below.
targets['CoreLinux'] = {'master': {}}

# pwiz-sharp is the .NET C# port; the corresponding TeamCity builds run
# `pwiz-sharp/tcbuild.bat` on Windows and `pwiz-sharp/tcbuild.sh` on Linux (dotnet restore +
# build + test). Independent from the cpp build configs above — only files under
# pwiz-sharp/ should trigger them. These are the historic Core x86_64 configs, which took
# over the steps of the temporary Core Windows/Linux .NET configs in Versioned Configs.
targets['CoreWindowsNet'] = {'master': {"bt83": "Core Windows x86_64"}}
targets['CoreLinuxNet'] = {'master': {"bt17": "Core Linux x86_64"}}
# Both platforms build the same C# sources from the same tree, so any change that warrants a
# Windows .NET build warrants the Linux one too — otherwise a cross-platform regression (a
# hardcoded 7za.exe, a backslash path, a Windows-only vendor reference) only surfaces on the
# next unrelated Linux trigger.
targets['CoreNet'] = merge(targets['CoreWindowsNet'], targets['CoreLinuxNet'])

targets['SkylineRelease'] = \
{
    'master':
    {
        "ProteoWizard_WindowsX8664msvcProfessionalSkylineResharperChecks": "Skyline code inspection" # depends on "bt209",
        ,"bt209": "Skyline master and PRs (Windows x86_64)"
        #,"bt19": "Skyline master and PRs (Windows x86)"
    },
    'release':
    {
        "ProteoWizard_SkylineReleaseBranchCodeInspection": "Skyline release code inspection" # depends on "ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional",
        ,"ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional": "Skyline Release Branch x86_64"
        #,"ProteoWizard_WindowsX86SkylineReleaseBranchMsvcProfessional": "Skyline Release Branch x86"
    }
}

#targets['SkylineDebug'] = \
#{
#    "bt210": "Skyline master and PRs (Windows x86_64 debug)"
#    ,"bt87": "Skyline master and PRs (Windows x86 debug)"
#}
#targets['Skyline'] = merge(targets['SkylineRelease'], targets['SkylineDebug'])

# Skyline builds and tests run via pwiz_tools/Skyline/tcbuild.bat (dotnet restore + build +
# test), which bt209 took over from the temporary Skyline Windows .NET config. Both inspections
# run in-build too: the custom CodeInspectionTest as a test inside Test.csproj, and ReSharper as
# the tcinspect.ps1 build step that replaces the standalone "Skyline Code Inspection" config.
# So bt209 is triggered directly, rather than through that config's snapshot dependency as in
# targets['SkylineRelease'] above.
targets['SkylineWindowsNet'] = {'master': {"bt209": "Skyline master and PRs (Windows x86_64)"}}
targets['Skyline'] = targets['SkylineWindowsNet']

# Configs that report more than one GitHub status, keyed by config id. bt209 runs ReSharper as
# a build step and publishes that verdict under the context the standalone inspection config
# publishes, so when this script skips the build it has to report the paired context as well.
# Otherwise the check is simply absent on commits that do not rebuild Skyline, and a PR sits
# with one green check and one that never arrives.
#
# This does nothing YET. bt209 is nested under 'master' above, and a
# target reachable only through a merge()'d matchPaths entry never enters
# notBuildingDueToChangedFiles at all - it is dropped silently, so today the build status and
# the inspection status are consistently absent together. Un-nesting it (which the NET8-PORT
# TEMP notes below say happens before this branch merges) starts reporting the build's skip
# status, and this is what keeps the inspection one alongside it rather than leaving a check
# that never arrives.
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
        # NET8-PORT TEMP (restore before merge): don't trigger TestConnected or Skyline
        # code inspection from the net8 port PR (the net8 build runs inspection in-build).
        #"ProteoWizard_SkylineMasterAndPRsTestConnectedTests": "Skyline master and PRs TestConnected tests" # depends on "bt209",
        #,"ProteoWizard_WindowsX8664msvcProfessionalSkylineResharperChecks": "Skyline code inspection" # depends on "bt209",
        # bt209 runs the .NET Skyline build now and is triggered through targets['Skyline'],
        # so it is not repeated here. The native shims are unaffected - MobilionShim and
        # MascotShim live under pwiz-sharp/ and are built by their own csproj/CMake inside
        # Core Windows x86_64 (bt83), and the pwiz-sharp/.* rule already covers their sources.
    },
    'release':
    {
        "ProteoWizard_SkylineReleaseTestConnectedTests": "Skyline release TestConnected tests" # depends on "ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional",
        ,"ProteoWizard_SkylineReleaseBranchCodeInspection": "Skyline release code inspection" # depends on "ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional",
        ,"ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional": "Skyline Release Branch x86_64"
    }
}

targets['Container'] = \
{
    'master':
    {
        # The net10 container. It takes the payload from this chain: snapshot + artifact
        # dependencies on Core Windows .NET (ProteoWizard-WithVendorSdks-Setup*.exe) and
        # Skyline Windows .NET (SkylineTester.zip), so it validates the artifacts this branch
        # actually produces. Note the id here carries the ProteoWizard_ prefix while the cpp
        # one below does not - both are as TeamCity has them, and smartBuildTrigger.py POSTs
        # the key verbatim as <buildType id="...">.
        "ProteoWizard_ProteoWizardAndSkylineDockerContainerNetWineX8664": "ProteoWizard and Skyline Docker container .NET (Wine x86_64)"
        # NET8-PORT TEMP (restore before merge): don't trigger the Wine x86_64 container during net8 iteration
        #,"ProteoWizardAndSkylineDockerContainerWineX8664": "ProteoWizard and Skyline Docker container (Wine x86_64)"
    },
    'release':
    {
        "ProteoWizard_ProteoWizardAndSkylineReleaseBranchDockerContainerWineX8664": "ProteoWizard and Skyline (release branch) Docker container (Wine x86_64)"
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

targets['BumbershootRelease'] = \
{
    'master':
    {
        # NET8-PORT TEMP (restore before merge): don't trigger Bumbershoot from the net8 port PR
        #"Bumbershoot_Windows_X86_64": "Bumbershoot Windows x86_64"
        #,"ProteoWizard_Bumbershoot_Windows_X86": "Bumbershoot Windows x86"
    }
}
# NET8-PORT TEMP (restore before merge): don't trigger Bumbershoot from the net8 port PR
#targets['BumbershootLinux'] = {'master': {"ProteoWizard_Bumbershoot_Linux_x86_64": "Bumbershoot Linux x86_64"}}
targets['BumbershootLinux'] = {'master': {}}
targets['Bumbershoot'] = merge(targets['BumbershootRelease'], targets['BumbershootLinux'])

targets['Core'] = merge(targets['CoreWindows'], targets['CoreLinux'])
targets['All'] = merge(targets['Core'], targets['SkylineWithTestConnected'], targets['Bumbershoot'], targets['Container'])
targets['Windows'] = merge(targets['CoreWindows'], targets['SkylineWithTestConnected'], targets['BumbershootRelease'], targets['Container'])
targets['Linux'] = merge(targets['CoreLinux'], targets['BumbershootLinux'])

# Patterns are processed in order. If a path matches multiple patterns, only the first pattern will trigger. For example,
# "pwiz_tools/Bumbershoot/Jamfile.jam" matches both "pwiz_tools/Bumbershoot/.*" and "pwiz_tools/.*", but will only trigger "Bumbershoot" targets
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
    # These must stay ABOVE the broader pwiz-sharp/ and pwiz_tools/Skyline/ patterns below:
    # first match wins, so a later-but-broader pattern would swallow them.
    ("pwiz-sharp/Tools/BiblioSpec/native/MascotShim/.*", merge(targets['NativeShims'], targets['CoreNet'])),
    ("pwiz-sharp/pwiz/src/Vendor/Mobilion/MobilionShim/.*", merge(targets['NativeShims'], targets['CoreNet'])),
    # pwiz-sharp: standalone .NET 8 port. Builds run via `pwiz-sharp/build.bat`. Match this
    # before the generic libraries/scripts/.bat patterns below so changes under pwiz-sharp/
    # don't trigger the cpp Core/Skyline/Bumbershoot chain.
    #
    # Container IS included: the net10 container's payload is pwiz-sharp's own installer, so a
    # pwiz-sharp change is exactly what needs validating there. Only the net10 container is in
    # targets['Container']['master'], so this does not pull in the cpp one.
    ("pwiz-sharp/.*", merge(targets['CoreNet'], targets['Container'])),
    ("libraries/.*", targets['All']),
    ("pwiz/.*", targets['All']),
    # Vendor SDK archives are pwiz-sharp inputs as well as cpp ones: build/vendor-sdk-pins.json
    # points VendorSdkLoader at the Agilent and ABI archives in here, so an archive swap changes
    # what every .NET install resolves at run time. targets['All'] alone carries no .NET config,
    # which would leave that untested; merged rather than replaced so the cpp chain still fires
    # if it is ever uncommented.
    ("pwiz_aux/msrc/utility/vendor_api_.*\\.7z", merge(targets['All'], targets['CoreNet'])),
    ("pwiz_aux/.*", targets['All']),
    ("scripts/wix/.*", targets['CoreWindows']),
    ("scripts/misc/tcbuild-native-shims.bat", targets['NativeShims']),
    # The container's msconvert sweep, which nothing else runs or builds. Must stay above the
    # generic scripts/ pattern: that one is targets['All'], so without this a shell-script edit
    # rebuilds the whole cpp + Skyline + Bumbershoot chain to exercise one container step.
    ("scripts/container/.*", targets['Container']),
    ("scripts/.*", targets['All']),
    ("pwiz_tools/BiblioSpec/.*", merge(targets['Core'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Bumbershoot/.*", targets['Bumbershoot']),
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
    ("pwiz_tools/Shared/.*", merge(targets['Skyline'], targets['BumbershootRelease'], targets['Container'])),
    ("pwiz_tools/Osprey/.*", targets['Osprey']),
    ("pwiz_tools/.*", targets['All']),
    ("Jamroot.jam", targets['All']),
    (".*\\.bat", targets['Windows']),
    (".*\\.sh", targets['Linux'])
]

