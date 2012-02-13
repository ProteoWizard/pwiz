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
// The Original Code is the ProteoWizard project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
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
        static void Main(string[] args)
        {
            string templatePath = args[0];
            string buildPath = args[1];
            string version = args[2];

            // a unique ProductGuid every time allows multiple parallel installations of pwiz
            string guid = Guid.NewGuid().ToString("B").ToUpper();

            var wxsTemplate = new StringBuilder(File.ReadAllText(Path.Combine(templatePath, "pwiz-setup.wxs.template")));

            wxsTemplate = wxsTemplate.Replace("<ProductGuid>", guid);
            wxsTemplate = wxsTemplate.Replace("<version>", version);

            // delete old wxs files
            foreach (string filepath in Directory.GetFiles(buildPath, "*.wxs"))
                File.Delete(filepath);

            string wxsFilepath = String.Format("{0}/pwiz-setup-{1}.wxs", buildPath, version);
            File.WriteAllText(wxsFilepath, wxsTemplate.ToString());
        }
    }
}
