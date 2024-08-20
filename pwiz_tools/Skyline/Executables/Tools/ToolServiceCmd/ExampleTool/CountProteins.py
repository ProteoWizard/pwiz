#!/usr/bin/python

import os
import sys
import subprocess
report_proteinlocators = """<?xml version="1.0"?>
<views>
  <view name="ProteinLocators" rowsource="pwiz.Skyline.Model.Databinding.Entities.Protein" sublist="Results!*">
    <column name="Name" />
  </view>
</views>"""

connectionname = sys.argv[1]
toolservicecmdexe = os.path.dirname(os.path.realpath(__file__)) + "/bin/ToolServiceCmd.exe"

args=[toolservicecmdexe, 'GetReport', '--connectionname', connectionname]
print "Executing command ", args
p = subprocess.Popen([toolservicecmdexe, 'GetReport', '--connectionname', connectionname], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
(report_text, error_text) = p.communicate(input=report_proteinlocators)
print "Result of command was", len(report_text), "characters long."

print "There are", report_text.count('\n') - 1, "proteins"
