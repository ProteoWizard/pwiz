using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CrosslinkModTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTransitionGroupDocNodeGetNeutralFormula()
        {
            var peptide = new Peptide("PEPTIDE");
            var transitionGroup = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings, ExplicitMods.EMPTY, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var moleculeOffset = transitionGroupDocNode.GetNeutralFormula(srmSettings, ExplicitMods.EMPTY);
            Assert.AreEqual("C34H53N7O15", moleculeOffset.Molecule.ToString());
        }
    }
}
