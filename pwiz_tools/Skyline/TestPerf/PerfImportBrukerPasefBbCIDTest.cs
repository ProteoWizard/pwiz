/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Bruker bbCID data.
    /// </summary>
    [TestClass]
    public class PerfImportBrukerPasefBBCIDTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void BrukerAllIonsbbCIDImportTest()
        { 
            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();
            TestFilesZip = GetPerfTestDataURL(@"PerfImportBrukerAllIonsbbCID.zip");
            TestFilesPersistent = new[] { ".d" }; // List of file basenames that we'd like to unzip alongside parent zipFile, and (re)use in place

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // Turn on performance measurement

            RunFunctionalTest();

            var logs = Log.GetMemoryAppendedLogEvents();
            var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // Show summary
            var log = new Log("Summary");
            if (TestFilesDirs != null)
                log.Info(stats.Replace(TestFilesDir.PersistentFilesDir, "")); // Remove tempfile info from log
        }

        protected override void DoTest()
        {
            
            // Load a .sky with mising .skyd, forcing re-import with existing parameters
            // This simplifies the test code since we have six different PASEF modes to deal with here
            string skyfile = TestFilesDir.GetTestPath("timsTOF_Lipidomics_test.sky");
            RunUI(() => SkylineWindow.OpenFile(skyfile));
            ImportResults(TestFilesDir.GetTestPath("HESI_Celegans_MTBE-2_150ul_bbCID_4ul_neg_19_1_3206.d"));
            var doc1 = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(doc1, null, 1, 1, 1, 10);
            float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = doc1.Settings.MeasuredResults;
            foreach (var pair in doc1.MoleculePrecursorPairs)
            {
                if (results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, out var chromGroupInfo))
                {
                    foreach (var chromGroup in chromGroupInfo)
                    {
                        foreach (var tranInfo in chromGroup.TransitionPointSets)
                        {
                            maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                        }
                    }
                }
            }
            AssertEx.AreEqual(2934925.25, maxHeight, 1);
        }
    }
}
