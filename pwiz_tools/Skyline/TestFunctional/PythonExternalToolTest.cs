using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PythonExternalToolTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPythonExternalTool()
        {
            TestFilesZip = @"TestFunctional\PythonExternalToolTest.data";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ThreeReplicates.sky")));
            var toolDlg = ShowDialog<ConfigureToolsDlg>(SkylineWindow.ShowConfigureToolsDlg);
            RunLongDlg<AlertDlg>(()=> toolDlg.InstallZipTool(TestFilesDir.GetTestPath("SkylinePRISM.zip")), alertDlg =>
            {
                Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, alertDlg.Message);
                OkDialog(alertDlg, alertDlg.OkDialog);
                // alertDlg = WaitForOpenForm<AlertDlg>();
                // Assert.AreEqual(ToolsUIResources.PythonInstaller_OkDialog_Successfully_set_up_Python_virtual_environment, alertDlg.Message);
                // OkDialog(alertDlg, alertDlg.OkDialog);
            }, _ =>{});
            OkDialog(toolDlg, toolDlg.OkDialog);
            RunUI(()=>SkylineWindow.PopulateToolsMenu());
            var toolMenuItem = FindToolMenuItem("Run PRISM Analysis");
            Assert.IsNotNull(toolMenuItem);
            RunUI(()=>toolMenuItem.PerformClick());
        }

        private SkylineWindow.ToolMenuItem FindToolMenuItem(string text)
        {
            for (int i = 0;; i++)
            {
                var toolMenuItem = SkylineWindow.GetToolMenuItem(i);
                if (toolMenuItem.Title == text)
                {
                    return toolMenuItem;
                }
            }
        }
    }
}
