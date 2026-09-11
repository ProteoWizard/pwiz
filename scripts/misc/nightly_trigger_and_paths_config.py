
targets = {}

targets['Skyline'] = \
{
    'master':
    {
        #"bt210": "Skyline master and PRs (Windows x86_64 debug, with code coverage)",
        # On the .NET 8 port branch, perf + tutorial tests run via pwiz_tools/Skyline/tc-perftests.bat
        # (dotnet build + Stage-Tests.ps1 + TestRunner perftests=on), not the old cpp/MSVC config.
        # Point the nightly master trigger at the net8 perf/tutorial build config instead.
        #"ProteoWizard_SkylinePrPerfAndTutorialTestsWindowsX8664": "Skyline PR Perf and Tutorial tests (Windows x86_64)"
        "ProteoWizard_SkylineWindowsNetPerfTutorialTests": "Skyline Windows .NET Perf and Tutorial tests"
    },
    'release':
    {
        # Skyline release branches that predate the .NET port live in ProteoWizard/pwiz-cpp;
        # add the .NET release perf/tutorial config here when the first .NET release branch is cut.
    }
}

targets['OspreyWindowsNetPerfRegressionTests'] = {} # Nightly perf tests for Osprey disbled until PR cadence slows down; {'master': {"ProteoWizard_OspreyWindowsNetPerfRegressionTests": "Osprey Windows .NET Perf Regression Tests"}}

# The C++ nightlies (Core, the Wine container, Bumbershoot) build from ProteoWizard/pwiz-cpp
# and are not triggered from this repo.
targets['Core'] = {}
targets['Container'] = {}

targets['All'] = merge(targets['Skyline'])

# Patterns are processed in order. If a path matches multiple patterns, only the first pattern will trigger. For example,
# "pwiz_tools/Bumbershoot/Jamfile.jam" matches both "pwiz_tools/Bumbershoot/.*" and "pwiz_tools/.*", but will only trigger "Bumbershoot" targets
matchPaths = [
    (".*/smartBuildTrigger.py", {}),
    (".*/ai/.*", {}),
    ("libraries/.*", targets['All']),
    ("pwiz/.*", targets['All']),
    ("build/.*", targets['All']),
    ("scripts/.*", targets['All']),
    ("pwiz_tools/BiblioSpec/.*", merge(targets['Core'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Commandline/.*", merge(targets['Core'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Skyline/.*", merge(targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Shared/.*", merge(targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Osprey/.*", targets['OspreyWindowsNetPerfRegressionTests']),
    ("pwiz_tools/.*", targets['All'])
]
