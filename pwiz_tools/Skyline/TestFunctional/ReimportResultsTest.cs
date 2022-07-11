/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ReimportResultsTest : AbstractFunctionalTestEx
    {
        private const double deltaRetentionTime = 0.001;
        [TestMethod]
        public void TestReimportResults()
        {
            TestFilesZip = @"TestFunctional\ReimportResultsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ReimportResultsTest.sky")));
            Assert.AreEqual(1.0, SkylineWindow.Document.Settings.TransitionSettings.FullScan.ProductRes.Value);
            Assert.IsFalse(SkylineWindow.Document.Settings.TransitionSettings.Instrument.TriggeredAcquisition);

            // Import results the first time
            var importStartTime = DateTime.UtcNow;
            ImportResults(TestFilesDir.GetTestPath("sample.raw"));
            var importEndTime = DateTime.UtcNow;
            var importedTime = SkylineWindow.Document.MeasuredResults.Chromatograms.First().MSDataFileInfos.First().ImportTime;
            Assert.IsNotNull(importedTime);
            AssertTimesInOrder(importStartTime, importedTime.Value, importEndTime);
            var graphChromatogram = FindOpenForm<GraphChromatogram>();
            Tuple<double, double> peak1 = null, peak2 = null;
            const double expectedArea1 = 4120;
            const double expectedArea2 = 8969;
            TransitionChromInfo transitionChromInfo;
            // Choose custom peak boundaries on the first two transitions in the document
            RunUI(() =>
            {
                SkylineWindow.SetDisplayTypeChrom(DisplayTypeChrom.single);
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Transitions, 0);
                peak1 = ChangePeakBounds(graphChromatogram, .185, .3);
                transitionChromInfo = SkylineWindow.Document.MoleculeTransitions.First().Results[0].First();
                Assert.AreEqual(peak1.Item1, transitionChromInfo.StartRetentionTime, deltaRetentionTime);
                Assert.AreEqual(peak1.Item2, transitionChromInfo.EndRetentionTime, deltaRetentionTime);
                Assert.AreEqual(expectedArea1, transitionChromInfo.Area, 10);

                SkylineWindow.SelectedPath =
                    SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Transitions, 1);
                peak2 = ChangePeakBounds(graphChromatogram, .095, .155);
                transitionChromInfo = SkylineWindow.Document.MoleculeTransitions.Skip(1).First().Results[0].First();
                Assert.AreEqual(peak2.Item1, transitionChromInfo.StartRetentionTime, deltaRetentionTime);
                Assert.AreEqual(peak2.Item2, transitionChromInfo.EndRetentionTime, deltaRetentionTime);
                Assert.AreEqual(expectedArea2, transitionChromInfo.Area, 10);
            });

            // Change Product Mass Accuracy to 1000 and reimport the results
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.ProductRes = 1000;
                transitionSettingsUi.OkDialog();
            });
            var doc = SkylineWindow.Document;
            DateTime? reimportStartTime = null;
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.SelectedChromatograms = doc.MeasuredResults.Chromatograms.ToArray();
                dlg.ReimportResults();
                reimportStartTime = DateTime.UtcNow;
                dlg.OkDialog();
            });
            Assert.IsNotNull(reimportStartTime);
            WaitForDocumentChangeLoaded(doc);
            var reimportEndTime = DateTime.UtcNow;
            var reimportedTime = SkylineWindow.Document.MeasuredResults.Chromatograms.First().MSDataFileInfos.First()
                .ImportTime;
            Assert.IsNotNull(reimportedTime);
            AssertTimesInOrder(reimportStartTime.Value, reimportedTime.Value, reimportEndTime);

            // Make sure that the reimported results reflect the correct peak area
            transitionChromInfo = SkylineWindow.Document.MoleculeTransitions.First().Results[0].First();
            Assert.AreEqual(peak1.Item1, transitionChromInfo.StartRetentionTime, deltaRetentionTime);
            Assert.AreEqual(peak1.Item2, transitionChromInfo.EndRetentionTime, deltaRetentionTime);
            const double expectedReimportedArea2 = 4987;
            const double expectedReimportedBackground2 = 10154;

            transitionChromInfo = SkylineWindow.Document.MoleculeTransitions.Skip(1).First().Results[0].First();
            Assert.AreEqual(peak2.Item1, transitionChromInfo.StartRetentionTime, deltaRetentionTime);
            Assert.AreEqual(peak2.Item2, transitionChromInfo.EndRetentionTime, deltaRetentionTime);
            Assert.AreEqual(expectedReimportedArea2, transitionChromInfo.Area, 10);
            Assert.AreEqual(expectedReimportedBackground2, transitionChromInfo.BackgroundArea, 10);

            // Change "Triggered Acquisition" to true, and do a Rescore
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.TriggeredAcquisition = true;
                transitionSettingsUi.OkDialog();
            });

            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            doc = SkylineWindow.Document;
            DateTime? rescoreStartTime = null;
            RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg =>
            {
                rescoreStartTime = DateTime.UtcNow;
                dlg.Rescore(false);
            });
            Assert.IsNotNull(rescoreStartTime);
            WaitForDocumentChangeLoaded(doc);
            DateTime rescoreEndTime = DateTime.UtcNow;
            DateTime? rescoredTime = SkylineWindow.Document.MeasuredResults.Chromatograms.First().MSDataFileInfos.First()
                .ImportTime;
            Assert.IsNotNull(rescoredTime);
            AssertTimesInOrder(rescoreStartTime.Value, rescoredTime.Value, rescoreEndTime);

            // Make sure that the rescored peak areas are correct
            transitionChromInfo = SkylineWindow.Document.MoleculeTransitions.First().Results[0].First();
            Assert.AreEqual(peak1.Item1, transitionChromInfo.StartRetentionTime, deltaRetentionTime);
            Assert.AreEqual(peak1.Item2, transitionChromInfo.EndRetentionTime, deltaRetentionTime);
            transitionChromInfo = SkylineWindow.Document.MoleculeTransitions.Skip(1).First().Results[0].First();
            Assert.AreEqual(peak2.Item1, transitionChromInfo.StartRetentionTime, deltaRetentionTime);
            Assert.AreEqual(peak2.Item2, transitionChromInfo.EndRetentionTime, deltaRetentionTime);
            Assert.AreEqual(expectedReimportedArea2 + expectedReimportedBackground2, transitionChromInfo.Area, 50);
            Assert.AreEqual(0, transitionChromInfo.BackgroundArea);
        }

        private static Tuple<double, double> ChangePeakBounds(GraphChromatogram graphChromatogram, double startTime, double endTime)
        {
            var document = SkylineWindow.Document;
            var selectedPath = SkylineWindow.SelectedPath;
            var groupPath = selectedPath.GetPathTo((int) SrmDocument.Level.TransitionGroups);
            Transition transition = null;
            if (GraphChromatogram.IsSingleTransitionDisplay && selectedPath.Length > (int) SrmDocument.Level.TransitionGroups)
            {
                transition = (Transition) document.FindNode(selectedPath).Id;
            }

            var firstGraphItem = graphChromatogram.GraphItems.First();
            var actualStartTime = firstGraphItem.GetValidPeakBoundaryTime(startTime);
            var actualEndTime = firstGraphItem.GetValidPeakBoundaryTime(endTime);
            graphChromatogram.SimulateChangedPeakBounds(new List<ChangedPeakBoundsEventArgs>
            {
                new ChangedPeakBoundsEventArgs(groupPath, transition, graphChromatogram.NameSet, graphChromatogram.FilePath,
                    actualStartTime,
                    actualEndTime, 
                    PeakIdentification.FALSE,
                    PeakBoundsChangeType.both)
            });
            return Tuple.Create(actualStartTime.MeasuredTime, actualEndTime.MeasuredTime);
        }

        private static void AssertTimesInOrder(params DateTime[] times)
        {
            for (int i = 1; i < times.Length; i++)
            {
                AssertEx.IsLessThanOrEqual(times[i - 1], times[i]);
            }
        }
    }
}
