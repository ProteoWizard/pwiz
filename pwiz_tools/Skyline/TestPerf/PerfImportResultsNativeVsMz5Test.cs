/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using log4net;
using log4net.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Compare performance of vendor readers vs mz5 for results import.
    /// </summary>
    [TestClass]
    public class PerformanceVsMz5Test : AbstractFunctionalTest
    {

        // Note to developers considering this as a template for new tests:
        // The use of a private TestFilesDir instead of the one provided by
        // AbstractFunctionalTest is useful for the purposes of this test,
        // which is trying to string together a bunch of results into a CSV
        // compatible output, but doesn't really follow the AbstractFunctionalTest
        // model and thus probably isn't what you really want.  
        // Something like ImportAgilentIMSTest probably makes for a better starting point.

        const int LOOPS_AVG = 0;  // Increase this to 3 for proper performance comparisons, but 0 is good for nightly (skips warmup, just runs once)

        private string _baseSkyFile;
        private string _skyFile;
        private string _dataFile;
        private bool _centroided;
        private LockMassParameters _lockMassParameters;
        private TestFilesDir _testFilesDir;
        private int _loopcount;

        /// <summary>
        /// compare various raw files and mz5 equivalents, 
        /// most with and without raw data centroiding with vendor algorithms.
        /// </summary>
        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_WatersChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsWatersVsMz5.zip",
                "Waters\\Hoofnagle_MSe_targeted.sky",
                "Waters\\DATA\\2013_03_13_UWash_S1_MSE_Adj_001.raw",
                false, LockMassParameters.EMPTY);
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_AbIdaChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsAbIdaVsMz5.zip",
                "AB\\Hoofnagle_5600_IDA_targeted.sky",
                "AB\\5600_DDA\\Hoofnagle_01_IDA.wiff",
                true); // Also run the raw data as centroided data
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_AbDiaChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsAbDiaVsMz5.zip",
                "AB\\Hoofnagle_5600_DIA_targeted.sky",
                "AB\\5600_DIA\\Hoofnagle_10xDil_SWATH_01.wiff",
                true); // Also run the raw data as centroided data
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_ThermoDdaVChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsThermoDDAVsMz5.zip",
                "Thermo\\Hoofnagle_QE_DDA_targeted.sky",
                "Thermo\\QE_DDA\\20130311_DDA_Pit01.raw",
                true); // Also run the raw data as centroided data
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_ThermoDiaChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsThermoDiaVsMz5.zip",
                "Thermo\\Hoofnagle_QE_DIA_targeted.sky",
                "Thermo\\QE_DIA\\20130311_DIA_Pit01.raw",
                true); // Also run the raw data as centroided data
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_AgilentDiaChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsAgilentDiaVsMz5.zip",
                "BSA_Agilent_DIA_profile.sky",
                "BSA_100fmol_SWATH.d",
                true); // Also run the raw data as centroided data
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_AgilentDdaChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsAgilentDDaVsMz5.zip",
                "fullscan_data\\BSA_Agilent_MS1.sky",
                "fullscan_data\\1-10amol-BSA-r001.d",
                true); // Also run the raw data as centroided data
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_BrukerFullScanMS1filteringPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsBrukerFullScanMS1filtering.zip",
                "MS1 filtering\\MS1 filtering for ID_BSA_minimized.sky",
                "MS1 filtering\\BSA_50fmol_30min_5sWOthr2000_BA8_01_2360.d",
                true); // Also run the raw data as centroided data
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_BrukerFullScanMSeDataPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsBrukerFullScanMSeData.zip",
                "MSe data\\MSe_BSA_Skyline 13-test-bsa.sky",
                "MSe data\\BSA_50fmol_30min_15-30V_1Hz_GA3_01_1097.d",
                true); // Also run the raw data as centroided data
        }

        [TestMethod]
        [Timeout(6000000)]  // These can take a long time
        public void zzzNativeVsMz5_BrukerFullScanSWATHDataPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsBrukerFullScanSWATHData.zip",
                "SWATH data\\Bruker SWATH Ecoli.sky",
                "SWATH data\\SWATH_Ecoli+UPS0.5pmol_120min_BC2_01_1919.d",
                true); // Also run the raw data as centroided data
        }

        private class LogInfoLevel : IDisposable
        {
            private readonly Level _saveLevel;

            public LogInfoLevel()
            {
                _saveLevel = LogManager.GetRepository().Threshold;
                LogManager.GetRepository().Threshold = LogManager.GetRepository().LevelMap["Info"];
            }

            public void Dispose()
            {
                // Restore logging level
                LogManager.GetRepository().Threshold = _saveLevel;
            }
        }

        public void NativeVsMz5ChromatogramPerformanceTest(string zipFile, string skyFile, string rawFile, 
            bool centroided=false,
            LockMassParameters lockMassParameters = null)
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global RunPerfTests flag is set
            _loopcount = LOOPS_AVG;

            // Caller may have disabled logging, but we depend on it for data gathering.
            using (new LogInfoLevel())
            {
                Log.AddMemoryAppender();
                for (var loop = 0; loop < _loopcount + 1; loop++) // one extra initial loop for warmup
                {
                    // compare mz5 and raw import times
                    TestFilesZip = "https://skyline.gs.washington.edu/perftests/" + zipFile;
                    var mz5File = Path.ChangeExtension(rawFile, "mz5");
                    TestFilesPersistent = new[] { rawFile, mz5File }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
                    _testFilesDir = new TestFilesDir(TestContext, TestFilesZip, null, TestFilesPersistent);
                    _baseSkyFile = _testFilesDir.GetTestPath(skyFile);
                    string nativeResults = _testFilesDir.GetTestPath(rawFile);
                    var rawfiles = new List<string>();
                    var mz5Results = Path.ChangeExtension(nativeResults, "mz5");
                    if (centroided)
                        rawfiles.Add(nativeResults); // First time through is centroided
                    rawfiles.Add(mz5Results);  // Then mz5
                    rawfiles.Add(nativeResults); // Then normal
                    MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = (loop == 0) && (_loopcount > 0); // turn on performance measurement after warmup loop
                    var centroidedThisPass = centroided;
                    var type = 0;
                    foreach (var resultspath in rawfiles)
                    {
                        _skyFile = _baseSkyFile.Replace(".sky", "_" + loop + "_" + type++ + ".sky");
                        _dataFile = resultspath;
                        _centroided = centroidedThisPass;
                        RunFunctionalTest();
                        centroidedThisPass = false;
                        _lockMassParameters = lockMassParameters;
                    }

                }
                DebugLog.Info("Done.");
                var logs = Log.GetMemoryAppendedLogEvents();
                var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // show summary, combining native per test and mz5 per test
                var report = stats.Replace(_testFilesDir.PersistentFilesDir.Replace(':', '_'), "");
                Console.Write(report); // Want this to appear in nightly log

                var log = new Log("Summary");
                log.Info(report);
            }
        }

        protected override void DoTest()
        {
            File.Copy(_baseSkyFile, _skyFile, true);
            WaitForCondition(() => File.Exists(_skyFile)); // Wait for copy and rename
            RunUI(() => SkylineWindow.OpenFile(_skyFile));
            string expectedErrorMessage = null;
            if (_centroided)
            {
                WaitForDocumentLoaded();
                RunUI(() => SkylineWindow.ModifyDocument("Use vendor centroiding", doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs =>
                fs.ChangePrecursorResolution(FullScanMassAnalyzerType.centroided, TransitionFullScan.DEFAULT_CENTROIDED_PPM, null).
                   ChangeProductResolution(FullScanMassAnalyzerType.centroided, TransitionFullScan.DEFAULT_CENTROIDED_PPM, null)))));
                if (_dataFile.EndsWith("mz5"))
                    expectedErrorMessage =
                        string.Format(
                            Resources.NoCentroidedDataException_NoCentroidedDataException_No_centroided_data_available_for_file___0_____Adjust_your_Full_Scan_settings_,_dataFile.Split('\\').Last());
            }
            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();
            ImportResultsFile(_dataFile, 60 * 60, expectedErrorMessage, _lockMassParameters);    // Allow 60 minutes for loading.
            loadStopwatch.Stop();

            DebugLog.Info("{0} load time = {1}", _dataFile, loadStopwatch.ElapsedMilliseconds);
        }
    }
}