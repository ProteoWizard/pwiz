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


using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Compare performance of vendor readers vs mz5 for results import.
    /// </summary>
    [TestClass]
    public class PerformanceVsMz5Test : AbstractFunctionalTest
    {
        private string _skyFile;
        private string _dataFile;

        /// <summary>
        /// compare various raw files and mz5 equivalents.
        /// </summary>
        public void WatersVsMz5ChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsWatersVsMz5.zip",
                "Waters\\Hoofnagle_MSe_targeted.sky",
                "Waters\\DATA\\2013_03_13_UWash_S1_MSE_Adj_001.raw");
        }

        public void AbIdaVsMz5ChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsAbIdaVsMz5.zip",
                "AB\\Hoofnagle_5600_IDA_targeted.sky",
                "AB\\5600_DDA\\Hoofnagle_01_IDA.wiff");
        }

        public void AbDiaVsMz5ChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsAbDiaVsMz5.zip",
                "AB\\Hoofnagle_5600_DIA_targeted.sky",
                "AB\\5600_DIA\\Hoofnagle_10xDil_SWATH_01.wiff");
        }

        public void ThermoDdaVsMz5ChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsThermoDDAVsMz5.zip",
                "Thermo\\Hoofnagle_QE_DDA_targeted.sky",
                "Thermo\\QE_DDA\\20130311_DDA_Pit01.raw");
        }

        public void ThermoDiaVsMz5ChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsThermoDiaVsMz5.zip",
                "Thermo\\Hoofnagle_QE_DIA_targeted.sky",
                "Thermo\\QE_DIA\\20130311_DIA_Pit01.raw");
        }

        public void AgilentDiaVsMz5ChromatogramPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsAgilentDiaVsMz5.zip",
                "BSA_Agilent_DIA_profile.sky",
                "BSA_100fmol_SWATH.d");
        }

        public void BrukerFullScanMS1filteringPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsBrukerFullScanMS1filtering.zip",
                "MS1 filtering\\MS1 filtering for ID_BSA_minimized.sky",
                "MS1 filtering\\BSA_50fmol_30min_5sWOthr2000_BA8_01_2360.d");
        }

        public void BrukerFullScanMSeDataPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsBrukerFullScanMSeData.zip",
                "MSe data\\MSe_BSA_Skyline 13-test-bsa.sky",
                "MSe data\\BSA_50fmol_30min_15-30V_1Hz_GA3_01_1097.d");
        }

        public void BrukerFullScanSWATHDataPerformanceTest()
        {
            NativeVsMz5ChromatogramPerformanceTest(
                "PerfImportResultsBrukerFullScanSWATHData.zip",
                "SWATH data\\SWATH Ecoli-test.sky",
                "SWATH data\\SWATH_Ecoli+UPS0.5pmol_120min_BC2_01_1919.d");
        }


//        [TestMethod] // commented out so it doesn't get run in normal use
        public void AllVsMz5ChromatogramPerformanceTests()
        {
            AbDiaVsMz5ChromatogramPerformanceTest();
            AbIdaVsMz5ChromatogramPerformanceTest();
            AgilentDiaVsMz5ChromatogramPerformanceTest();
            //BrukerFullScanMS1filteringPerformanceTest();  Bruker and MSTest don't get along
            //BrukerFullScanMSeDataPerformanceTest();
            //BrukerFullScanSWATHDataPerformanceTest();
            ThermoDdaVsMz5ChromatogramPerformanceTest();
            ThermoDiaVsMz5ChromatogramPerformanceTest();
            WatersVsMz5ChromatogramPerformanceTest();
        }

        public void NativeVsMz5ChromatogramPerformanceTest(string zipFile, string skyFile, string rawFile)
        {
            // compare mz5 and raw import times
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/" + zipFile;
            var mz5File = Path.ChangeExtension(rawFile, "mz5");
            TestFilesPersistent = new[] { rawFile, mz5File }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip, null, TestFilesPersistent);
            _skyFile = testFilesDir.GetTestPath(skyFile);
            string nativeResults = testFilesDir.GetTestPath(rawFile);
            string mz5Results = Path.ChangeExtension(nativeResults,"mz5");

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // turn on performance measurement

            foreach (var resultspath in new[] { mz5Results, nativeResults })  //
            {
                _dataFile = resultspath;
                RunFunctionalTest();
                File.Delete(Path.ChangeExtension(_skyFile,"skyd")); // make sure we're clean for next pass
            }
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(_skyFile));

            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();
            ImportResultsFile(_dataFile, 60 * 60);    // Allow 60 minutes for loading.
            loadStopwatch.Stop();

            DebugLog.Info("{0} load time = {1}", _dataFile, loadStopwatch.ElapsedMilliseconds);
        }  
    }
}