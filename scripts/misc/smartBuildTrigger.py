import os
import sys
import subprocess
import re

def runCommandAndExit(args):
        cmd = args[0]
        args = args[0:len(args)]
        os.execv(cmd, args)
        exit(0)

args = sys.argv[1:len(sys.argv)]

#if len(args) == 0:
#    print("Usage:")
#    print(" %s <command to run if any changed path in current git branch is a core path> [<arg1> [<arg2> ...]]" % os.path.basename(sys.argv[0]))
#    exit(0)

targets = {}
targets['WindowsRelease'] = \
[
    "bt83", # WindowsRelease_x86_64,
    "bt36" # WindowsRelease_x86
]
targets['WindowsDebug'] = \
[
    "bt84", # "WindowsDebug_x86_64",
    "bt75" # "WindowsDebug_x86"
]
targets['Windows'] = targets['WindowsRelease'] + targets['WindowsDebug']
targets['Linux'] = ["bt17"]

targets['SkylineRelease'] = \
[
    "ProteoWizard_WindowsX8664msvcProfessionalSkylineResharperChecks", # depends on "bt209", # SkylineRelease_x86_64,
    "bt19" # SkylineRelease_x86
]
targets['SkylineDebug'] = \
[
    "bt210", # "SkylineDebug_x86_64",
    "bt87" # "SkylineDebug_x86"
]
targets['Skyline'] = targets['SkylineRelease'] + targets['SkylineDebug']

targets['BumbershootRelease'] = \
[
    "Bumbershoot_Windows_X86_64",
    "Bumbershoot_Windows_X86"
]
targets['BumbershootLinux'] = ["ProteoWizard_Bumbershoot_Linux_x86_64"]
targets['Bumbershoot'] = targets['BumbershootRelease'] + targets['BumbershootLinux']

targets['Core'] = targets['Windows'] + targets['Linux']
targets['All'] = targets['Core'] + targets['Skyline'] + targets['Bumbershoot']

matchPaths = {
    "pwiz/.*" : targets['All'],
    "pwiz_aux/.*" : targets['All'],
    "scripts/.*" : targets['All'],
    "pwiz_tools/Bumbershoot/.*": targets['Bumbershoot'],
    "pwiz_tools/Skyline/.*": targets['Skyline'],
    "pwiz_tools/Topograph/.*": targets['Skyline'],
    "pwiz_tools/Shared/.*": targets['Skyline'] + targets['Bumbershoot'],
    "Jamroot.jam" : targets['All']
}

branch = subprocess.check_output("git branch", shell=True).decode(sys.stdout.encoding)
print("Branches:\n", branch)
branch = re.search("(?<=\* )([^\n]*)", branch).groups(0)[0]
print("Current branch: %s" % branch)

current_commit = subprocess.check_output('git log -n 1 --pretty="%H"', shell=True).decode(sys.stdout.encoding)
print("Current commit: %s" % current_commit)

if branch == "master":
    changed_files = subprocess.check_output("git show --pretty="" --name-only", shell=True).decode(sys.stdout.encoding)
else:
    #changed_files = subprocess.check_output("git whatchanged --name-only --pretty=\"\" master..HEAD", shell=True).decode(sys.stdout.encoding)
    print(subprocess.check_output('git log -n 10', shell=True).decode(sys.stdout.encoding))
    last_commit_before_merge = subprocess.check_output('git log -n 1 --skip 1 --pretty="%H"', shell=True).decode(sys.stdout.encoding)
    changed_files = subprocess.check_output("git diff --name-only %s...master" % last_commit_before_merge, shell=True).decode(sys.stdout.encoding)
print("Changed files:\n", changed_files)
changed_files = changed_files.splitlines()

teamcityUrl = "http://teamcity.labkey.org/app/rest/buildQueue"
buildNodeToPOST = '<build><buildType id="%s"/></build>'

# if any changed file does not match to one of the paths above, then we run the command
triggers = {}
for path in changed_files:
    for pattern in matchPaths:
        if re.match(pattern, path):
            for target in matchPaths[pattern]:
                if target not in triggers:
                    print("Core path triggering build %s: %s" % (target, path))
                    triggers[target] = path
            #runCommandAndExit(args)
for trigger in triggers:
    print(buildNodeToPOST % trigger)

notBuilding = {}
for targetKey in targets:
    for target in targets[targetKey]:
        if target not in triggers:
            notBuilding[target] = 0

githubUrl = "https://api.github.com/repos/ProteoWizard/pwiz/statuses/fa243ca817204ebfc5a1ed242cbcb24508b20eb8"
for target in notBuilding:
    print("Not building %s, but reporting success to GitHub." % target)
#    teamcityUrl
# otherwise we don't run it but still report success
