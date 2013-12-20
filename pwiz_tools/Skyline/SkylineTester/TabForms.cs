/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using TestRunnerLib;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void OpenForms()
        {
            InitLanguages(comboBoxFormsLanguage);
        }

        private void RunForms(object sender, EventArgs e)
        {
            if (!ToggleRunButtons(tabForms))
            {
                commandShell.Stop();
                return;
            }

            var args = new StringBuilder("loop=1 offscreen=off culture=");
            args.Append(GetCulture(comboBoxFormsLanguage));
            if (RegenerateCache.Checked)
            {
                args.Append(" form=__REGEN__");
            }
            else
            {
                // Create list of forms the user wants to see.
                var formList = new List<string>();
                var skylineNode = FormsTree.Nodes[0];
                foreach (TreeNode node in skylineNode.Nodes)
                {
                    if (node.Checked)
                        formList.Add(node.Text);
                }
                args.Append(" form=");
                args.Append(string.Join(",", formList));
                int pauseSeconds = -1;
                if (PauseFormDelay.Checked && !int.TryParse(PauseFormSeconds.Text, out pauseSeconds))
                    pauseSeconds = 0;
                args.Append(" pause=");
                args.Append(pauseSeconds);
            }

            commandShell.LogFile = _defaultLogFile;
            StartTestRunner(args.ToString(), DoneForms);
        }

        private void DoneForms(bool success)
        {
            if (success && RegenerateCache.Checked)
                CreateFormsTree();
            RegenerateCache.Checked = false;
            TestRunnerDone(success);
        }

        private void CreateFormsTree()
        {
            FormsTree.Nodes.Clear();

            var forms = new List<TreeNode>();
            var skylinePath = Path.Combine(_exeDir, "Skyline.exe");
            var skylineDailyPath = Path.Combine(_exeDir, "Skyline-daily.exe");
            skylinePath = File.Exists(skylinePath) ? skylinePath : skylineDailyPath;
            var assembly = Assembly.LoadFrom(skylinePath);
            var types = assembly.GetTypes();
            var formLookup = new FormLookup();

            foreach (var type in types)
            {
                if (type.IsSubclassOf(typeof(Form)) && !type.IsAbstract)
                {
                    var node = new TreeNode(type.Name);
                    if (!formLookup.HasTest(type.Name))
                        node.ForeColor = Color.Gray;
                    forms.Add(node);
                }
            }

            forms = forms.OrderBy(node => node.Text).ToList();
            FormsTree.Nodes.Add(new TreeNode("Skyline", forms.ToArray()));
            FormsTree.ExpandAll();
        }
    }
}
