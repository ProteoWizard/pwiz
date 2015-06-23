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
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
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
using TestStack.White.UIItems.ListViewItems;
using TestStack.White.UIItems.ListBoxItems;
using TestStack.White.UIItems.Finders;
using TestStack.White.UIItems.Container;
using TestStack.White.UIItems.Actions;
using TestStack.White.UIItems.Custom;
using System.Windows.Automation;
//using Microsoft.VisualStudio.Profiler;

namespace Test
{
    using AppRunner = Action<Application, Stack<Window>>;

    [TestClass]
    public class SpectrumViewTests : BaseInteractionTest
    {
        /*private List<List<string>> getRowsFromGridViewExport(Window window, Button exportButton)
        {
            exportButton.DoubleClick();
            var exportMenu = new PopUpMenu(window.GetElement(SearchCriteria.ByNativeProperty(AutomationElement.NameProperty, "DropDown")), new NullActionListener());
            var copyToClipboardMenuItem = exportMenu.Items[0];

            copyToClipboardMenuItem.Click();
            window.WaitWhileBusy();

            var clipboardText = System.Windows.Forms.Clipboard.GetText();
            return clipboardText.Split('\n').Select(o => o.TrimEnd('\r').Split('\t').ToList()).ToList();
        }*/

        private string getRowString(TableRow row, string emptyStringPlaceholder = "", string cellDelimiter = " ", bool includeRowHeader = true)
        {
            return row.GetValuesAsString(emptyStringPlaceholder, cellDelimiter, includeRowHeader);
        }

        [TestMethod]
        [TestCategory("GUI")]
        public void TestSpectrumView()
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

                //System.Windows.Forms.MessageBox.Show("Ready");
                //DataCollection.ResumeProfile(ProfileLevel.Global, DataCollection.CurrentId);

                window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                var dockableForms = window.GetDockableForms();

                var spectrumTableForm = dockableForms.Single(o => o.Id == "SpectrumTableForm");
                spectrumTableForm.Focus();
                var toolstrip = spectrumTableForm.RawGet<ToolStrip>("toolStrip");
                var exportButton = toolstrip.Get<Button>(SearchCriteria.ByText("exportButton"));

                Assert.AreEqual("Spectrum View: 1 groups, 1 sources, 207 spectra", spectrumTableForm.Name);

                // /							129	191	207	3	9								
                // 201203-624176-12				129	191	207	3	9								
                // 	0.1.171						8.465	831.3822	830.3749	830.377		-0.0021	1	0	EDVPSER
                // 		number of matched peaks = 8													
                // 		number of unmatched peaks = 3													
                // 		MyriMatch:MVH = 16.060721755028													
                // 		MyriMatch:mzFidelity = 29.437343800031													
                // 		xcorr = 1.18933083362579													
                // 	0.1.207						9.681	818.4236	817.4163	817.4181	-0.0018	1	0	GASIVEDK
                // 	...
                // 	0.1.1374					39.9247	1199.6108	1198.6036	1198.6063	-0.0027	1	0	FFVAPFPEVF
                // 		number of matched peaks = 11													
                // 		number of unmatched peaks = 4													
                // 		MyriMatch:MVH = 33.321454644203													
                // 		MyriMatch:mzFidelity = 45.387281396128													
                // 		xcorr = 1.49844166556597
                var spectrumView = new FastTable(spectrumTableForm.GetElement(SearchCriteria.ByAutomationId("treeDataGridView")), new NullActionListener());
                spectrumView.Focus();
                var rows = spectrumView.Rows;
                var columns = spectrumView.Header.Columns;
                int row = 0;
                Assert.AreEqual(2, rows.Count);
                Assert.AreEqual(14, columns.Count);
                Assert.AreEqual("Group/Source/Spectrum Distinct Peptides Distinct Matches Filtered Spectra Distinct Charges Protein Groups Scan Time Precursor m/z Observed Mass Exact Mass Mass Error Charge Q Value Sequence", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("129 191 207 3 9", getRowString(rows[row++]).Trim());
                Assert.AreEqual("201203-624176-12 129 191 207 3 9", getRowString(rows[row++]).Trim());

                // test expanding to show PSMs
                rows[1].Cells[0].Click();
                rows[1].KeyIn(TestStack.White.WindowsAPI.KeyboardInput.SpecialKeys.RIGHT);
                spectrumView.Refresh();
                rows = spectrumView.Rows;
                columns = spectrumView.Header.Columns;
                row = 0;
                Assert.AreEqual(209, rows.Count);
                Assert.AreEqual(14, columns.Count);
                Assert.AreEqual("Group/Source/Spectrum Distinct Peptides Distinct Matches Filtered Spectra Distinct Charges Protein Groups Scan Time Precursor m/z Observed Mass Exact Mass Mass Error Charge Q Value Sequence", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("129 191 207 3 9", getRowString(rows[row++]).Trim());
                Assert.AreEqual("201203-624176-12 129 191 207 3 9", getRowString(rows[row++]).Trim());
                Assert.AreEqual("0.1.171      8#4650 831#3822 830#3749 830#3770 -0#0021 1 0#00 EDVPSER".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]).Trim());
                Assert.AreEqual("0.1.207      9#6810 818#4236 817#4163 817#4181 -0#0018 1 0#00 GASIVEDK".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]).Trim());
                row = rows.Count - 1;
                Assert.AreEqual("0.1.1374      39#9247 1199#6108 1198#6036 1198#6063 -0#0027 1 0#00 FFVAPFPEVF".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]).Trim());
            },
            closeAppOnError: true);
        }
    }
}