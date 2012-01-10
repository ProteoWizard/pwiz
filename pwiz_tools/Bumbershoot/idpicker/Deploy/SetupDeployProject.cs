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
using System.Reflection;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using IDPicker;

namespace SetupDeployProject
{
    class SetupDeployProject
    {
        static void Main(string[] args)
        {
            new IDPicker.Forms.NotifyingStringWriter(); // don't optimize away the IDPicker reference

            string version = Util.GetAssemblyVersion(Util.GetAssemblyByName("IDPicker"));
            if (version.EndsWith(".0"))
                version = version.Substring(0, version.Length - 2);

            string guid = Guid.NewGuid().ToString("B").ToUpper();

            string IDPickerExe = String.Empty;

            var wxsTemplate = new StringBuilder(File.ReadAllText("Deploy.wxs.template"));

            wxsTemplate = wxsTemplate.Replace("<ProductGuid>", guid);
            wxsTemplate = wxsTemplate.Replace("<version>", version);

            var componentList = new StringBuilder();
            foreach (string filepath in Directory.GetFiles(args[0]))
            {
                if (filepath.EndsWith(".pdb") || filepath.EndsWith("dummy.c"))
                    continue;

                // Bruker CompassXtract requires XML files to function, but all other XMLs are excluded
                if (filepath.EndsWith(".xml") && !filepath.EndsWith("VariableTable.xml"))
                    continue;

                if (Path.GetFileName(filepath) == "IDPicker.exe")
                {
                    IDPickerExe = filepath;
                    continue;
                }

                string filename = Path.GetFileName(filepath);
                var validId = new StringBuilder("_" + filename);
                for (int i = 0; i < validId.Length; ++i)
                    if (!Char.IsLetterOrDigit(validId[i]) && !(":._".Contains(validId[i])))
                        validId[i] = '_';

                componentList.AppendFormat("<Component Feature=\"MainFeature\"><File Id=\"{0}\" Name=\"{1}\" Source=\"{2}\" KeyPath=\"yes\"/></Component>\n", validId, filename, filepath);
            }

            wxsTemplate = wxsTemplate.Replace("<IDPickerExe>", IDPickerExe);
            wxsTemplate = wxsTemplate.Replace("<ComponentList/>", componentList.ToString());

            // delete old wxs files
            foreach (string filepath in Directory.GetFiles(args[1], "*.wxs"))
                File.Delete(filepath);

            string wxsFilepath = String.Format("{0}/IDPicker-{1}.wxs", args[1], version);
            File.WriteAllText(wxsFilepath, wxsTemplate.ToString());
        }
    }
}
