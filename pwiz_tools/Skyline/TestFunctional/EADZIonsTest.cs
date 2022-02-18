using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
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
            WaitForGraphs();
            var libMatch = SkylineWindow.GraphSpectrum.DisplayedSpectrum;
            ImportResults("FilteredScans\\LITV56_EAD" + ExtensionTestContext.ExtMzml);
            WaitForGraphs();
            FindNode((505.5810).ToString("F4", LocalizationHelper.CurrentCulture) + "+++");

            var testIons = new[]{
                new {type = IonType.zh, offset = 8},
                new {type = IonType.zhh, offset = 5},
                new {type = IonType.zhh, offset = 5}
            };

            TransitionGroupDocNode selectedPrecursor = null;
            RunUI(() =>
            {
                selectedPrecursor = (SkylineWindow.SelectedNode as TransitionGroupTreeNode)?.DocNode;
            });
            Assert.IsNotNull(selectedPrecursor);
            var graphChrom = SkylineWindow.GraphChromatograms.ToList()[0];
            ClickChromatogram(6.46, 2600.0);

            foreach (var testIon in testIons)
            {
                var testNode = new Transition(selectedPrecursor.Id as TransitionGroup, testIon.type, testIon.offset, 1, Adduct.M_PLUS);
                //check for the peaks in the library spectrum
                Assert.IsTrue( libMatch.PeaksMatched.Any(peak =>
                    peak.MatchedIons[0].IonType.Equals(testNode.IonType) &&
                    peak.MatchedIons[0].Ordinal.Equals(testNode.Ordinal)));
                //check for the transitions in the tree
                Assert.IsTrue(selectedPrecursor.Children.Any(t => (t is TransitionDocNode trans) &&
                    trans.Transition.GetFragmentIonName(CultureInfo.CurrentCulture).StartsWith(testNode.FragmentIonName)));
                //check for the chromatograms
                Assert.IsTrue(graphChrom.GraphPane.CurveList.Any(curve => curve.Label.Text.StartsWith(testNode.FragmentIonName)));
                //check that the ions are present in the full scan viewer.
                Assert.IsTrue(SkylineWindow.GraphFullScan.ZedGraphControl.GraphPane.CurveList.Any(c =>
                    c.Label.Text.StartsWith(testNode.FragmentIonName)));
            }
        }
    }
}
