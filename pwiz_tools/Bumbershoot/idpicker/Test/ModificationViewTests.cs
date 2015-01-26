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
using System.Windows.Automation;
//using Microsoft.VisualStudio.Profiler;

namespace Test
{
    using AppRunner = Action<Application, Stack<Window>>;
    using System.Drawing;

    [TestClass]
    public class ModificationViewTests
    {
        public TestContext TestContext { get; set; }
        public bool CloseAppOnError { get { return false; } }

        #region Test initialization and cleanup
        public Application Application { get { return TestContext.Properties["Application"] as Application; } set { TestContext.Properties["Application"] = value; } }
        public string TestOutputSubdirectory { get { return (string)TestContext.Properties["TestOutputSubdirectory"]; } set { TestContext.Properties["TestOutputSubdirectory"] = value; } }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            testContext.Properties["Application"] = null;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            try
            {
                //Application.Attach("IDPicker").Close();
            }
            catch (WhiteException e)
            {
                if (!e.Message.Contains("Could not find process"))
                    throw e;
            }
        }

        [TestInitialize]
        public void TestInitialize()
        {
            TestOutputSubdirectory = TestContext.TestName;
            Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
        }
        #endregion

        private string getRowString(TableRow row, string emptyStringPlaceholder = "", string cellDelimiter = " ", bool includeRowHeader = true, bool forceRefresh = false)
        {
            IEnumerable<string> rowValue;
            if (forceRefresh)
            {
                rowValue = row.Cells.Select(o => (string)o.Value == String.Empty ? emptyStringPlaceholder : o.Value.ToString());
                if (includeRowHeader)
                    rowValue = new List<string> { row.GetHeaderCell().Name }.Concat(rowValue);
            }
            else
            {
                rowValue = row.AutomationElement.GetCurrentPropertyValue(ValuePatternIdentifiers.ValueProperty).ToString().Split(';').Select(o => o == String.Empty ? emptyStringPlaceholder : o);
                if (!includeRowHeader)
                    rowValue = rowValue.Skip(1);
            }
            return String.Join(cellDelimiter, rowValue);
        }

