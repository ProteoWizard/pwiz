using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
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
            Assert.AreEqual(0.0, fragmentedMolecule.PrecursorFormula.MonoMassOffset);
            Assert.AreEqual(0.0, fragmentedMolecule.PrecursorFormula.AverageMassOffset);
            var sequenceMassCalc = new SequenceMassCalc(MassType.Monoisotopic);
            var expectedFormula = sequenceMassCalc.GetMolecularFormula(modifiedSequence.GetUnmodifiedSequence());
            Assert.AreEqual(expectedFormula.Count, precursorFormula.Molecule.Count);
            foreach (var entry in expectedFormula)
            {
                Assert.AreEqual(entry.Value, precursorFormula.Molecule.GetElementCount(entry.Key));
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
            foreach (var ionType in FragmentIonTypes)
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
        public void TestGetFragmentFormulaWithNeutralLosses()
        {
            var peptide = new Peptide("PEPTIDE");
            var modPhospho = UniMod.GetModification("Phospho (ST)", true);
            var modPlus100or102 = new StaticMod("Plus100or102", null, null, null, LabelAtoms.None, 100, 102)
                .ChangeLosses(new[]
                {
                    new FragmentLoss("H2O"),
                    new FragmentLoss(null, 10, 11)
                });
            var explicitMods = new ExplicitMods(peptide, new[]
            {
                new ExplicitMod(3, modPhospho),
                new ExplicitMod(1, modPlus100or102)
            }, Array.Empty<TypedExplicitModifications>());
            var transitionGroup = new TransitionGroup(peptide, Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var precursorTransition = new Transition(transitionGroup, IonType.precursor, peptide.Length - 1, 0,
                Adduct.SINGLY_PROTONATED);
            var settings = SrmSettingsList.GetDefault();
            var peptideFragmentedMolecule = FragmentedMolecule.EMPTY.ChangeModifiedSequence(
                ModifiedSequence.GetModifiedSequence(settings, peptide.Sequence, explicitMods, IsotopeLabelType.light)).ChangePrecursorCharge(1);
            foreach (var massType in new[] { MassType.Monoisotopic, MassType.Average })
            {
                double delta = massType == MassType.Monoisotopic ? 1e-3 : 1e-2;
                var sequenceMassCalc = new ExplicitSequenceMassCalc(explicitMods, new SequenceMassCalc(massType), IsotopeLabelType.light);
                var expectedPrecursorMass = sequenceMassCalc.GetPrecursorMass(peptide.Sequence);
                var actualPrecursorMass = GetMass(peptideFragmentedMolecule.PrecursorFormula, massType);
                Assert.AreEqual(expectedPrecursorMass, actualPrecursorMass, delta, "Mass type: {0}", massType);
                var possibleTransitionLosses = new[] { modPhospho, modPlus100or102 }.SelectMany(mod =>
                    mod.Losses.Select(loss => new TransitionLoss(mod, loss, massType))).ToList();
                for (int iCombination = 0; iCombination < 1 << possibleTransitionLosses.Count; iCombination++)
                {
                    var transitionLossList = new List<TransitionLoss>();
                    for (int iLoss = 0; iLoss < possibleTransitionLosses.Count; iLoss++)
                    {
                        if (0 != (iCombination & (1 << iLoss)))
                        {
                            transitionLossList.Add(possibleTransitionLosses[iLoss]);
                        }
                    }
                    var transitionLosses = new TransitionLosses(transitionLossList, massType);
                    var fragmentLosses = transitionLosses.Losses.Select(loss => loss.Loss).ToList();
                    string precursorMessage = string.Format("Mass Type: {0} Losses: {1}", massType,
                        string.Join(",", fragmentLosses));
                    var precursorFragmentedMolecule = peptideFragmentedMolecule.ChangeFragmentIon(IonType.precursor, 0)
                        .ChangeFragmentLosses(transitionLossList.Select(loss=>loss.Loss))
                        .ChangeFragmentCharge(1);
                    var expectedPrecursorMz = sequenceMassCalc.GetFragmentMass(precursorTransition, null) - transitionLosses.Mass;
                    var actualPrecursorMz = GetMass(precursorFragmentedMolecule.FragmentFormula, massType);
                    AssertEx.AreEqual(expectedPrecursorMz, actualPrecursorMz, delta, precursorMessage);
                    foreach (var ionType in FragmentIonTypes)
                    {
                        for (int ionOrdinal = 1; ionOrdinal < peptide.Sequence.Length - 1; ionOrdinal++)
                        {
                            string fragmentMessage = TextUtil.SpaceSeparate(precursorMessage,
                                string.Format("Ion:{0}{1}", ionType, ionOrdinal));
                            var fragmentTransition = new Transition(transitionGroup, ionType,
                                Transition.OrdinalToOffset(ionType, ionOrdinal, peptide.Sequence.Length), 0,
                                Adduct.SINGLY_PROTONATED);
                            var expectedFragmentMz = sequenceMassCalc.GetFragmentMass(fragmentTransition, null) -
                                                     transitionLosses.Mass;
                            var fragmentFragmentedMolecule = peptideFragmentedMolecule
                                .ChangeFragmentIon(ionType, ionOrdinal).ChangeFragmentLosses(fragmentLosses)
                                .ChangeFragmentCharge(1);
                            var actualFragmentMz = GetMass(fragmentFragmentedMolecule.FragmentFormula, massType);
                            AssertEx.AreEqual(expectedFragmentMz, actualFragmentMz, delta, fragmentMessage);
                        }
                    }
                }
            }
        }

        private IEnumerable<IonType> FragmentIonTypes
        {
            get
            {
                return new[]
                    { IonType.a, IonType.b, IonType.c, IonType.x, IonType.y, IonType.z, IonType.zh, IonType.zhh };
            }
        }

        private double GetMass(MoleculeMassOffset moleculeMassOffset, MassType massType)
        {
            if (massType.IsMonoisotopic())
            {
                return FragmentedMolecule.Settings.DEFAULT.GetMonoMass(moleculeMassOffset);
            }

            return FragmentedMolecule.Settings.DEFAULT.GetAverageMass(moleculeMassOffset);
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
                var mzDistribution = fragment.GetFragmentDistribution(FragmentedMolecule.Settings.DEFAULT, null, null, MassType.Monoisotopic);
                var maxAbundance = mzDistribution.Values.Max();
                var monoMz = mzDistribution.First(entry => Equals(entry.Value, maxAbundance)).Key;
                Assert.AreEqual(tuple.Item2, monoMz, .0001);
            }
        }
    }
}
