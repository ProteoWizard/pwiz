﻿/*
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
using System.Windows.Forms;

namespace SkylineTester
{
    public class TabTutorials : TabBase
    {
        public override void Enter()
        {
            MainWindow.DefaultButton = MainWindow.RunTutorials;
        }

        public override bool Run()
        {
            StartLog("Tutorials");

            var testList = new List<string>();
            TabTests.GetCheckedTests(MainWindow.TutorialsTree.TopNode, testList);
            if (testList.Count == 0)
            {
                MessageBox.Show(MainWindow, "Check at least one tutorial to run.");
                return false;
            }

            var args = new StringBuilder("offscreen=off loop=1 perftests=on language=");
            args.Append(MainWindow.GetCulture(MainWindow.TutorialsLanguage));
            if (MainWindow.ShowFormNamesTutorial.Checked)
                args.Append(" showformnames=on");
            if (MainWindow.TutorialsDemoMode.Checked)
                args.Append(" demo=on");
            else
            {
                int pauseSeconds = -1;
                if (MainWindow.ModeTutorialsCoverShots.Checked)
                    pauseSeconds = -2; // Magic number that tells TestRunner to grab tutorial cover shot then move on to next test
                else if (MainWindow.PauseTutorialsScreenShots.Checked)
                {
                    int startingScreenshot;
                    if (Int32.TryParse(MainWindow.PauseStartingScreenshot.Text, out startingScreenshot) && startingScreenshot > 1)
                        args.Append(" startingshot=").Append(startingScreenshot);
                }
                else if (!Int32.TryParse(MainWindow.PauseTutorialsSeconds.Text, out pauseSeconds))
                    pauseSeconds = 0;
                args.Append(" pause=").Append(pauseSeconds);
            }
            args.Append(" test=");
            args.Append(string.Join(",", testList));

            MainWindow.AddTestRunner(args.ToString());
            MainWindow.RunCommands();
            return true;
        }

        public override int Find(string text, int position)
        {
            return MainWindow.TutorialsTree.Find(text.Trim(), position);
        }
    }
}
