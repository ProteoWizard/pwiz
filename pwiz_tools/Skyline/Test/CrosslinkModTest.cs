using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
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

        [TestMethod]
        public void TestCrosslinkGetNeutralFormula()
        {
            var mainPeptide = new Peptide("MERCURY");
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var mainTransitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings,
                ExplicitMods.EMPTY, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var unlinkedFormula = mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, ExplicitMods.EMPTY);
            Assert.AreEqual("C39H64N14O12S2Se", unlinkedFormula.ToString());

            var crosslinkerDef = new CrosslinkerDef("disulfide", new FormulaMass("-H2"));
            var linkedPeptide = new LinkedPeptide(new Peptide("ARSENIC"), 2, ExplicitMods.EMPTY);

            var linkedPeptideFormula = linkedPeptide.GetNeutralFormula(srmSettings, IsotopeLabelType.light);
            Assert.AreEqual("C32H56N12O13S", linkedPeptideFormula.ToString());
            var crosslinkMod = new CrosslinkMod(6, crosslinkerDef, new[] { linkedPeptide });

            var explicitModsWithCrosslink = ExplicitMods.EMPTY.ChangeCrosslinkMods(new[] { crosslinkMod });
            var crosslinkedFormula =
                mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, explicitModsWithCrosslink);
            
            Assert.AreEqual("C71H118N26O25S3Se", crosslinkedFormula.Molecule.ToString());
        }

        [TestMethod]
        public void TestComplexIonGetNeutralFormula()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var fullTransitionGroup = new TransitionGroupDocNode(
                new TransitionGroup(new Peptide("ELVIS"), Adduct.SINGLY_PROTONATED, IsotopeLabelType.light), 
                Annotations.EMPTY, srmSettings, ExplicitMods.EMPTY, null, ExplicitTransitionGroupValues.EMPTY, 
                null, null, false);
            var fullFormula = fullTransitionGroup.GetNeutralFormula(srmSettings, ExplicitMods.EMPTY);
            Assert.AreEqual("C25H45N5O9", fullFormula.Molecule.ToString());

            var hydrolysisDef = new CrosslinkerDef("hydrolysis", new FormulaMass("-H2O"));
            var crossLinkMod = new CrosslinkMod(1, hydrolysisDef, new []{new LinkedPeptide(new Peptide("VIS"), 0, ExplicitMods.EMPTY), });
            var explicitMods = ExplicitMods.EMPTY.ChangeCrosslinkMods(new[] {crossLinkMod});
            var mainTransitionGroup = new TransitionGroupDocNode(
                new TransitionGroup(new Peptide("EL"), Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
                Annotations.EMPTY, srmSettings, 
                explicitMods, null,
                ExplicitTransitionGroupValues.EMPTY,
                null, null, false);
            var mainFullFormula = mainTransitionGroup.GetNeutralFormula(srmSettings, explicitMods);
            Assert.AreEqual(fullFormula, mainFullFormula);
        }

        [TestMethod]
        public void TestPermuteComplexIons()
        {
            var mainPeptide = new Peptide("MERCURY");
            var srmSettings = SrmSettingsList.GetDefault();
            var transitionFilter = srmSettings.TransitionSettings.Filter;
            transitionFilter = transitionFilter
                .ChangeFragmentRangeFirstName(TransitionFilter.StartFragmentFinder.ION_1.Name)
                .ChangeFragmentRangeLastName(@"last ion")
                .ChangePeptideIonTypes(new[]{IonType.precursor,IonType.y});
            srmSettings =  srmSettings.ChangeTransitionSettings(
                srmSettings.TransitionSettings.ChangeFilter(transitionFilter));

            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var crosslinkerDef = new CrosslinkerDef("disulfide", new FormulaMass("-H2"));
            var linkedPeptide = new LinkedPeptide(new Peptide("ARSENIC"), 2, ExplicitMods.EMPTY);
            var crosslinkMod = new CrosslinkMod(3, crosslinkerDef, new[] { linkedPeptide });
            var explicitModsWithCrosslink = ExplicitMods.EMPTY.ChangeCrosslinkMods(new[] { crosslinkMod });
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings,
                explicitModsWithCrosslink, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var choices = transitionGroupDocNode.GetPrecursorChoices(srmSettings, explicitModsWithCrosslink, true)
                .Cast<TransitionDocNode>().ToArray();
            var complexFragmentIons = choices.Select(transition => transition.ComplexFragmentIon).ToArray();

            Assert.AreNotEqual(0, complexFragmentIons.Length);
        }
    }
}
