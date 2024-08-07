/*
 * Original author: Henry Sanford <henrytsanford .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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

using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakAreaRelativeAbundanceGraphTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakAreaRelativeAbundanceGraph()
        {
            TestFilesZip = @"TestFunctional\PeakAreaRelativeAbundanceGraphTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.ShowPeakAreaRelativeAbundanceGraph();
                var graphPane = FindGraphPane();
                Assert.IsNotNull(graphPane);
                var peakAreaGraph = FormUtil.OpenForms.OfType<GraphSummary>().FirstOrDefault(graph =>
                    graph.Type == GraphTypeSummary.abundance && graph.Controller is AreaGraphController);
                Assert.IsNotNull(peakAreaGraph);

                // Verify that setting the targets to Proteins or Peptides produces the correct number of points
                SkylineWindow.SetAreaProteinTargets(true);
                Assert.AreEqual(48, graphPane.CurveList.Sum(curve=>curve.NPts));
                SkylineWindow.SetAreaProteinTargets(false);
                Assert.AreEqual(125, graphPane.CurveList.Sum(curve=>curve.NPts));

                // Verify that excluding peptide lists reduces the number of points
                SkylineWindow.SetExcludePeptideListsFromAbundanceGraph(true);
                Assert.AreEqual(45, graphPane.CurveList.Sum(curve => curve.NPts));
                //CONSIDER add quantitative checks for relative abundance results
            });

            TestFormattingDialog();
        }

        private void TestFormattingDialog()
        {
            RunUI(()=>
            {
                Settings.Default.ExcludeStandardsFromAbundanceGraph = false;
                SkylineWindow.SetExcludePeptideListsFromAbundanceGraph(true);
                SkylineWindow.SetAreaProteinTargets(false);
            });
            Assert.AreEqual(RelativeAbundanceFormatting.DEFAULT, SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting);
            var pane = FindGraphPane();

            var formattingDlg = ShowDialog<VolcanoPlotFormattingDlg>(pane.ShowFormattingDialog);
            // Add a line which says all peptides containing "QE" should be indigo diamonds 
            // and all peptides containing "GQ" should be turquoise triangles
            RunUI(() =>
            {
                Assert.AreEqual(Skyline.Controls.GroupComparison.GroupComparisonResources
                        .VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Protein_Expression_Formatting,
                    formattingDlg.Text);
                var row = formattingDlg.GetCurrentBindingList().AddNew();
                Assert.IsNotNull(row);
                row.Expression = new MatchExpression("QE", new[] { MatchOption.PeptideSequence }).ToString();
                row.PointSymbol = PointSymbol.Diamond;
                row.Color = Color.FromArgb(Color.Indigo.ToArgb());
                row = formattingDlg.GetCurrentBindingList().AddNew();
                Assert.IsNotNull(row);
                row.Expression = new MatchExpression("GQ", new[] { MatchOption.PeptideSequence }).ToString();
                row.PointSymbol = PointSymbol.Triangle;
                row.Color = Color.FromArgb(Color.Turquoise.ToArgb());
            });
            WaitForGraphs();

            // Verify that 2 peptides are drawn as diamonds and 2 are drawn as triangles
            RunUI(() =>
            {
                var diamondCurve = pane.CurveList.OfType<LineItem>().Single(curve => curve.Symbol.Type == SymbolType.Diamond);
                Assert.AreEqual(2, diamondCurve.Points.Count);
                var triangleCurve = pane.CurveList.OfType<LineItem>().Single(curve => curve.Symbol.Type == SymbolType.Triangle);
                Assert.AreEqual(2, triangleCurve.Points.Count);
            });

            // The document should still have its original RelativeAbundanceFormatting because the formatting dialog has not been OK'd yet
            Assert.AreEqual(RelativeAbundanceFormatting.DEFAULT, SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting);
            
            OkDialog(formattingDlg, formattingDlg.OkDialog);
            // The document should have the new RelativeAbundanceFormatting
            WaitForCondition(() => !Equals(SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting,
                RelativeAbundanceFormatting.DEFAULT));
            var relativeAbundanceFormatting = SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting;
            Assert.AreEqual(2, relativeAbundanceFormatting.ColorRows.Count());
            Assert.AreEqual(PointSymbol.Diamond, relativeAbundanceFormatting.ColorRows.First().PointSymbol);
            Assert.AreEqual(PointSymbol.Triangle, relativeAbundanceFormatting.ColorRows.ElementAt(1).PointSymbol);
            
            // Include peptide lists and verify that the number of diamonds on the graph has changed to 4
            RunUI(() =>
            {
                SkylineWindow.SetExcludePeptideListsFromAbundanceGraph(false);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                var diamondCurve = pane.CurveList.OfType<LineItem>().Single(curve => curve.Symbol.Type == SymbolType.Diamond);
                Assert.AreEqual(4, diamondCurve.Points.Count);
                var triangleCurve = pane.CurveList.OfType<LineItem>().Single(curve => curve.Symbol.Type == SymbolType.Triangle);
                Assert.AreEqual(3, triangleCurve.Points.Count);
            });

            // Save and reopen the document
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.OpenFile(SkylineWindow.DocumentFilePath);
            });
            WaitForDocumentLoaded();

            relativeAbundanceFormatting = SkylineWindow.Document.Settings.DataSettings.RelativeAbundanceFormatting;
            Assert.AreEqual(2, relativeAbundanceFormatting.ColorRows.Count());
            Assert.AreEqual(PointSymbol.Diamond, relativeAbundanceFormatting.ColorRows.First().PointSymbol);
            Assert.AreEqual(PointSymbol.Triangle, relativeAbundanceFormatting.ColorRows.ElementAt(1).PointSymbol);

            pane = FindGraphPane();
            Assert.IsNotNull(pane);

            // Bring up the formatting dialog again, make a change, change it back, and OK dialog
            formattingDlg = ShowDialog<VolcanoPlotFormattingDlg>(pane.ShowFormattingDialog);
            RunUI(() =>
            {
                Assert.AreEqual(PointSymbol.Diamond, formattingDlg.GetCurrentBindingList()[0].PointSymbol);
                formattingDlg.GetCurrentBindingList()[0].PointSymbol = PointSymbol.Plus;
            });
            WaitForGraphs();
            RunUI(() => formattingDlg.GetCurrentBindingList()[0].PointSymbol =PointSymbol.Diamond);
            WaitForGraphs();
            OkDialog(formattingDlg, formattingDlg.OkDialog);
        }

        private SummaryRelativeAbundanceGraphPane FindGraphPane()
        {
            foreach (var graphSummary in SkylineWindow.ListGraphPeakArea)
            {
                if (graphSummary.TryGetGraphPane<SummaryRelativeAbundanceGraphPane>(out var pane))
                {
                    return pane;
                }
            }
            return null;
        }
    }
}
