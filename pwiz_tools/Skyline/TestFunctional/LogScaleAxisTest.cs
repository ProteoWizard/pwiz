/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LogScaleAxisTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLogScaleAxis()
        {
            TestFilesZip = @"TestFunctional\LogScaleAxisTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("LogScaleAxisTest.sky"));
                SkylineWindow.ShowPeakAreaReplicateComparison();
                var areaReplicateGraph = FindAreaReplicateGraph();
                Assert.IsNotNull(areaReplicateGraph);
                areaReplicateGraph.DockState = DockState.DockBottom;
                SkylineWindow.ShowPeakAreaPeptideGraph();
                var areaPeptideGraph = FindAreaPeptideGraph();
                Assert.IsNotNull(areaPeptideGraph);
                areaPeptideGraph.DockState = DockState.DockTop;
            });
            foreach (var configuration in EnumerateConfigurations(SkylineWindow.Document))
            {
                RunUI(() =>
                {
                    SkylineWindow.SelectedPath = configuration.IdentityPath;
                    SkylineWindow.ShowPeptideLogScale(configuration.LogScale);
                    SkylineWindow.SetNormalizationMethod(configuration.NormalizeOption);
                });
                WaitForGraphs();
                RunUI(() =>
                {
                    var areaReplicateGraph = FindAreaReplicateGraph();
                    Assert.IsNotNull(areaReplicateGraph);
                    VerifyAxisScale(areaReplicateGraph.GraphControl, "Replicate: " + configuration);
                    var areaPeptideGraph = FindAreaPeptideGraph();
                    Assert.IsNotNull(areaPeptideGraph);
                    VerifyAxisScale(areaPeptideGraph.GraphControl, "Peptide: " + configuration);
                });
            }
        }

        private IEnumerable<Configuration> EnumerateConfigurations(SrmDocument document)
        {
            foreach (var normalizeOption in NormalizeOption.AvailableNormalizeOptions(document))
            {
                foreach (bool logScale in new[] { true, false })
                {
                    foreach (var peptideGroupDocNode in document.MoleculeGroups)
                    {
                        foreach (var peptideDocNode in peptideGroupDocNode.Molecules)
                        {
                            yield return new Configuration(normalizeOption,
                                new IdentityPath(peptideGroupDocNode.PeptideGroup, peptideDocNode.Peptide), logScale);
                        }
                    }
                }
            }
        }

        private class Configuration
        {
            public Configuration(NormalizeOption normalizeOption, IdentityPath identityPath, bool logScale)
            {
                NormalizeOption = normalizeOption;
                IdentityPath = identityPath;
                LogScale = logScale;
            }

            public NormalizeOption NormalizeOption { get; }
            public IdentityPath IdentityPath { get; }
            public bool LogScale { get; }

            public override string ToString()
            {
                return string.Format("Normalization:{0} IdentityPath:{1} LogScale:{2}", 
                    NormalizeOption, IdentityPath, LogScale);
            }
        }

        private void VerifyAxisScale(ZedGraphControl graphControl, string message)
        {
            for (int iPane = 0; iPane < graphControl.MasterPane.PaneList.Count; iPane++)
            {
                var graphPane = graphControl.MasterPane.PaneList[iPane];
                VerifyAxisScale(graphPane, message + " Pane#:" + iPane);
            }
        }

        private void VerifyAxisScale(GraphPane graphPane, string message)
        {
            var minY = graphPane.YAxis.Scale.Min;
            var maxY = graphPane.YAxis.Scale.Max;

            foreach (var curve in graphPane.CurveList)
            {
                if (curve.IsY2Axis)
                {
                    continue;
                }

                var curveMessage = message + " Curve:" + curve.Label.Text;
                for (int iPoint = 0; iPoint < curve.NPts; iPoint++)
                {
                    var point = curve.Points[iPoint];
                    if (PointPairBase.IsValueInvalid(point.Y))
                    {
                        continue;
                    }
                    Assert.IsFalse(point.Y < minY, "{0} should not be less than {1} in point#{2} {3}", point.Y, minY, iPoint, curveMessage);
                    Assert.IsFalse(point.Y > maxY, "{0} should not be greater than {1} point#{2} {3}", point.Y, maxY, iPoint, curveMessage);
                }
            }
        }

        private GraphSummary FindAreaReplicateGraph()
        {
            return FormUtil.OpenForms.OfType<GraphSummary>().FirstOrDefault(graph =>
                graph.Type == GraphTypeSummary.replicate && graph.Controller is AreaGraphController);
        }

        private GraphSummary FindAreaPeptideGraph()
        {
            return FormUtil.OpenForms.OfType<GraphSummary>().FirstOrDefault(graph =>
                graph.Type == GraphTypeSummary.peptide && graph.Controller is AreaGraphController);
        }
    }
}
