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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportResultsCancelTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// Verify that the import results cancel button works
        /// </summary>
      //  [TestMethod]  // TODO uncomment this when this test actually passes.  It works fine on its own in Test Explorer, but when run in a loop in SkylineTester it tends to fail, and/or issue warnings like "*** Attempt to complete document with non-final status ***"
        public void TestImportResultsCancel()
        {
            Run(@"TestFunctional\RetentionTimeFilterTest.zip");
        }

        protected override void DoTest()
        {
            TestCancellation(true);  // Start with progress window visible
            TestCancellation(false);  // Start with progress window invisible
        }

        private void TestCancellation(bool initiallyVisible)
        {
            var files = new[] {"8fmol.mz5", "20fmol.mz5", "40fmol.mz5", "200fmol.mz5"};
            OpenDocument("RetentionTimeFilterTest.sky");
            var skyfile = initiallyVisible ? "TestImportResultsCancelA.sky" : "TestImportResultsCancelB.sky";
            RunUI(() => { SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(skyfile)); });  // Make a clean copy

            // Try individual cancellation - can be timing dependent (do we get to the cancel button quickly enough?) so allow some retry
            for (var retry = 10; retry-- > 0;)
            {
                OpenDocument(skyfile);
                Settings.Default.AutoShowAllChromatogramsGraph = initiallyVisible; // Start with progress window hidden?
                Settings.Default.ImportResultsSimultaneousFiles =
                    (int) MultiFileLoader.ImportResultsSimultaneousFileOptions.many; // Ensure all buttons are enabled
                ImportResultsAsync(files);
                WaitForConditionUI(
                    () =>
                        SkylineWindow.ImportingResultsWindow != null &&
                        SkylineWindow.ImportingResultsWindow.ProgressTotalPercent >= 1); // Get at least partway in
                if (!initiallyVisible)
                {
                    RunUI(() => SkylineWindow.ShowAllChromatogramsGraph()); // Turn it on
                }
                var dlg2 = TryWaitForOpenForm<AllChromatogramsGraph>(30000);
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
                WaitForCondition(30*1000, () => dlg2.ProgressTotalPercent >= 1); // Get a least a little way in
                var cancelTarget = files[retry%4].Replace(".mz5", "");
                RunUI(() => dlg2.FileButtonClick(cancelTarget));
                WaitForDocumentLoaded();
                foreach (var file in files)
                {
                    int index;
                    ChromatogramSet chromatogramSet;
                    var chromatogramSetName = file.Replace(".mz5", "");
                    // Can we find a loaded chromatogram set by this name?
                    SkylineWindow.Document.Settings.MeasuredResults.TryGetChromatogramSet(chromatogramSetName,
                        out chromatogramSet, out index);
                    if (!chromatogramSetName.Equals(cancelTarget))
                    {
                        // Should always find it since we didn't try to cancel this one
                        Assert.AreNotEqual(-1, index);
                    }
                    else
                    {
                        // Should not find anything by that name, but sometimes we're not quick enough on the cancel button
                        if (retry == 0)
                        {
                            if (index != -1)
                                Assert.Fail("Failed to cancel individual file import");
                                    // No more retries, declare failure or success
                        }
                        else if (index == -1)
                        {
                            retry = 0; // Success, no more retry needed
                        }
                        else
                        {
                            break; // Go back and retry
                        }
                    }
                }
            }

            // Cancelled load should revert to initial document
            OpenDocument("RetentionTimeFilterTest.sky");
            var docUnloaded = SkylineWindow.Document;
            ImportResultsAsync(files);
            var dlg = WaitForOpenForm<AllChromatogramsGraph>();
            WaitForCondition(30*1000, () => dlg.ProgressTotalPercent >= 1); // Get a least a little way in
            RunUI(() => dlg.ClickCancel()); // Simulate user clicking the cancel button        
            Assert.AreEqual(docUnloaded, SkylineWindow.Document);
            Assert.IsFalse(SkylineWindow.Document.Settings.HasResults);

            // Now try a proper import
            Settings.Default.AutoShowAllChromatogramsGraph = initiallyVisible;
            ImportResultsAsync(files);
            WaitForConditionUI(
                () =>
                    SkylineWindow.ImportingResultsWindow != null &&
                    SkylineWindow.ImportingResultsWindow.ProgressTotalPercent > 2); // Get at least partway in
            if (!initiallyVisible)
            {
                RunUI(() => SkylineWindow.ShowAllChromatogramsGraph()); // Turn it on
            }
            var dlgACG = WaitForOpenForm<AllChromatogramsGraph>();
            Assert.IsTrue(dlgACG.ChromatogramManager.SupportAllGraphs);
            Assert.IsNotNull(dlgACG.SelectedControl, "unable to select a loader control in chromatogram progress window");
            Assert.IsTrue(dlgACG.Width > 500, "Initially hidden chromatogram progress window did not size properly when enabled by user"); // Did it resize properly?
            WaitForDocumentLoaded();
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, "8fmol", 4, 4, 0, 13, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, "20fmol", 5, 5, 0, 13, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, "40fmol", 3, 3, 0, 11, 0);
            AssertResult.IsDocumentResultsState(SkylineWindow.Document, "200fmol", 4, 4, 0, 12, 0);
        }
    }

}
