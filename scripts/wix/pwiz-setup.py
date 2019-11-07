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

if (len(sys.argv) < 6):
    print("usage: %s <template path> <build path> <install path> <version string> <address-model>" % ntpath.basename(sys.argv[0]))
    print("# normally this is called by the WiX Jamfile")
    quit(1)

templatePath = sys.argv[1]
buildPath = sys.argv[2]
installPath = sys.argv[3]
version = sys.argv[4]
numericVersion = sys.argv[5]

installerSuffix = "-x86"
if sys.argv[6] == "64":
    installerSuffix = "-x86_64"

# a unique ProductGuid every time allows multiple parallel installations of pwiz
guid = str(uuid.uuid4())

# apps the user can add to the explorer rightclick menu if they want
appNames = ["MSConvertGUI","SeeMS"]

def contextMenuProperties() :
    txt = ""
    for n in appNames :
        txt = txt + '<Property Id="INSTALL%sMENU" Value="0" />\n    '%n.upper()
    return txt

def contextMenuOptions() :
    t = ""
    y = 100
    for n in appNames :
        N = n.upper()
        t = t + '<Control Id="%sCheckBox" Type="CheckBox" X="20" Y="%d" Width="290" Height="17" Property="INSTALL%sMENU" CheckBoxValue="0" Text="Add %s to the Windows Explorer right-click menu" />\n\t\t\t'%(n,y,N,n)
        y = y+20
    return t

def contextMenuRegistries() :
    componentText = ' \
    <Component Feature="MainFeature">\n \
       <Condition>INSTALL_MY_APPU_MENU</Condition>\n \
       <RegistryValue Root="HKCR" Key="*\shell\__MY_APP__\command" Value="&quot;__MY_PATH__&quot; &quot;%1&quot;" Type="string"/>\n \
       <RegistryKey Root="HKCR" Key="*\shell\__MY_APP__\command" />\n \
       <RegistryValue Root="HKCR" Key="Directory\shell\__MY_APP__\command" Value="&quot;__MY_PATH__&quot; &quot;%1&quot;" Type="string"/>\n \
       <RegistryKey Root="HKCR" Key="Directory\shell\__MY_APP__\command" />\n \
    </Component>\n '
    registries = ""
    for appName in appNames :
        txt = componentText.replace("_MY_APPU_",appName.upper())
        txt = txt.replace("__MY_APP__","Open with "+appName)
        txt = txt.replace("__MY_PATH__","[APPLICATIONFOLDER]"+appName+".exe")
        registries = registries + txt
    return registries

wxsTemplate = open(templatePath + "/pwiz-setup.wxs.template").read()
wxsVendorDlls = open(templatePath + "/vendor-dlls.wxs-fragment").read()

wxsTemplate = wxsTemplate.replace("__VENDOR_DLLS__", wxsVendorDlls)
wxsTemplate = wxsTemplate.replace("__CONTEXTMENU_PROPERTIES__",contextMenuProperties())
wxsTemplate = wxsTemplate.replace("__CONTEXTMENU_REGISTRY__",contextMenuRegistries())
wxsTemplate = wxsTemplate.replace("__CONTEXTMENU_CHECKBOXEN__",contextMenuOptions())
wxsTemplate = wxsTemplate.replace("{ProductGuid}", guid)
wxsTemplate = wxsTemplate.replace("{version}", version)
wxsTemplate = wxsTemplate.replace("{numeric-version}", numericVersion)
wxsTemplate = wxsTemplate.replace("msvc-release", installPath)

# delete old wxs and wixObj files
for filepath in glob.glob(buildPath + "/*.wxs"):
    os.remove(filepath)
for filepath in glob.glob(buildPath + "/*.wixObj"):
    os.remove(filepath)

wxsFilepath = buildPath + "/pwiz-setup-" + version + installerSuffix + ".wxs"
wxsFile = open(wxsFilepath, 'w')
wxsFile.write(wxsTemplate)