        [TestMethod]
        public void TestGridView()
        {
            var inputFiles = new string[] { "iPRG2012_PP1.2.idpDB" };

            TestOutputSubdirectory = TestContext.TestName;
            TestContext.CopyTestInputFiles(inputFiles);
            TestContext.LaunchAppTest("IDPicker.exe", TestContext.TestOutputPath("iPRG2012_PP1.2.idpDB").QuotePathWithSpaces() + " --test-ui-layout",

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

                var modificationTableForm = dockableForms.Single(o => o.Id == "ModificationTableForm");
                var toolstrip = modificationTableForm.RawGet<ToolStrip>("toolStrip");

                Assert.AreEqual("Modification View: 1375 modified spectra", modificationTableForm.Name);

                // test that grid view is default
                var viewModeComboBox = toolstrip.Get<ComboBox>("viewModeComboBox");
                Assert.AreEqual("Grid View", viewModeComboBox.SelectedItemText);

                // test that spectra pivot mode is default
                var pivotModeComboBox = toolstrip.Get<ComboBox>("pivotModeComboBox");
                Assert.AreEqual("Spectra", pivotModeComboBox.SelectedItemText);

                // test that irrelevant filters/controls are hidden and relevant filters/controls are shown in grid view
                var minColumnTextBox = toolstrip.Get<TextBox>("minColumnTextBox");
                var minRowTextBox = toolstrip.Get<TextBox>("minRowTextBox");
                Assert.AreEqual(false, toolstrip.Items.Any(o => o.Id == "minMatchesTextBox"));
                var roundToNearestUpDown = toolstrip.Get<Spinner>("roundToNearestUpDown");
                var unimodButton = toolstrip.Get<Button>(SearchCriteria.ByText("unimodButton"));
                var exportButton = toolstrip.Get<Button>(SearchCriteria.ByText("exportButton"));

                Assert.AreEqual("2", minColumnTextBox.Text);
                Assert.AreEqual("2", minRowTextBox.Text);
                Assert.AreEqual(0.0001, roundToNearestUpDown.Value);

                // test that cell values are correct and the default sort order is correct:
                // (rows sorted descending by mass and columns sorted ascending by site, with totals for the first row and column)
                // Mass       Total  N-t   C     D     E     K     M     Q     R     S     T     Y 
                // Total      1375   271   198   140   149   191   81    36    55    126   60    68
                // 79.9663    230                                                    126   60    44
                // 57.0215    198          198                                                     
                // 44.985     24                                                                 24
                // 42.0106    337    271                     66                                    
                // 37.9559    75                 44    31                                          
                // 28.0313    87                             57                30                  
                // 21.9819    205                96    109                                         
                // 15.9949    81                                   81                              
                // 14.0157    93                             68                25                  
                // -17.0265   36                                         36                        
                // -18.0106   9                        9
                var gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                var rows = gridViewTable.Rows;
                var properties = rows.Select(o => o.AutomationElement.GetCurrentPropertyValue(ValuePatternIdentifiers.ValueProperty)).ToList();
                var columns = gridViewTable.Header.Columns;
                int row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 1375 271 198 140 149 191 81 36 55 126 60 68", getRowString(rows[row++], "0"));
                Assert.AreEqual("79.9663\nPhospho 230 0 0 0 0 0 0 0 0 126 60 44", getRowString(rows[row++], "0"));
                Assert.AreEqual("57.0215\nCarbamidomethyl\nAla->Gln\nGly->Asn\nGly 198 0 198 0 0 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("44.985\nNitro 24 0 0 0 0 0 0 0 0 0 0 24", getRowString(rows[row++], "0"));
                Assert.AreEqual("42.0106\nAcetyl\nSer->Glu 337 271 0 0 0 66 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("37.9559\nCation:K 75 0 0 44 31 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("28.0313\nDimethyl\nDelta:H(4)C(2)\nEthyl\nAla->Val\nCys->Met 87 0 0 0 0 57 0 0 30 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("21.9819\nCation:Na 205 0 0 96 109 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("15.9949\nOxidation\nAla->Ser\nPhe->Tyr 81 0 0 0 0 0 81 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("14.0157\nMethyl\nAsp->Glu\nGly->Ala\nSer->Thr\nVal->Xle\nAsn->Gln 93 0 0 0 0 68 0 0 25 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("-17.0265\nGln->pyro-Glu\nAmmonia-loss 36 0 0 0 0 0 0 36 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 9 0 0 0 9 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));

                // test pivot combo box (distinct matches)
                // Mass     Total N-t C   D   E   K   M   Q   R   S   T   Y 
                // Total     1348 168 186 182 249 273 69  29  60  56  30  46
                // 79.9663   104                                  56  30  18
                // 57.0215   186      186                                   
                // 44.985    28                                           28
                // 42.0106   234  168             66                        
                // 37.9559   125          61  64                            
                // 28.0313   94                   62          32            
                // 21.9819   300          121 179                           
                // 15.9949   69                       69                    
                // 14.0157   173                  145         28            
                // -17.0265  29                           29                
                // -18.0106  6                6
                pivotModeComboBox.Select(1);
                statusText.WaitForReady();
                gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 1348 168 186 182 249 273 69 29 60 56 30 46", getRowString(rows[row++], "0"));
                Assert.AreEqual("79.9663\nPhospho 104 0 0 0 0 0 0 0 0 56 30 18", getRowString(rows[row++], "0"));
                Assert.AreEqual("57.0215\nCarbamidomethyl\nAla->Gln\nGly->Asn\nGly 186 0 186 0 0 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("44.985\nNitro 28 0 0 0 0 0 0 0 0 0 0 28", getRowString(rows[row++], "0"));
                Assert.AreEqual("42.0106\nAcetyl\nSer->Glu 234 168 0 0 0 66 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("37.9559\nCation:K 125 0 0 61 64 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("28.0313\nDimethyl\nDelta:H(4)C(2)\nEthyl\nAla->Val\nCys->Met 94 0 0 0 0 62 0 0 32 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("21.9819\nCation:Na 300 0 0 121 179 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("15.9949\nOxidation\nAla->Ser\nPhe->Tyr 69 0 0 0 0 0 69 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("14.0157\nMethyl\nAsp->Glu\nGly->Ala\nSer->Thr\nVal->Xle\nAsn->Gln 173 0 0 0 0 145 0 0 28 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("-17.0265\nGln->pyro-Glu\nAmmonia-loss 29 0 0 0 0 0 0 29 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 6 0 0 0 6 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));

                // test pivot combo box (distinct peptides)
                // Mass    Total N-t C   D   E   K   M   Q   R   S   T   Y 
                // Total     768 134 149 95  95  116 55  29  33  23  20  19
                // 79.9663   51                                  23  20  8 
                // 57.0215   149     149                                   
                // 44.985    11                                          11
                // 42.0106   177 134             43                        
                // 37.9559   53          31  22                            
                // 28.0313   49                  34          15            
                // 21.9819   133         64  69                            
                // 15.9949   55                      55                    
                // 14.0157   57                  39          18            
                // -17.0265  29                          29              	  
                // -18.0106  4               4
                pivotModeComboBox.Select(2);
                statusText.WaitForReady();
                gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                Assert.AreEqual("79.9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8", getRowString(rows[row++], "0"));
                Assert.AreEqual("57.0215\nCarbamidomethyl\nAla->Gln\nGly->Asn\nGly 149 0 149 0 0 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("44.985\nNitro 11 0 0 0 0 0 0 0 0 0 0 11", getRowString(rows[row++], "0"));
                Assert.AreEqual("42.0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("37.9559\nCation:K 53 0 0 31 22 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("28.0313\nDimethyl\nDelta:H(4)C(2)\nEthyl\nAla->Val\nCys->Met 49 0 0 0 0 34 0 0 15 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("21.9819\nCation:Na 133 0 0 64 69 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("15.9949\nOxidation\nAla->Ser\nPhe->Tyr 55 0 0 0 0 0 55 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("14.0157\nMethyl\nAsp->Glu\nGly->Ala\nSer->Thr\nVal->Xle\nAsn->Gln 57 0 0 0 0 39 0 0 18 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("-17.0265\nGln->pyro-Glu\nAmmonia-loss 29 0 0 0 0 0 0 29 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));

                // test row sorting by total column (number of modifications at a mass)
                columns[0].Click();
                gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                Assert.AreEqual("42.0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0", getRowString(rows.Last(), "0"));

                // test reversal of row sort order by total column (number of modifications at a mass)
                columns[0].Click();
                gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("42.0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0", getRowString(rows.Last(), "0"));

                // test sorting by per-site total (delta mass is tie-breaker)
                columns[1].Click();
                gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                Assert.AreEqual("42.0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0", getRowString(rows[row++], "0"));
                Assert.AreEqual("79.9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8", getRowString(rows[row++], "0"));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0", getRowString(rows.Last(), "0"));

                // test returning to sort by delta-mass by clicking on top-left cell
                var totalRowHeaderCellClickablePoint = rows[0].GetHeaderCell().ClickablePoint;
                var topLeftCellClickablePoint = new System.Windows.Point(totalRowHeaderCellClickablePoint.X, columns[0].Bounds.Y + columns[0].Bounds.Height / 2);
                window.Mouse.Click(topLeftCellClickablePoint);
                window.Mouse.Click(topLeftCellClickablePoint); // FIXME: this extra click is to get the grid in descending order but that should happen on the first click
                window.WaitWhileBusy();

                gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                Assert.AreEqual("79.9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8", getRowString(rows[row++], "0"));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0", getRowString(rows.Last(), "0"));

                // test column sorting by total row (number of total modified spectra/matches/peptides at each site)
                window.Mouse.Click(totalRowHeaderCellClickablePoint);
                window.WaitWhileBusy();
                gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total C N-term K D E M R Q S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 768 149 134 116 95 95 55 33 29 23 20 19", getRowString(rows[row++], "0", forceRefresh: true));
                Assert.AreEqual("79.9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8", getRowString(rows[row++], "0", forceRefresh: true));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 0 4 0 0 0 0 0 0", getRowString(rows.Last(), "0", forceRefresh: true));

                // test reversal of column sort order by total row (number of total modified spectra/matches/peptides at each site)
                window.Mouse.Click(totalRowHeaderCellClickablePoint);
                window.WaitWhileBusy();
                gridViewTable = modificationTableForm.RawGet<Table>("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total Y T S Q R M D E K N-term C", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 768 19 20 23 29 33 55 95 95 116 134 149", getRowString(rows[row++], "0", forceRefresh: true));
                Assert.AreEqual("79.9663\nPhospho 51 8 20 23 0 0 0 0 0 0 0 0", getRowString(rows[row++], "0", forceRefresh: true));
                Assert.AreEqual("-18.0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 0 0 0 0 4 0 0 0", getRowString(rows.Last(), "0", forceRefresh: true));

                // test that clicking outside any content cells does not change sort order
                // test column and row minimum filters 
                // test round to nearest (and test that rows are matched to the correct Unimod mods)
                // test unimod filter
                // test export (to clipboard, to file, and to excel)

                // test that changing view filter changes mods? maybe not here
            },
            closeAppOnError: true);
        }

        [TestMethod]
        public void TestDetailView()
        {
        }

        [TestMethod]
        public void TestSiteView()
        {
        }
    }
}
