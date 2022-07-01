
targets = {}

targets['Skyline'] = \
{
    'master':
    {
        "bt210": "Skyline master and PRs (Windows x86_64 debug, with code coverage)"
        ,"ProteoWizard_SkylinePrPerfAndTutorialTestsWindowsX8664": "Skyline PR Perf and Tutorial tests (Windows x86_64)"
    },
    'release':
    {
        "ProteoWizard_SkylineReleasePerfAndTutorialTestsWindowsX8664": "Skyline Release Perf and Tutorial tests (Windows x86_64)"
    }
}

targets['Core'] = {}
targets['Container'] = {}
targets['Bumbershoot'] = {}

targets['All'] = merge(targets['Skyline'])

# Patterns are processed in order. If a path matches multiple patterns, only the first pattern will trigger. For example,
# "pwiz_tools/Bumbershoot/Jamfile.jam" matches both "pwiz_tools/Bumbershoot/.*" and "pwiz_tools/.*", but will only trigger "Bumbershoot" targets
matchPaths = [
    (".*/smartBuildTrigger.py", {}),
    ("libraries/.*", targets['All']),
    ("pwiz/.*", targets['All']),
    ("pwiz_aux/.*", targets['All']),
    ("scripts/.*", targets['All']),
    ("pwiz_tools/BiblioSpec/.*", merge(targets['Core'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Bumbershoot/.*", targets['Bumbershoot']),
    ("pwiz_tools/Skyline/.*", merge(targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Topograph/.*", targets['Skyline']),
    ("pwiz_tools/Shared/.*", merge(targets['Skyline'], targets['Bumbershoot'], targets['Container'])),
    ("pwiz_tools/.*", targets['All']),
    ("Jamroot.jam", targets['All'])
]
