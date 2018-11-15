/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportResultsCancelTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// Verify that the import results cancel button works
        /// </summary>
        [TestMethod]
        public void TestImportResultsCancel()
        {
            // Not ready for stress testing yet. Causes intermittent failures at the level we currently test
            // with hundreds of runs a nights
            if (Program.StressTest)
                return;
            Run(@"TestFunctional\ImportResultsCancelTest.zip");
        }

        protected override void DoTest()
        {
            TestCancellation(true);  // Start with progress window visible
            TestCancellation(false);  // Start with progress window invisible
        }

        private const int maxTries = 10;

        private void TestCancellation(bool initiallyVisible)
        {
            string mz5 = ExtensionTestContext.ExtMz5;
            int initialStatusHeight = 0;
            RunUI(() => initialStatusHeight = SkylineWindow.StatusBarHeight);
            var files = new[] {"8fmol" + mz5, "20fmol" + mz5, "40fmol" + mz5, "200fmol" + mz5};
            OpenDocument("RetentionTimeFilterTest.sky");
            var skyfile = initiallyVisible ? "TestImportResultsCancelA.sky" : "TestImportResultsCancelB.sky";
            RunUI(() => { SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(skyfile)); });  // Make a clean copy

            // Try individual cancellation - can be timing dependent (do we get to the cancel button quickly enough?) so allow some retry
            int retry = 0;
            for (; retry < maxTries; retry++)
            {
                RemovePartialCacheFiles(files);
                OpenDocument(skyfile);
                Assert.IsFalse(SkylineWindow.Document.Settings.HasResults);
                Settings.Default.AutoShowAllChromatogramsGraph = initiallyVisible; // Start with progress window hidden?
                Settings.Default.ImportResultsSimultaneousFiles =
                    (int) MultiFileLoader.ImportResultsSimultaneousFileOptions.many; // Ensure all buttons are enabled
                ImportResultsAsync(files);
                WaitForConditionUI(
                    () => SkylineWindow.Document.IsLoaded ||
                        (SkylineWindow.ImportingResultsWindow != null &&
                        SkylineWindow.ImportingResultsWindow.ProgressTotalPercent >= 1)); // Get at least partway in
                // Make sure status bar height does not change showing import progress
                // Failure here is usually caused by statusProgress.Size y-dimension getting reset to 20 instead of 16
                RunUI(() => Assert.AreEqual(initialStatusHeight, SkylineWindow.StatusBarHeight, "Progress indicator changed status bar height"));
                if (!initiallyVisible)
                {
                    RunUI(() => SkylineWindow.ShowAllChromatogramsGraph()); // Turn it on
                }
                var dlg2 = TryWaitForOpenForm<AllChromatogramsGraph>(30000, () => SkylineWindow.Document.IsLoaded);
                if (dlg2 == null)
                {
                    if (SkylineWindow.Document.IsLoaded)
                    {
                        continue; // Loaded faster than we could react
                    }
                    else
                    {
                        dlg2 = WaitForOpenForm<AllChromatogramsGraph>();
                    }
                }
                WaitForConditionUI(30*1000, () => dlg2.ProgressTotalPercent >= 1 && dlg2.Files.Count() == 4); // Get a least a little way in
                int cancelIndex = retry%4;
                string cancelTarget = null;
                RunUI(() =>
                {
                    var fileStatus = dlg2.Files.ToArray();
                    Assert.AreEqual(4, fileStatus.Length);
                    for (int i = 0; i < files.Length; i++)
                    {
                        var statusCancel = fileStatus[(cancelIndex + i)%4];
                        if (0 < statusCancel.Progress && statusCancel.Progress < 100)
                        {
                            cancelTarget = Path.GetFileNameWithoutExtension(statusCancel.FilePath.GetFilePath());
                            dlg2.FileButtonClick(cancelTarget);
                            Assert.AreEqual(3, SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count);
                            Assert.IsFalse(SkylineWindow.Document.Settings.MeasuredResults.ContainsChromatogram(cancelTarget));
                            break;
                        }
                    }
                });
                WaitForDocumentLoaded();
                WaitForClosedAllChromatogramsGraph();
                
                if (cancelTarget == null)
                    continue;   // Found everything at 100%

                int fileCheck = 0;
                foreach (var file in files)
                {
                    int index;
                    ChromatogramSet chromatogramSet;
                    var chromatogramSetName = file.Replace(mz5, "");
                    // Can we find a loaded chromatogram set by this name?
                    SkylineWindow.Document.Settings.MeasuredResults.TryGetChromatogramSet(chromatogramSetName,
                        out chromatogramSet, out index);
                    if (!chromatogramSetName.Equals(cancelTarget))
                    {
                        // Should always find it since we didn't try to cancel this one
                        if (index == -1)
                            Assert.AreNotEqual(-1, index, string.Format("Missing chromatogram set {0} after cancelling {1}", chromatogramSetName, cancelTarget));
                        fileCheck++;
                    }
                    else if (index == -1)
                    {
                        fileCheck++;
                    }
                }
                if (fileCheck == files.Length)
                {
                    break;  // Success
                }
            }

            if (retry >= maxTries)
            {
                Assert.Fail("Failed to cancel individual file import after {0} tries", retry);
            }

            // Cancelled load should revert to initial document
            if (initiallyVisible)
            {
                CancelAll(new[] { files[0] }, false);
                CancelAll(new[] { files[3] }, true);
                CancelAll(files, false);
                CancelAll(files, true);
            }

            for (retry = 0; retry < maxTries; retry++)
            {
                // Now try a proper import
                Settings.Default.AutoShowAllChromatogramsGraph = initiallyVisible;
                RemovePartialCacheFiles(files);
                OpenDocument("RetentionTimeFilterTest.sky");
                ImportResultsAsync(files);
                if (!TryWaitForConditionUI(() =>
                    SkylineWindow.ImportingResultsWindow != null &&
                    SkylineWindow.ImportingResultsWindow.ProgressTotalPercent >= 1)) // Get at least partway in
                {
                    Assert.Fail("AllChromagotramsGraph missing in action");
                }

                if (!initiallyVisible)
                {
                    RunUI(() => SkylineWindow.ShowAllChromatogramsGraph()); // Turn it on
                }

                var dlgACG = TryWaitForOpenForm<AllChromatogramsGraph>(5000, () => SkylineWindow.Document.IsLoaded);
                if (dlgACG == null)
                    continue;   // Try again

                Assert.IsTrue(dlgACG.ChromatogramManager.SupportAllGraphs);
                Assert.IsNotNull(dlgACG.SelectedControl,
                    "unable to select a loader control in chromatogram progress window");
                Assert.IsTrue(dlgACG.Width > 500,
                    "Initially hidden chromatogram progress window did not size properly when enabled by user");
                    // Did it resize properly?
                WaitForDocumentLoaded();
                WaitForClosedAllChromatogramsGraph();
                AssertResult.IsDocumentResultsState(SkylineWindow.Document, "8fmol", 4, 4, 0, 13, 0);
                AssertResult.IsDocumentResultsState(SkylineWindow.Document, "20fmol", 5, 5, 0, 13, 0);
                AssertResult.IsDocumentResultsState(SkylineWindow.Document, "40fmol", 3, 3, 0, 11, 0);
                AssertResult.IsDocumentResultsState(SkylineWindow.Document, "200fmol", 4, 4, 0, 12, 0);

                break;
            }
        }

        private void CancelAll(string[] files, bool closeOnFinish)
        {
            Settings.Default.ImportResultsAutoCloseWindow = closeOnFinish;

            for (int retry = 0; retry < maxTries; retry++)
            {
                RemovePartialCacheFiles(files);
                OpenDocument("RetentionTimeFilterTest.sky");
                Assert.IsFalse(SkylineWindow.Document.Settings.HasResults);
                var docUnloaded = SkylineWindow.Document;
                ImportResultsAsync(files);
                var dlg = TryWaitForOpenForm<AllChromatogramsGraph>(5000, () => SkylineWindow.Document.IsLoaded);
                if (dlg == null)
                {
                    // May have happened so fast in the single file case that the ACG never opened
                    Assert.IsTrue(SkylineWindow.Document.Settings.HasResults);
                    continue;
                }
                RunUI(dlg.ClickCancel);
                if (closeOnFinish)
                    WaitForClosedAllChromatogramsGraph();
                WaitForDocumentLoaded();
                if (SkylineWindow.Document.Settings.HasResults)
                {
                    if (!closeOnFinish)
                        OkDialog(dlg, dlg.ClickClose);
                }
                else
                {
                    Assert.AreEqual(docUnloaded, SkylineWindow.Document);
                    if (!closeOnFinish)
                    {
                        WaitForConditionUI(() =>
                        {
                            for (int i = 0; i < files.Length; i++)
                            {
                                if (!dlg.IsItemCanceled(i))
                                    return false;
                            }
                            return true;
                        });
                        OkDialog(dlg, () =>
                        {
                            Assert.IsTrue(dlg.IsUserCanceled);
                            dlg.ClickClose();
                        });
                    }
                    break;
                }
            }
        }

        private void RemovePartialCacheFiles(string[] files)
        {
            WaitForChromatogramManagerQuiet();
            foreach (var file in files)
            {
                string cacheFile = ChromatogramCache.PartPathForName(SkylineWindow.DocumentFilePath, new MsDataFilePath(file));
                FileEx.SafeDelete(cacheFile, true);
            }
        }

        /// <summary>
        /// Because we keep opening the same document over and over and canceling processing,
        /// we need to wait for the ChromatogramManager to truly stop working before starting
        /// new work.
        /// </summary>
        private void WaitForChromatogramManagerQuiet()
        {
            WaitForConditionUI(() => !((ChromatogramManager)SkylineWindow.BackgroundLoaders.First(l => l is ChromatogramManager)).AnyProcessing());
        }
    }
}
