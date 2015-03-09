/*
 * Original author: Daniel Broudy <daniel.broudy .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImmediateWindowTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void ImmediateWindowTestMethod()
        {
            RunFunctionalTest();
        }

        // Also tested in ConfigureToolsDlgTest
        protected override void DoTest()
        {
            Settings.Default.ToolList.Clear();

            TestToolAdd();
        }

        private static void TestToolAdd()
        {
            ImmediateWindow immediateWindow = ShowDialog<ImmediateWindow>(SkylineWindow.ShowImmediateWindow);
            const string exePath = "example.exe"; //Not L10N
            RunUI(()=>
            {
                immediateWindow.Clear();

                int countStart = Settings.Default.ToolList.Count;
                const string addToolCommand = "--tool-add=ImToolAdded --tool-command=" + exePath; //Not L10N
                immediateWindow.WriteLine(addToolCommand);
                immediateWindow.RunLine(immediateWindow.LineCount - 1);
                AssertEx.AreComparableStrings(Resources.CommandLine_ImportTool__0__was_added_to_the_Tools_Menu_, immediateWindow.TextContent, 1); //Not L10N will be when command line stuff is localized.
                SkylineWindow.PopulateToolsMenu();
                Assert.AreEqual("ImToolAdded", SkylineWindow.GetToolText(countStart));

                immediateWindow.Clear();

                // Write the title of the tool and then run it from the immediate window.
                immediateWindow.WriteLine("ImToolAdded");

             });
            RunDlg<MessageDlg>(() => immediateWindow.RunLine(0), messageDlg =>
            {
                AssertEx.Contains(messageDlg.Message, Resources.ToolDescription_RunTool_Please_check_the_command_location_is_correct_for_this_tool_);
                messageDlg.OkDialog();
            });
            RunUI(immediateWindow.Dispose);
        }
    }
}
