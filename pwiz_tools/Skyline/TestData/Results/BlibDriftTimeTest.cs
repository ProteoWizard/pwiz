/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    /// <summary>
    /// Unit test for drift time data in Bibliospec libraries,
    /// specifically for the ability to borrow from sublibraries
    /// when the drift time for a charged peptide isn't found in
    /// in the sublibrary associated with the original final_fragment.csv
    /// </summary>
    [TestClass]
    public class BlibDriftTimeTest : AbstractUnitTest
    {

        [TestMethod]
        public void TestBlibDriftTimes()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\Results\BlibDriftTimeTest.zip");
            // Open document with some peptides but no results
            var docPath = TestFilesDir.GetTestPath("BlibDriftTimeTest.sky");
            SrmDocument docOriginal = ResultsUtil.DeserializeDocument(docPath);
            using (var docContainer = new ResultsTestDocumentContainer(docOriginal, docPath))
            {
                var doc = docContainer.Document;

                // Use the bare drift times in the spectral library
                var librarySpec = new BiblioSpecLiteSpec("drift test",
                                                    TestFilesDir.GetTestPath("BlibDriftTimeTest.blib"));
                var ionMobility = doc.Settings.TransitionSettings.IonMobilityFiltering
                    .ChangeFilterWindowWidthCalculator(
                        new IonMobilityWindowWidthCalculator(
                            IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power, 20, 0, 0, 0))
                    .ChangeUseSpectralLibraryIonMobilityValues(true);
                doc = doc.ChangeSettings(
                    doc.Settings.ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] { librarySpec })).
                        ChangeTransitionSettings(t => t.ChangeIonMobilityFiltering(ionMobility)));

                // Import an mz5 file that needs drift info that's in the original data set, 
                // but preserved in the .blib file associated with a different raw source
                // Without the bugfix this won't get any drift time filtering.
                const string replicateName = "ID12692_01_UCA168_3727_040714";
                var chromSets = new[]
                                {
                                    new ChromatogramSet(replicateName, new[]
                                        { new MsDataFilePath(TestFilesDir.GetTestPath("ID12692_01_UCA168_3727_040714" + ExtensionTestContext.ExtMz5)),  }),
                                };
                var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                Assert.IsTrue(docContainer.SetDocument(docResults, docOriginal, true));
                docContainer.AssertComplete();
                var document = docContainer.Document;

                float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                double maxHeight = 0;
                var results = document.Settings.MeasuredResults;
                Assert.AreEqual(2, document.PeptidePrecursorPairs.Count());
                var pair = document.PeptidePrecursorPairs.ToArray()[1];
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, out chromGroupInfo));
                Assert.AreEqual(1, chromGroupInfo.Length);
                var chromGroup = chromGroupInfo[0];
                Assert.AreEqual(2, chromGroup.NumPeaks); // This will be higher if we don't filter on DT
                foreach (var tranInfo in chromGroup.TransitionPointSets)
                {
                    maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                }
                Assert.AreEqual(278, maxHeight, 1);  // Without DT filtering, this will be much greater - about 996
            }
        }

        /// <summary>
        /// Issue 4150: the spectral library carries ion mobility values, but "use spectral library
        /// ion mobility values" is disabled, so importing results produces no ion mobility filter.
        /// The import should emit a non-blocking diagnostic naming the specific settings change
        /// that would restore filtering, which depends on whether a filter window is configured.
        /// </summary>
        [TestMethod]
        public void TestBlibDriftTimeFilteringDisabledWarning()
        {
            // Both cases import the same replicate, so each needs its own extraction directory.
            // Sharing one would let the second import reuse the first one's chromatogram cache -
            // ion mobility settings changes do not invalidate a cache, which is why the diagnostic
            // tells the user to re-import - and no extraction means no diagnostic to assert on.
            TestFilesDirs = new[]
            {
                new TestFilesDir(TestContext, @"TestData\Results\BlibDriftTimeTest.zip", null, @"WindowOnly"),
                new TestFilesDir(TestContext, @"TestData\Results\BlibDriftTimeTest.zip", null, @"NoWindow"),
                new TestFilesDir(TestContext, @"TestData\Results\BlibDriftTimeTest.zip", null, @"ZeroResolvingPower")
            };

            // A usable filter window is configured, so enabling the spectral library option is the
            // only change needed. This exercises the spectral-library probe path, which harvesting
            // ion mobility values with the option left off would silently defeat.
            AssertFilteringDisabledWarning(TestFilesDirs[0], new IonMobilityWindowWidthCalculator(20),
                ResultsResources.SpectrumFilter_IonMobilityFilteringDisabled_EnableSpectralLibrary);

            // No filter window either, so the user needs both changes.
            AssertFilteringDisabledWarning(TestFilesDirs[1], IonMobilityWindowWidthCalculator.EMPTY,
                ResultsResources.SpectrumFilter_IonMobilityFilteringDisabled_SetWindowAndEnableSpectralLibrary);

            // Resolving power 0 is accepted as "no filtering", but widens to an unbounded window
            // rather than an empty one. It must still count as no window, so the user is told to set
            // one as well, not only to enable the spectral library option.
            AssertFilteringDisabledWarning(TestFilesDirs[2], new IonMobilityWindowWidthCalculator(0),
                ResultsResources.SpectrumFilter_IonMobilityFilteringDisabled_SetWindowAndEnableSpectralLibrary);
        }

        /// <summary>
        /// Imports two replicates in one import with the spectral library's ion mobility values
        /// disabled and the given filter window, then asserts that the expected "filtering is
        /// disabled" diagnostic was written exactly once for the whole import - not once per
        /// file - and that neither of the other two variants was written.
        /// </summary>
        private void AssertFilteringDisabledWarning(TestFilesDir testFilesDir,
            IonMobilityWindowWidthCalculator windowWidthCalculator, string expectedMessageFormat)
        {
            var docPath = testFilesDir.GetTestPath("BlibDriftTimeTest.sky");
            SrmDocument docOriginal = ResultsUtil.DeserializeDocument(docPath);
            using (var docContainer = new ResultsTestDocumentContainer(docOriginal, docPath))
            {
                var doc = docContainer.Document;

                // Spectral library has drift times, but the option to use those library values is
                // left OFF - so no filter is produced.
                var librarySpec = new BiblioSpecLiteSpec("drift test",
                    testFilesDir.GetTestPath("BlibDriftTimeTest.blib"));
                var ionMobility = doc.Settings.TransitionSettings.IonMobilityFiltering
                    .ChangeFilterWindowWidthCalculator(windowWidthCalculator)
                    .ChangeUseSpectralLibraryIonMobilityValues(false);
                doc = doc.ChangeSettings(
                    doc.Settings.ChangePeptideLibraries(lib => lib.ChangeLibrarySpecs(new[] { librarySpec })).
                        ChangeTransitionSettings(t => t.ChangeIonMobilityFiltering(ionMobility)));

                // A second copy of the data file makes this a multi-file import, in which every
                // file reaches the same conclusion about the settings.
                const string replicateName = "ID12692_01_UCA168_3727_040714";
                const string replicateNameCopy = replicateName + "_copy";
                var fileName = replicateName + ExtensionTestContext.ExtMz5;
                var fileNameCopy = replicateNameCopy + ExtensionTestContext.ExtMz5;
                File.Copy(testFilesDir.GetTestPath(fileName), testFilesDir.GetTestPath(fileNameCopy));
                var fileNames = new[] { fileName, fileNameCopy };
                var chromSets = new[]
                {
                    new ChromatogramSet(replicateName, new[]
                        { new MsDataFilePath(testFilesDir.GetTestPath(fileName)) }),
                    new ChromatogramSet(replicateNameCopy, new[]
                        { new MsDataFilePath(testFilesDir.GetTestPath(fileNameCopy)) }),
                };

                List<string> capturedMessages;
                using (var capture = new UserMessageCapture())
                {
                    var docResults = doc.ChangeMeasuredResults(new MeasuredResults(chromSets));
                    AssertEx.IsTrue(docContainer.SetDocument(docResults, docOriginal, true));
                    docContainer.AssertComplete();
                    capturedMessages = capture.CapturedMessages;
                }

                // The diagnostic has to name the one settings change that would restore filtering,
                // so the variants describing the other changes must not appear. It is given once
                // per import, naming whichever file reached it first.
                var capturedText = string.Join(Environment.NewLine, capturedMessages);
                Assert.AreEqual(1, CountNotices(capturedMessages, expectedMessageFormat, fileNames), capturedText);
                foreach (var messageFormat in new[]
                         {
                             ResultsResources.SpectrumFilter_IonMobilityFilteringDisabled_SetWindow,
                             ResultsResources.SpectrumFilter_IonMobilityFilteringDisabled_EnableSpectralLibrary,
                             ResultsResources.SpectrumFilter_IonMobilityFilteringDisabled_SetWindowAndEnableSpectralLibrary
                         })
                {
                    if (!Equals(messageFormat, expectedMessageFormat))
                    {
                        Assert.AreEqual(0, CountNotices(capturedMessages, messageFormat, fileNames), capturedText);
                    }
                }
            }
        }

        private static int CountNotices(IList<string> capturedMessages, string messageFormat, IEnumerable<string> fileNames)
        {
            var notices = fileNames.Select(name => string.Format(messageFormat, name)).ToHashSet();
            return capturedMessages.Count(notices.Contains);
        }
    }
}
