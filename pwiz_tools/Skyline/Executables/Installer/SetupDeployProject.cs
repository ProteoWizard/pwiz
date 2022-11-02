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
// The Original Code is the Skyline project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2020
//
// Contributor(s):
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
            if (args.Length != 6)
            {
                Console.Error.WriteLine(String.Join(" ", args));
                Console.Error.WriteLine("Usage: SetupDeployProject <Skyline path> <Skyline build path> <pwiz build path> <version string> <address-model> <target-name>");
                return 1;
            }

            try
            {
                string skylinePath = Path.GetFullPath(args[0]);
                string skylineBuildPath = args[1];
                string pwizBuildPath = args[2];
                string version = args[3];
                string addressModel = args[4];
                string targetName = args[5];
                string platform = addressModel == "64" ? "x64" : "x86";
                string installerSuffix = addressModel == "64" ? "-x86_64" : "-x86";

                string templateDirectory = Path.Combine(skylinePath, "Executables/Installer");
                string templatePath = Path.Combine(templateDirectory, "Product-template.wxs");
                string installerOutputDirectory = Path.Combine(skylinePath, "bin", platform);

                string filelistPath = Path.Combine(templateDirectory, "FileList64-template.txt");

                var wxsVendorDlls = new StringBuilder();
                var filelistDlls = new List<string>();
                foreach (var line in File.ReadAllText(pwizBuildPath + "/without-cxt/" + platform + "/INSTALLER_VENDOR_FILES.txt").Trim().Split('\n'))
                {
                    string filename = line.Trim();
                    string filepath = Path.Combine(skylineBuildPath, filename);
                    if (File.Exists(filepath))
                    {
                        wxsVendorDlls.AppendLine($"<Component><File Source=\"{filepath}\" KeyPath=\"yes\"/></Component>");
                        filelistDlls.Add(filename);//$"{filename} (included automatically from ProteoWizard; DO NOT ADD TO TEMPLATE!)");
                    }
                    // if file doesn't exist, assume it's not needed or is already taken care of by non-dynamic elements
                    else
                        Console.Error.WriteLine($"File '{filepath}' specified by INSTALLER_VENDOR_FILES does not exist. Does it need to be added to Skyline.csproj?");
                }

                var wxsTemplate = new StringBuilder(File.ReadAllText(templatePath));
                wxsTemplate.Replace("$(var.Skyline.TargetDir)", skylineBuildPath);
                wxsTemplate.Replace("__VERSION__", version);
                wxsTemplate.Replace("__TEMPLATE_DIR__", Path.GetDirectoryName(templatePath));
                wxsTemplate.Replace("__VENDOR_DLLS__", wxsVendorDlls.ToString());

                if (platform == "x64")
                {
                    var filelistTemplate = new StringBuilder(File.ReadAllText(filelistPath));
                    foreach (var filename in filelistDlls)
                    {
                        var preReplaceLength = filelistTemplate.Length;
                        string automaticIncludedFilename = $"{filename} (included automatically from ProteoWizard; DO NOT ADD TO THE WXS TEMPLATE!)";
                        if (filelistTemplate.ToString().IndexOf(automaticIncludedFilename) > 0)
                            continue;
                        filelistTemplate.Replace(filename, automaticIncludedFilename);
                        bool didReplace = filelistTemplate.Length != preReplaceLength;
                        if (!didReplace) // the pwiz file is not in the template
                            filelistTemplate.AppendLine($"{filename} (included automatically from ProteoWizard, but missing from FileList64-template.txt; add it to FileList64-template.txt BUT NOT TO THE WXS TEMPLATE!)");
                    }
                    File.WriteAllText(filelistPath.Replace("-template", ""), filelistTemplate.ToString());
                }

                /*var httpSources = Regex.Matches(wxsTemplate.ToString(), "Name=\"(.*)\" Source=\"(http://.*?)\"");
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
                }*/

                // delete old wxs files
                foreach (string filepath in Directory.GetFiles(pwizBuildPath, "*.wxs"))
                    File.Delete(filepath);
                foreach (string filepath in Directory.GetFiles(pwizBuildPath, "*.wixObj"))
                    File.Delete(filepath);

                string wxsFilepath = $"{installerOutputDirectory}/{targetName}-{version}{installerSuffix}.wxs";
                File.WriteAllText(wxsFilepath, wxsTemplate.ToString());

                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("ERROR: " + e);
                return 1;
            }
        }
    }
}
