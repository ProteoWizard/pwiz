using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SpectrumFilterTransitionListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectrumFilterTransitionList()
        {
            TestFilesZip = @"TestFunctional\SpectrumFilterTransitionListTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SpectrumFilterTransitionListTest.sky"));

            });
            RunDlg<ImportTransitionListColumnSelectDlg>(
                ()=>SkylineWindow.ImportMassList(TestFilesDir.GetTestPath("TransitionList.csv")),
                dlg =>
                {
                    dlg.OkDialog();
                });
            Assert.AreEqual(14, SkylineWindow.Document.PeptideCount);
            Assert.AreNotEqual(1, SkylineWindow.Document.Peptides.First().TransitionGroupCount);
        }
    }
}
