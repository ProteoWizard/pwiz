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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RetentionTimeManagerTest : AbstractFunctionalTest
    {
        private List<SrmDocument> _documents = new List<SrmDocument>();
        [TestMethod]
        public void TestRetentionTimeManager()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeManagerTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var docContainer = (IDocumentContainer)SkylineWindow;
            using var _ = new ScopedAction(
                () => docContainer.Listen(OnDocumentChanged),
                () => docContainer.Unlisten(OnDocumentChanged));

            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ThreeReplicates.sky"));
            });
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                _documents.Clear();
                SkylineWindow.OpenFile(SkylineWindow.DocumentFilePath);
            });
            WaitForDocumentLoaded();
            Assert.AreNotEqual(0, _documents.Count);
            var initialDocument = _documents[0];
            Assert.AreEqual(1, initialDocument.Settings.DocumentRetentionTimes.ResultFileAlignments.GetAlignmentFunctions().Count());
            string documentLibraryName = initialDocument.Settings.PeptideSettings.Libraries.LibrarySpecs
                .FirstOrDefault(spec => spec.IsDocumentLibrary)?.Name;
            Assert.IsNotNull(documentLibraryName);
            Assert.IsNull(initialDocument.Settings.DocumentRetentionTimes.GetLibraryAlignment(documentLibraryName));
            var deserializedAlignments =
                initialDocument.Settings.DocumentRetentionTimes
                    .GetDeserializedAlignmentsForLibrary(documentLibraryName);
            Assert.IsNotNull(deserializedAlignments);
            Assert.IsNull(GetDocumentLibraryAlignment(initialDocument));
            var initialLibraries = initialDocument.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(initialLibraries.HasDocumentLibrary);
            Assert.AreEqual(1, initialLibraries.LibrarySpecs.Count);
            Assert.AreEqual(1, initialLibraries.Libraries.Count);
            Assert.IsTrue(initialLibraries.LibrarySpecs[0].IsDocumentLibrary);
            var unloadedDocumentLibrary = initialLibraries.Libraries[0];
            Assert.IsNotNull(unloadedDocumentLibrary);
            Assert.IsFalse(unloadedDocumentLibrary.IsLoaded);
            Assert.IsNull(GetDocumentLibraryAlignment(initialDocument));
            int indexLibraryLoaded =
                _documents.FindIndex(doc => doc.Settings.PeptideSettings.Libraries.Libraries[0].IsLoaded);
            AssertEx.IsGreaterThan(indexLibraryLoaded, 0);
            var indexDocRetentionTimesLoaded = _documents.FindIndex(doc => doc.Settings.DocumentRetentionTimes.GetLibraryAlignment(documentLibraryName) != null);
            Assert.IsFalse(indexDocRetentionTimesLoaded < indexLibraryLoaded, "{0} should not be less than {1}", indexDocRetentionTimesLoaded, indexLibraryLoaded);
            var docRetentionTimesLoaded = _documents[indexDocRetentionTimesLoaded];
            var loadedLibraryAlignment = docRetentionTimesLoaded.Settings.DocumentRetentionTimes
                .GetLibraryAlignment(documentLibraryName);
            Assert.IsNotNull(loadedLibraryAlignment);
            Assert.AreSame(deserializedAlignments, loadedLibraryAlignment.Alignments);
            VerifyNoDocumentRetentionTimesChanges(_documents.Skip(indexDocRetentionTimesLoaded));
            _documents.Clear();
            RunUI(()=>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.SelectedResultsIndex = 2;
                SkylineWindow.RemovePeak();
            });
            WaitForDocumentLoaded();
            var docWithRemovedPeak = _documents[0];
            var docWithNewAlignment = _documents[_documents.Count - 1];
            Assert.IsTrue(SameAlignments(docWithRemovedPeak.Settings.DocumentRetentionTimes.ResultFileAlignments, docWithNewAlignment.Settings.DocumentRetentionTimes.ResultFileAlignments));
            RunUI(()=>SkylineWindow.EditDelete());
            WaitForDocumentLoaded();
            var docWithDeletedPeptide = _documents[_documents.Count - 1];
            Assert.IsFalse(SameAlignments(docWithRemovedPeak.Settings.DocumentRetentionTimes.ResultFileAlignments, docWithDeletedPeptide.Settings.DocumentRetentionTimes.ResultFileAlignments));
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.SelectedChromatograms = new[] { SkylineWindow.Document.MeasuredResults.Chromatograms[2] };
                dlg.RemoveReplicates();
                dlg.OkDialog();
            });
            WaitForDocumentLoaded();
            RunUI(()=>
            {
                SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("TwoReplicates.sky"));
                _documents.Clear();
            });
            Assert.IsTrue(SkylineWindow.Document.Settings.DocumentRetentionTimes.ResultFileAlignments.IsEmpty);

            // Wait for the document to be loaded so that it does not get switched out during a ReadPeaks call which might reopen a file stream
            WaitForDocumentLoaded();
        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            _documents.Add(SkylineWindow.Document);
        }

        private LibraryAlignment GetDocumentLibraryAlignment(SrmDocument document)
        {
            var libraries = document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(libraries.HasDocumentLibrary);
            var documentLibrary = libraries.LibrarySpecs.First(spec => spec.IsDocumentLibrary);
            return document.Settings.DocumentRetentionTimes.GetLibraryAlignment(documentLibrary.Name);
        }

        private void VerifyNoDocumentRetentionTimesChanges(IEnumerable<SrmDocument> documents)
        {
            SrmDocument lastDocument = null;
            int index = 0;
            foreach (var document in documents)
            {
                if (lastDocument != null)
                {
                    VerifySameDocumentRetentionTimes(lastDocument, document);
                }
                lastDocument = document;
                index++;
            }
        }

        private void VerifySameDocumentRetentionTimes(SrmDocument oldDocument, SrmDocument newDocument)
        {
            var oldDocRetentionTimes = oldDocument.Settings.DocumentRetentionTimes;
            var newDocRetentionTimes = newDocument.Settings.DocumentRetentionTimes;
            if (ReferenceEquals(oldDocRetentionTimes, newDocRetentionTimes))
            {
                return;
            }

            foreach (var librarySpec in newDocument.Settings.PeptideSettings.Libraries.LibrarySpecs.Concat(oldDocument
                         .Settings.PeptideSettings.Libraries.LibrarySpecs))
            {
                if (librarySpec != null)
                {
                    var oldAlignment = oldDocRetentionTimes.GetLibraryAlignment(librarySpec.Name);
                    var newAlignment = newDocRetentionTimes.GetLibraryAlignment(librarySpec.Name);
                    Assert.AreSame(oldAlignment?.Alignments, newAlignment?.Alignments);
                }
            }
            Assert.IsTrue(SameAlignments(oldDocRetentionTimes.ResultFileAlignments, newDocRetentionTimes.ResultFileAlignments));
        }

        private bool SameAlignments(ResultFileAlignments alignments1, ResultFileAlignments alignments2)
        {
            var alignmentFunctions1 =
                alignments1.GetAlignmentFunctions().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var alignmentFunctions2 = alignments2.GetAlignmentFunctions().ToList();
            if (alignmentFunctions1.Count != alignmentFunctions2.Count)
            {
                return false;
            }

            foreach (var alignmentFunctionEntry2 in alignmentFunctions2)
            {
                if (!alignmentFunctions1.TryGetValue(alignmentFunctionEntry2.Key, out var function1))
                {
                    return false;
                }

                if (!ReferenceEquals(function1, alignmentFunctionEntry2.Value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
