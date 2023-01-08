using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Spectra;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SpectraGridFormTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectraGridForm()
        {
            TestFilesZip = @"TestFunctional\SpectraGridFormTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var spectraGridForm = ShowDialog<SpectraGridForm>(SkylineWindow.ViewMenu.ShowSpectraGridForm);
            RunUI(() =>
            {
                spectraGridForm.AddFile(new MsDataFilePath(TestFilesDir.GetTestPath("SpectrumClassFilterTest.mzML")));
            });
            WaitForConditionUI(spectraGridForm.IsComplete);
            Assert.AreEqual(3, spectraGridForm.DataGridView.RowCount);
            OkDialog(spectraGridForm, spectraGridForm.Close);
        }
    }
}
