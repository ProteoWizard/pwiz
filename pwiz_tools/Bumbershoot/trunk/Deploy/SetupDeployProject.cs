//
// $Id: SetupDeployProject.cs 48 2010-11-04 21:22:23Z chambm $
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
// The Original Code is the BumberDash project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
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
            if (args.Length != 4)
            {
                Console.Error.WriteLine("Usage: SetupDeployProject <template path> <install path> <version string> <address-model>");
                return 1;
            }

            var wxsTemplate = new StringBuilder(File.ReadAllText(args[0]));
            string guid = Guid.NewGuid().ToString("B").ToUpper();

            string installPath = args[1];
            string buildPath = Path.Combine(installPath, "..");
            string version = args[2];
            string addressModel = args[3];
            string installerSuffix = addressModel == "64" ? "-x86_64" : "-x86";

            wxsTemplate = wxsTemplate.Replace("{ProductGuid}", guid);
            wxsTemplate = wxsTemplate.Replace("{version}", version);
            wxsTemplate = wxsTemplate.Replace("msvc-release", installPath);

            var subdirectoryComponents = new StringBuilder();
            AddSubdirectoryComponents(Path.Combine(installPath, "lib"), subdirectoryComponents, 0);
            wxsTemplate = wxsTemplate.Replace("<!-- Components unique to BumberDash -->",
                                              "<!-- Components unique to BumberDash -->\n" + subdirectoryComponents.ToString());

            // delete old wxs files
            foreach (string filepath in Directory.GetFiles(buildPath, "*.wxs"))
                File.Delete(filepath);
            foreach (string filepath in Directory.GetFiles(buildPath, "*.wixObj"))
                File.Delete(filepath);

            string wxsFilepath = String.Format("{0}/BumberDash-{1}{2}.wxs", buildPath, version, installerSuffix);
            File.WriteAllText(wxsFilepath, wxsTemplate.ToString());

            return 0;
        }

        private static string MakeValidId(string name)
        {
            var validId = new StringBuilder(name);
            for (int i = 0; i < validId.Length; ++i)
                if (!Char.IsLetterOrDigit(validId[i]) && !(":._".Contains(validId[i])))
                    validId[i] = '_';
            return validId.ToString();
        }

        private static void AddSubdirectoryComponents(string currentDirectory, StringBuilder componentList, int level)
        {
            string validDirectoryId = MakeValidId(currentDirectory.Substring(currentDirectory.IndexOf("lib")));

            componentList.AppendFormat("{3}<Directory Id=\"{0}_{1}\" Name=\"{2}\">\n",
                                       validDirectoryId,
                                       (uint) validDirectoryId.GetHashCode(),
                                       Path.GetFileName(currentDirectory),
                                       new string(' ', 2 * level));

            foreach (string path in Directory.GetDirectories(currentDirectory))
                AddSubdirectoryComponents(path, componentList, level + 1);


            foreach (string filepath in Directory.GetFiles(currentDirectory))
            {
                string filename = Path.GetFileName(filepath);
                componentList.AppendFormat("{4}  <Component Feature=\"MainFeature\"><File Id=\"{0}_{1}\" Name=\"{2}\" Source=\"{3}\" KeyPath=\"yes\"/></Component>\n",
                                           MakeValidId(filename),
                                           (uint) validDirectoryId.GetHashCode(),
                                           filename,
                                           filepath,
                                           new string(' ', 2 * level));
            }

            componentList.AppendFormat("{0}</Directory>\n", new string(' ', 2 * level));
        }
    }
}
