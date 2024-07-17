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
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AutoZoomTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAutoZoom()
        {
            TestFilesZip = @"TestFunctional\AutoZoomTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => { SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AutoZoomTest.sky")); });
            WaitForDocumentLoaded();
            RunUI(() => { SkylineWindow.ArrangeGraphsTabbed(); });
            var document = SkylineWindow.Document;
            var protein = document.MoleculeGroups.First();
            // Iterate over all of the peptides in the first protein in the document
            // and make sure that the chromatogram graphs are zoomed correctly.
            // Some of these peptides have no chosen peak, and some of these peptides
            // have no predicted retention time
            foreach (var peptideDocNode in protein.Molecules)
            {
                RunUI(() => SkylineWindow.SelectedPath =
                    new IdentityPath(protein.PeptideGroup, peptideDocNode.Peptide));
                WaitForGraphs();
                for (int replicateIndex = 0;
                     replicateIndex < document.MeasuredResults.Chromatograms.Count;
                     replicateIndex++)
                {
                    RunUI(() => SkylineWindow.SelectedResultsIndex = replicateIndex);
                    foreach (var autoZoom in new[]
                                 { AutoZoomChrom.none, AutoZoomChrom.peak, AutoZoomChrom.window, AutoZoomChrom.both })
                    {
                        RunUI(() => SkylineWindow.SetAutoZoomChrom(autoZoom));
                        var graphChromatogram =
                            SkylineWindow.GetGraphChrom(document.MeasuredResults.Chromatograms[replicateIndex]
                                .Name);
                        var expectedZoom = GetExpectedZoom(document, peptideDocNode, replicateIndex);
                        var xAxis = graphChromatogram.GraphPane.XAxis;
                        string message = string.Format("AutoZoom: {0} Peptide: {1} Replicate: {2}", autoZoom,
                            peptideDocNode.ModifiedTarget, replicateIndex);
                        if (expectedZoom != null)
                        {
                            AssertEx.AreEqual(expectedZoom.StartTime, xAxis.Scale.Min, .1, message);
                            AssertEx.AreEqual(expectedZoom.EndTime, xAxis.Scale.Max, .1, message);
                        }
                        // "MinAuto" and "MaxAuto" should be true if and only if expectedZoom is null
                        Assert.AreEqual(expectedZoom == null, xAxis.Scale.MinAuto, message);
                        Assert.AreEqual(expectedZoom == null, xAxis.Scale.MaxAuto, message);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the expected range that the chromatogram graph x-axis should be zoomed to,
        /// or null if it should be "auto".
        /// This method takes into account the current value of "GraphChromatogram.AutoZoom"
        /// as well as whether the peptide has a chosen peak and/or a predicted retention time.
        /// </summary>
        private PeakBounds GetExpectedZoom(SrmDocument document, PeptideDocNode peptideDocNode,
            int replicateIndex)
        {
            var peakBounds = GetPeakBounds(peptideDocNode, replicateIndex);
            var prediction = document.Settings.PeptideSettings.Prediction.RetentionTime;
            double? predictedRetentionTime = prediction?.GetRetentionTime(
                peptideDocNode.SourceModifiedTarget,
                document.Settings.MeasuredResults.Chromatograms[replicateIndex].MSDataFileInfos.Single().FileId);
            var windowHalf = prediction?.TimeWindow * 2 / 3;
            var autoZoom = GraphChromatogram.AutoZoom;
            if (autoZoom == AutoZoomChrom.both)
            {
                if (peakBounds != null)
                {
                    if (predictedRetentionTime != null)
                    {
                        return new PeakBounds(Math.Min((predictedRetentionTime - windowHalf).Value,
                            peakBounds.StartTime - windowHalf.Value / 8), Math.Max(
                            (predictedRetentionTime + windowHalf).Value,
                            peakBounds.EndTime + windowHalf.Value / 8));
                    }

                    autoZoom = AutoZoomChrom.peak;
                }
                else
                {
                    autoZoom = AutoZoomChrom.window;
                }
            }

            if (autoZoom == AutoZoomChrom.window)
            {
                if (predictedRetentionTime != null)
                {
                    return new PeakBounds((predictedRetentionTime - windowHalf).Value,
                        (predictedRetentionTime + windowHalf).Value);
                }

                autoZoom = AutoZoomChrom.none;
            }

            if (autoZoom == AutoZoomChrom.peak)
            {
                if (peakBounds != null)
                {
                    var fwb = (peakBounds.EndTime - peakBounds.StartTime) / 2;
                    double multiplier = GraphChromatogram.DEFAULT_PEAK_RELATIVE_WINDOW;

                    return new PeakBounds(peakBounds.StartTime - fwb * (multiplier - 1),
                        peakBounds.EndTime + fwb * (multiplier - 1));
                }
            }

            return null;
        }

        private PeakBounds GetPeakBounds(PeptideDocNode peptideDocNode, int replicateIndex)
        {
            foreach (var transitionGroupDocNode in peptideDocNode.TransitionGroups)
            {
                var transitionGroupChromInfo = transitionGroupDocNode.GetSafeChromInfo(replicateIndex).FirstOrDefault();
                if (transitionGroupChromInfo?.StartRetentionTime != null)
                {
                    return new PeakBounds(transitionGroupChromInfo.StartRetentionTime.Value,
                        transitionGroupChromInfo.EndRetentionTime.Value);
                }
            }

            return null;
        }
    }
}
