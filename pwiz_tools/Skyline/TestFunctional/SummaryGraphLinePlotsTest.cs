/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SummaryGraphLinePlotsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestSummaryGraphLinePlots()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(@"Rat_plasma.sky");
            Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.none.ToString();

            RunUI(SkylineWindow.ShowRTReplicateGraph);
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            SelectNode(SrmDocument.Level.MoleculeGroups, 3);
            WaitForGraphs();

            const int repCount = 42;
            const int removedPeaks = 5;
            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.lines, 3, repCount);
            VerifyGraphState(SkylineWindow.GraphRetentionTime, AreaGraphDisplayType.bars, 3, repCount);

            RunUI(() => SkylineWindow.ShowRTPeptideValue(RTPeptideValue.FWHM));
            WaitForGraphs();
            VerifyGraphState(SkylineWindow.GraphRetentionTime, AreaGraphDisplayType.lines, 3, repCount);

            SelectNode(SrmDocument.Level.MoleculeGroups, 2);
            WaitForGraphs();

            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.lines, 1, repCount-removedPeaks);
            VerifyGraphState(SkylineWindow.GraphRetentionTime, AreaGraphDisplayType.lines, 1, repCount-removedPeaks);

            SelectNode(SrmDocument.Level.Molecules, 3);
            WaitForGraphs();

            const int tranCount = 5;
            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.bars, tranCount, repCount+1);
            VerifyGraphState(SkylineWindow.GraphRetentionTime, AreaGraphDisplayType.lines, tranCount, repCount - removedPeaks);

            RunUI(() => SkylineWindow.SetAreaGraphDisplayType(AreaGraphDisplayType.lines));
            WaitForGraphs();

            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.lines, tranCount, repCount - removedPeaks + 1);
            VerifyGraphState(SkylineWindow.GraphRetentionTime, AreaGraphDisplayType.lines, tranCount, repCount - removedPeaks);

            RunUI(() => SkylineWindow.GroupByReplicateAnnotation("SubjectId"));
            WaitForGraphs();

            int subjectCount = repCount/3;
            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.lines, tranCount, subjectCount + 1, subjectCount);
            VerifyGraphState(SkylineWindow.GraphRetentionTime, AreaGraphDisplayType.lines, tranCount, subjectCount, subjectCount);

            ActivateReplicate("D_103_REP1");
            WaitForGraphs();
            RunUI(() => SkylineWindow.RemovePeak());
            ActivateReplicate("D_103_REP2");
            WaitForGraphs();
            RunUI(() => SkylineWindow.RemovePeak());
            WaitForGraphs();

            subjectCount--;
            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.lines, tranCount, subjectCount + 1, subjectCount);
            VerifyGraphState(SkylineWindow.GraphRetentionTime, AreaGraphDisplayType.lines, tranCount, subjectCount, subjectCount);

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.GLOBAL_STANDARDS));
            WaitForGraphs();
            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.lines, tranCount, subjectCount + 1, subjectCount);
            VerifyGraphYRange(SkylineWindow.GraphPeakArea, 5E-5, 2E-2, subjectCount + 1, subjectCount);

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.TOTAL));
            WaitForGraphs();
            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.lines, tranCount, subjectCount + 1, subjectCount);
            VerifyGraphYRange(SkylineWindow.GraphPeakArea, 5, 50, subjectCount + 1, subjectCount);

            subjectCount++;
            SelectNode(SrmDocument.Level.MoleculeGroups, 13);
            WaitForGraphs();
            VerifyGraphState(SkylineWindow.GraphPeakArea, AreaGraphDisplayType.lines, 2, subjectCount, subjectCount);
            VerifyGraphYRange(SkylineWindow.GraphPeakArea, 4E+4, 1.5E+5, subjectCount, subjectCount);
        }

        private void VerifyGraphState(GraphSummary graphSummary, AreaGraphDisplayType displayType, int repCount, int pointCount, int errorCount = 0)
        {
            RunUI(() =>
            {
                Assert.AreEqual(repCount, graphSummary.CountCurves(c =>
                {
                    if (pointCount != CountPoints(c))
                        Assert.AreEqual(pointCount, CountPoints(c));
                    if (errorCount != CountErrors(c))
                        Assert.AreEqual(errorCount, CountErrors(c));
                    return displayType == AreaGraphDisplayType.bars
                        ? c.IsBar
                        : c.IsLine;
                }));
            });
        }

        private void VerifyGraphYRange(GraphSummary graphSummary, double minHeight, double maxHeight, int pointCount, int errorCount = 0)
        {
            RunUI(() =>
            {
                graphSummary.CountCurves(c =>
                {
                    if (pointCount != CountPoints(c, minHeight, maxHeight))
                        Assert.AreEqual(pointCount, CountPoints(c, minHeight, maxHeight));
                    if (errorCount != CountErrors(c, minHeight, maxHeight))
                        Assert.AreEqual(errorCount, CountErrors(c, minHeight, maxHeight));
                    return true;
                });
            });
        }

        private int CountPoints(CurveItem curveItem)
        {
            var pointList = curveItem.Points as PointPairList;
            Assert.IsNotNull(pointList);
            return pointList.Count(p => p.Y != PointPairBase.Missing);
        }

        private int CountPoints(CurveItem curveItem, double minHeight, double maxHeight)
        {
            var pointList = curveItem.Points as PointPairList;
            Assert.IsNotNull(pointList);
            return pointList.Count(p => p.Y != PointPairBase.Missing && minHeight < p.Y && p.Y < maxHeight);
        }

        private int CountErrors(CurveItem curveItem)
        {
            var pointList = curveItem.Points as PointPairList;
            Assert.IsNotNull(pointList);
            return pointList.Count(HasError);
        }

        private int CountErrors(CurveItem curveItem, double minHeight, double maxHeight)
        {
            var pointList = curveItem.Points as PointPairList;
            Assert.IsNotNull(pointList);
            return pointList.Count(p => HasError(p, maxHeight - minHeight));
        }

        private bool HasError(PointPair pointPair)
        {
            return HasError(pointPair, null);
        }

        private bool HasError(PointPair pointPair, double? range)
        {
            var errorTag = pointPair.Tag as ErrorTag;
            if (errorTag != null && errorTag.Error != 0 && errorTag.Error != PointPairBase.Missing)
                return true;
            else
                return false;
        }
    }
}