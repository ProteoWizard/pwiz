/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Agilent IMS data as we work on various code optimizations.
    /// </summary>
    [TestClass]
    public class ImportAgilentIMSTest : AbstractFunctionalTest
    {

        [TestMethod] 
        public void AgilentIMSImportTest()
        {
            Log.AddMemoryAppender();

            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfImportResultsAgilentIMSv3.zip";
            TestFilesPersistent = new[] { "19pep_1700V_pos_3May14_Legolas.d", "19pep_1700V_CE22_pos_5May14_Legolas.d" }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place

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
            string skyFile = TestFilesDir.GetTestPath("Erin 19pep test subset.sky");
            Program.ExtraRawFileSearchFolder = TestFilesDir.PersistentFilesDir; // So we don't have to reload the raw files, which have moved relative to skyd file 
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            const int chromIndex = 1;
            const int waitTimeMillis = 10 * 60 * 1000;   // 10 minutes
            var doc0 = WaitForDocumentLoaded(waitTimeMillis);  // If it decides to remake chromatograms this can take awhile
            AssertEx.IsDocumentState(doc0, null, 19, 19, 28, 607);
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(); // Avoid "document changed since last edit" message
                doc0 = SkylineWindow.DocumentUI;
            }); 
            float tolerance = (float)doc0.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight0 = 0;
            var results0 = doc0.Settings.MeasuredResults;
            var intensities = new List<List<float>>();
            foreach (var pair in doc0.PeptidePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(results0.TryLoadChromatogram(chromIndex, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out chromGroupInfo));
                foreach (var chromGroup in chromGroupInfo)
                {
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        maxHeight0 = Math.Max(maxHeight0, tranInfo.MaxIntensity);
                        intensities.Add(new List<float>(tranInfo.Intensities));
                    }
                }
            }

            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();
            var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(() =>
            {
                // Just reload the CE22 stuff, using the RT info from the MS1 scan
                manageResults.SelectedChromatograms = new[] { SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[chromIndex] };
                manageResults.ReimportResults();
                manageResults.OkDialog();
            });
            WaitForCondition(waitTimeMillis, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            loadStopwatch.Stop();

            var doc1 = WaitForDocumentLoaded(400000);
            AssertEx.IsDocumentState(doc1, null, 19, 19, 28, 607);
             
            var chroms0 = doc0.Settings.MeasuredResults.Chromatograms[chromIndex];
            var chroms1 = doc1.Settings.MeasuredResults.Chromatograms[chromIndex];
            Assert.AreEqual(StripPathInfo(chroms0), StripPathInfo(chroms1));

            int intensityIndex = 0;
            var results1 = doc1.Settings.MeasuredResults;
            foreach (var pair in doc1.PeptidePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(results1.TryLoadChromatogram(chromIndex, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out chromGroupInfo));
                foreach (var chromGroup in chromGroupInfo)
                {
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        AssertEx.AreEqualDeep(intensities[intensityIndex++],new List<float>(tranInfo.Intensities));
                    }
                }
            }
            
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
                new DateTime(0), chromFileInfo.RunStartTime, (float) chromFileInfo.MaxRetentionTime,
                (float) chromFileInfo.MaxIntensity, chromFileInfo.InstrumentInfoList);
            return chromFileInfo.ChangeInfo(chromCachedFile);
        }
    }
}
