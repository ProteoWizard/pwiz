/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class NoQuantitativeTransitionsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNoQuantitativeTransitions()
        {
            TestFilesZip = @"TestFunctional\NoQuantitativeTransitionsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const double peakWidthZoomFactor = 2.0;
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("NoQuantitativeTransitionsTest.sky"));
                SkylineWindow.AutoZoomBestPeak();
            });
            WaitForDocumentLoaded();
            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, dlg =>
            {
                dlg.TimeRange = peakWidthZoomFactor;
                dlg.IsPeakWidthRelative = true;
                dlg.OkDialog();
            });

            // Do a rescore so that we can be sure that the way things are in the document and exactly
            // how the current version of Skyline calculates them
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg=>dlg.Rescore(false));
            WaitForDocumentLoaded();

            var document = SkylineWindow.Document;

            // Make sure that the TransitionGroupChromInfo's have their values set based on the quantitative peaks, unless there
            // are no quantitative peaks
            foreach (var transitionGroupDocNode in document.MoleculeTransitionGroups)
            {
                for (int iReplicate = 0; iReplicate < transitionGroupDocNode.Results.Count; iReplicate++)
                {
                    var transitionGroupChromInfo = transitionGroupDocNode.Results[iReplicate][0];
                    var quanChromInfos = new List<TransitionChromInfo>();
                    var nonQuanChromInfos = new List<TransitionChromInfo>();
                    foreach (var t in transitionGroupDocNode.Transitions)
                    {
                        var chromInfo = t.Results[iReplicate][0];
                        if (chromInfo.IsEmpty)
                        {
                            continue;
                        }

                        if (t.IsQuantitative(document.Settings))
                        {
                            quanChromInfos.Add(chromInfo);
                        }
                        else
                        {
                            nonQuanChromInfos.Add(chromInfo);
                        }
                    }

                    if (quanChromInfos.Any())
                    {
                        AssertEx.AreEqual(transitionGroupChromInfo.StartRetentionTime, quanChromInfos.Min(c=>c.StartRetentionTime));
                        AssertEx.AreEqual(transitionGroupChromInfo.EndRetentionTime, quanChromInfos.Max(c=>c.EndRetentionTime));
                        AssertEx.AreEqual(transitionGroupChromInfo.Height, quanChromInfos.Max(c=>c.Height));
                    }
                    else
                    {
                        AssertEx.AreEqual(transitionGroupChromInfo.StartRetentionTime, nonQuanChromInfos.Min(c=>c.StartRetentionTime));
                        AssertEx.AreEqual(transitionGroupChromInfo.EndRetentionTime, nonQuanChromInfos.Max(c=>c.EndRetentionTime));
                        AssertEx.IsNull(transitionGroupChromInfo.Height);
                    }
                }
            }

            // Verify that the chromatogram graph correctly zooms to the best quantitative peak, or,
            // if there are no quantitative peaks, falls back to the best non-quantitative peak,
            // or is zoomed out entirely
            foreach (var displayTypeChrom in new[]
                {DisplayTypeChrom.all, DisplayTypeChrom.single, DisplayTypeChrom.total})
            {
                RunUI(()=>SkylineWindow.SetDisplayTypeChrom(displayTypeChrom));
                for (int iReplicate = 0; iReplicate < document.Settings.MeasuredResults.Chromatograms.Count; iReplicate++)
                {
                    var chromatogramSet = document.Settings.MeasuredResults.Chromatograms[iReplicate];
                    var graphChrom = SkylineWindow.GetGraphChrom(chromatogramSet.Name);
                    RunUI(()=>
                    {
                        graphChrom.Activate();
                        graphChrom.Focus();
                    });
                    foreach (var protein in SkylineWindow.Document.MoleculeGroups)
                    {
                        foreach (var peptide in protein.Molecules)
                        {
                            foreach (var precursor in peptide.TransitionGroups)
                            {
                                foreach (var transition in precursor.Transitions)
                                {
                                    RunUI(() =>
                                    {
                                        SkylineWindow.SelectedPath = new IdentityPath(protein.Id, peptide.Id,
                                            precursor.Id, transition.Id);
                                        SkylineWindow.AutoZoomNone();
                                        SkylineWindow.AutoZoomBestPeak();
                                    });
                                    var transitionChromInfos = new List<TransitionChromInfo>();
                                    if (displayTypeChrom == DisplayTypeChrom.single)
                                    {
                                        var transitionChromInfo = transition.Results[iReplicate][0];
                                        if (!transitionChromInfo.IsEmpty)
                                        {
                                            transitionChromInfos.Add(transitionChromInfo);
                                        }
                                    }
                                    else
                                    {
                                        transitionChromInfos.AddRange(precursor.Transitions.Where(t => t.ExplicitQuantitative)
                                            .Select(t => t.Results[iReplicate][0]).Where(t=>!t.IsEmpty));
                                        if (!transitionChromInfos.Any())
                                        {
                                            if (displayTypeChrom != DisplayTypeChrom.total ||
                                                precursor.Transitions.All(t => !t.ExplicitQuantitative))
                                            {
                                                transitionChromInfos.AddRange(precursor.Transitions
                                                    .Select(t => t.Results[iReplicate][0]).Where(t => !t.IsEmpty));
                                            }
                                        }
                                    }

                                    var zoomState = graphChrom.ZoomState;
                                    if (transitionChromInfos.Any())
                                    {
                                        var peakStartTime = transitionChromInfos.Min(t => t.StartRetentionTime);
                                        var peakEndTime = transitionChromInfos.Max(t => t.EndRetentionTime);
                                        var midPeak = (peakStartTime + peakEndTime) / 2;
                                        var halfPeakWidth = (peakEndTime - peakStartTime) / 2;
                                        double expectedZoomStart = midPeak - halfPeakWidth * peakWidthZoomFactor;
                                        double expectedZoomEnd = midPeak + halfPeakWidth * peakWidthZoomFactor;
                                        const double tolerance = .001;
                                        AssertEx.AreEqual(expectedZoomStart, zoomState.XAxis.Min, tolerance);
                                        AssertEx.AreEqual(expectedZoomEnd, zoomState.XAxis.Max, tolerance);
                                    }
                                    else
                                    {
                                        var expectedStart = graphChrom.ChromGroupInfos[0].TimeIntensitiesGroup.MinTime;
                                        var expectedEnd = graphChrom.ChromGroupInfos[0].TimeIntensitiesGroup.MaxTime;
                                        AssertEx.IsTrue(zoomState.XAxis.Min <= expectedStart);
                                        AssertEx.IsTrue(zoomState.XAxis.Max >= expectedEnd);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
