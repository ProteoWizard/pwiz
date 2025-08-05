using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DissociationMethodTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDissociationMethod()
        {
            TestFilesZip = @"TestFunctional\DissociationMethodTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DissociationMethodTest.sky"));
            });
            ImportResultsFile(TestFilesDir.GetTestPath("DissociationMethodTest.mzML"));
            PauseTest();
        }
    }
}
