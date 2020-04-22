using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
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
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings, null, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var moleculeOffset = transitionGroupDocNode.GetNeutralFormula(srmSettings, null);
            Assert.AreEqual("C34H53N7O15", moleculeOffset.Molecule.ToString());
        }

        [TestMethod]
        public void TestCrosslinkGetNeutralFormula()
        {
            var mainPeptide = new Peptide("MERCURY");
            var srmSettings = SrmSettingsList.GetDefault();
            srmSettings = srmSettings.ChangePeptideSettings(
                srmSettings.PeptideSettings.ChangeModifications(srmSettings.PeptideSettings.Modifications
                    .ChangeStaticModifications(new StaticMod[0])));
            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var mainTransitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings,
                null, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var unlinkedFormula = mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, null);
            Assert.AreEqual("C37H61N13O11S2Se", unlinkedFormula.ToString());

            var linkedPeptide = new LinkedPeptide(new Peptide("ARSENIC"), 2, null);

            var linkedPeptideFormula = linkedPeptide.GetNeutralFormula(srmSettings, IsotopeLabelType.light);
            Assert.AreEqual("C30H53N11O12S", linkedPeptideFormula.ToString());
            var crosslinkMod = new StaticMod("disulfide", null, null, "-H2");


            var explicitModsWithCrosslink = new ExplicitMods(mainPeptide,
                new[] {new ExplicitMod(3, crosslinkMod).ChangeLinkedPeptide(linkedPeptide)},
                new TypedExplicitModifications[0]);
            var crosslinkedFormula =
                mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, explicitModsWithCrosslink);
            
            Assert.AreEqual("C67H112N24O23S3Se", crosslinkedFormula.Molecule.ToString());
        }

        [TestMethod]
        public void TestSingleAminoAcidLinkedPeptide()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var mainPeptide = new Peptide("A");
            var staticMod = new StaticMod("crosslinker", null, null, "-C2");
            var linkedPeptide = new LinkedPeptide(new Peptide("D"), 0, null);
            var mainTransitionGroup = new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var mainTransitionGroupDocNode = new TransitionGroupDocNode(mainTransitionGroup, 
                Annotations.EMPTY, srmSettings, 
                null, (SpectrumHeaderInfo) null,
                ExplicitTransitionGroupValues.EMPTY, null, new TransitionDocNode[0], false);
            var modsWithoutLinkedPeptide = new ExplicitMods(mainPeptide, new[]{new ExplicitMod(0, staticMod), }, new TypedExplicitModifications[0]);
            Assert.AreEqual("C3H7NO2", AminoAcidFormulas.Default.GetFormula("A").ToString());
            Assert.AreEqual("C3H7NO2", mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, null).Molecule.ToString());
            Assert.AreEqual("CH7NO2", mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, modsWithoutLinkedPeptide).Molecule.ToString());
            Assert.AreEqual("C4H7NO4", AminoAcidFormulas.Default.GetFormula("D").ToString());
            var modsWithLinkedPeptide = new ExplicitMods(mainPeptide,
                new[] {new ExplicitMod(0, staticMod).ChangeLinkedPeptide(linkedPeptide)},
                new TypedExplicitModifications[0]);
            Assert.AreEqual("C5H14N2O6", mainTransitionGroupDocNode.GetNeutralFormula(srmSettings, modsWithLinkedPeptide).Molecule.ToString());

        }

        [TestMethod]
        public void TestTwoAminoAcidLinkedPeptide()
        {
            const string modName = "crosslinker";
            var srmSettings = SrmSettingsList.GetDefault();
            srmSettings = srmSettings.ChangeTransitionSettings(srmSettings.TransitionSettings.ChangeFilter(
                srmSettings.TransitionSettings.Filter
                    .ChangeFragmentRangeFirstName(TransitionFilter.StartFragmentFinder.ION_1.Name)
                    .ChangeFragmentRangeLastName(@"last ion")
                    .ChangePeptideIonTypes(new[] {IonType.precursor, IonType.y, IonType.b})
            ));


            var mainPeptide = new Peptide("AD");
            var staticMod = new StaticMod(modName, null, null, "-C2");
            var linkedPeptide = new LinkedPeptide(new Peptide("EF"), 1, null);
            var mainTransitionGroup = new TransitionGroup(mainPeptide, Adduct.DOUBLY_PROTONATED, IsotopeLabelType.light);
            var mainTransitionGroupDocNode = new TransitionGroupDocNode(mainTransitionGroup,
                Annotations.EMPTY, srmSettings, null, null, ExplicitTransitionGroupValues.EMPTY, null,
                new TransitionDocNode[0], false);
            var modsWithLinkedPeptide = new ExplicitMods(mainPeptide,
                new[] {new ExplicitMod(0, staticMod).ChangeLinkedPeptide(linkedPeptide)},
                new TypedExplicitModifications[0]);
            Assert.AreEqual(1, srmSettings.PeptideSettings.Modifications.MaxNeutralLosses);
            var oneNeutralLossChoices = mainTransitionGroupDocNode.GetTransitions(
                srmSettings,
                modsWithLinkedPeptide,
                mainTransitionGroupDocNode.PrecursorMz,
                mainTransitionGroupDocNode.IsotopeDist,
                null,
                null,
                true).Select(transition => transition.ComplexFragmentIon.GetName()).ToList();
            var modSite = new ModificationSite(0, modName);
            var expectedFragmentIons = new[]
            {
                ComplexFragmentIonName.PRECURSOR.AddChild(modSite, ComplexFragmentIonName.PRECURSOR),
                ComplexFragmentIonName.PRECURSOR.AddChild(modSite, new ComplexFragmentIonName(IonType.y, 1)),
                new ComplexFragmentIonName(IonType.y, 1), 
                new ComplexFragmentIonName(IonType.b, 1).AddChild(modSite, new ComplexFragmentIonName(IonType.precursor, 0)), 
                ComplexFragmentIonName.ORPHAN.AddChild(modSite, new ComplexFragmentIonName(IonType.b, 1)),
            };
            CollectionAssert.AreEquivalent(expectedFragmentIons, oneNeutralLossChoices);
        }

        private SrmSettings SetMaxNeutralLossCount(SrmSettings settings, int count)
        {
            return settings.ChangePeptideSettings(
                settings.PeptideSettings.ChangeModifications(
                    settings.PeptideSettings.Modifications.ChangeMaxNeutralLosses(count)));
        }

        [TestMethod]
        public void TestComplexIonGetNeutralFormula()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var fullTransitionGroup = new TransitionGroupDocNode(
                new TransitionGroup(new Peptide("ELVIS"), Adduct.SINGLY_PROTONATED, IsotopeLabelType.light), 
                Annotations.EMPTY, srmSettings, null, null, ExplicitTransitionGroupValues.EMPTY, 
                null, null, false);
            var fullFormula = fullTransitionGroup.GetNeutralFormula(srmSettings, null);
            Assert.AreEqual("C25H45N5O9", fullFormula.Molecule.ToString());

            var hydrolysisDef = new StaticMod("hydrolysis", null, ModTerminus.C, "-H2O")
                .ChangeCrosslinkerSettings(CrosslinkerSettings.EMPTY);
            var crossLinkMod = new ExplicitMod(1, hydrolysisDef)
                .ChangeLinkedPeptide(new LinkedPeptide(new Peptide("VIS"), 0, null));
            var mainPeptide = new Peptide("EL");
            var explicitMods = new ExplicitMods(mainPeptide, new[] { crossLinkMod }, new TypedExplicitModifications[0]);
            var mainTransitionGroup = new TransitionGroupDocNode(
                new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light),
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
                .ChangePeptideIonTypes(new[]{IonType.precursor,IonType.y, IonType.b});
            srmSettings =  srmSettings.ChangeTransitionSettings(
                srmSettings.TransitionSettings.ChangeFilter(transitionFilter));

            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var crosslinkerDef = new StaticMod("disulfide", "C", null, "-H2");
            var linkedPeptide = new LinkedPeptide(new Peptide("ARSENIC"), 6, null);
            var crosslinkMod = new ExplicitMod(3, crosslinkerDef).ChangeLinkedPeptide(linkedPeptide);
            var explicitModsWithCrosslink = new ExplicitMods(mainPeptide, new[]{crosslinkMod}, new TypedExplicitModifications[0]);
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, srmSettings,
                explicitModsWithCrosslink, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            var choices = transitionGroupDocNode.GetPrecursorChoices(srmSettings, explicitModsWithCrosslink, true)
                .Cast<TransitionDocNode>().ToArray();
            var complexFragmentIons = choices.Select(transition => transition.ComplexFragmentIon.GetName()).ToArray();

            Assert.AreNotEqual(0, complexFragmentIons.Length);
        }
        [TestMethod]
        public void TestCrosslinkModSerialization()
        {
            var settings = SrmSettingsList.GetDefault();
            var crosslinkerDef = new StaticMod("disulfide", null, null, "-H2")
                .ChangeCrosslinkerSettings(CrosslinkerSettings.EMPTY);
            settings = settings.ChangePeptideSettings(settings.PeptideSettings.ChangeModifications(
                settings.PeptideSettings.Modifications.ChangeStaticModifications(new[] {crosslinkerDef})));
            settings = settings.ChangeTransitionSettings(settings.TransitionSettings.ChangeFilter(
                settings.TransitionSettings.Filter
                    .ChangeFragmentRangeFirstName(TransitionFilter.StartFragmentFinder.ION_1.Name)
                    .ChangeFragmentRangeLastName(@"last ion")
                    .ChangePeptideIonTypes(new[] { IonType.precursor, IonType.y, IonType.b })
            )); var mainPeptide = new Peptide("MERCURY");
            var transitionGroup = new TransitionGroup(mainPeptide, Adduct.DOUBLY_PROTONATED, IsotopeLabelType.light);
            var linkedPeptide = new LinkedPeptide(new Peptide("ARSENIC"), 2, null);
            var crosslinkMod = new ExplicitMod(3, crosslinkerDef).ChangeLinkedPeptide(linkedPeptide);
            var explicitModsWithCrosslink = new ExplicitMods(mainPeptide, new[]{crosslinkMod}, new TypedExplicitModifications[0]);
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, settings,
                explicitModsWithCrosslink, null, ExplicitTransitionGroupValues.EMPTY, null, null, true);
            
            var peptideDocNode = new PeptideDocNode(mainPeptide, settings, explicitModsWithCrosslink, null, ExplicitRetentionTimeInfo.EMPTY, new []{transitionGroupDocNode}, false);
            peptideDocNode = peptideDocNode.ChangeSettings(settings, SrmSettingsDiff.ALL, true);
            Assert.AreNotEqual(0, peptideDocNode.TransitionCount);
            var peptideGroupDocNode = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY, "Peptides", null, new []{peptideDocNode});
            var srmDocument = (SrmDocument) new SrmDocument(settings).ChangeChildren(new[] {peptideGroupDocNode});
            AssertEx.Serializable(srmDocument);
            String docXML = null;
            AssertEx.RoundTrip(srmDocument, ref docXML);
            Console.Out.WriteLine(docXML);
        }

        [TestMethod]
        public void TestIncludesAaIndex()
        {
            var peptide = new Peptide("AD");
            var transitionGroup = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var precursor =
                new ComplexFragmentIon(new Transition(transitionGroup, IonType.precursor, peptide.Length - 1, 0, Adduct.SINGLY_PROTONATED), null);
            Assert.IsTrue(precursor.IncludesAaIndex(0));
            Assert.IsTrue(precursor.IncludesAaIndex(1));
            var y1 = new ComplexFragmentIon(new Transition(transitionGroup, IonType.y, Transition.OrdinalToOffset(IonType.y, 1, peptide.Length), 0, Adduct.SINGLY_PROTONATED), null);
            Assert.AreEqual(1, y1.Transition.Ordinal);
            Assert.IsFalse(y1.IncludesAaIndex(0));
            Assert.IsTrue(y1.IncludesAaIndex(1));
            var b1 = new ComplexFragmentIon(new Transition(transitionGroup, IonType.b, Transition.OrdinalToOffset(IonType.b, 1, peptide.Length), 0, Adduct.SINGLY_PROTONATED), null);
            Assert.AreEqual(1, b1.Transition.Ordinal);
            Assert.IsTrue(b1.IncludesAaIndex(0));
            Assert.IsFalse(b1.IncludesAaIndex(1));
        }
    }
}
