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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests converting from and to older versions of skyd file before
    /// <see cref="ChromTransition.OptimizationStep"/> existed.
    /// </summary>
    [TestClass]
    public class LegacyOptimizationStepTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLegacyOptimizationStep()
        {
            TestFilesZip = @"TestFunctional\LegacyOptimizationStepTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini.sky")));
            WaitForDocumentLoaded();
            var originalDocument = SkylineWindow.Document;
            var originalCache = LoadChromatogramRawData(originalDocument);

            Assert.AreEqual(CacheFormatVersion.Fifteen, originalDocument.MeasuredResults.CacheVersion);
            RunLongDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                RunDlg<MinimizeResultsDlg>(manageResultsDlg.MinimizeResults, minimizeResultsDlg =>
                {
                    minimizeResultsDlg.Settings = minimizeResultsDlg.Settings.ChangeNoiseTimeRange(null)
                        .ChangeDiscardUnmatchedChromatograms(false);
                    minimizeResultsDlg.MinimizeToFile(TestFilesDir.GetTestPath("CurrentVersion.sky"));
                });
            }, dlg => { });
            WaitForDocumentLoaded();
            Assert.AreEqual(CacheFormatVersion.CURRENT, SkylineWindow.Document.MeasuredResults.CacheVersion);
            string v22_2SharedFile = TestFilesDir.GetTestPath("Version22_2.sky.zip");
            RunUI(() =>
            {
                SkylineWindow.ShareDocument(v22_2SharedFile,
                    ShareType.COMPLETE.ChangeSkylineVersion(SkylineVersion.V22_2));
                SkylineWindow.OpenSharedFile(v22_2SharedFile);
            });
            WaitForDocumentLoaded();
            Assert.AreEqual(CacheFormatVersion.Fifteen, SkylineWindow.Document.MeasuredResults.CacheVersion);
            var roundTrippedDocument = SkylineWindow.Document;
            var roundTrippedCache = LoadChromatogramRawData(roundTrippedDocument);
            Assert.AreEqual(originalCache.ChromatogramEntries.Count, roundTrippedCache.ChromatogramEntries.Count);
            for (int iChromGroup = 0; iChromGroup < originalCache.ChromatogramEntries.Count; iChromGroup++)
            {
                var originalChromGroupHeaderInfo = originalCache.ChromatogramEntries[iChromGroup];
                var roundTripChromGroupHeaderInfo = roundTrippedCache.ChromatogramEntries[iChromGroup];
                Assert.AreEqual(originalChromGroupHeaderInfo.Precursor, roundTripChromGroupHeaderInfo.Precursor);
                Assert.AreEqual(originalChromGroupHeaderInfo.NumTransitions, roundTripChromGroupHeaderInfo.NumTransitions);
                for (int iChromTransition = 0;
                     iChromTransition < originalChromGroupHeaderInfo.NumTransitions;
                     iChromTransition++)
                {
                    var originalChromTransition =
                        originalCache.ChromTransitions[
                            originalChromGroupHeaderInfo.StartTransitionIndex + iChromTransition];
                    var roundTripChromTransition =
                        roundTrippedCache.ChromTransitions[roundTripChromGroupHeaderInfo.StartTransitionIndex +
                                                           iChromTransition];
                    Assert.AreEqual(originalChromTransition.Product, roundTripChromTransition.Product, .00001);
                    Assert.AreEqual(originalChromTransition.OptimizationStep, roundTripChromTransition.OptimizationStep);
                }
            }
        }

        private ChromatogramCache.RawData LoadChromatogramRawData(SrmDocument document)
        {
            using (var stream = File.OpenRead(document.Settings.MeasuredResults.CachePaths.Single()))
            {
                ChromatogramCache.LoadStructs(stream, new ProgressStatus(), new SilentProgressMonitor(),
                    out var rawData);
                return rawData;
            }
        }
    }
}
