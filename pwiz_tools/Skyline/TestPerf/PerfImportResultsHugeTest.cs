/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Compare performance of vendor readers vs mz5 for results import.
    /// </summary>
    [TestClass]
    public class PerfImportResultsHugeTest : AbstractFunctionalTest
    {
        // Solid state drive
//        private const string SKY_FILE = @"C:\Users\donmarsh\Documents\Huge\Hains_700.sky";
//        private const string DATA_FILE = @"C:\Users\donmarsh\Documents\Huge\Adult_2_SW_01.wiff";

        // Hard drive
        private const string SKY_FILE = @"D:\Data\Hains_700.sky\Hains_700.sky";
        private const string DATA_FILE = @"D:\Data\HAINS_SWATH_MS\Adult_2_SW_01.wiff";

        [TestMethod]
        [Timeout(int.MaxValue)]  // These can take a long time
        public void ImportResultsHugeTest()
        {
            // RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(SKY_FILE));
            WaitForDocumentLoaded();

            ImportResultsFile(DATA_FILE, 10 * 24 * 60 * 60);    // Allow 10 days for loading.
        }
    }
}