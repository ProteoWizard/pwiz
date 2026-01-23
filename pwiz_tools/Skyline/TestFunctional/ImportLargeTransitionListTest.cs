using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests importing a transition list with thousands of transitions under a single precursor.
    /// This test verifies that the batching optimization for transition imports works correctly,
    /// avoiding O(n^2) performance issues when adding many transitions to the same transition group.
    /// </summary>
    [TestClass]
    public class ImportLargeTransitionListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportLargeTransitionList()
        {
            TestFilesZip = @"TestFunctional\ImportLargeTransitionListTest.data";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Document.sky")));
            var docOrig = SkylineWindow.Document;
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(
                () => SkylineWindow.ImportMassList(TestFilesDir.GetTestPath("TransitionList.csv")));
            OkDialog(columnSelectDlg, columnSelectDlg.OkDialog);
            DismissAutoManageDialog();  // Decline auto-manage for new transitions
            WaitForDocumentChangeLoaded(docOrig);

            // Verify the document has loaded correctly with all expected transitions
            // The transition list has 20000 transitions for a single molecule/precursor
            var doc = SkylineWindow.Document;
            Assert.AreEqual(1, doc.MoleculeGroupCount, "Expected 1 molecule group");
            Assert.AreEqual(1, doc.MoleculeCount, "Expected 1 molecule");
            Assert.AreEqual(1, doc.MoleculeTransitionGroupCount, "Expected 1 precursor");
            Assert.AreEqual(20000, doc.MoleculeTransitionCount, "Expected 20000 transitions");
        }
    }
}
