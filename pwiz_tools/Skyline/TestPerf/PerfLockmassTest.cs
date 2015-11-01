/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify operation of Waters lockmass correction.
    /// </summary>
    [TestClass]
    public class LockmassTest : AbstractFunctionalTestEx
    {

        [TestMethod] 
        public void WatersLockmassPerfTest()
        {
            Log.AddMemoryAppender();
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfTestLockmass.zip";
            TestFilesPersistent = new[] { "ID19638_01_UCA195_2533_082715.raw" }; // List of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // Turn on performance measurement

            RunFunctionalTest();
            
            var logs = Log.GetMemoryAppendedLogEvents();
            var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // Show summary
            var log = new Log("Summary");
            if (TestFilesDirs != null)
                log.Info(stats.Replace(TestFilesDir.PersistentFilesDir, "")); // Remove tempfile info from log
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(relativePath);
        }


        protected override void DoTest()
        {
            var skyfile = TestFilesDir.GetTestPath("2533_FattyAcids.sky");
            const double lockmassNegative = 554.2615;

            SrmDocument corrected = null, uncorrected2 = null, uncorrected1 = null;

            for (var testloop = 2; testloop >= 0; testloop--)
            {
                RunUI(() => SkylineWindow.OpenFile(skyfile));

                var doc0 = WaitForDocumentLoaded();
                AssertEx.IsDocumentState(doc0, null, 1, 19, 19, 38);

                Stopwatch loadStopwatch = new Stopwatch();
                loadStopwatch.Start();
                ImportResults(GetTestPath(TestFilesPersistent[0]),
                    (testloop == 0)
                        ? new LockMassParameters(0, lockmassNegative, LockMassParameters.LOCKMASS_TOLERANCE_DEFAULT) // ESI-, per Will T
                        : LockMassParameters.EMPTY);

                var document = WaitForDocumentLoaded(400000);

                loadStopwatch.Stop();

                if (testloop < 2)
                    DebugLog.Info("lockmass {0} load time = {1}", (testloop == 0) ? "corrected" : "uncorrected", loadStopwatch.ElapsedMilliseconds);

                if (testloop == 0)
                {
                    corrected = document;
                }
                else if (testloop == 1)
                {
                    uncorrected1 = document;
                }
                else
                {
                    uncorrected2 = document;
                }
                if (testloop > 0)
                    RunUI(() => SkylineWindow.NewDocument(true));
            }
            Assert.AreNotEqual(corrected, uncorrected2);  // Corrected pass should differ
            Assert.AreEqual(uncorrected2, uncorrected1);  // Both uncorrected passes should agree
            Assert.IsNotNull(corrected);
            Assert.IsNotNull(uncorrected2);
            ComparePeaks(corrected, uncorrected2);
            var correctedPeaks = Peaks(corrected);
            var uncorrectedPeaks = Peaks(uncorrected1);

            // Verify roundtrip with and without .skyd
            for (var loop = 0; loop < 2; loop++) {
                var outfile = TestFilesDirs[0].GetTestPath("test"+loop+".sky");
                var withoutCache = (loop==0);
                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(outfile, !withoutCache);
                    SkylineWindow.NewDocument(true);
                    if (withoutCache)
                        FileEx.SafeDelete(Path.ChangeExtension(outfile, ChromatogramCache.EXT)); // kill the .skyd file
                    SkylineWindow.OpenFile(outfile);
                });
                var reopened = WaitForDocumentLoaded();
                var reopenedPeaks = Peaks(reopened);
                Assert.AreEqual(correctedPeaks.Count, reopenedPeaks.Count);
                for (var i = 0; i < correctedPeaks.Count; i++)
                {
                    var correctedPeak = correctedPeaks[i];
                    var uncorrectedPeak = uncorrectedPeaks[i];
                    var reopenedPeak = reopenedPeaks[i];
                    Assert.AreNotEqual(uncorrectedPeak, reopenedPeak, "reopened peaks should have lockmass correction");
                    Assert.AreEqual(correctedPeak, reopenedPeak, "reopened peaks should agree");
                }
            }

            // And finally, verify that reimport successfully uses the lockmass values cached in the chromatograms
            Settings.Default.LockmassParameters = LockMassParameters.EMPTY; // Make sure we aren't pulling from settngs
            var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(() =>
            {
                manageResults.SelectedChromatograms = new[] { SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[0] };
                manageResults.ReimportResults();
                manageResults.OkDialog();
            });
            WaitForDocumentChange(corrected);
            WaitForCondition(10 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 10 minutes
            var reimportedPeaks = Peaks(SkylineWindow.Document);
            Assert.AreEqual(correctedPeaks.Count, reimportedPeaks.Count);
            for (var i = 0; i < correctedPeaks.Count; i++)
            {
                var correctedPeak = correctedPeaks[i];
                var uncorrectedPeak = uncorrectedPeaks[i];
                var reimportedPeak = reimportedPeaks[i];
                Assert.AreNotEqual(uncorrectedPeak, reimportedPeak, "reimported peaks should have lockmass correction"); 
                Assert.AreEqual(correctedPeak, reimportedPeak, "reimported peaks should agree");
            }

        }

        private static List<TransitionGroupChromInfo> Peaks(SrmDocument doc)
        {
            return (from tg in doc.MoleculeTransitionGroups
                       from r in tg.Results
                       from p in r
                       select p).ToList();
        }

        private static void ComparePeaks(SrmDocument corrected, SrmDocument uncorrected)
        {
            var correctedPeaks = Peaks(corrected);
            var uncorrectedPeaks = Peaks(uncorrected);
            var nWorse = 0;
            Assert.AreEqual(uncorrectedPeaks.Count, correctedPeaks.Count);
            for (var i = 0; i < correctedPeaks.Count; i++)
            {
                var correctedPeak = correctedPeaks[i];
                var uncorrectedPeak = uncorrectedPeaks[i];
                Assert.AreEqual(uncorrectedPeak.RetentionTime ?? -1, correctedPeak.RetentionTime ?? -1, 0.01,
                    "peak retention times should be similar"); // Expect similar RT
                if (Math.Abs(uncorrectedPeak.MassError ?? 0) < Math.Abs(correctedPeak.MassError ?? 0))
                    nWorse++;
            }
            Assert.IsTrue(nWorse < (3 * correctedPeaks.Count)/10, "mass error should be reduced"); // Expect overall lower mass error
        }
    }
}
