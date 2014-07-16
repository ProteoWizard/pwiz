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


using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Compare performance of vendor readers vs mz5 for results import.
    /// </summary>
    [TestClass]
    public class PerformanceOptimizeCeImportVsMz5Test : AbstractFunctionalTest
    {

        // Note to developers considering this as a template for new tests:
        // The use of a private TestFilesDir instead of the one provided by
        // AbstractFunctionalTest is useful for the purposes of this test,
        // which is trying to string together a bunch of results into a CSV
        // compatible output, but doesn't really follow the AbstractFunctionalTest
        // model and thus probably isn't what you really want.  
        // Something like ImportAgilentIMSTest probably makes for a better starting point.

        private string _skyFile;
        private string _replicatesDirName;
        private int _loopCount;
        private TestFilesDir _testFilesDir;

        /// <summary>
        /// compare various raw files and mz5 equivalents.
        /// </summary>
        public void AbVsMz5OptimzeCeImportPerformanceTest()
        {
            NativeVsMz5OptimzeCeImportPerformanceTest(
                "AbVsMz5OptimizeCeImportPerfTest",
                "ABI_scheduledCEopt_110809_v2.sky");
        }

        public void AgilentVsMz5OptimzeCeImportPerformanceTest()
        {
            NativeVsMz5OptimzeCeImportPerformanceTest(
                "AgilentVsMz5OptimizeCeImportPerfTest",
                "072210_Agilent_perf.sky");
        }

        public void ThermoVsMz5OptimzeCeImportPerformanceTest()
        {
            NativeVsMz5OptimzeCeImportPerformanceTest(
                "ThermoVsMz5OptimizeCeImportPerfTest",
                "CE_Vantage_15mTorr_scheduled_Good_v2.sky");
        }

        public void WatersVsMz5OptimzeCeImportPerformanceTest()
        {
            NativeVsMz5OptimzeCeImportPerformanceTest(
                "WatersVsMz5OptimizeCeImportPerfTest",
                "CE_Xevo_sch_062810_import.sky");
        }


        [TestMethod] 
        public void AllVsMz5OptimzeCeImportPerformanceTests()
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global RunPerfTests flag is set

            _loopCount = 3;
            Log.AddMemoryAppender();
            AgilentVsMz5OptimzeCeImportPerformanceTest();
            ThermoVsMz5OptimzeCeImportPerformanceTest();
            AbVsMz5OptimzeCeImportPerformanceTest();
            WatersVsMz5OptimzeCeImportPerformanceTest();
            var logs = Log.GetMemoryAppendedLogEvents();
            var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // show summary, combining native per test and mz5 per test
            var log = new Log("Summary");
            log.Info(stats.Replace(_testFilesDir.PersistentFilesDir,""));

        }

        public void NativeVsMz5OptimzeCeImportPerformanceTest(string baseName, string skyFile)
        {
            // compare mz5 and raw import times
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/" + baseName +".zip";
            TestFilesPersistent = new[] {"Native/", "Mz5/"}; // list of files (directories, in this case) that we'd like to unzip alongside parent zipFile, and (re)use in place
            _testFilesDir = new TestFilesDir(TestContext, TestFilesZip, null, TestFilesPersistent);
            _skyFile = _testFilesDir.GetTestPath(Path.Combine(baseName,skyFile));


            foreach (var resultspath in TestFilesPersistent)  
            {
                _replicatesDirName = Path.Combine(_testFilesDir.PersistentFilesDir, Path.Combine(baseName, resultspath.Substring(0, resultspath.Length - 1)));
                for (var loop = 0; loop < _loopCount + 1; loop++) // one extra initial loop for warmup
                {
                    MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = (loop==0); // turn on performance measurement after initial warmup loop
                    RunFunctionalTest();
                    File.Delete(Path.ChangeExtension(_skyFile,"skyd")); // make sure we're clean for next pass
                    
                }
            }
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(_skyFile));

            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();
            ImportResultsReplicatesCE(_replicatesDirName, 60 * 60);    // Allow 60 minutes for loading.
            loadStopwatch.Stop();

            DebugLog.Info("{0} load time = {1}", _replicatesDirName, loadStopwatch.ElapsedMilliseconds);
        }  
    }
}