/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportFailureTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestImportFailure()
        {
            Run(@"TestFunctional\ImportFailureTest.zip");
        }

        private const string SUCCEEDS_FILE_NAME = "succeeds.mzML";
        private const string SUCCEEDS2_FILE_NAME = "succeeds2.mzML";
        private const string FAILS_FILE_NAME = "fails.mzML";

        protected override void DoTest()
        {
            OpenDocument("Bovine_std_curated_seq_small2.sky");
            string succeedsFile = TestFilesDir.GetTestPath(SUCCEEDS_FILE_NAME);
            string succeeds2File = TestFilesDir.GetTestPath(SUCCEEDS2_FILE_NAME);
            string failsFile = TestFilesDir.GetTestPath(FAILS_FILE_NAME);
            File.Copy(succeedsFile, succeeds2File);
            var doc = WaitForDocumentLoaded();

            // Import one failing and one okay file.
            doc = ImportFailure(doc, FAILS_FILE_NAME, SUCCEEDS_FILE_NAME);
            Assert.IsTrue(doc.Settings.HasResults);
            var files = SkylineWindow.ImportingResultsWindow.Files.ToArray();
            Assert.AreEqual(failsFile, files[0].FilePath.GetFilePath());
            Assert.IsNotNull(files[0].Error);
            Assert.AreEqual(succeedsFile, files[1].FilePath.GetFilePath());
            Assert.IsNull(files[1].Error);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(SUCCEEDS_FILE_NAME),
                doc.Settings.MeasuredResults.Chromatograms[0].Name);
            RunUI(() => SkylineWindow.DestroyAllChromatogramsGraph());

            // Import another okay file, followed by failing file.
            doc = ImportFailure(doc, SUCCEEDS2_FILE_NAME, FAILS_FILE_NAME);
            files = SkylineWindow.ImportingResultsWindow.Files.ToArray();
            Assert.AreEqual(succeeds2File, files[0].FilePath.GetFilePath());
            Assert.IsNull(files[0].Error);
            Assert.AreEqual(failsFile, files[1].FilePath.GetFilePath());
            Assert.IsNotNull(files[1].Error);
            Assert.AreEqual(2, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(SUCCEEDS_FILE_NAME),
                doc.Settings.MeasuredResults.Chromatograms[0].Name);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(SUCCEEDS2_FILE_NAME),
                doc.Settings.MeasuredResults.Chromatograms[1].Name);

            // Fix failure and retry.
            File.Copy(succeedsFile, failsFile, true);
            RunUI(() => SkylineWindow.ImportingResultsWindow.ClickAutoCloseWindow());
            RunUI(() => SkylineWindow.ImportingResultsWindow.RetryImport(1));
            doc = WaitForDocumentChangeLoaded(doc); 
            WaitForConditionUI(() => SkylineWindow.ImportingResultsWindow.IsComplete(1));
            files = SkylineWindow.ImportingResultsWindow.Files.ToArray();
            Assert.AreEqual(2, files.Length);
            Assert.AreEqual(succeeds2File, files[0].FilePath.GetFilePath());
            Assert.IsNull(files[0].Error);
            Assert.AreEqual(failsFile, files[1].FilePath.GetFilePath());
            Assert.IsNull(files[1].Error);

            Assert.AreEqual(3, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(SUCCEEDS_FILE_NAME),
                doc.Settings.MeasuredResults.Chromatograms[0].Name);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(SUCCEEDS2_FILE_NAME),
                doc.Settings.MeasuredResults.Chromatograms[1].Name);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(FAILS_FILE_NAME),
                doc.Settings.MeasuredResults.Chromatograms[2].Name);

            RunUI(() => SkylineWindow.DestroyAllChromatogramsGraph());
        }

        private SrmDocument ImportFailure(SrmDocument doc, params string[] dataFiles)
        {
            // Keep import progress window open after failure.
            ImportResultsAsync(dataFiles);
            doc = WaitForDocumentChangeLoaded(doc);
            WaitForConditionUI(() => SkylineWindow.ImportingResultsWindow.Finished);
            return doc;
        }
    }
}