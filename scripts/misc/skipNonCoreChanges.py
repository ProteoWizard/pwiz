import os
import sys
import subprocess
import re

args = sys.argv[1:len(sys.argv)]

if len(args) == 0:
    print("Usage:")
    print(" %s <command to run if any changed path in current git branch is a core path> [<arg1> [<arg2> ...]]" % os.path.basename(sys.argv[0]))
    exit(0)

matchPaths = \
[
    "pwiz_tools/Bumbershoot/.*",
    "pwiz_tools/Skyline/.*",
    "pwiz_tools/Topograph/.*",
    "pwiz_tools/Shared/.*"
]

changed_files = subprocess.check_output("git whatchanged --name-only --pretty=\"\" master..HEAD", shell=True).decode(sys.stdout.encoding) 
changed_files = changed_files.splitlines()
pathsPattern = "(?:" + ")|(?:".join(matchPaths) + ")"

# if any changed file does not match to one of the paths above, then we run the command
for path in changed_files:
    if not re.match(pathsPattern, path):
        cmd = args[0]
        args = args[0:len(args)]
        os.execv(cmd, args)
        exit(0)

# otherwise we don't run it but still report success
