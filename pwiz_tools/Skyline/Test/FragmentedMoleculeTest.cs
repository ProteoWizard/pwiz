using System;
using System.Linq;
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
            var expectedFormula = sequenceMassCalc.GetMolecularFormula(modifiedSequence.GetUnmodifiedSequence());
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
                        expectedMz = TypedMass.Create(expectedMz.Value - BioMassCalc.MassProton, expectedMz.MassType & ~MassType.bMassH);
                    }
                    var actualMz = actualMassDistribution.MostAbundanceMass;
                    if (Math.Abs(expectedMz - actualMz) > .001)
                    {
                        Assert.AreEqual(expectedMz, actualMz, .001, "Ion type {0} Ordinal {1}", ionType, ordinal);
                    }
                }
            }
        }

        [TestMethod]
        public void TestGetFragmentMz()
        {
            var chargeTwoOrdinal4Mzs = new[]
            {
                Tuple.Create(IonType.a, 199.1077),
                Tuple.Create(IonType.b, 213.1052),
                Tuple.Create(IonType.c, 221.6185),
                Tuple.Create(IonType.x, 252.1028),
                Tuple.Create(IonType.y, 239.1132),
                Tuple.Create(IonType.z, 230.5999)
            };
            var precursor = FragmentedMolecule.EMPTY.ChangeModifiedSequence(new ModifiedSequence("PEPTIDE",
                new ModifiedSequence.Modification[0], MassType.Monoisotopic));
            foreach (var tuple in chargeTwoOrdinal4Mzs)
            {
                var fragment = precursor.ChangeFragmentCharge(2)
                    .ChangeFragmentIon(tuple.Item1, 4);
                var mzDistribution = fragment.GetFragmentDistribution(FragmentedMolecule.Settings.DEFAULT, null, null);
                var maxAbundance = mzDistribution.Values.Max();
                var monoMz = mzDistribution.First(entry => Equals(entry.Value, maxAbundance)).Key;
                Assert.AreEqual(tuple.Item2, monoMz, .0001);
            }
        }
    }
}
