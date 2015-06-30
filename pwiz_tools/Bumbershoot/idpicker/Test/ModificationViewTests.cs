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
    public class ModificationViewTests : BaseInteractionTest
    {
        private string getRowString(TableRow row, string emptyStringPlaceholder = "", string cellDelimiter = " ", bool includeRowHeader = true)
        {
            return row.GetValuesAsString(emptyStringPlaceholder, cellDelimiter, includeRowHeader);
        }
        

        [TestMethod]
        [TestCategory("GUI")]
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
                // 44.9851     24                                                                 24
                // 42.0106    337    271                     66                                    
                // 37.9559    75                 44    31                                          
                // 28.0313    87                             57                30                  
                // 21.9819    205                96    109                                         
                // 15.9949    81                                   81                              
                // 14.0157    93                             68                25                  
                // -17.0265   36                                         36                        
                // -18.0106   9                        9
                var gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                var rows = gridViewTable.Rows;
                var columns = gridViewTable.Header.Columns;
                int row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 1375 271 198 140 149 191 81 36 55 126 60 68", getRowString(rows[row++], "0"));
                Assert.AreEqual("79#9663\nPhospho 230 0 0 0 0 0 0 0 0 126 60 44".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("57#0215\nCarbamidomethyl\nAla->Gln\nGly->Asn\nGly 198 0 198 0 0 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("44#9851\nNitro 24 0 0 0 0 0 0 0 0 0 0 24".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("42#0106\nAcetyl\nSer->Glu 337 271 0 0 0 66 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("37#9559\nCation:K 75 0 0 44 31 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("28#0313\nDimethyl\nDelta:H(4)C(2)\nEthyl\nAla->Val\nCys->Met 87 0 0 0 0 57 0 0 30 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("21#9819\nCation:Na 205 0 0 96 109 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("15#9949\nOxidation\nAla->Ser\nPhe->Tyr 81 0 0 0 0 0 81 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("14#0157\nMethyl\nAsp->Glu\nGly->Ala\nSer->Thr\nVal->Xle\nAsn->Gln 93 0 0 0 0 68 0 0 25 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("-17#0265\nGln->pyro-Glu\nAmmonia-loss 36 0 0 0 0 0 0 36 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 9 0 0 0 9 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));

                // test pivot combo box (distinct matches)
                // Mass     Total N-t C   D   E   K   M   Q   R   S   T   Y 
                // Total     1348 168 186 182 249 273 69  29  60  56  30  46
                // 79.9663   104                                  56  30  18
                // 57.0215   186      186                                   
                // 44.9851    28                                           28
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
                gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 1348 168 186 182 249 273 69 29 60 56 30 46", getRowString(rows[row++], "0"));
                Assert.AreEqual("79#9663\nPhospho 104 0 0 0 0 0 0 0 0 56 30 18".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("57#0215\nCarbamidomethyl\nAla->Gln\nGly->Asn\nGly 186 0 186 0 0 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("44#9851\nNitro 28 0 0 0 0 0 0 0 0 0 0 28".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("42#0106\nAcetyl\nSer->Glu 234 168 0 0 0 66 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("37#9559\nCation:K 125 0 0 61 64 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("28#0313\nDimethyl\nDelta:H(4)C(2)\nEthyl\nAla->Val\nCys->Met 94 0 0 0 0 62 0 0 32 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("21#9819\nCation:Na 300 0 0 121 179 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("15#9949\nOxidation\nAla->Ser\nPhe->Tyr 69 0 0 0 0 0 69 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("14#0157\nMethyl\nAsp->Glu\nGly->Ala\nSer->Thr\nVal->Xle\nAsn->Gln 173 0 0 0 0 145 0 0 28 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("-17#0265\nGln->pyro-Glu\nAmmonia-loss 29 0 0 0 0 0 0 29 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 6 0 0 0 6 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));

                // test pivot combo box (distinct peptides)
                // Mass    Total N-t C   D   E   K   M   Q   R   S   T   Y 
                // Total     768 134 149 95  95  116 55  29  33  23  20  19
                // 79.9663   51                                  23  20  8 
                // 57.0215   149     149                                   
                // 44.9851    11                                          11
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
                gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                rows = gridViewTable.Rows;
                columns = gridViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(12, rows.Count);
                Assert.AreEqual(12, columns.Count);
                Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("57#0215\nCarbamidomethyl\nAla->Gln\nGly->Asn\nGly 149 0 149 0 0 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("44#9851\nNitro 11 0 0 0 0 0 0 0 0 0 0 11".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("42#0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("37#9559\nCation:K 53 0 0 31 22 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("28#0313\nDimethyl\nDelta:H(4)C(2)\nEthyl\nAla->Val\nCys->Met 49 0 0 0 0 34 0 0 15 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("21#9819\nCation:Na 133 0 0 64 69 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("15#9949\nOxidation\nAla->Ser\nPhe->Tyr 55 0 0 0 0 0 55 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("14#0157\nMethyl\nAsp->Glu\nGly->Ala\nSer->Thr\nVal->Xle\nAsn->Gln 57 0 0 0 0 39 0 0 18 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("-17#0265\nGln->pyro-Glu\nAmmonia-loss 29 0 0 0 0 0 0 29 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));

                // test sorting options
                {
                    // test row sorting by total column (number of modifications at a mass)
                    columns[0].ClickAndWaitWhileBusy(window);
                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("42#0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    // test reversal of row sort order by total column (number of modifications at a mass)
                    columns[0].ClickAndWaitWhileBusy(window);
                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("42#0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    // test sorting by per-site total (delta mass is tie-breaker)
                    columns[1].ClickAndWaitWhileBusy(window);
                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("42#0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    // test returning to sort by delta-mass by clicking on top-left cell
                    var topLeftCellClickablePoint = new System.Windows.Point(rows[0].GetHeaderCell().ClickablePoint.X, columns[0].Bounds.Y + columns[0].Bounds.Height / 2);
                    window.Mouse.Click(topLeftCellClickablePoint);
                    window.WaitWhileBusy();

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    // test column sorting by total row (number of total modified spectra/matches/peptides at each site)
                    rows[0].GetHeaderCell().ClickAndWaitWhileBusy(window);
                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total C N-term K E D M R Q S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 149 134 116 95 95 55 33 29 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    // test reversal of column sort order by total row (number of total modified spectra/matches/peptides at each site)
                    rows[0].GetHeaderCell().ClickAndWaitWhileBusy(window);
                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total Y T S Q R M D E K N-term C", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 19 20 23 29 33 55 95 95 116 134 149", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 8 20 23 0 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 0 0 0 0 4 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    // test that right-clicking on top-left cell sorts by site
                    window.Mouse.Location = topLeftCellClickablePoint;
                    window.Mouse.RightClick();

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));
                }

                // get bounds for a cell outside the grid content and test that clicking there does not change sort order (this point is also used for committing changes from the other toolbar controls and Unimod filter)
                var rightmostCell = rows[0].Cells.Last();
                var pointOutsideGridViewContent = new System.Windows.Point(rightmostCell.Bounds.Right + rightmostCell.Bounds.Width, rightmostCell.Bounds.Bottom);
                {
                    window.Mouse.Click(pointOutsideGridViewContent);
                    window.WaitWhileBusy();

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));
                }

                // test round to nearest (and test that rows are merged as expected)
                {
                    roundToNearestUpDown.Increment();
                    window.WaitWhileBusy();
                    Assert.AreEqual(0.0010, roundToNearestUpDown.Value); // only the mass rounding should change, not the counts

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#966\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#011\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    roundToNearestUpDown.Increment();
                    window.WaitWhileBusy();
                    Assert.AreEqual(0.0100, roundToNearestUpDown.Value); // only the mass rounding should change, not the counts

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#97\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#01\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    roundToNearestUpDown.Increment();
                    window.WaitWhileBusy();
                    Assert.AreEqual(0.1000, roundToNearestUpDown.Value); // only the mass rounding should change, not the counts; but the delta mass annotations become ambiguous

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("80\n(4 Unimod matches) 51 0 0 0 0 0 0 0 0 23 20 8", getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18\n(6 Unimod matches) 4 0 0 0 4 0 0 0 0 0 0 0", getRowString(rows.Last(), "0"));

                    roundToNearestUpDown.Increment();
                    window.WaitWhileBusy();
                    Assert.AreEqual(1.0000, roundToNearestUpDown.Value); // the mass rounding and counts stays the same (because in this test data everything rounds to an integer for the nearest 0.1), but the delta mass annotations become even more ambiguous

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("80\n(5 Unimod matches) 51 0 0 0 0 0 0 0 0 23 20 8", getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18\n(8 Unimod matches) 4 0 0 0 4 0 0 0 0 0 0 0", getRowString(rows.Last(), "0"));

                    roundToNearestUpDown.Increment();
                    window.WaitWhileBusy();
                    Assert.AreEqual(10.000, roundToNearestUpDown.Value); // now some rows are merged so the row count and cell values will change

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(8, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("80\n(38 Unimod matches) 51 0 0 0 0 0 0 0 0 23 20 8", getRowString(rows[row++], "0"));
                    Assert.AreEqual("-20\n(57 Unimod matches) 33 0 0 0 4 0 0 29 0 0 0 0", getRowString(rows.Last(), "0"));

                    // enter rounding value manually
                    roundToNearestUpDown.DoubleClick();
                    roundToNearestUpDown.KeyIn(TestStack.White.WindowsAPI.KeyboardInput.SpecialKeys.DELETE);
                    roundToNearestUpDown.Enter(0.0001m.ToString(CultureInfo.CurrentCulture));
                    window.Mouse.Click(pointOutsideGridViewContent); // click outside the spinner to commit the changed value
                    window.WaitWhileBusy();
                    Assert.AreEqual(0.0001, roundToNearestUpDown.Value);

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));
                }

                // test column and row minimum filters
                {
                    minColumnTextBox.ClearAndEnter("100");
                    window.Mouse.Click(pointOutsideGridViewContent); // click outside the text box to commit the changed value

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(4, columns.Count);
                    Assert.AreEqual("Total N-term C K", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 116", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    minColumnTextBox.ClearAndEnter("1");
                    minRowTextBox.ClearAndEnter("100");
                    window.Mouse.Click(pointOutsideGridViewContent); // click outside the text box to commit the changed values
                    System.Threading.Thread.Sleep(500);

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(4, rows.Count);
                    Assert.AreEqual(12, columns.Count);

                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("57#0215\nCarbamidomethyl\nAla->Gln\nGly->Asn\nGly 149 0 149 0 0 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("42#0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("21#9819\nCation:Na 133 0 0 64 69 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));

                    // test that invalid values fall back to a safe default
                    minRowTextBox.ClearAndEnter("0");
                    window.Mouse.Click(pointOutsideGridViewContent); // click outside the text box to commit the changed value
                    Assert.AreEqual("1", minColumnTextBox.Text);
                    Assert.AreEqual("2", minRowTextBox.Text);
                }

                // test unimod filter
                {
                    //var modTableTree = new AutomationElementTreeNode(window.AutomationElement);
                    //var c = modTableTree.Children.Count;
                    //var modTableTree = new AutomationElementTreeNode(selectedMassesTree.AutomationElement);
                    //var c = modTableTree.Children.Count;

                    unimodButton.RaiseClickEvent();
                    window.WaitWhileBusy();
                    var unimodFilterControl = window.Get<Panel>("UnimodControl"); // not a child of modificationTableTree
                    var selectedMassesLabel = unimodFilterControl.Get<Label>("MassesLabel");
                    var selectedSitesLabel = unimodFilterControl.Get<Label>("SitesLabel");
                    var siteFilterComboBox = unimodFilterControl.Get<ComboBox>("SiteFilterBox");
                    var showHiddenCheckBox = unimodFilterControl.Get<CheckBox>("HiddenModBox");
                    var selectedMassesTree = unimodFilterControl.Get<Tree>("UnimodTree");

                    Assert.AreEqual("0", selectedSitesLabel.Text);
                    Assert.AreEqual("0", selectedMassesLabel.Text);

                    // test all mods (in this test data, all mod should snap to Unimod, so this should not change the number of rows)
                    var allModsNode = selectedMassesTree.Node("Unimod Modifications");
                    allModsNode.SelectAndToggle();

                    Assert.IsTrue(allModsNode.IsChecked());
                    Assert.AreEqual("10", selectedSitesLabel.Text);
                    Assert.AreEqual("8", selectedMassesLabel.Text);

                    showHiddenCheckBox.Toggle();
                    allModsNode.SelectAndToggle(); // uncheck
                    allModsNode.SelectAndToggle(); // recheck to get hidden nodes

                    Assert.AreEqual("11", selectedSitesLabel.Text);
                    Assert.AreEqual("11", selectedMassesLabel.Text);

                    unimodButton.RaiseClickEvent();
                    window.WaitWhileBusy();
                    window.Mouse.Click(pointOutsideGridViewContent); // click outside the popup to commit the new filter
                    window.WaitWhileBusy();

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(12, rows.Count);
                    Assert.AreEqual(12, columns.Count);
                    Assert.AreEqual("Total N-term C D E K M Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 55 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows.Last(), "0"));

                    allModsNode.SelectAndToggle();
                    var ptModsNode = selectedMassesTree.Node("Unimod Modifications", "PostTranslational");
                    ptModsNode.SelectAndToggle();
                    Assert.IsFalse(allModsNode.IsChecked());
                    Assert.IsTrue(ptModsNode.IsChecked());

                    Assert.AreEqual("10", selectedSitesLabel.Text);
                    Assert.AreEqual("7", selectedMassesLabel.Text);

                    unimodButton.RaiseClickEvent();
                    window.WaitWhileBusy();
                    window.Mouse.Click(pointOutsideGridViewContent); // click outside the popup to commit the new filter
                    window.WaitWhileBusy();

                    gridViewTable = modificationTableForm.GetFastTable("dataGridView");
                    rows = gridViewTable.Rows;
                    columns = gridViewTable.Header.Columns;
                    row = 0;
                    Assert.AreEqual(8, rows.Count);
                    Assert.AreEqual(11, columns.Count);
                    Assert.AreEqual("Total N-term C D E K Q R S T Y", String.Join(" ", columns.Select(o => o.Name)));
                    Assert.AreEqual("Total 768 134 149 95 95 116 29 33 23 20 19", getRowString(rows[row++], "0"));
                    Assert.AreEqual("79#9663\nPhospho 51 0 0 0 0 0 0 0 23 20 8".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("42#0106\nAcetyl\nSer->Glu 177 134 0 0 0 43 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("28#0313\nDimethyl\nDelta:H(4)C(2)\nEthyl\nAla->Val\nCys->Met 49 0 0 0 0 34 0 15 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("15#9949\nOxidation\nAla->Ser\nPhe->Tyr 55 0 0 0 0 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("14#0157\nMethyl\nAsp->Glu\nGly->Ala\nSer->Thr\nVal->Xle\nAsn->Gln 57 0 0 0 0 39 0 18 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-17#0265\nGln->pyro-Glu\nAmmonia-loss 29 0 0 0 0 0 29 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                    Assert.AreEqual("-18#0106\nDehydrated\nGlu->pyro-Glu 4 0 0 0 4 0 0 0 0 0 0".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++], "0"));
                }

                // test export (to clipboard, to file, and to excel)
                {

                    exportButton.DoubleClick();
                    //var modTableTree = new AutomationElementTreeNode(window.AutomationElement, 5);
                    //var c = modTableTree.Children.Count;
                    var exportMenu = new PopUpMenu(window.GetElement(SearchCriteria.ByNativeProperty(AutomationElement.NameProperty, "DropDown")), new NullActionListener());
                    var copyToClipboardMenuItem = exportMenu.Items[0];
                    var exportToFileMenuItem = exportMenu.Items[1];
                    var openInExcelMenuItem = exportMenu.Items[2];

                    string expectedTsvText = "Delta Mass\tTotal\tN-term\tC\tD\tE\tK\tQ\tR\tS\tT\tY\n" +
                                             "Total\t768\t134\t149\t95\t95\t116\t29\t33\t23\t20\t19\n" +
                                             "79#9663 ; Phospho\t51\t\t\t\t\t\t\t\t23\t20\t8\n" +
                                             "42#0106 ; Acetyl ; Ser->Glu\t177\t134\t\t\t\t43\t\t\t\t\t\n" +
                                             "28#0313 ; Dimethyl ; Delta:H(4)C(2) ; Ethyl ; Ala->Val ; Cys->Met\t49\t\t\t\t\t34\t\t15\t\t\t\n" +
                                             "15#9949 ; Oxidation ; Ala->Ser ; Phe->Tyr\t55\t\t\t\t\t\t\t\t\t\t\n" +
                                             "14#0157 ; Methyl ; Asp->Glu ; Gly->Ala ; Ser->Thr ; Val->Xle ; Asn->Gln\t57\t\t\t\t\t39\t\t18\t\t\t\n" +
                                             "-17#0265 ; Gln->pyro-Glu ; Ammonia-loss\t29\t\t\t\t\t\t29\t\t\t\t\n" +
                                             "-18#0106 ; Dehydrated ; Glu->pyro-Glu\t4\t\t\t\t4";
                    expectedTsvText = expectedTsvText.Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

                    IDPicker.Util.TryRepeatedly<Exception>(() =>
                    {
                        exportButton.ClickAndWaitWhileBusy(window);
                        copyToClipboardMenuItem.ClickAndWaitWhileBusy(window);
                        System.Threading.Thread.Sleep(500);
                        var clipboardText = System.Windows.Forms.Clipboard.GetText();
                        expectedTsvText.AssertMultilineStringEquals(clipboardText, "copyToClipboardTest");
                        }, 5, 500);

                    exportButton.ClickAndWaitWhileBusy(window);
                    exportToFileMenuItem.ClickAndWaitWhileBusy(window);
                    var saveDialog = new PopUpMenu(window.GetElement(SearchCriteria.ByNativeProperty(AutomationElement.NameProperty, "Save As")), new NullActionListener());

                    // HACK: saveDialog.Get<TextBox>() won't work because of some unsupported control types in the Save Dialog (at least on Windows 7); I'm not sure if the 1001 id is stable
                    string saveFilepath = TestContext.TestOutputPath("GridView.tsv");
                    var saveTarget = new TextBox(saveDialog.AutomationElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "1001")), new NullActionListener());
                    var saveButton = new Button(saveDialog.AutomationElement.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1"), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))), new NullActionListener());
                    saveTarget.BulkText = saveFilepath;
                    window.WaitWhileBusy();
                    saveButton.ClickAndWaitWhileBusy(window);

                    var exportedText = File.ReadAllText(saveFilepath).Replace("\"", "");
                    expectedTsvText.AssertMultilineStringEquals(exportedText, "exportToTsvTest");

                    // TODO: how to test Excel if it's not necessarily installed on the test agent?
                }

                // test that changing view filter changes mods? maybe not here
            },
            closeAppOnError: true);
        }

        [TestMethod]
        [TestCategory("GUI")]
        public void TestDetailView()
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

                window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
                var dockableForms = window.GetDockableForms();

                var modificationTableForm = dockableForms.Single(o => o.Id == "ModificationTableForm");
                var toolstrip = modificationTableForm.RawGet<ToolStrip>("toolStrip");

                Assert.AreEqual("Modification View: 1375 modified spectra", modificationTableForm.Name);

                // change to detail view
                var viewModeComboBox = toolstrip.Get<ComboBox>("viewModeComboBox");
                viewModeComboBox.Select(1);

                // test that irrelevant filters/controls are hidden and relevant filters/controls are shown in grid view
                Assert.AreEqual(false, toolstrip.Items.Any(o => o.Id == "pivotModeComboBox"));
                Assert.AreEqual(false, toolstrip.Items.Any(o => o.Id == "minColumnTextBox"));
                Assert.AreEqual(false, toolstrip.Items.Any(o => o.Id == "minRowTextBox"));
                var minMatchesTextBox = toolstrip.Get<TextBox>("minMatchesTextBox");
                var roundToNearestUpDown = toolstrip.Get<Spinner>("roundToNearestUpDown");
                var unimodButton = toolstrip.Get<Button>(SearchCriteria.ByText("unimodButton"));
                var exportButton = toolstrip.Get<Button>(SearchCriteria.ByText("exportButton"));

                Assert.AreEqual("2", minMatchesTextBox.Text);
                Assert.AreEqual(0.0001, roundToNearestUpDown.Value);

                // test that cell values are correct and the default sort order is correct:
                // Site Mass     DP  DM  Sp  Description
                // (    42.0106  134 168 271 Acetyl
                // C    57.0215  149 186 198 Carbamidomethyl
                // D    21.9819  64  121 96  Cation:Na
                // D    37.9559  31  61  44  Cation:K
                // E    -18.0106 4   6   9   Glu->pyro-Glu
                // E    21.9819  69  179 109 Cation:Na
                // E    37.9559  22  64  31  Cation:K
                // K    14.0157  39  145 68  Methyl
                // K    28.0313  34  62  57  Dimethyl; Delta:H(4)C(2); Ethyl
                // K    42.0106  43  66  66  Acetyl
                // M    15.9949  55  69  81  Oxidation
                // Q    -17.0265 29  29  36  Gln->pyro-Glu
                // R    14.0157  18  28  25  Methyl
                // R    28.0313  15  32  30  Dimethyl
                // S    79.9663  23  56  126 Phospho
                // T    79.9663  20  30  60  Phospho
                // Y    44.9851  11  28  24  Nitro
                // Y    79.9663  8   18  44  Phospho
                var detailViewTable = modificationTableForm.GetFastTable("detailDataGridView");
                var rows = detailViewTable.Rows;
                var columns = detailViewTable.Header.Columns;
                int row = 0;
                Assert.AreEqual(18, rows.Count);
                Assert.AreEqual(6, columns.Count);
                Assert.AreEqual("Site ΔMass Peptides Matches Spectra Description", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("( 42#0106 134 168 271 Acetyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("C 57#0215 149 186 198 Carbamidomethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("D 21#9819 64 121 96 Cation:Na".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("D 37#9559 31 61 44 Cation:K".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("E -18#0106 4 6 9 Glu->pyro-Glu".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("E 21#9819 69 179 109 Cation:Na".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("E 37#9559 22 64 31 Cation:K".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("K 14#0157 39 145 68 Methyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("K 28#0313 34 62 57 Dimethyl  Delta:H(4)C(2)  Ethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++])); // semicolons are used as separators in getRowString
                Assert.AreEqual("K 42#0106 43 66 66 Acetyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("M 15#9949 55 69 81 Oxidation".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Q -17#0265 29 29 36 Gln->pyro-Glu".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("R 14#0157 18 28 25 Methyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("R 28#0313 15 32 30 Dimethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("S 79#9663 23 56 126 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("T 79#9663 20 30 60 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Y 44#9851 11 28 24 Nitro".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Y 79#9663 8 18 44 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));

                // test sorting by delta mass (site is tie-breaker)
                columns[1].ClickAndWaitWhileBusy(window);
                detailViewTable = modificationTableForm.GetFastTable("detailDataGridView");
                rows = detailViewTable.Rows;
                columns = detailViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(18, rows.Count);
                Assert.AreEqual(6, columns.Count);
                Assert.AreEqual("Site ΔMass Peptides Matches Spectra Description", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("E -18#0106 4 6 9 Glu->pyro-Glu".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Q -17#0265 29 29 36 Gln->pyro-Glu".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                row = rows.Count - 4;
                Assert.AreEqual("C 57#0215 149 186 198 Carbamidomethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("S 79#9663 23 56 126 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("T 79#9663 20 30 60 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Y 79#9663 8 18 44 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));

                // test reversal of sort order
                columns[1].ClickAndWaitWhileBusy(window);
                detailViewTable = modificationTableForm.GetFastTable("detailDataGridView");
                rows = detailViewTable.Rows;
                columns = detailViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(18, rows.Count);
                Assert.AreEqual(6, columns.Count);
                Assert.AreEqual("Site ΔMass Peptides Matches Spectra Description", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("S 79#9663 23 56 126 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("T 79#9663 20 30 60 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Y 79#9663 8 18 44 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("C 57#0215 149 186 198 Carbamidomethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                row = rows.Count - 2;
                Assert.AreEqual("Q -17#0265 29 29 36 Gln->pyro-Glu".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("E -18#0106 4 6 9 Glu->pyro-Glu".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));

                // test returning to sort by site (delta mass is tie-breaker)
                columns[0].ClickAndWaitWhileBusy(window);
                detailViewTable = modificationTableForm.GetFastTable("detailDataGridView");
                rows = detailViewTable.Rows;
                columns = detailViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(18, rows.Count);
                Assert.AreEqual(6, columns.Count);
                Assert.AreEqual("Site ΔMass Peptides Matches Spectra Description", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("( 42#0106 134 168 271 Acetyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("C 57#0215 149 186 198 Carbamidomethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                row = rows.Count - 3;
                Assert.AreEqual("T 79#9663 20 30 60 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Y 44#9851 11 28 24 Nitro".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Y 79#9663 8 18 44 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));

                // test reversal of sort by site (delta mass is tie-breaker)
                columns[0].ClickAndWaitWhileBusy(window);
                detailViewTable = modificationTableForm.GetFastTable("detailDataGridView");
                rows = detailViewTable.Rows;
                columns = detailViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(18, rows.Count);
                Assert.AreEqual(6, columns.Count);
                Assert.AreEqual("Site ΔMass Peptides Matches Spectra Description", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("Y 44#9851 11 28 24 Nitro".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Y 79#9663 8 18 44 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("T 79#9663 20 30 60 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                row = rows.Count - 2;
                Assert.AreEqual("C 57#0215 149 186 198 Carbamidomethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("( 42#0106 134 168 271 Acetyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));

                // test sorting by spectral count
                columns[4].ClickAndWaitWhileBusy(window);
                detailViewTable = modificationTableForm.GetFastTable("detailDataGridView");
                rows = detailViewTable.Rows;
                columns = detailViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(18, rows.Count);
                Assert.AreEqual(6, columns.Count);
                Assert.AreEqual("Site ΔMass Peptides Matches Spectra Description", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("E -18#0106 4 6 9 Glu->pyro-Glu".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("Y 44#9851 11 28 24 Nitro".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                row = rows.Count - 3;
                Assert.AreEqual("S 79#9663 23 56 126 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("C 57#0215 149 186 198 Carbamidomethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("( 42#0106 134 168 271 Acetyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));

                // test reversal of sort by spectral count
                columns[4].ClickAndWaitWhileBusy(window);
                detailViewTable = modificationTableForm.GetFastTable("detailDataGridView");
                rows = detailViewTable.Rows;
                columns = detailViewTable.Header.Columns;
                row = 0;
                Assert.AreEqual(18, rows.Count);
                Assert.AreEqual(6, columns.Count);
                Assert.AreEqual("Site ΔMass Peptides Matches Spectra Description", String.Join(" ", columns.Select(o => o.Name)));
                Assert.AreEqual("( 42#0106 134 168 271 Acetyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("C 57#0215 149 186 198 Carbamidomethyl".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("S 79#9663 23 56 126 Phospho".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                row = rows.Count - 2;
                Assert.AreEqual("Y 44#9851 11 28 24 Nitro".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
                Assert.AreEqual("E -18#0106 4 6 9 Glu->pyro-Glu".Replace("#", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), getRowString(rows[row++]));
            },
            closeAppOnError: true);
        }

        [TestMethod]
        [TestCategory("GUI")]
        public void TestSiteView()
        {
            // TODO: should the embed spectra for PhosphoRS testing occur here, or should a new data file be added that already has the PhosphoRS probabilities calculated?
        }
    }
}
