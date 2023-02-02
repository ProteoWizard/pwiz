/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify import of Agilent IMS data with some marginal values that don't roundtrip through their CCS/DT conversion.
    /// </summary>
    [TestClass]
    public class ImportAgilentIMSFringeValuesTest : AbstractFunctionalTest
    {
        static string DATA_FILE = "internal_05Aug_0CE[stitch]30CE.d";

        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)] // No parallel testing as Agilent reader locks the files it reads 
        public void AgilentIMSImportFringeValuesTest()
        {
            Log.AddMemoryAppender();

            TestFilesZip = GetPerfTestDataURL(@"PerfImportResultsAgilentIMSFringeValues.zip");
            TestFilesPersistent = new[] { DATA_FILE }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // turn on performance measurement

            RunFunctionalTest();
            
            var logs = Log.GetMemoryAppendedLogEvents();
            var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // show summary
            var log = new Log("Summary");
            if (TestFilesDirs != null)
                log.Info(stats.Replace(TestFilesDir.PersistentFilesDir, "")); // Remove tempfile info from log
        }

        protected override void DoTest()
        {
            string skyFile = TestFilesDir.GetTestPath("NIST_majorsensitivity_ForEval.sky");
            Program.ExtraRawFileSearchFolder = TestFilesDir.PersistentFilesDir; // So we don't have to reload the raw files, which have moved relative to skyd file 
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            const int waitTimeMillis = 10 * 60 * 1000;   // 10 minutes
            var doc0 = WaitForDocumentLoaded(waitTimeMillis);  // If it decides to remake chromatograms this can take awhile
            AssertEx.IsDocumentState(doc0, null, 8, 83, 492, 6412);
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(); // Avoid "document changed since last edit" message
                doc0 = SkylineWindow.DocumentUI;
            });

            var loadStopwatch = new Stopwatch();
            loadStopwatch.Start();

            ImportResultsFile(TestFilesDir.GetTestPath(DATA_FILE), 10 * 60);    // Allow 10 minutes for loading

            // If we get this far, the problem has been solved

            loadStopwatch.Stop();
            
            DebugLog.Info("load time = {1}", loadStopwatch.ElapsedMilliseconds);
        }

        private ChromatogramSet StripPathInfo(ChromatogramSet chromatogramSet)
        {
            return chromatogramSet.ChangeMSDataFileInfos(chromatogramSet.MSDataFileInfos
                .Select(StripFilePathInfo).ToArray());
        }

        private ChromFileInfo StripFilePathInfo(ChromFileInfo chromFileInfo)
        {
            // Remove the everything but the filename from the FilePath, and zero out the FileModifiedTime.
            var chromCachedFile = new ChromCachedFile(new MsDataFilePath(chromFileInfo.FilePath.GetFileName()), 0,
                new DateTime(0), chromFileInfo.RunStartTime, null, (float) chromFileInfo.MaxRetentionTime,
                (float) chromFileInfo.MaxIntensity, 0, 0, 0, chromFileInfo.IonMobilityUnits, null, 
                null, chromFileInfo.InstrumentInfoList);
            return chromFileInfo.ChangeInfo(chromCachedFile).ChangeImportTime(null);
        }
    }
}
