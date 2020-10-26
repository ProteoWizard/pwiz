//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace SetupDeployProject
{
    class SetupDeployProject
    {
        static int Main(string[] args)
        {
            if (args.Length != 5)
            {
                Console.Error.WriteLine("Usage: SetupDeployProject <template path> <install path> <version string> <numeric version string> <address-model>");
                return 1;
            }

            var wxsTemplate = new StringBuilder(File.ReadAllText(args[0]));
            string guid = Guid.NewGuid().ToString("B").ToUpper();

            string installPath = args[1];
            string buildPath = Path.Combine(installPath, "..");
            string version = args[2];
            string numericVersion = args[3];
            string addressModel = args[4];
            string platform = addressModel == "64" ? "x64" : "x86";
            string installerSuffix = addressModel == "64" ? "-x86_64" : "-x86";

            var wxsVendorDlls = new StringBuilder();
            foreach (var line in File.ReadAllText(buildPath + "/" + platform + "/INSTALLER_VENDOR_FILES.txt").Trim().Split('\n'))
                wxsVendorDlls.Append($"<Component Feature=\"MainFeature\"><File Source=\"{installPath}\\{line.Trim()}\" KeyPath=\"yes\"/></Component>");

            wxsTemplate.Replace("{ProductGuid}", guid);
            wxsTemplate.Replace("{version}", version);
            wxsTemplate.Replace("{numeric-version}", numericVersion);
            wxsTemplate.Replace("msvc-release", installPath);
            wxsTemplate.Replace("__VENDOR_DLLS__", wxsVendorDlls.ToString());

            var httpSources = Regex.Matches(wxsTemplate.ToString(), "Name=\"(.*)\" Source=\"(http://.*?)\"");
            WebClient webClient = null;
            foreach (Match match in httpSources)
            {
                var nameCapture = match.Groups[1];
                var sourceCapture = match.Groups[2];

                // download file
                webClient = webClient ?? new WebClient();
                webClient.DownloadFile(sourceCapture.Value, Path.Combine(installPath, nameCapture.Value));

                // replace http link with the path to the downloaded HTTP file
                wxsTemplate.Replace(sourceCapture.Value, installPath + "\\" + nameCapture.Value);
            }

            // delete old wxs files
            foreach (string filepath in Directory.GetFiles(buildPath, "*.wxs"))
                File.Delete(filepath);
            foreach (string filepath in Directory.GetFiles(buildPath, "*.wixObj"))
                File.Delete(filepath);

            string wxsFilepath = String.Format("{0}/IDPicker-{1}{2}.wxs", buildPath, version, installerSuffix);
            File.WriteAllText(wxsFilepath, wxsTemplate.ToString());

            return 0;
        }
    }
}
