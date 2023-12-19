/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakAreaProteinExpressionGraphTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakAreaProteinExpressionGraph()
        {
            TestFilesZip = @"TestFunctional\PeakAreaProteinExpressionGraphTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.ShowPeakAreaProteinExpressionGraph();
                PauseForManualTutorialStep();
                SkylineWindow.ShowIntensityFormatting();
                var peakAreaGraph = FormUtil.OpenForms.OfType<GraphSummary>().FirstOrDefault(graph =>
                    graph.Type == GraphTypeSummary.abundance && graph.Controller is AreaGraphController);
                Assert.IsNotNull(peakAreaGraph);
                var formattingDlg = FormUtil.OpenForms.OfType<VolcanoPlotFormattingDlg>().FirstOrDefault();
                Assert.IsNotNull(formattingDlg);
                var createExprDlg = ShowDialog<CreateMatchExpressionDlg>(() =>
                {
                    var bindingList = formattingDlg.GetCurrentBindingList();
                    formattingDlg.ClickCreateExpression(bindingList.Count - 1);
                });
            });
            WaitForGraphs();
            var formattingDlg = FormUtil.OpenForms.OfType<VolcanoPlotFormattingDlg>().FirstOrDefault();
            Assume.IsNotNull(formattingDlg);
            
            var createExpression = FormUtil.OpenForms.OfType<CreateMatchExpressionDlg>().FirstOrDefault();
            createExpression.ClickEnterList();
            var matchExpressionList = FormUtil.OpenForms.OfType<MatchExpressionListDlg>().FirstOrDefault();
            WaitForOpenForm<CreateMatchExpressionDlg>();
            PauseForManualTutorialStep();
            WaitForGraphs();
            RunUI(() =>
            {
                SkylineWindow.AreaScopeTo(AreaScope.protein);
            });
            WaitForGraphs();
            
        }
    }
}
