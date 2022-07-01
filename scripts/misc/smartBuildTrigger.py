# This script is responsible for triggering most builds on ProteoWizard's TeamCity project.
# It avoids redundant builds (build configs that have nothing to do with a given set of changed files),
# but still reports those untriggered builds to GitHub so they can be required to pass for merging a PR.
#
# The "zSmart build trigger" config runs this script on all git changes.
# Then this script runs git for master or an active pull request[1] to check the files changed by the latest commit (for master)
# or by any commit (for PRs).
#
# When a build is NOT triggered due to changed files, the script reports this fact to GitHub so that the config can still be a "required check" for merging the PR.
#
# The 'targets' dictionary maps build config ids (e.g. 'bt83') to the status name shown in GitHub (e.g. "teamcity - Core Windows x86");
# THESE NAMES MUST MATCH THE STATUS NAME REPORTED BY THE CORRESPONDING TEAMCITY CONFIGS (usually the name of the config as seen on the TeamCity project page).
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
import json

args = sys.argv[1:len(sys.argv)]

if len(args) < 5:
    print("Usage:")
    print(" %s <targets_and_paths_file> <current branch> <GitHub authorization token> <teamcity_username> <teamcity_password>" % os.path.basename(sys.argv[0]))
    exit(0)

targets_and_paths_file = args[0]
current_branch = args[1]
bearer_token = args[2]
teamcity_username = args[3]
teamcity_password = args[4]

def post(url, params, headers):
    if not 'TEAMCITY_VERSION' in os.environ:
        return

    if isinstance(params, dict):
        data = urllib.parse.urlencode(params).encode('ascii')
    else:
        data = params.encode('ascii')
    req = urllib.request.Request(url, data, method="POST", headers=headers)
    with urllib.request.urlopen(req) as conn:
        return conn.read().decode('utf-8')

def get(url, always = False):
    if not always and not 'TEAMCITY_VERSION' in os.environ:
        return
    
    req = urllib.request.Request(url)
    with urllib.request.urlopen(req) as conn:
        return conn.read().decode('utf-8')

def merge(dict1, *dicts):
    r = dict1.copy()
    for d in dicts:
        for k in set(r.keys()).union(d.keys()):
            if k in r and k in d:
                if isinstance(r[k], dict) and isinstance(d[k], dict):
                    r[k] = merge(r[k], d[k])
                else:
                    # If one of the values is not a dict, you can't continue merging it.
                    # Value from second dict overrides one in first and we move on.
                    return d[k]
                    # Alternatively, replace this with exception raiser to alert you of value conflicts
            elif k in r:
                pass
            else:
                r[k] = d[k]
    return r

# exec the targets_and_paths_file to define the targets and matchPaths variables
with open(targets_and_paths_file, "rb") as source_file:
    code = compile(source_file.read(), targets_and_paths_file, "exec")
exec(code)

print("Current branch: %s" % current_branch) # must be either 'master' or 'pull/#'

if current_branch == "master" or current_branch.startswith("skyline_"):
    changed_files = subprocess.check_output("git show --pretty="" --name-only", shell=True).decode(sys.stdout.encoding)
    current_commit = subprocess.check_output('git log -n1 --format="%H"', shell=True).decode(sys.stdout.encoding).strip()
    base_branch = current_branch
elif current_branch.startswith("pull/"):
    pullMetadata = json.loads(get("https://api.github.com/repos/ProteoWizard/pwiz/" + current_branch.replace("pull", "pulls"), True))
    base_branch = pullMetadata["base"]["ref"]
    print("Base branch: %s" % base_branch)
    print(subprocess.check_output('git fetch origin %s && git checkout %s && git pull origin %s && git fetch origin %s' % (base_branch, base_branch, base_branch, current_branch + "/head"), shell=True).decode(sys.stdout.encoding))
    changed_files = subprocess.check_output("git diff --name-only %s...FETCH_HEAD" % base_branch, shell=True).decode(sys.stdout.encoding)
    current_commit = subprocess.check_output('git log -n1 --format="%H" FETCH_HEAD', shell=True).decode(sys.stdout.encoding).strip()
else:
    print("Cannot handle branch with name: %s" % current_branch)
    exit(1)

print("Current commit: '%s'" % current_commit)

