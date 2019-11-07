/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Test for importing one document into antother on the command line
    /// borrowing heavily from TestFunctional.ImportDocTest
    /// </summary>
    [TestClass]
    public class CommandLineImportDocTest : AbstractUnitTestEx
    {
        private const string ZIP_FILE = @"TestFunctional\ImportDocTest.zip";

        [TestMethod]
        public void ConsoleImportDocTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string[] documentPaths =
            {
                TestFilesDir.GetTestPath("document1.sky"), // subject1, subject2, buffer (waters calcurv - annotations, manual integration, removed peak)
                TestFilesDir.GetTestPath("document2.sky"), // subject1, buffer (waters calcurve - annotations + custom, manual integration)
                TestFilesDir.GetTestPath("document3.sky"), // subject2 (waters calcurve - node notes, manual integration)
                TestFilesDir.GetTestPath("document4.sky"), // subject2 (agilent bovine1 - manual integration)
                TestFilesDir.GetTestPath("document5.sky"), // opt1, opt2 (thermo bovine2 optimization data)
            };

            int[] groupCounts = {36, 24, 12, 6, 10};
            int[] tranCounts = {72, 48, 24, 23, 440};
            int[] peakCounts = {518, 310, 162, 52, 3608};
            string[] cachePaths = new string[documentPaths.Length];
            long[] cacheSizes = new long[documentPaths.Length];
            for (int i = 0; i < documentPaths.Length; i++)
            {
                cachePaths[i] = ChromatogramCache.FinalPathForName(documentPaths[i], null);
                cacheSizes[i] = new FileInfo(cachePaths[i]).Length;  // Actual length of the file
            }

            const int firstIndex = 1;
            string firstDocPath = documentPaths[firstIndex];
            string emptyDocPath = TestFilesDir.GetTestPath("in_document.sky");
            var docEmpty = new SrmDocument(SrmSettingsList.GetDefault());
            docEmpty.SerializeToFile(emptyDocPath, emptyDocPath, SkylineVersion.CURRENT, null);
            
            string docPersistPath = TestFilesDir.GetTestPath("out_document.sky");
            string cachePersistPath = ChromatogramCache.FinalPathForName(docPersistPath, null);
            string msg = RunCommand("--in=" + emptyDocPath,
                "--import-document=" + firstDocPath,
                "--import-document-results=" + MeasuredResults.MergeAction.merge_names,
                "--out=" + docPersistPath);

            CheckRunCommandOutputContains(string.Format(Resources.SkylineWindow_ImportFiles_Importing__0__, Path.GetFileName(firstDocPath)), msg);

            var doc = ResultsUtil.DeserializeDocument(docPersistPath);
            var docImported = ResultsUtil.DeserializeDocument(documentPaths[firstIndex]);

            Assert.IsTrue(doc.Settings.HasResults);
            // Make sure original cache file is still on disk
            Assert.IsTrue(File.Exists(cachePaths[firstIndex]));
            Assert.IsTrue(File.Exists(cachePersistPath));

            // The cache version of the original test file is 3.
            // The cache file just created is version 4 or higher.
            long startCacheLen = ResultsUtil.CacheSize(doc, cacheSizes[firstIndex],
                groupCounts[firstIndex], tranCounts[firstIndex], peakCounts[firstIndex]);
            AssertEx.IsDocumentState(doc, null, docImported.MoleculeGroupCount, docImported.MoleculeCount,
                docImported.MoleculeTransitionGroupCount, docImported.MoleculeTransitionCount);
            
            long singleCacheLen = new FileInfo(cachePersistPath).Length;
            Assert.AreEqual(startCacheLen, singleCacheLen);

            // Import a document removing the results
            string docPersistPath2 = TestFilesDir.GetTestPath("out_document2.sky");
            string lastDocPath = documentPaths[3];
            msg = RunCommand("--in=" + docPersistPath,
                "--import-document=" + lastDocPath,
                "--import-document-results=" + MeasuredResults.MergeAction.remove,
                "--out=" + docPersistPath2);

            CheckRunCommandOutputContains(string.Format(Resources.SkylineWindow_ImportFiles_Importing__0__, Path.GetFileName(lastDocPath)), msg);

            // Skyd file should not have changed, but new molecules should have been added
            var doc2 = ResultsUtil.DeserializeDocument(docPersistPath2);
            var docImported2 = ResultsUtil.DeserializeDocument(lastDocPath);
            // One protein should get merged
            int groups2 = docImported.MoleculeGroupCount + docImported2.MoleculeTransitionGroupCount - 1;
            int mols = docImported.MoleculeCount + docImported2.MoleculeCount;
            int trans = docImported.MoleculeTransitionCount + docImported2.MoleculeTransitionCount;
            AssertEx.IsDocumentState(doc2, null, groups2, mols, trans);
            Assert.AreEqual(doc.MeasuredResults, doc2.MeasuredResults);
            Assert.AreEqual(startCacheLen, new FileInfo(cachePersistPath).Length);

            // Try again importing both at the same time without any results
            string docPersistPath3 = TestFilesDir.GetTestPath("out_document3.sky");
            string cachePersistPath3 = ChromatogramCache.FinalPathForName(docPersistPath3, null);
            msg = RunCommand("--in=" + emptyDocPath,
                "--import-document=" + firstDocPath,
                "--import-document=" + lastDocPath,
                "--import-document-results=" + MeasuredResults.MergeAction.remove,
                "--out=" + docPersistPath3);

            CheckRunCommandOutputContains(string.Format(Resources.SkylineWindow_ImportFiles_Importing__0__, Path.GetFileName(firstDocPath)), msg);
            CheckRunCommandOutputContains(string.Format(Resources.SkylineWindow_ImportFiles_Importing__0__, Path.GetFileName(lastDocPath)), msg);

            // Skyd file should not have changed, but new molecules should have been added
            var doc3 = ResultsUtil.DeserializeDocument(docPersistPath3);
            AssertEx.IsDocumentState(doc2, null, groups2, mols, trans);
            Assert.IsFalse(doc3.Settings.HasResults);
            Assert.IsFalse(File.Exists(cachePersistPath3));
            var moleculeGroups2 = doc2.MoleculeGroups.ToArray();
            var moleculeGroups3 = doc3.MoleculeGroups.ToArray();
            // Make sure the order is the same
            for (int i = 0; i < groups2; i++)
            {
                Assert.AreEqual(moleculeGroups2[i].Id, moleculeGroups3[i].Id);
            }

            // Import a document adding all replicates
            const int nextIndex = 0;
            string nextDocPath = documentPaths[nextIndex];
            string docPersistPath4 = TestFilesDir.GetTestPath("out_document4.sky");
            string cachePersistPath4 = ChromatogramCache.FinalPathForName(docPersistPath4, null);
            msg = RunCommand("--in=" + docPersistPath,
                "--import-document=" + nextDocPath,
                "--import-document-results=" + MeasuredResults.MergeAction.add,
                "--out=" + docPersistPath4);

            CheckRunCommandOutputContains(string.Format(Resources.SkylineWindow_ImportFiles_Importing__0__, Path.GetFileName(nextDocPath)), msg);

            var docAdd = ResultsUtil.DeserializeDocument(docPersistPath4);
            var docAdded = ResultsUtil.DeserializeDocument(nextDocPath);
            long expectCacheLen = startCacheLen
                + ResultsUtil.CacheSize(docAdded, cacheSizes[nextIndex], groupCounts[nextIndex], tranCounts[nextIndex], peakCounts[nextIndex])
                - ChromatogramCache.HeaderSize; // Only one header between the two caches

            // No peptide merging should have happened
            AssertEx.IsDocumentState(docAdd, null,
                doc.PeptideGroupCount + docAdded.PeptideGroupCount,
                doc.PeptideCount + docAdded.PeptideCount,
                doc.PeptideTransitionCount + docAdded.PeptideTransitionCount);

            Assert.AreEqual(3, docAdded.Settings.MeasuredResults.Chromatograms.Count);
            var chromatograms = docAdd.Settings.MeasuredResults.Chromatograms;
            var chromatogramsInitial = doc.Settings.MeasuredResults.Chromatograms;
            int chromCount = chromatograms.Count;
            Assert.AreEqual(chromatogramsInitial.Count + 3, chromatograms.Count);
            Assert.AreEqual("buffer1", chromatograms[chromCount - 1].Name);
            Assert.AreEqual("subject3", chromatograms[chromCount - 2].Name);
            Assert.AreEqual("subject2", chromatograms[chromCount - 3].Name);

            // Make sure annotations and user set peaks were added and not lost
            var stateAdd = new DocResultsState(docAdd);
            var stateAdded = new DocResultsState(docAdded);
            var state = new DocResultsState(doc);
            Assert.IsTrue(stateAdd.HasResults && stateAdded.HasResults);
            Assert.AreEqual(stateAdd.NoteCount, state.NoteCount + stateAdded.NoteCount);
            Assert.AreEqual(stateAdd.AnnotationCount, state.AnnotationCount + stateAdded.AnnotationCount);
            Assert.AreEqual(stateAdd.UserSetCount, state.UserSetCount + stateAdded.UserSetCount);
            // Because the data in the two documents actually cover the same results,
            // some calculation is required to determine the number of chromInfo objects
            // expected.
            double fOld = chromCount / (double)chromatogramsInitial.Count;
            double fAdded = chromCount / (double)docAdded.Settings.MeasuredResults.Chromatograms.Count;
            Assert.AreEqual(stateAdd.PeptideResults,
                            (int)(state.PeptideResults * fOld + stateAdded.PeptideResults * fAdded));
            Assert.AreEqual(stateAdd.TransitionGroupResults,
                            (int)(state.TransitionGroupResults * fOld + stateAdded.TransitionGroupResults * fAdded));
            Assert.AreEqual(stateAdd.TransitionResults,
                            (int)(state.TransitionResults * fOld + stateAdded.TransitionResults * fAdded));
            foreach (var nodeGroup in docAdd.PeptideTransitionGroups)
            {
                for (int i = 0; i < 5; i++)
                    Assert.AreEqual(1, nodeGroup.Results[i].Count);
            }

            // Cache should now contain results for both documents
            long newCacheLen = new FileInfo(cachePersistPath4).Length;
            Assert.AreEqual(expectCacheLen, newCacheLen);        
        }

        private static void CheckRunCommandOutputContains(string expectedMessage, string actualMessage)
        {
            Assert.IsTrue(actualMessage.Contains(expectedMessage),
                string.Format("Expected RunCommand result message containing \n\"{0}\",\ngot\n\"{1}\"\ninstead.", expectedMessage, actualMessage));
        }
    }
}