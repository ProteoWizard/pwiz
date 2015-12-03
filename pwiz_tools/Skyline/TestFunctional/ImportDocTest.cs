/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for CE Optimization.
    /// </summary>
    [TestClass]
    public class ImportDocTest : AbstractFunctionalTest
    {
        private string[] _documentPaths;
        private string[] _cachePaths;
        private int[] _groupCounts;
        private int[] _tranCounts;
        private long[] _cacheSizes;

        [TestMethod]
        public void TestImportDoc()
        {
            TestFilesZip = @"TestFunctional\ImportDocTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test import document functionality with results importing
        /// </summary>
        protected override void DoTest()
        {            
            _documentPaths = new[]
                                         {
                                             TestFilesDir.GetTestPath("document1.sky"), // subject1, subject2, buffer (waters calcurv - annotations, manual integration, removed peak)
                                             TestFilesDir.GetTestPath("document2.sky"), // subject1, buffer (waters calcurve - annotations + custom, manual integration)
                                             TestFilesDir.GetTestPath("document3.sky"), // subject2 (waters calcurve - node notes, manual integration)
                                             TestFilesDir.GetTestPath("document4.sky"), // subject2 (agilent bovine1 - manual integration)
                                             TestFilesDir.GetTestPath("document5.sky"), // opt1, opt2 (thermo bovine2 optimization data)
                                         };
            _groupCounts = new[] {36, 24, 12, 6, 10};
            _tranCounts = new[] {72, 48, 24, 23, 440};

            _cachePaths = new string[_documentPaths.Length];
            _cacheSizes = new long[_documentPaths.Length];
            for (int i = 0; i < _documentPaths.Length; i++)
            {
                _cachePaths[i] = ChromatogramCache.FinalPathForName(_documentPaths[i], null);
                _cacheSizes[i] = new FileInfo(_cachePaths[i]).Length;
            }

            var docEmpty = SkylineWindow.Document;
            var state = new DocResultsState(docEmpty);
            // Import a document into the empty document attempt to keep the results and fail
            var importDlg = ShowDialog<ImportDocResultsDlg>(() => SkylineWindow.ImportFiles(_documentPaths[1]));
            RunUI(() => importDlg.Action = MeasuredResults.MergeAction.merge_names);
            var messageDlg = ShowDialog<MessageDlg>(importDlg.OkDialog);

            // Allow results to be removed
            RunUI(messageDlg.OkDialog);
            WaitForClosedForm(messageDlg);
            RunUI(importDlg.OkDialog);
            WaitForClosedForm(importDlg);
            var docFirstAttempt = WaitForDocumentChange(docEmpty);
            // Results state should not have changed
            state.AreEqual(docFirstAttempt);

            // Undo and save
            string docPersistPath = TestFilesDir.GetTestPath("out_document.sky");
            string cachePersistPath = ChromatogramCache.FinalPathForName(docPersistPath, null);
            RunUI(() =>
                {
                    SkylineWindow.Undo();
                    SkylineWindow.SaveDocument(docPersistPath);
                });
            // Document now changes because of document GUID
            Assert.AreNotSame(docEmpty, SkylineWindow.Document);
            Assert.AreNotSame(docEmpty.Settings.DataSettings, SkylineWindow.Document.Settings.DataSettings);
            Assert.AreSame(docEmpty.Children, SkylineWindow.Document.Children);
            Assert.AreSame(docEmpty.Settings.PeptideSettings, SkylineWindow.Document.Settings.PeptideSettings);
            Assert.AreSame(docEmpty.Settings.TransitionSettings, SkylineWindow.Document.Settings.TransitionSettings);
            Assert.IsTrue(File.Exists(docPersistPath));
            Assert.IsFalse(File.Exists(cachePersistPath));

            // Try again
            const int firstIndex = 1;
            RunDlg<ImportDocResultsDlg>(() => SkylineWindow.ImportFiles(_documentPaths[firstIndex]), dlg =>
                {
                    dlg.Action = MeasuredResults.MergeAction.merge_names;
                    dlg.OkDialog();
                });

            var docInitial = WaitForDocumentChangeLoaded(docEmpty);
            state = new DocResultsState(docInitial);
            Assert.IsTrue(state.HasResults);
            // Make sure cache is where it is expected to be
            Assert.AreEqual(cachePersistPath, docInitial.Settings.MeasuredResults.CachePaths.ToArray()[0]);
            // Make sure original cache file is still on disk
            Assert.IsTrue(File.Exists(_cachePaths[firstIndex]));
            Assert.IsTrue(File.Exists(cachePersistPath));

            // The cache version of the original test file is 3.
            // The cache file just created is version 4 or higher.
            long startCacheLen = GetCacheSize(firstIndex, docInitial);
            long singleCacheLen = new FileInfo(cachePersistPath).Length;
            Assert.AreEqual(startCacheLen, singleCacheLen);

            RunUI(() =>
                      {
                          SkylineWindow.SelectedPath = new IdentityPath(SequenceTree.NODE_INSERT_ID);
                      });

            // Import a document removing the results
            RunDlg<ImportDocResultsDlg>(() => SkylineWindow.ImportFiles(_documentPaths[3]), dlg =>
                {
                    dlg.Action = MeasuredResults.MergeAction.remove;
                    dlg.OkDialog();
                });
            var docRemove = WaitForDocumentChange(docInitial);
            // Results state should not have changed
            state.AreEqual(docRemove);

            RunUI(SkylineWindow.Undo);

            Assert.AreSame(docInitial, SkylineWindow.Document);

            // Import a document adding all replicates
            const int nextIndex = 0;
            RunDlg<ImportDocResultsDlg>(() => SkylineWindow.ImportFiles(_documentPaths[nextIndex]), dlg =>
            {
                dlg.Action = MeasuredResults.MergeAction.add;
                dlg.OkDialog();
            });
            var docAdd = WaitForDocumentChangeLoaded(docInitial);
            var docAdded = ResultsUtil.DeserializeDocument(_documentPaths[nextIndex]);
            long expectCacheLen = startCacheLen
                + GetCacheSize(nextIndex, docAdded)
                - ChromatogramCache.HeaderSize; // Only one header between the two caches

            // No peptide merging should have happened
            AssertEx.IsDocumentState(docAdd, null,
                docInitial.PeptideGroupCount + docAdded.PeptideGroupCount,
                docInitial.PeptideCount + docAdded.PeptideCount,
                docInitial.PeptideTransitionCount + docAdded.PeptideTransitionCount);

            Assert.AreEqual(3, docAdded.Settings.MeasuredResults.Chromatograms.Count);
            var chromatograms = docAdd.Settings.MeasuredResults.Chromatograms;
            var chromatogramsInitial = docInitial.Settings.MeasuredResults.Chromatograms;
            int chromCount = chromatograms.Count;
            Assert.AreEqual(chromatogramsInitial.Count + 3, chromatograms.Count);
            Assert.AreEqual("buffer1", chromatograms[chromCount - 1].Name);
            Assert.AreEqual("subject3", chromatograms[chromCount - 2].Name);
            Assert.AreEqual("subject2", chromatograms[chromCount - 3].Name);

            // Make sure annotations and user set peaks were added and not lost
            var stateAdd = new DocResultsState(docAdd);
            var stateAdded = new DocResultsState(docAdded);
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
                            (int) (state.PeptideResults*fOld + stateAdded.PeptideResults*fAdded));
            Assert.AreEqual(stateAdd.TransitionGroupResults,
                            (int) (state.TransitionGroupResults*fOld + stateAdded.TransitionGroupResults*fAdded));
            Assert.AreEqual(stateAdd.TransitionResults,
                            (int) (state.TransitionResults*fOld + stateAdded.TransitionResults*fAdded));
            foreach (var nodeGroup in docAdd.PeptideTransitionGroups)
            {
                for (int i = 0; i < 5; i++)
                    Assert.AreEqual(1, nodeGroup.Results[i].Count);
            }

            // Cache should now contain results for both documents
            long newCacheLen = new FileInfo(cachePersistPath).Length;
            Assert.AreEqual(expectCacheLen, newCacheLen);
            
            // An undo followed by a redo should not change that
            Assert.AreEqual(9, SkylineWindow.Document.RevisionIndex);
            RunUI(SkylineWindow.Undo);
            Assert.AreEqual(6, SkylineWindow.Document.RevisionIndex);
            Assert.AreEqual(newCacheLen, new FileInfo(cachePersistPath).Length);
            RunUI(SkylineWindow.Redo);
            Assert.AreEqual(9, SkylineWindow.Document.RevisionIndex);
            Assert.AreEqual(newCacheLen, new FileInfo(cachePersistPath).Length);
            // Undo followed by a save, should reduce cache to previous size
            RunUI(SkylineWindow.Undo);
            Assert.AreEqual(6, SkylineWindow.Document.RevisionIndex);
            RunUI(() => SkylineWindow.SaveDocument());
            Assert.AreEqual(startCacheLen, new FileInfo(cachePersistPath).Length);
            Assert.AreEqual(7, SkylineWindow.Document.RevisionIndex);
            Thread.Sleep(10);  // Wait 10 ms to make sure the cache change in Redo registers as a cache modification
            // After which, a redo should return the document to the add state and
            // restore the cache
            RunUI(SkylineWindow.Redo);
            var docRedo = WaitForDocumentLoaded();
            Assert.AreEqual(10, docRedo.RevisionIndex);
            stateAdd.AreEqual(docRedo);
            Assert.AreEqual(newCacheLen, new FileInfo(cachePersistPath).Length);

            // Import matching replicates by name
            var docPreUndo = SkylineWindow.Document;
            RunUI(SkylineWindow.Undo);
            var docUndoLoaded = WaitForDocumentChangeLoaded(docPreUndo);
            Assert.AreEqual(8, docUndoLoaded.RevisionIndex);
            Assert.AreNotSame(docInitial, docUndoLoaded);
            Assert.AreEqual(docInitial, docUndoLoaded);    // Cache optimization changes document
            docInitial = docUndoLoaded;
            RunDlg<ImportDocResultsDlg>(() => SkylineWindow.ImportFiles(_documentPaths[0]), dlg =>
            {                
                dlg.Action = MeasuredResults.MergeAction.merge_names;
                dlg.OkDialog();
            });
            var docNames = WaitForDocumentChangeLoaded(docInitial);
            // Should have the same impact on state as adding all replicates.  The information
            // just gets stored differently.
            chromatograms = docNames.Settings.MeasuredResults.Chromatograms;
            Assert.AreEqual(3, chromatograms.Count);
            Assert.AreEqual(chromatogramsInitial[0].Name, chromatograms[0].Name);
            Assert.AreEqual(2, chromatograms[0].MSDataFileInfos.Count);
            Assert.AreEqual(chromatogramsInitial[1].Name, chromatograms[1].Name);
            Assert.AreEqual(2, chromatograms[1].MSDataFileInfos.Count);
            var missingNames = (from chromatogramSet in docAdded.Settings.MeasuredResults.Chromatograms
                                where !chromatogramsInitial.Any(cs => Equals(cs.Name, chromatogramSet.Name))
                                select chromatogramSet.Name).ToArray();
            Assert.AreEqual(missingNames[0], chromatograms[2].Name);
            Assert.AreEqual(1, chromatograms[2].MSDataFileInfos.Count);
            stateAdd.AreEqual(docNames);
            foreach (var nodeGroup in docNames.PeptideTransitionGroups)
            {
                Assert.AreEqual(2, nodeGroup.Results[0].Count);
                Assert.AreEqual(2, nodeGroup.Results[1].Count);
                Assert.AreEqual(1, nodeGroup.Results[2].Count);
            }

            // Import merging by order
            RunUI(() =>
                      {
                          SkylineWindow.Undo();
                          var docCurrent = SkylineWindow.DocumentUI;
                          if (ReferenceEquals(docInitial, docCurrent))
                              Assert.AreEqual(8, docInitial.RevisionIndex);
                          else
                          {
                              // Attempt to report more information when the test fails here
                              AssertEx.DocsEqual(docInitial, docCurrent);
                              Assert.AreEqual(docInitial.Id.GlobalIndex, docCurrent.Id.GlobalIndex);
                              Assert.AreEqual(docInitial.UserRevisionIndex, docCurrent.UserRevisionIndex);
                              Assert.AreEqual(docInitial.RevisionIndex, docCurrent.RevisionIndex);
                              Assert.AreSame(docInitial, docCurrent);
                          }
                      });
            Assert.AreSame(docInitial, SkylineWindow.Document);
            RunDlg<ImportDocResultsDlg>(() => SkylineWindow.ImportFiles(_documentPaths[3]), dlg =>
            {
                dlg.Action = MeasuredResults.MergeAction.merge_indices;
                dlg.OkDialog();
            });
            var docOrder = WaitForDocumentChangeLoaded(docInitial);
            var docOrderAdded = ResultsUtil.DeserializeDocument(_documentPaths[3]);

            chromatograms = docOrder.Settings.MeasuredResults.Chromatograms;
            Assert.AreEqual(2, chromatograms.Count);
            AssertEx.AreEqualDeep(chromatogramsInitial.Select(chrom => chrom.Name).ToArray(),
                chromatogramsInitial.Select(chrom => chrom.Name).ToArray());

            var stateOrder = new DocResultsState(docOrder);
            var stateOrderAdded = new DocResultsState(docOrderAdded);
            Assert.AreEqual(stateOrder.NoteCount, state.NoteCount + stateOrderAdded.NoteCount);
            Assert.AreEqual(stateOrder.AnnotationCount, state.AnnotationCount + stateOrderAdded.AnnotationCount);
            Assert.AreEqual(stateOrder.UserSetCount, state.UserSetCount + stateOrderAdded.UserSetCount);
            Assert.AreEqual(stateOrder.PeptideResults, state.PeptideResults + stateOrderAdded.PeptideResults);
            Assert.AreEqual(stateOrder.TransitionGroupResults,
                state.TransitionGroupResults + stateOrderAdded.TransitionGroupResults);
            Assert.AreEqual(stateOrder.TransitionResults,
                state.TransitionResults + stateOrderAdded.TransitionResults);
            foreach (var nodeGroup in docOrder.PeptideTransitionGroups)
            {
                Assert.AreEqual(1, nodeGroup.Results[0].Count);
            }

            // Import merging by order with overflow and multiple files
            RunUI(SkylineWindow.Undo);
            Assert.AreEqual(docInitial, SkylineWindow.Document);
            RunDlg<ImportDocResultsDlg>(() => SkylineWindow.ImportFiles(_documentPaths[0], _documentPaths[2]), dlg =>
            {
                dlg.Action = MeasuredResults.MergeAction.merge_indices;
                dlg.OkDialog();
            });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count > 2);
            var docOrder2 = WaitForDocumentLoaded();
            var docAdded2 = ResultsUtil.DeserializeDocument(_documentPaths[2]);

            // No peptide merging should have happened
            AssertEx.IsDocumentState(docOrder2, null,
                docInitial.PeptideGroupCount + docAdded.PeptideGroupCount + docAdded2.PeptideGroupCount,
                docInitial.PeptideCount + docAdded.PeptideCount + docAdded2.PeptideCount,
                docInitial.PeptideTransitionCount + docAdded.PeptideTransitionCount + docAdded2.PeptideTransitionCount);

            chromatograms = docOrder2.Settings.MeasuredResults.Chromatograms;
            Assert.AreEqual(3, chromatograms.Count);
            Assert.AreEqual(chromatogramsInitial[0].Name, chromatograms[0].Name);
            Assert.AreEqual(chromatogramsInitial[1].Name, chromatograms[1].Name);
            Assert.AreEqual("buffer1", chromatograms[2].Name);

            var stateOrder2 = new DocResultsState(docOrder2);
            var stateAdded2 = new DocResultsState(docAdded2);
            Assert.AreEqual(stateOrder2.NoteCount, stateAdd.NoteCount + stateAdded2.NoteCount);
            Assert.AreEqual(stateOrder2.AnnotationCount, stateAdd.AnnotationCount + stateAdded2.AnnotationCount);
            Assert.AreEqual(stateOrder2.UserSetCount, stateAdd.UserSetCount + stateAdded2.UserSetCount);
            // Because the data all 3 documents actually cover the same results,
            // some calculation is required to determine the number of chromInfo objects
            // expected.
            int fileCount = docOrder2.Settings.MeasuredResults.Chromatograms.SelectMany(chrom => chrom.MSDataFileInfos).Count();
            fOld = fileCount/(double) chromatogramsInitial.Count;
            fAdded = fileCount/(double) docAdded.Settings.MeasuredResults.Chromatograms.Count;
            double fAdded2 = fileCount/(double) docAdded2.Settings.MeasuredResults.Chromatograms.Count;
            Assert.AreEqual(stateOrder2.PeptideResults,
                            (int)(state.PeptideResults*fOld + stateAdded.PeptideResults*fAdded + stateAdded2.PeptideResults*fAdded2));
            Assert.AreEqual(stateOrder2.TransitionGroupResults,
                            (int)(state.TransitionGroupResults*fOld + stateAdded.TransitionGroupResults*fAdded + stateAdded2.TransitionGroupResults*fAdded2));
            Assert.AreEqual(stateOrder2.TransitionResults,
                            (int)(state.TransitionResults*fOld + stateAdded.TransitionResults*fAdded + stateAdded2.TransitionResults*fAdded2));
            foreach (var nodeGroup in docOrder2.PeptideTransitionGroups)
            {
                Assert.AreEqual(3, nodeGroup.Results[0].Count);
                Assert.AreEqual(2, nodeGroup.Results[1].Count);
                Assert.AreEqual(1, nodeGroup.Results[2].Count);

                // Make sure files are ordered as expected
                Assert.AreEqual(docInitial.Settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].FilePath,
                    chromatograms[0].GetFileInfo(nodeGroup.Results[0][0].FileId).FilePath);
                Assert.AreEqual(docAdded.Settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].FilePath,
                    chromatograms[0].GetFileInfo(nodeGroup.Results[0][1].FileId).FilePath);
                Assert.AreEqual(docAdded2.Settings.MeasuredResults.Chromatograms[0].MSDataFileInfos[0].FilePath,
                    chromatograms[0].GetFileInfo(nodeGroup.Results[0][2].FileId).FilePath);
            }

            // Now import allowing matching peptides to be merged
            RunUI(SkylineWindow.Undo);
            Assert.AreEqual(docInitial, SkylineWindow.Document);
            RunDlg<ImportDocResultsDlg>(() => SkylineWindow.ImportFiles(_documentPaths[0], _documentPaths[2]), dlg =>
            {
                dlg.Action = MeasuredResults.MergeAction.add;
                dlg.IsMergePeptides = true;
                dlg.OkDialog();
            });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Count > 2);
            var docMerged = WaitForDocumentLoaded();

            chromatograms = docMerged.Settings.MeasuredResults.Chromatograms;
            Assert.AreEqual(6, chromatograms.Count);

            var dictPeptides = docInitial.Peptides.ToDictionary(nodePep => nodePep.Key);
            foreach(var nodePep in docAdded.Peptides.Where(p => !dictPeptides.ContainsKey(p.Key)))
                dictPeptides.Add(nodePep.Key, nodePep);
            foreach (var nodePep in docAdded2.Peptides.Where(p => !dictPeptides.ContainsKey(p.Key)))
                dictPeptides.Add(nodePep.Key, nodePep);
            AssertEx.IsDocumentState(docMerged, null, 3, dictPeptides.Count,
                dictPeptides.Values.Sum(nodePep => nodePep.TransitionCount));

            var stateMerged = new DocResultsState(docMerged);
            Assert.AreEqual(stateMerged.NoteCount, stateAdd.NoteCount + stateAdded2.NoteCount);
            Assert.AreEqual(stateMerged.AnnotationCount, stateAdd.AnnotationCount + stateAdded2.AnnotationCount);
            Assert.AreEqual(stateMerged.UserSetCount, stateAdd.UserSetCount + stateAdded2.UserSetCount);

            var setColors = new HashSet<int>();
            foreach (var nodePep in docMerged.Peptides.Take(4))
            {
                setColors.Add(nodePep.Annotations.ColorIndex);
            }
            Assert.AreEqual(4, setColors.Count);
            Assert.IsFalse(setColors.Contains(-1));

            // TODO: Import optimization data

            // Check cache sizes
            // At this point, the main cache should be about the size of the sum of
            // the caches it has incorporated.
            Assert.AreEqual(newCacheLen + GetCacheSize(2) + GetCacheSize(3) - 2*ChromatogramCache.HeaderSize,
                            new FileInfo(cachePersistPath).Length, 200);
            // Undo and save should have set the main cache back to the initial state
            RunUI(SkylineWindow.Undo);
            Assert.AreEqual(docInitial, SkylineWindow.Document);
            RunUI(() => SkylineWindow.SaveDocument());
            Assert.AreEqual(startCacheLen, new FileInfo(cachePersistPath).Length);
            // And the original caches should remain unchanged
            for (int i = 0; i < _cachePaths.Length; i++)
            {
                Assert.AreEqual(_cacheSizes[i], new FileInfo(_cachePaths[i]).Length);
            }
            // Another undo and save should remove the results cache for the active document
            RunUI(() =>
                      {
                          SkylineWindow.Undo();
                          SkylineWindow.SaveDocument();
                      });
            Assert.IsFalse(File.Exists(cachePersistPath));
        }

        private long GetCacheSize(int docIndex, SrmDocument docInitial = null)
        {
            if (docInitial == null)
                docInitial = ResultsUtil.DeserializeDocument(_documentPaths[docIndex]);
            int dataGroupCount = _groupCounts[docIndex];
            int dataTranCount = _tranCounts[docIndex];
            long format3Size = _cacheSizes[docIndex];

            long cacheSize = format3Size;
            int fileCachedCount = docInitial.Settings.MeasuredResults.MSDataFileInfos.Count();
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_3)
            {
                // Cache version 4 stores instrument information, and is bigger in size.
                cacheSize += sizeof(int) * fileCachedCount;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_4)
            {
                // Cache version 5 adds an int for flags for each file
                // Allow for a difference in sizes due to the extra information.
                int fileFlagsSize = sizeof(int)*fileCachedCount;
                // And SeqIndex, SeqCount, StartScoreIndex and padding
                int groupHeadersSize = ChromGroupHeaderInfo5.DeltaSize5*dataGroupCount;
                // And flags for each transition
                int transitionFlagsSize = ChromTransition5.DeltaSize5*dataTranCount;
                // And num seq byte count, seq location, score types, num scores and score location
                const int headerScoreSize = sizeof(int) + sizeof(long) + sizeof(int) + sizeof(int) + sizeof(long);
                cacheSize += groupHeadersSize + fileFlagsSize + transitionFlagsSize + headerScoreSize;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_5)
            {
                // Cache version 6 adds status graph dimensions for every file
                cacheSize += sizeof(float) * 2 * fileCachedCount;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_6)
            {
                // Cache version 7 adds ion mobility information
                cacheSize += sizeof(float) * 2 * dataTranCount;
            }
            if (ChromatogramCache.FORMAT_VERSION_CACHE > ChromatogramCache.FORMAT_VERSION_CACHE_8)
            {
                // Cache version 9 adds scan id values for every file
                cacheSize += (sizeof(int) + sizeof(long)) * fileCachedCount;
                // And scan ids location to global header
                cacheSize += sizeof(long);
            }
            return cacheSize;
        }

        private class DocResultsState
        {
            public DocResultsState(SrmDocument document)
            {
                AddDocument(document);
            }

            private void AddDocument(SrmDocument document)
            {
//                var fileIndices = document.Settings.HasResults ?
//                    document.Settings.MeasuredResults.Chromatograms.SelectMany(set => set.MSDataFileInfos).Select(
//                    info => info.FileIndex).ToArray() : new int[0];
//                Console.WriteLine("--->");
                foreach (PeptideDocNode nodePep in document.Peptides)
                {
                    if (nodePep.HasResults)
                    {
                        PeptideResults += nodePep.Results.Where(result => result != null)
                                                         .SelectMany(info => info).Count();
                    }

                    foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                    {
                        if (nodeGroup.HasResults)
                        {
//                            int startSize = TransitionGroupResults;
                            foreach (var chromInfo in nodeGroup.Results.Where(result => result != null)
                                                                       .SelectMany(info => info))
                            {
                                TransitionGroupResults++;
                                if (chromInfo.Annotations.Note != null)
                                    NoteCount++;
                                if (chromInfo.Annotations.ListAnnotations().Length > 0)
                                    AnnotationCount++;
                            }
//                            if (TransitionGroupResults - startSize < fileIndices.Length)
//                            {
//                                var listIds = fileIndices.ToList();
//                                foreach (var chromInfo in nodeGroup.Results.Where(result => result != null)
//                                                                           .SelectMany(info => info))
//                                {
//                                    listIds.Remove(chromInfo.FileIndex);
//                                }
//                                Console.WriteLine("{0} ({1})", nodePep.Peptide.Sequence, String.Join(", ", listIds.Select(i => i.ToString()).ToArray()));
//                            }
                        }

                        foreach (var nodeTran in
                            nodeGroup.Children.Cast<TransitionDocNode>().Where(nodeTran => nodeTran.HasResults))
                        {
                            foreach (var chromInfo in nodeTran.Results.Where(result => result != null)
                                                                      .SelectMany(info => info))
                            {
                                TransitionResults++;
                                if (chromInfo.Annotations.Note != null)
                                    NoteCount++;
                                if (chromInfo.Annotations.ListAnnotations().Length > 0)
                                    AnnotationCount++;
                                if (chromInfo.IsUserSetManual)
                                    UserSetCount++;
                            }
                        }
                    }
                }
            }

            public void AreEqual(SrmDocument document)
            {
                var state = new DocResultsState(document);
                Assert.AreEqual(PeptideResults, state.PeptideResults);
                Assert.AreEqual(TransitionGroupResults, state.TransitionGroupResults);
                Assert.AreEqual(TransitionResults, state.TransitionResults);
                Assert.AreEqual(UserSetCount, state.UserSetCount);
                Assert.AreEqual(NoteCount, state.NoteCount);
                Assert.AreEqual(AnnotationCount, state.AnnotationCount);
            }

            public bool HasResults
            {
                get { return PeptideResults != 0 && TransitionGroupResults != 0 && TransitionResults != 0; }
            }

            public int PeptideResults { get; private set; }
            public int TransitionGroupResults { get; private set; }
            public int TransitionResults { get; private set; }
            public int UserSetCount { get; private set; }
            public int NoteCount { get; private set; }
            public int AnnotationCount { get; private set; }
        }
    }
}