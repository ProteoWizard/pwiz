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
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Performance tests to verify that changing our zlib implementation isn't 
    /// going to slow us down with minimization.
    /// </summary>
    [TestClass]
    public class PerfMinimizeResultsTest : AbstractFunctionalTest
    {
        private const string ZIP_FILE = @"https://skyline.gs.washington.edu/perftests/PerfMinimizeResultsTest.zip";

        [TestMethod]
        public void TestMinimizeResultsPerformance()
        {

            TestFilesZip = ZIP_FILE; // handles the download if needed

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestFilesPersistent = new[] { "Buck" };
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip, null, TestFilesPersistent);
            string documentPath = testFilesDir.GetTestPath("CM Buck_Lum_Comm_fr III for MS1 paper_v1.sky"); // this will be in user download area as a persistent doc
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.MeasuredResults.IsLoaded);

            Stopwatch loadStopwatch = new Stopwatch();

            var doc = SkylineWindow.Document;
            var minimizedFile = testFilesDir.GetTestPath("minimized.sky"); // Not L10N
            var cacheFile = Path.ChangeExtension(minimizedFile, ChromatogramCache.EXT);
            {
                RunUI(() => SkylineWindow.SaveDocument(minimizedFile));

                var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                var minimizeResultsDlg = ShowDialog<MinimizeResultsDlg>(manageResultsDlg.MinimizeResults);
                RunUI(() =>
                {
                    minimizeResultsDlg.LimitNoiseTime = false;
                });

                loadStopwatch.Start();
                OkDialog(minimizeResultsDlg, () => minimizeResultsDlg.MinimizeToFile(minimizedFile));
                WaitForCondition(() => File.Exists(cacheFile));
                WaitForClosedForm(manageResultsDlg);
                loadStopwatch.Stop();
            }
            WaitForDocumentChange(doc);

            DebugLog.Info("minimization time = {0}", loadStopwatch.ElapsedMilliseconds);

        }

    }

}
