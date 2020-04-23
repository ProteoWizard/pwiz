using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class FragmentedMoleculeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestGetPrecursorFormula()
        {
            var modifiedSequence = new ModifiedSequence("PEPTIDE", new ModifiedSequence.Modification[0], MassType.Monoisotopic);
            var fragmentedMolecule = FragmentedMolecule.EMPTY.ChangeModifiedSequence(modifiedSequence);
            var precursorFormula = fragmentedMolecule.PrecursorFormula;
            Assert.AreEqual(0, fragmentedMolecule.PrecursorMassShift);
            var sequenceMassCalc = new SequenceMassCalc(MassType.Monoisotopic);
            var expectedFormula = Molecule.Parse(sequenceMassCalc.GetMolecularFormula(modifiedSequence.GetUnmodifiedSequence()));
            Assert.AreEqual(expectedFormula.Count, precursorFormula.Count);
            foreach (var entry in expectedFormula)
            {
                Assert.AreEqual(entry.Value, precursorFormula.GetElementCount(entry.Key));
            }
        }

        [TestMethod]
        public void TestGetFragmentFormula()
        {
            var pepseq = "PEPTIDE";
            var sequenceMassCalc = new SequenceMassCalc(MassType.Monoisotopic);
            var precursor = FragmentedMolecule.EMPTY.ChangeModifiedSequence(
                new ModifiedSequence(pepseq, new ModifiedSequence.Modification[0], MassType.Monoisotopic));
            var peptide = new Peptide(precursor.UnmodifiedSequence);
            var transitionGroup = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var settings = SrmSettingsList.GetDefault();
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, settings,
                ExplicitMods.EMPTY, null, null, null, new TransitionDocNode[0], false);
            foreach (var ionType in new[] {IonType.a, IonType.b, IonType.c, IonType.x, IonType.y, IonType.z})
            {
                for (int ordinal = 1; ordinal < pepseq.Length; ordinal++)
                {
                    var transition = new Transition(transitionGroup, ionType, Transition.OrdinalToOffset(ionType, ordinal, pepseq.Length), 0, Adduct.SINGLY_PROTONATED);
                    var fragment = precursor.ChangeFragmentIon(ionType, ordinal);
                    var actualMassDistribution = FragmentedMolecule.Settings.DEFAULT.GetMassDistribution(
                        fragment.FragmentFormula, 0, 0);
                    var expectedMz = sequenceMassCalc.GetFragmentMass(transition, transitionGroupDocNode.IsotopeDist);
                    if (expectedMz.IsMassH())
                    {
                        expectedMz = new TypedMass(expectedMz.Value - BioMassCalc.MassProton, expectedMz.MassType & ~MassType.bMassH);
                    }
                    var actualMz = actualMassDistribution.MostAbundanceMass;
                    if (Math.Abs(expectedMz - actualMz) > .001)
                    {
                        Assert.AreEqual(expectedMz, actualMz, .001, "Ion type {0} Ordinal {1}", ionType, ordinal);
                    }
                }
            }
        }
    }
}
