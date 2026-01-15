/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests minimizing the chromatograms in a document where some of the files do not have any chromatograms in the .skyd file.
    /// Those files with no chromatograms need to still have file entries in the .skyd file because otherwise Skyline
    /// will try to extract chromatograms from the .raw files.
    /// </summary>
    [TestClass]
    public class MinimizeWithEmptyFilesTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMinimizeWithEmptyFiles()
        {
            TestFilesZip = @"TestFunctional\MultiInjectCandidatePeakTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiInjectCandidatePeakTest.sky")));
            WaitForDocumentLoaded();
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = new[]
                    { SkylineWindow.Document.MeasuredResults.Chromatograms.Last() };
                manageResultsDlg.RemoveReplicates();
                manageResultsDlg.OkDialog();
            });
            RunUI(()=>SkylineWindow.SaveDocument());
            RunLongDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                RunDlg<MinimizeResultsDlg>(manageResultsDlg.MinimizeResults, minimizeResultsDlg =>
                {
                    minimizeResultsDlg.Minimize(false);
                });
            }, manageResultsDlg => { });
            WaitForDocumentLoaded();
            var cachedFiles = SkylineWindow.Document.MeasuredResults.CachedFilePaths.ToHashSet();
            foreach (var filePath in SkylineWindow.Document.MeasuredResults.MSDataFilePaths)
            {
                Assert.IsTrue(cachedFiles.Contains(filePath), "File {0} is not in .skyd file", filePath);
            }
        }
    }
}
