using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
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
            Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Libraries.HasDocumentLibrary);
            SkylineWindow.Listen(OnDocumentChanged);
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ThreeReplicates.sky"));
            });
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.Listen(OnDocumentChanged);
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
            AssertEx.IsGreaterThan(indexDocRetentionTimesLoaded, indexLibraryLoaded);
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
            Assert.AreNotSame(docWithRemovedPeak.Settings.DocumentRetentionTimes, docWithNewAlignment.Settings.DocumentRetentionTimes);

        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            var newDocument = SkylineWindow.Document;
            if (_documents.Count > 0)
            {
                var oldDocument = _documents[_documents.Count - 1];
                var oldResultFileAlignments = oldDocument.Settings.DocumentRetentionTimes.ResultFileAlignments;
                var newResultFileAlignments = newDocument.Settings.DocumentRetentionTimes.ResultFileAlignments;
                if (!ReferenceEquals(oldResultFileAlignments, newResultFileAlignments) && ReferenceEquals(oldDocument.Children, newDocument.Children) && ReferenceEquals(oldDocument.MeasuredResults?.Chromatograms, newDocument.MeasuredResults?.Chromatograms))
                {
                    Console.Out.WriteLine("here");
                }
            }
            if (_documents.Count > 0 &&
                !ReferenceEquals(_documents[_documents.Count - 1].Settings.DocumentRetentionTimes,
                    newDocument.Settings.DocumentRetentionTimes))
            {
                Console.Out.WriteLine("New DocumentRetentionTimes at {0}", _documents.Count);
            }
            _documents.Add(SkylineWindow.Document);
        }

        private LibraryAlignment GetDocumentLibraryAlignment(SrmDocument document)
        {
            var documentLibrary =
                document.Settings.PeptideSettings.Libraries.LibrarySpecs.FirstOrDefault(spec => spec.IsDocumentLibrary);
            Assert.IsNotNull(documentLibrary);
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

            var oldResultFileAlignments = oldDocRetentionTimes.ResultFileAlignments;
            var newResultFileAlignments = newDocRetentionTimes.ResultFileAlignments;
            if (!ReferenceEquals(oldResultFileAlignments, newResultFileAlignments))
            {
                Assert.IsFalse(oldResultFileAlignments.IsUpToDate(newDocument));
                Assert.IsTrue(newResultFileAlignments.IsUpToDate(newDocument));
                Assert.IsTrue(!ReferenceEquals(oldDocument.Children, newDocument.Children) ||
                              !ReferenceEquals(oldDocument.MeasuredResults?.Chromatograms,
                                  newDocument.MeasuredResults?.Chromatograms));
            }
        }
    }
}
