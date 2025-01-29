using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PeakImputationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakImputation()
        {
            TestFilesZip = @"TestFunctional\PeakImputationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PeakImputationTest.sky"));
                SkylineWindow.ShowPeakImputation();
            });

            var peakImputationForm = FindOpenForm<PeakImputationForm>();
            Assert.IsNotNull(peakImputationForm);
            PauseTest();
        }
    }
}
