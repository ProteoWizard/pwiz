
targets = {}
targets['CoreWindowsRelease'] = \
{
    "bt83": "Core Windows x86_64"
    ,"bt36": "Core Windows x86"
    ,"bt143": "Core Windows x86_64 (no vendor DLLs)"
}
#targets['CoreWindowsDebug'] = \
#{
#    "bt84": "Core Windows x86_64 debug"
#    ,"bt75": "Core Windows debug"
#}
#targets['CoreWindows'] = merge(targets['CoreWindowsRelease'], targets['CoreWindowsDebug'])
targets['CoreWindows'] = targets['CoreWindowsRelease']
targets['CoreLinux'] = {"bt17": "Core Linux x86_64"}

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
targets['Skyline'] = targets['SkylineRelease']

targets['Container'] = \
{
    'master':
    {
        "ProteoWizardAndSkylineDockerContainerWineX8664": "ProteoWizard and Skyline Docker container (Wine x86_64)"
    },
    'release':
    {
        "ProteoWizard_ProteoWizardAndSkylineReleaseBranchDockerContainerWineX8664": "ProteoWizard and Skyline (release branch) Docker container (Wine x86_64)"
    }
}

targets['BumbershootRelease'] = \
{
    "Bumbershoot_Windows_X86_64": "Bumbershoot Windows x86_64"
    ,"ProteoWizard_Bumbershoot_Windows_X86": "Bumbershoot Windows x86"
}
targets['BumbershootLinux'] = {"ProteoWizard_Bumbershoot_Linux_x86_64": "Bumbershoot Linux x86_64"}
targets['Bumbershoot'] = merge(targets['BumbershootRelease'], targets['BumbershootLinux'])

targets['Core'] = merge(targets['CoreWindows'], targets['CoreLinux'])
targets['All'] = merge(targets['Core'], targets['Skyline'], targets['Bumbershoot'], targets['Container'])

# Patterns are processed in order. If a path matches multiple patterns, only the first pattern will trigger. For example,
# "pwiz_tools/Bumbershoot/Jamfile.jam" matches both "pwiz_tools/Bumbershoot/.*" and "pwiz_tools/.*", but will only trigger "Bumbershoot" targets
matchPaths = [
    (".*/smartBuildTrigger.py", {}),
    ("libraries/.*", targets['All']),
    ("pwiz/.*", targets['All']),
    ("pwiz_aux/.*", targets['All']),
    ("scripts/wix/.*", targets['CoreWindows']),
    ("scripts/.*", targets['All']),
    ("pwiz_tools/BiblioSpec/.*", merge(targets['Core'], targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Bumbershoot/.*", targets['Bumbershoot']),
    ("pwiz_tools/Skyline/.*", merge(targets['Skyline'], targets['Container'])),
    ("pwiz_tools/Topograph/.*", targets['Skyline']),
    ("pwiz_tools/Shared/.*", merge(targets['Skyline'], targets['BumbershootRelease'], targets['Container'])),
    ("pwiz_tools/.*", targets['All']),
    ("Jamroot.jam", targets['All'])
]