changed_files_str = changed_files
changed_files = changed_files.splitlines()
print(f"Changed files ({len(changed_files)}):\n", changed_files_str)

# substitute "release" for specific skyline_##.# versions
base_branch = re.sub("(Skyline/)?skyline_.*", "release", base_branch)
print("Base branch: '%s'" % base_branch)

# promote branch-specific targets into main match dictionaries
for tuple in matchPaths:
    if base_branch in tuple[1]:
        tuple[1].update(tuple[1][base_branch])

# match changed file paths to triggers
triggers = {}
if (current_branch == "master" or current_branch.startswith("skyline_")) and len(changed_files) == 0:
    print("Empty change list on master branch; this is some merge I don't know how to get a reliable change list for yet. Building everything!")
    for target in targets['All']:
        if target not in triggers:
            isBaseBranchDict = isinstance(tuple[1][target], dict) # these targets were promoted into top-level above
            if not isBaseBranchDict and target not in triggers:
                triggers[target] = "merge to %s" % base_branch
else:
    for path in changed_files:
        if os.path.basename(path) == "smartBuildTrigger.py":
            continue
        triggered = False # only trigger once per path
        for tuple in matchPaths:
            if re.match(tuple[0], path):
                for target in tuple[1]:
                    isBaseBranchDict = isinstance(tuple[1][target], dict) # these targets were promoted into top-level above
                    if not isBaseBranchDict and target not in triggers:
                        triggers[target] = path
                    triggered = True
            if triggered:
                break
    
notBuildingDueToBranch = {}
notBuildingDueToChangedFiles = {}
building = {}
for targetKey in targets:
    for target in targets[targetKey]:
        isBaseBranchDict = isinstance(targets[targetKey][target], dict) # these targets were promoted into top-level above
        if not isBaseBranchDict and target not in triggers:
            notBuildingDueToChangedFiles[target] = targets[targetKey][target]
        elif isBaseBranchDict:
            for target2 in targets[targetKey][target]:
                if target2 not in triggers and not base_branch == target:
                    notBuildingDueToBranch[target2] = targets[targetKey][target][target2]
        else:
            building[target] = targets[targetKey][target]

# Trigger builds
teamcityUrl = "https://teamcity.labkey.org/httpAuth/app/rest/buildQueue"
buildNodeToPOST = '<build branchName="%s"><buildType id="%s"/></build>'
base64string = base64.b64encode(('%s:%s' % (teamcity_username, teamcity_password)).encode('ascii')).decode('ascii')
headers = {"Authorization": "Basic %s" % base64string, "Content-Type": "application/xml"}
for trigger in triggers:
    if trigger == "bt209" or trigger == "ProteoWizard_WindowsX8664SkylineReleaseBranchMsvcProfessional":
        continue # special case to skip triggering this build since Skyline Code Inspection starts it as part of build chain (TODO: refactor to make this capability more generic)
    print("Triggering build %s (%s): %s" % (building[trigger], trigger, triggers[trigger]))
    data = buildNodeToPOST % (current_branch, trigger)
    rsp = post(teamcityUrl, data, headers)


# For builds not being triggered, report success to GitHub
githubUrl = "https://api.github.com/repos/ProteoWizard/pwiz/statuses/%s" % current_commit
headers = {"Authorization": "Bearer %s" % bearer_token, "Content-type": "application/json"}
for target in notBuildingDueToChangedFiles:
    print("Not building %s (%s) due to unchanged files, but reporting success to GitHub." % (notBuildingDueToChangedFiles[target], target))
    data = '{"state": "success", "context": "teamcity - %s", "description": "Build not necessary with these changed files"}' %  notBuildingDueToChangedFiles[target]
    rsp = post(githubUrl, data, headers)

for target in notBuildingDueToBranch:
    print("Not building %s (%s) or reporting to GitHub due to PR's target branch." % (notBuildingDueToBranch[target], target))

# when no builds are triggered (e.g. if the only update is to this script), report a GitHub status that the script ran successfully
if len(building) == 0:
    print("Reporting successful script run to GitHub.")
    data = '{"state": "success", "context": "smartBuildTrigger.py", "description": "Ran without errors - no builds triggered."}'
    rsp = post(githubUrl, data, headers)
