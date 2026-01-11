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
            VerifyRetentionTimeLoadSequence(1);
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.SelectedChromatograms = new[]{SkylineWindow.Document.MeasuredResults.Chromatograms[2]};
                manageResultsDlg.RemoveReplicates();
                manageResultsDlg.OkDialog();
            });
            RunUI(()=>
            {
                SkylineWindow.SaveDocument();
                _documents.Clear();
                SkylineWindow.OpenFile(SkylineWindow.DocumentFilePath);
            });
            WaitForDocumentLoaded();
            VerifyRetentionTimeLoadSequence(0);
        }

        private void VerifyRetentionTimeLoadSequence(int resultFileAlignmentCount)
        {
            Assert.AreNotEqual(0, _documents.Count);
            var initialDocument = _documents[0];
            string documentLibraryName = initialDocument.Settings.PeptideSettings.Libraries.LibrarySpecs
                .FirstOrDefault(spec => spec.IsDocumentLibrary)?.Name;
            Assert.IsNotNull(documentLibraryName);
            Assert.IsNull(initialDocument.Settings.DocumentRetentionTimes.GetLibraryAlignment(documentLibraryName));
            var deserializedAlignments =
                initialDocument.Settings.DocumentRetentionTimes
                    .GetDeserializedAlignmentsForLibrary(documentLibraryName);
            Assert.IsNotNull(deserializedAlignments);
            var deserializedResultFileAlignments =
                initialDocument.Settings.DocumentRetentionTimes.GetDeserializedResultFileAlignments();
            if (resultFileAlignmentCount == 0)
            {
                Assert.IsNull(deserializedResultFileAlignments);
            }
            else
            {
                Assert.AreEqual(resultFileAlignmentCount, deserializedResultFileAlignments.Count);
            }
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

        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            _documents.Add(SkylineWindow.Document);
        }

        private LibraryAlignment GetDocumentLibraryAlignment(SrmDocument document)
        {
            var documentLibrary =
                document.Settings.PeptideSettings.Libraries.LibrarySpecs.FirstOrDefault(spec => spec.IsDocumentLibrary);
            Assert.IsNotNull(documentLibrary);
            return document.Settings.DocumentRetentionTimes.GetLibraryAlignment(documentLibrary.Name);
        }
    }
}
