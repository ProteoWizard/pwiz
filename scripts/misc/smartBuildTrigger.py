# This script is responsible for triggering most builds on ProteoWizard's TeamCity project.
# It avoids redundant builds (build configs that have nothing to do with a given set of changed files),
# but still reports those untriggered builds to GitHub so they can be required to pass for merging a PR.
#
# The "zSmart build trigger" config runs this script on all git changes.
# Then this script runs git for master or an active pull request[1] to check the files changed by the latest commit (for master)
# or by any commit (for PRs).
#
# When a build is NOT triggered, the script reports this fact to GitHub so that the config can still be a "required check" for merging the PR.
#
# The 'targets' dictionary maps build config ids (e.g. 'bt83') to the status name shown in GitHub (e.g. "teamcity - Core Windows x86");
# these names must match the status name reported by the corresponding TeamCity configs (usually the name of the config as seen on the TeamCity project page).
# There are metatargets in this dictionary which create aliases to group targets together (e.g. 'CoreWindows' maps to "bt83", "bt36", and "bt143").
#
# The 'matchPaths' list is a list of tuples where the first value is a regular expression to match against the list of changed files and the second value is a set of targets picked out from the 'targets' dictionary.
# The patterns are processed in order and the first pattern to match for each changed file triggers the corresponding target set. Multiple files can match to the same pattern but the configs will actually only be triggered once.
#
# [1] the original PR, not the PR merged with master which is what TeamCity actually builds

import os
import sys
import subprocess
import re
import urllib.request
import urllib.parse
import base64

args = sys.argv[1:len(sys.argv)]
current_branch = args[0]
bearer_token = args[1]
teamcity_username = args[2]
teamcity_password = args[3]

def post(url, params, headers):
    if os.environ['USERNAME'] != 'teamcity':
        return

    if isinstance(params, dict):
        data = urllib.parse.urlencode(params).encode('ascii')
    else:
        data = params.encode('ascii')
    req = urllib.request.Request(url, data, method="POST", headers=headers)
    with urllib.request.urlopen(req) as conn:
        return conn.read().decode('utf-8')

def get(url):
    if os.environ['USERNAME'] != 'teamcity':
        return

    conn = httplib.HTTPConnection(url)
    conn.request("GET", "")
    return conn.getresponse()

def merge(a, *b):
    r = a.copy()
    for i in b:
        r.update(i)
    return r

if len(args) < 4:
    print("Usage:")
    print(" %s <current branch> <current commit SHA1> <GitHub authorization token>" % os.path.basename(sys.argv[0]))
    exit(0)

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
    "ProteoWizard_WindowsX8664msvcProfessionalSkylineResharperChecks": "Skyline code inspection" # depends on "bt209",
    ,"bt209": "Skyline master and PRs (Windows x86_64)"
    ,"bt19": "Skyline master and PRs (Windows x86)"
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
    "ProteoWizardAndSkylineDockerContainerWineX8664": "ProteoWizard and Skyline Docker container (Wine x86_64)"
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

print("Current branch: %s" % current_branch) # must be either 'master' or 'pull/#'

if current_branch == "master":
    changed_files = subprocess.check_output("git show --pretty="" --name-only", shell=True).decode(sys.stdout.encoding)
    current_commit = subprocess.check_output('git log -n1 --format="%H"', shell=True).decode(sys.stdout.encoding).strip()
elif current_branch.startswith("pull/"):
    print(subprocess.check_output('git fetch origin master && git checkout master && git pull origin master && git fetch origin %s' % (current_branch + "/head"), shell=True).decode(sys.stdout.encoding))
    changed_files = subprocess.check_output("git diff --name-only master...FETCH_HEAD", shell=True).decode(sys.stdout.encoding)
    current_commit = subprocess.check_output('git log -n1 --format="%H" FETCH_HEAD', shell=True).decode(sys.stdout.encoding).strip()
else:
    print("Cannot handle branch with name: %s" % current_branch)
    exit(1)

print("Current commit: '%s'" % current_commit)

print("Changed files:\n", changed_files)
changed_files = changed_files.splitlines()

# match changed file paths to triggers
triggers = {}
if current_branch == "master" and len(changed_files) == 0:
    print("Empty change list on master branch; this is some merge I don't know how to get a reliable change list for yet. Building everything!")
    for target in targets['All']:
        if target not in triggers:
            triggers[target] = "merge to master"
else:
    for path in changed_files:
        if os.path.basename(path) == "smartBuildTrigger.py":
            continue
        triggered = False # only trigger once per path
        for tuple in matchPaths:
            if re.match(tuple[0], path):
                for target in tuple[1]:
                    if target not in triggers:
                        triggers[target] = path
                    triggered = True
            if triggered:
                break
    
notBuilding = {}
building = {}
for targetKey in targets:
    for target in targets[targetKey]:
        if target not in triggers:
            notBuilding[target] = targets[targetKey][target]
        else:
            building[target] = targets[targetKey][target]

# Trigger builds
teamcityUrl = "https://teamcity.labkey.org/httpAuth/app/rest/buildQueue"
buildNodeToPOST = '<build branchName="%s"><buildType id="%s"/></build>'
base64string = base64.b64encode(('%s:%s' % (teamcity_username, teamcity_password)).encode('ascii')).decode('ascii')
headers = {"Authorization": "Basic %s" % base64string, "Content-Type": "application/xml"}
for trigger in triggers:
    if trigger == "bt209":
        continue # special case to skip triggering this build since Skyline Code Inspection starts it as part of build chain (TODO: refactor to make this capability more generic)
    print("Triggering build %s (%s): %s" % (building[trigger], trigger, triggers[trigger]))
    data = buildNodeToPOST % (current_branch, trigger)
    rsp = post(teamcityUrl, data, headers)


# For builds not being triggered, report success to GitHub
githubUrl = "https://api.github.com/repos/ProteoWizard/pwiz/statuses/%s" % current_commit
headers = {"Authorization": "Bearer %s" % bearer_token, "Content-type": "application/json"}
for target in notBuilding:
    print("Not building %s (%s), but reporting success to GitHub." % (notBuilding[target], target))
    data = '{"state": "success", "context": "teamcity - %s", "description": "Build not necessary with these changed files"}' %  notBuilding[target]
    rsp = post(githubUrl, data, headers)

# when no builds are triggered (e.g. if the only update is to this script), report a GitHub status that the script ran successfully
if len(building) == 0:
    print("Reporting successful script run to GitHub.")
    data = '{"state": "success", "context": "smartBuildTrigger.py", "description": "Ran without errors - no builds triggered."}'
    rsp = post(githubUrl, data, headers)
