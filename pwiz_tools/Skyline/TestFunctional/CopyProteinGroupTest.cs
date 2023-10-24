using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CopyProteinGroupTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestProteinGroupCopy()
        {
            TestFilesZip = @"TestFunctional\CopyProteinGroupTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            SetClipboardText("ELVIS");
            RunUI(()=>SkylineWindow.Paste());
            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(SkylineWindow.ShowAssociateProteinsDlg);
            RunUI(()=>
            {
                associateProteinsDlg.FastaFileName = TestFilesDir.GetTestPath("elvis.fasta");
                associateProteinsDlg.GroupProteins = true;
            });
            WaitForConditionUI(() => associateProteinsDlg.IsOkEnabled);
            OkDialog(associateProteinsDlg, associateProteinsDlg.OkDialog);
            RunUI(() =>
            {
                SkylineWindow.SelectAll();
                SkylineWindow.Copy();
                SkylineWindow.EditDelete();
                SkylineWindow.Paste();
            });
        }
    }
}
