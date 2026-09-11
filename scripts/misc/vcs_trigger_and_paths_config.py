targets = {}

# Retired C++ build configurations. The C++ tree lives in ProteoWizard/pwiz-cpp now and these
# configurations build from that repo; nothing in this repo may trigger them. Kept here,
# commented out, so the ids stay findable when the TeamCity side is re-pointed.
#targets['CoreWindows'] = {'master': {"bt83": "Core Windows x86_64", "bt36": "Core Windows x86", "bt143": "Core Windows x86_64 (no vendor DLLs)"}}
#targets['CoreWindowsDebug'] = {'master': {"bt84": "Core Windows x86_64 debug", "bt75": "Core Windows debug"}}
#targets['CoreLinux'] = {'master': {"bt17": "Core Linux x86_64"}}
#targets['SkylineRelease'] = {'master': {"bt209": "Skyline master and PRs (Windows x86_64)",
#                                        "ProteoWizard_WindowsX8664msvcProfessionalSkylineResharperChecks": "Skyline code inspection",
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
# Windows-only vendor reference) only surfaces on the next unrelated Linux trigger.
targets['CoreWindowsNet'] = {'master': {"ProteoWizard_CoreWindowsNet": "Core Windows .NET"}}
targets['CoreLinuxNet'] = {'master': {"ProteoWizard_CoreLinuxNet": "Core Linux .NET"}}
targets['CoreNet'] = merge(targets['CoreWindowsNet'], targets['CoreLinuxNet'])

# Skyline builds and tests run via pwiz_tools/Skyline/build.bat (dotnet build + TestRunner;
# CodeInspection runs inside Test.csproj). Skyline release branches that predate the .NET
# port (skyline_26_1 and older) live in ProteoWizard/pwiz-cpp, so there is no 'release'
# target here yet: add one when the first .NET-based release branch is cut.
targets['SkylineWindowsNet'] = {'master': {"ProteoWizard_SkylineWindowsNet": "Skyline Windows .NET"}}
targets['Skyline'] = targets['SkylineWindowsNet']

targets['SkylineWithTestConnected'] = \
{
    'master':
    {
        # TestConnected tests still run through the retired cpp/MSVC chain
        # (ProteoWizard_SkylineMasterAndPRsTestConnectedTests depends on bt209); re-enable
        # once a .NET TestConnected config exists.
        #"ProteoWizard_SkylineMasterAndPRsTestConnectedTests": "Skyline master and PRs TestConnected tests"
        "ProteoWizard_SkylineWindowsNet": "Skyline Windows .NET"
    }
}

targets['Container'] = \
{
    'master':
    {
        # The Wine container is built from the .NET msconvert/Skyline now; enable once the
        # config is green on master.
        #"ProteoWizard_ProteoWizardAndSkylineDockerContainerNetWineX8664": "ProteoWizard and Skyline Docker container .NET (Wine x86_64)"
    }
}

targets['OspreyWindowsNet'] = {'master': {"ProteoWizard_OspreyWindowsNet": "Osprey Windows .NET"}}

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
    # Native shims: these three dirs hold C++ that only a Windows agent with the VC++ toolchain
    # can build, so they get their own config. Each entry ALSO triggers the config that actually
    # tests the result, so a shim change still runs BiblioSpec's Mascot tests / Mobilion.Tests /
    # Skyline's Hardklor-Bullseye tests rather than only producing a binary nobody exercised.
    # These must stay ABOVE the broader pwiz/ and pwiz_tools/ patterns below: first match wins,
    # so a later-but-broader pattern would swallow them.
    ("pwiz_tools/BiblioSpec/native/MascotShim/.*", merge(targets['NativeShims'], targets['CoreNet'])),
    ("pwiz/data/vendor_readers/Mobilion/MobilionShim/.*", merge(targets['NativeShims'], targets['CoreNet'])),
    ("scripts/misc/tcbuild-native-shims.bat", targets['NativeShims']),
    # pwiz library: pwiz/src + pwiz/test + the data fixtures beside them. Skyline consumes the
    # library through ProjectReferences, but (as before the hoist) library-only edits trigger
    # only the Core builds; the Skyline run happens on the next Skyline-side change.
    ("pwiz/.*", targets['CoreNet']),
    # MSBuild targets shared by pwiz and Skyline (PwizVersion, ExtractTestData, vendor SDK pins).
    ("build/.*", merge(targets['CoreNet'], targets['Skyline'])),
    ("vendor-archives/.*", targets['CoreNet']),
    # 7za/bsdtar/msparser/zlib/expat: used by the pwiz build and by Skyline's Hardklor build.
    ("libraries/.*", merge(targets['CoreNet'], targets['Skyline'])),
    ("examples/.*", targets['CoreNet']),
    ("example_data/.*", targets['CoreNet']),
    ("scripts/installer/.*", targets['CoreNet']),
    ("scripts/.*", targets['All']),
    ("Pwiz.sln", targets['CoreNet']),
    ("Directory.Build.*", targets['CoreNet']),
    ("global.json", targets['All']),
    # pwiz tools that Skyline bundles (BlibBuild/BlibFilter, msconvert, bullseye-sharp).
    ("pwiz_tools/BiblioSpec/.*", merge(targets['CoreNet'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Commandline/.*", merge(targets['CoreNet'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/BullseyeSharp/.*", merge(targets['CoreNet'], targets['Skyline'])),
    ("pwiz_tools/MSConvertGUI/.*", targets['CoreNet']),
    ("pwiz_tools/SeeMS/.*", targets['CoreNet']),
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
    ("pwiz_tools/Osprey/.*", targets['OspreyWindowsNet']),
    ("pwiz_tools/.*", targets['All']),
    (".*\\.bat", targets['Windows']),
    (".*\\.sh", targets['Linux'])
]
