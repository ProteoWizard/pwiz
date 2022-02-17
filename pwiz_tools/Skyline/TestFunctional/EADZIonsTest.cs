using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Testing z+1 and z+2 ions feature.
    /// </summary>
    [TestClass]
    public class EADZIonsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestEADIons()
        {
            Run(@"TestFunctional\EADZIonsTest.zip");
        }

        protected override void DoTest()
        {
            OpenDocument("EADZIonsTest.sky");
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.FragmentTypes = "c, z., z'";
                transitionSettingsUI.IonCount = 5;
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

            ImportResults("FilteredScans\\LITV56_EAD" + ExtensionTestContext.ExtMzml);
            WaitForGraphs();
            FindNode((505.5810).ToString("F4", LocalizationHelper.CurrentCulture) + "+++");

            var graphChrom = SkylineWindow.GraphChromatograms.ToList()[0];
            //Assert.AreEqual(5, graphChrom.GraphPane.CurveList.Count);
        }
    }
}
