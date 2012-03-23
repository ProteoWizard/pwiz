#
# $Id$
#
#
# Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
#
# Copyright 2012 Vanderbilt University - Nashville, TN 37232
#
# Licensed under the Apache License, Version 2.0 (the "License"); 
# you may not use this file except in compliance with the License. 
# You may obtain a copy of the License at 
# 
# http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing, software 
# distributed under the License is distributed on an "AS IS" BASIS, 
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
# See the License for the specific language governing permissions and 
# limitations under the License.
#

import os
import sys
import uuid
import glob
import ntpath

if (len(sys.argv) < 5):
	print("usage: %s <template path> <build path> <install path> <version string>" % ntpath.basename(sys.argv[0]))
	print("# normally this is called by the WiX Jamfile")
	quit(1)

templatePath = sys.argv[1]
buildPath = sys.argv[2]
installPath = sys.argv[3]
version = sys.argv[4]

# a unique ProductGuid every time allows multiple parallel installations of pwiz
guid = str(uuid.uuid4())

wxsTemplate = open(templatePath + "/pwiz-setup.wxs.template").read()
wxsTemplate = wxsTemplate.replace("{ProductGuid}", guid)
wxsTemplate = wxsTemplate.replace("{version}", version)
wxsTemplate = wxsTemplate.replace("msvc-release", installPath)

# delete old wxs and wixObj files
for filepath in glob.glob(buildPath + "/*.wxs"):
    os.remove(filepath)
for filepath in glob.glob(buildPath + "/*.wixObj"):
    os.remove(filepath)

wxsFilepath = buildPath + "/pwiz-setup-" + version + ".wxs"
wxsFile = open(wxsFilepath, 'w')
wxsFile.write(wxsTemplate)
