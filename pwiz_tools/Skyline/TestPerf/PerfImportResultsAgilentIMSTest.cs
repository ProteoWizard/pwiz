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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
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

            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfImportResultsAgilentIMS.zip";
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

            // Fix up paths in local copy of skyfile to use the persistent files
            string text = File.ReadAllText(skyFile);
            text = text.Replace(@"M:\brendanx\data\IonMobility\Agilent\FromPNNL", TestFilesDir.PersistentFilesDir);
            File.WriteAllText(skyFile, text);

            RunUI(() => SkylineWindow.OpenFile(skyFile));

            const int chromIndex = 1;
            var doc0 = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(doc0, null, 19, 19, 28, 607);
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
            WaitForCondition(10 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 10 minutes
            loadStopwatch.Stop();

            var doc1 = WaitForDocumentLoaded(400000);
            AssertEx.IsDocumentState(doc1, null, 19, 19, 28, 607);
             
            var chroms0 = doc0.Settings.MeasuredResults.Chromatograms[chromIndex];
            var chroms1 = doc1.Settings.MeasuredResults.Chromatograms[chromIndex];
            Assert.AreEqual(chroms0, chroms1);

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
    }
}
