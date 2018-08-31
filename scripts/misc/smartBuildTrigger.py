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
    if isinstance(params, dict):
        data = urllib.parse.urlencode(params).encode('ascii')
    else:
        data = params.encode('ascii')
    req = urllib.request.Request(url, data, method="POST", headers=headers)
    with urllib.request.urlopen(req) as conn:
        return conn.read().decode('utf-8')

def get(url):
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
targets['WindowsRelease'] = \
{
    "bt83": "Windows x86_64"
    ,"bt36": "Windows x86"
    ,"bt143": "Windows x86_64 (no vendor DLLs)"
}
#targets['WindowsDebug'] = \
#{
#    "bt84": "Windows x86_64 debug"
#    ,"bt75": "Windows debug"
#}
#targets['Windows'] = merge(targets['WindowsRelease'], targets['WindowsDebug'])
targets['Windows'] = targets['WindowsRelease']
targets['Linux'] = {"bt17": "Linux x86_64"}

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

targets['BumbershootRelease'] = \
{
    "Bumbershoot_Windows_X86_64": "Bumbershoot Windows x86_64"
    ,"ProteoWizard_Bumbershoot_Windows_X86": "Bumbershoot Windows x86"
}
targets['BumbershootLinux'] = {"ProteoWizard_Bumbershoot_Linux_x86_64": "Bumbershoot Linux x86_64"}
targets['Bumbershoot'] = merge(targets['BumbershootRelease'], targets['BumbershootLinux'])

targets['Core'] = merge(targets['Windows'], targets['Linux'])
targets['All'] = merge(targets['Core'], targets['Skyline'], targets['Bumbershoot'])

# Patterns are processed in order. If a path matches multiple patterns, only the first pattern will trigger. For example,
# "pwiz_tools/Bumbershoot/Jamfile.jam" matches both "pwiz_tools/Bumbershoot/.*" and "pwiz_tools/.*", but will only trigger "Bumbershoot" targets
matchPaths = [
    (".*/smartBuildTrigger.py", {}),
    ("pwiz/.*", targets['All']),
    ("pwiz_aux/.*", targets['All']),
    ("scripts/wix/.*", targets['Windows']),
    ("scripts/.*", targets['All']),
    ("pwiz_tools/BiblioSpec/.*", merge(targets['Core'], targets['Skyline'])),
    ("pwiz_tools/Bumbershoot/.*", targets['Bumbershoot']),
    ("pwiz_tools/Skyline/.*", targets['Skyline']),
    ("pwiz_tools/Topograph/.*", targets['Skyline']),
    ("pwiz_tools/Shared/.*", merge(targets['Skyline'], targets['BumbershootRelease'])),
    ("pwiz_tools/.*", targets['All']),
    ("Jamroot.jam", targets['All'])
]

print("Current branch: %s" % current_branch) # must be either 'master' or 'pull/#'

if current_branch == "master":
    changed_files = subprocess.check_output("git show --pretty="" --name-only", shell=True).decode(sys.stdout.encoding)
    current_commit = subprocess.check_output('git log -n1 --format="%H"', shell=True).decode(sys.stdout.encoding).strip()
elif current_branch.startswith("pull/"):
    print(subprocess.check_output('git fetch origin %s' % (current_branch + "/head"), shell=True).decode(sys.stdout.encoding))
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
