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
using System.Text;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void OpenTutorials()
        {
            InitLanguages(comboBoxTutorialsLanguage);
        }

        private void RunTutorials(object sender, EventArgs e)
        {
            if (!ToggleRunButtons(tabTutorials))
            {
                commandShell.Stop();
                return;
            }

            Tabs.SelectTab(tabOutput);

            var testList = new List<string>();
            GetCheckedTests(TutorialsTree.TopNode, testList);

            var args = new StringBuilder("offscreen=off loop=1 culture=");
            args.Append(GetCulture(comboBoxTutorialsLanguage));
            if (TutorialsDemoMode.Checked)
                args.Append(" demo=on");
            else
            {
                int pauseSeconds = -1;
                if (!PauseTutorialsScreenShots.Checked && !int.TryParse(PauseTutorialsSeconds.Text, out pauseSeconds))
                    pauseSeconds = 0;
                args.Append(" pause=");
                args.Append(pauseSeconds);
            }
            args.Append(" test=");
            args.Append(string.Join(",", testList));

            StartTestRunner(args.ToString());
        }
    }
}
