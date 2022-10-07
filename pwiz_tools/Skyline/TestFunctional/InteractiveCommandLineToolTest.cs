using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class InteractiveCommandLineToolTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestInteractiveCommandLineTool()
        {
            TestFilesZip = @"TestFunctional\InteractiveCommandLineToolTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, dlg=>
            {
                dlg.RemoveAllTools();
                dlg.OkDialog();
            });
            RunDlg<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg, dlg =>
            {
                dlg.InstallZipTool(TestFilesDir.GetTestPath("TestCommandLineInteractiveTool.zip"));
                dlg.OkDialog();
            });
            RunUI(()=>
            {
                SkylineWindow.PopulateToolsMenu();
                Assert.AreEqual("Delete Selected Node", SkylineWindow.GetToolText(0));
                Assert.AreEqual("Monitor Selection", SkylineWindow.GetToolText(1));
                Assert.AreEqual("Set Note On Selected Node", SkylineWindow.GetToolText(2));
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MultiLabel.sky"));
            });
            IdentityPath idPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 3);
            RunUI(() =>
            {
                SkylineWindow.SelectedPath = idPath;
                SkylineWindow.RunTool(2);
            });
            WaitForCondition(() =>
            {
                var transitionGroup = (TransitionGroupDocNode)SkylineWindow.Document.FindNode(idPath);
                return transitionGroup.Note == "Test Interactive Tool Note";
            });
        }
    }
}
