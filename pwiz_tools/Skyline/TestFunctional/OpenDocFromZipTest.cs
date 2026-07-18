/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.Database.FileSystems;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests opening a shared document (.sky.zip) directly, without extracting it, when the .zip
    /// contains only document files and the ones needing random access (.skyd, .blib) are stored
    /// uncompressed. The same document opened in place should be indistinguishable from the same
    /// document opened normally: the chromatograms come out of the .skyd and the spectra out of the
    /// .blib, all read from inside the .zip.
    /// </summary>
    [TestClass]
    public class OpenDocFromZipTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOpenDocFromZip()
        {
            TestFilesZip = @"TestFunctional\LibraryShareTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Open the original document from disk and remember what a fully loaded version looks like.
            string documentPath = TestFilesDir.GetTestPath("LibraryShareTest.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath));
            var originalDoc = WaitForDocumentLoaded();
            var expected = Summarize(originalDoc);
            Assert.IsTrue(expected.PeptideCount > 0);
            Assert.IsTrue(expected.SpectrumCount > 0);
            Assert.IsTrue(expected.ChromatogramPointCount > 0);

            // Share the document as a .sky.zip. The .skyd and .blib get stored uncompressed.
            string inPlaceZip = TestFilesDir.GetTestPath("InPlace.sky.zip");
            RunUI(() => SkylineWindow.ShareDocument(inPlaceZip, new ShareType(true, null)));
            WaitForConditionUI(() => SkylineWindow.DocumentUI.Settings.PeptideSettings.Libraries.IsLoaded);

            // The shared .zip must qualify for opening in place: only document files, and the
            // random-access files stored uncompressed.
            Assert.IsTrue(new SrmDocumentSharing(inPlaceZip).CanOpenInPlace(),
                "shared .zip has entries that would prevent opening in place: " +
                string.Join(", ", new RandomAccessZipFile(inPlaceZip).Entries.Select(e => e.ToString())));

            // Clear the document, then open the shared file. It should open in place, not extract.
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.OpenSharedFile(inPlaceZip));
            var inPlaceDoc = WaitForDocumentLoaded();

            // The document was opened directly from the .zip: its path is inside the .zip, and
            // SharedZipFilePath records the container so later edits extract the files first.
            Assert.AreEqual(inPlaceZip, SkylineWindow.SharedZipFilePath);
            Assert.IsTrue(new FilePath(SkylineWindow.DocumentFilePath).IsInZipFile,
                "expected the document to be opened from inside the .zip: " + SkylineWindow.DocumentFilePath);
            StringAssert.StartsWith(SkylineWindow.DocumentFilePath, inPlaceZip);

            // Everything read from inside the .zip must match the document read normally.
            var actual = Summarize(inPlaceDoc);
            Assert.AreEqual(expected.PeptideCount, actual.PeptideCount);
            Assert.AreEqual(expected.TransitionCount, actual.TransitionCount);
            Assert.AreEqual(expected.SpectrumCount, actual.SpectrumCount, "library spectra read from the .blib differ");
            Assert.AreEqual(expected.ChromatogramPointCount, actual.ChromatogramPointCount,
                "chromatogram points read from the .skyd differ");

            // Sharing a document which is itself open in place: its .skyd and .blib have no path on
            // disk, so they are added to the new .zip as streams read from the old one.
            string reSharedZip = TestFilesDir.GetTestPath("ReShared.sky.zip");
            RunUI(() => SkylineWindow.ShareDocument(reSharedZip, new ShareType(true, null)));
            AssertEx.FileExists(reSharedZip);
            Assert.IsTrue(new SrmDocumentSharing(reSharedZip).CanOpenInPlace(),
                "re-shared .zip has entries that would prevent opening in place: " +
                string.Join(", ", new RandomAccessZipFile(reSharedZip).Entries.Select(e => e.ToString())));

            // The re-shared .zip must hold the same document: open it in place and compare.
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.OpenSharedFile(reSharedZip));
            var reSharedDoc = WaitForDocumentLoaded();
            Assert.AreEqual(reSharedZip, SkylineWindow.SharedZipFilePath);
            var reShared = Summarize(reSharedDoc);
            Assert.AreEqual(expected.PeptideCount, reShared.PeptideCount);
            Assert.AreEqual(expected.TransitionCount, reShared.TransitionCount);
            Assert.AreEqual(expected.SpectrumCount, reShared.SpectrumCount,
                "library spectra differ after re-sharing from inside a .zip");
            Assert.AreEqual(expected.ChromatogramPointCount, reShared.ChromatogramPointCount,
                "chromatogram points differ after re-sharing from inside a .zip");

            // Saving a document opened in place cannot write back into the .zip; everything is
            // extracted to a folder and reopened from there, and that document is what gets saved.
            RunUI(() => SkylineWindow.SaveDocument());
            var extractedDoc = WaitForDocumentLoaded();
            Assert.IsNull(SkylineWindow.SharedZipFilePath, "the extracted document should no longer be .zip-backed");
            Assert.IsFalse(new FilePath(SkylineWindow.DocumentFilePath).IsInZipFile,
                "the extracted document should be on disk: " + SkylineWindow.DocumentFilePath);
            AssertEx.FileExists(SkylineWindow.DocumentFilePath);
            var extracted = Summarize(extractedDoc);
            Assert.AreEqual(expected.PeptideCount, extracted.PeptideCount);
            Assert.AreEqual(expected.SpectrumCount, extracted.SpectrumCount);
            Assert.AreEqual(expected.ChromatogramPointCount, extracted.ChromatogramPointCount);
        }

        private class DocSummary
        {
            public int PeptideCount { get; set; }
            public int TransitionCount { get; set; }
            public int SpectrumCount { get; set; }
            public int ChromatogramPointCount { get; set; }
        }

        /// <summary>
        /// Reads enough of the document (peptide/transition counts, library spectrum count, and the
        /// chromatogram points of the first precursor) to prove the .skyd and .blib were read.
        /// </summary>
        private static DocSummary Summarize(SrmDocument doc)
        {
            Assert.IsTrue(doc.Settings.PeptideSettings.Libraries.IsLoaded);
            Assert.IsTrue(doc.Settings.HasResults && doc.Settings.MeasuredResults.IsLoaded);

            var summary = new DocSummary
            {
                PeptideCount = doc.PeptideCount,
                TransitionCount = doc.PeptideTransitionCount,
                SpectrumCount = doc.Settings.PeptideSettings.Libraries.Libraries.Sum(lib => lib?.SpectrumCount ?? 0),
            };

            var measuredResults = doc.Settings.MeasuredResults;
            float tolerance = (float)doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var nodePep in doc.Peptides)
            {
                foreach (var nodeGroup in nodePep.TransitionGroups)
                {
                    if (measuredResults.TryLoadChromatogram(0, nodePep, nodeGroup, tolerance, out var infoSet)
                        && infoSet.Length > 0)
                    {
                        // Reading TimeIntensitiesGroup forces the actual chromatogram points to be
                        // read out of the .skyd (in place, from inside the .zip).
                        summary.ChromatogramPointCount = infoSet.Sum(info =>
                            info.TimeIntensitiesGroup.TransitionTimeIntensities.Sum(ti => ti.NumPoints));
                        return summary;
                    }
                }
            }

            return summary;
        }
    }
}
