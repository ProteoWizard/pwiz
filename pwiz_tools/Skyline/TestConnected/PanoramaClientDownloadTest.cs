using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.PanoramaClient;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    public class PanoramaClientDownloadTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPanoramaDownloadFile()
        {

            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestDownloadFile();
        }

        private void TestDownloadFile()
        {
            var remoteDlg = ShowDialog<FilePicker>(() =>
                SkylineWindow.OpenFromPanorama());

            WaitForCondition(9000, () => remoteDlg.IsLoaded);

            RunUI(() =>
            {
                remoteDlg.FolderBrowser.SelectNode("AutoQC");
                remoteDlg.Close();
            });
            WaitForClosedForm(remoteDlg);
        }
    }
}
