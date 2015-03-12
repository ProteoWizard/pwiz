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
// Copyright 2015 Vanderbilt University
//
// Contributor(s):
//

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestStack.White;
using TestStack.White.Factory;
using TestStack.White.Configuration;
using TestStack.White.UIItems;
using TestStack.White.UIItems.WindowItems;
using TestStack.White.UIItems.WindowStripControls;
using TestStack.White.UIItems.MenuItems;
using TestStack.White.UIItems.TreeItems;
using TestStack.White.UIItems.TableItems;
using TestStack.White.UIItems.ListBoxItems;
using TestStack.White.UIItems.Finders;
using TestStack.White.UIItems.Container;
using TestStack.White.UIItems.Actions;
using TestStack.White.UIItems.Custom;

namespace Test
{
    [TestClass]
    public class EmbedSpectraTests : BaseInteractionTest
    {
        [TestMethod]
        [TestCategory("GUI")]
        public void TestPhosphoAttestation()
        {
            var inputFiles = new string[] { "201203-624176-12-mm-gui-test.idpDB" };

            TestOutputSubdirectory = TestContext.TestName;
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath(inputFiles[0]).QuotePathWithSpaces() + " --test-ui-layout",

            (app, windowStack) =>
            {
                var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                windowStack.Push(window);

                var statusBar = window.Get<StatusStrip>();
                var statusText = statusBar.Get<TextBox>();
                statusText.WaitForReady();

                var menu = window.RawGet<MenuBar>(SearchCriteria.ByAutomationId("menuStrip1"), 2);
                //menu.MenuItemBy(SearchCriteria.ByAutomationId("toolsToolStripMenuItem"), SearchCriteria.ByAutomationId("optionsToolStripMenuItem")).Click();
                menu.MenuItem("File", "Embed...").RaiseClickEvent(); // FIXME: not localized, but the AutomationIds aren't being set properly so the above line won't work

                var dockableForms = window.GetDockableForms();

                var embedDialog = window.ModalWindow(SearchCriteria.ByAutomationId("EmbedderForm"), InitializeOption.WithCache);
                var extensionsTextBox = embedDialog.Get<TextBox>("extensionsTextBox");
                var searchPathTextBox = embedDialog.Get<TextBox>("searchPathTextBox");
                var deleteAllButton = embedDialog.Get<Button>("deleteAllButton");
                var embedAllButton = embedDialog.Get<Button>("embedAllButton");
                var closeButton = embedDialog.Get<Button>("okButton");
                var embedScanTimeOnlyCheckBox = embedDialog.Get<CheckBox>("embedScanTimeOnlyBox");
                var defaultQuantitationMethodComboBox = embedDialog.Get<ComboBox>("defaultQuantitationMethodBox");
                var defineQuantitationSettingsButton = embedDialog.Get<Button>("defaultQuantitationSettingsButton");

                // TODO: add another source to the GUI test idpDB? probably a good idea to cover more use cases (e.g. source grouping)

                // test scan times are already embedded
                {
                    var dataGridView = embedDialog.GetFastTable("dataGridView");
                    var rows = dataGridView.Rows;
                    Assert.AreEqual(1, rows.Count);
                    Assert.AreEqual("201203-624176-12;279 spectra with scan times;None;n/a", rows[0].GetValuesAsString());
                }

                // test embedding spectra; test that they are used when visualizing?
                // test deleting embedded info
            });
        }
    }
}
