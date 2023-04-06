/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class FragmentedMolecule : Immutable
    {
        public static readonly FragmentedMolecule EMPTY = new FragmentedMolecule();
        private static MoleculeMassOffset H2O = ParsedMoleculeMassOffset.Create(@"H2O");

        private FragmentedMolecule()
        {
            PrecursorFormula = FragmentFormula = MoleculeMassOffset.EMPTY;
            FragmentIonType = IonType.custom;
            FragmentOrdinal = 0;
            FragmentLosses = ImmutableList<FragmentLoss>.EMPTY;
        }
        public ModifiedSequence ModifiedSequence { get; private set; }

        public string UnmodifiedSequence
        {
            get { return ModifiedSequence == null ? null : ModifiedSequence.GetUnmodifiedSequence(); }
        }

        public FragmentedMolecule ChangeModifiedSequence(ModifiedSequence modifiedSequence)
        {
            return ChangeMoleculeProp(im => im.ModifiedSequence = modifiedSequence);
        }

        public int PrecursorCharge { get; private set; }

        public FragmentedMolecule ChangePrecursorCharge(int precursorCharge)
        {
            return ChangeMoleculeProp(im => im.PrecursorCharge = precursorCharge);
        }

        private FragmentedMolecule ChangeMoleculeProp(Action<FragmentedMolecule> action)
        {
            return ChangeProp(ImClone(this), im =>
            {
                action(im);
                im.UpdateFormulas();
            });
        }
        
        public MoleculeMassOffset PrecursorFormula { get; private set; }

        public FragmentedMolecule ChangePrecursorFormula(MoleculeMassOffset precursorFormula)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.PrecursorFormula = precursorFormula;
                im.ModifiedSequence = null;
            });
        }

        public MassType PrecursorMassType { get; private set; }

        public IonType FragmentIonType { get; private set; }

        public FragmentedMolecule ChangeFragmentIon(IonType ionType, int ordinal)
        {
            return ChangeMoleculeProp(im =>
            {
                im.FragmentIonType = ionType;
                im.FragmentOrdinal = ordinal;
            });
        }
        public int FragmentOrdinal { get; private set; }
        public ImmutableList<FragmentLoss> FragmentLosses { get; private set; }

        public FragmentedMolecule ChangeFragmentLosses(IEnumerable<FragmentLoss> losses)
        {
            return ChangeMoleculeProp(im =>
            {
                im.FragmentLosses = ImmutableList.ValueOfOrEmpty(losses);
            });
        }

        public MoleculeMassOffset FragmentFormula { get; private set; }

        public FragmentedMolecule ChangeFragmentFormula(MoleculeMassOffset fragmentFormula)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.FragmentFormula = fragmentFormula;
                im.FragmentIonType = IonType.custom;
                im.FragmentOrdinal = 0;
            });
        }
        public int FragmentCharge { get; private set; }

        public FragmentedMolecule ChangeFragmentCharge(int charge)
        {
            return ChangeMoleculeProp(im => im.FragmentCharge = charge);
        }
        public MassType FragmentMassType { get; private set; }

        private void UpdateFormulas()
        {
            if (ModifiedSequence == null)
            {
                return;
            }
            var precursorNeutralFormula = MoleculeMassOffset.Sum(
                GetSequenceFormula(ModifiedSequence).Append(H2O));
            PrecursorFormula = precursorNeutralFormula.Plus(FormulaForCharge(PrecursorCharge));
            if (FragmentIonType == IonType.custom)
            {
                return;
            }

            ModifiedSequence fragmentSequence;
            if (FragmentIonType == IonType.precursor)
            {
                FragmentOrdinal = UnmodifiedSequence.Length;
                fragmentSequence = ModifiedSequence;
            }
            else
            {
                FragmentOrdinal = Math.Max(1, Math.Min(UnmodifiedSequence.Length, FragmentOrdinal));
                fragmentSequence = GetFragmentSequence(ModifiedSequence, FragmentIonType, FragmentOrdinal);
            }

            if (FragmentIonType == IonType.precursor && FragmentLosses.Count == 0)
            {
                FragmentFormula = precursorNeutralFormula.Plus(FormulaForCharge(FragmentCharge));
            }
            else
            {
                FragmentFormula = MoleculeMassOffset.Sum(
                    GetSequenceFormula(fragmentSequence)
                        .Concat(FragmentLosses.Select(LossAsMoleculeMassOffset))
                        .Append(FormulaDiffForIonType(FragmentIonType))
                        .Append(FormulaForCharge(FragmentCharge)));
            }
        }

        public IDictionary<double, double> GetFragmentDistribution(Settings settings, double? precursorMinMz, double? precursorMaxMz, MassType massType)
        {
            var fragmentDistribution = settings.GetMassDistribution(FragmentFormula, massType, FragmentCharge);
            var otherFragmentFormula = GetComplementaryProductFormula();
            var otherFragmentDistribution = settings.GetMassDistribution(otherFragmentFormula, massType, PrecursorCharge);
            var result = new Dictionary<double, double>();
            foreach (var entry in fragmentDistribution)
            {
                var fragmentPrecursorMz = entry.Key * FragmentCharge / PrecursorCharge;
                double? minOtherMz = precursorMinMz - fragmentPrecursorMz;
                double? maxOtherMz = precursorMaxMz - fragmentPrecursorMz;
                var otherFragmentAbundance = otherFragmentDistribution
                    .Where(oFrag => !minOtherMz.HasValue || oFrag.Key >= minOtherMz
                                    && !maxOtherMz.HasValue || oFrag.Key <= maxOtherMz).Sum(frag => frag.Value);
                if (otherFragmentAbundance > 0)
                {
                    result.Add(entry.Key, otherFragmentAbundance * entry.Value);
                }
            }
            return result;
        }

        public MoleculeMassOffset GetComplementaryProductFormula()
        {
            var result = PrecursorFormula.Difference(FragmentFormula);
            var negative = result.Molecule.FirstOrDefault(entry => entry.Value < 0);
            if (null != negative.Key)
            {
                string message = string.Format(
                    @"Unable to calculate expected distribution because the fragment contains more '{0}' atoms than the precursor.",
                    negative.Key);
                throw new InvalidOperationException(message);
            }
            return result;
        }

        private static IEnumerable<MoleculeMassOffset> GetSequenceFormula(ModifiedSequence modifiedSequence)
        {
            string unmodifiedSequence = modifiedSequence.GetUnmodifiedSequence();
            var modifications = modifiedSequence.GetModifications().ToLookup(mod => mod.IndexAA);
            for (int i = 0; i < unmodifiedSequence.Length; i++)
            {
                char aminoAcid = unmodifiedSequence[i];
                yield return AminoAcidFormula(aminoAcid);
                foreach (var mod in modifications[i])
                {
                    var formula = mod.Formula;
                    if (formula == null)
                    {
                        var staticMod = mod.StaticMod;
                        var aa = unmodifiedSequence[i];
                        if ((staticMod.LabelAtoms & LabelAtoms.LabelsAA) != LabelAtoms.None && AminoAcid.IsAA(aa))
                        {
                            yield return SequenceMassCalc.GetHeavyFormula(aa, staticMod.LabelAtoms);
                        }
                    }
                    if (formula != null)
                    {
                        yield return formula;
                    }
                    else
                    {
                        yield return MoleculeMassOffset.Create(null, mod.MonoisotopicMass, mod.AverageMass);
                    }
                }
            }
        }

        private static MoleculeMassOffset AminoAcidFormula(char aminoAcid)
        {
            var formula = AminoAcidFormulas.Default.GetAminoAcidFormula(aminoAcid);
            if (null != formula)
            {
                return MoleculeMassOffset.Create(formula, 0, 0);
            }

            // Must be a nonstandard amino acid such as 'B' or 'J'.
            return MoleculeMassOffset.Create(null, SrmSettings.MonoisotopicMassCalc.GetAAMass(aminoAcid),
                SrmSettings.AverageMassCalc.GetAAMass(aminoAcid));
        }

        private static void Add(Dictionary<string, int> dict, Molecule molecule)
        {
            foreach (var item in molecule)
            {
                int count;
                if (dict.TryGetValue(item.Key, out count))
                {
                    dict[item.Key] = count + item.Value;
                }
                else
                {
                    dict[item.Key] = item.Value;
                }
            }
        }

        private static MoleculeMassOffset FormulaForCharge(int charge)
        {
            if (charge <= 0)
            {
                return MoleculeMassOffset.EMPTY;
            }

            return MoleculeMassOffset.EMPTY.SetElementCount(@"H", charge);
        }

        public static ModifiedSequence GetFragmentSequence(ModifiedSequence modifiedSequence, IonType ionType,
            int ordinal)
        {
            string unmodifiedSequence = modifiedSequence.GetUnmodifiedSequence();
            switch (ionType)
            {
                case IonType.a:
                case IonType.b:
                case IonType.c:
                    return new ModifiedSequence(unmodifiedSequence.Substring(0, ordinal),
                        modifiedSequence.GetModifications().Where(mod => mod.IndexAA < ordinal),
                        MassType.Monoisotopic);
            }

            int offset = unmodifiedSequence.Length - ordinal;
            string fragmentSequence = unmodifiedSequence.Substring(offset);
            var newModifications = modifiedSequence.GetModifications()
                .Where(mod => mod.IndexAA >= offset)
                .Select(mod => mod.ChangeIndexAa(mod.IndexAA - offset));

            return new ModifiedSequence(fragmentSequence, newModifications, MassType.Monoisotopic);
        }

        private static Dictionary<IonType, MoleculeMassOffset> _ionTypeMolecules = new Dictionary<IonType, MoleculeMassOffset>()
        {
            { IonType.precursor, ParsedMoleculeMassOffset.Create(@"H2O") },
            { IonType.a, ParsedMoleculeMassOffset.Create(@"-CO") },
            { IonType.b, MoleculeMassOffset.EMPTY },
            { IonType.c, ParsedMoleculeMassOffset.Create(@"H3N") },
            { IonType.x, ParsedMoleculeMassOffset.Create(@"CO2") },
            { IonType.y, ParsedMoleculeMassOffset.Create(@"H2O") },
            { IonType.z, ParsedMoleculeMassOffset.Create(@"O-HN") },
            { IonType.zh, ParsedMoleculeMassOffset.Create(@"O-N") },
            { IonType.zhh, ParsedMoleculeMassOffset.Create(@"OH-N") }
        };


        public static MoleculeMassOffset FormulaDiffForIonType(IonType ionType)
        {
            return _ionTypeMolecules[ionType];
        }

        public class Settings : Immutable
        {
            public static readonly Settings DEFAULT = new Settings().ChangeMassResolution(.001).ChangeMinAbundance(.00001)
                .ChangeIsotopeAbundances(IsotopeEnrichmentsList.DEFAULT.IsotopeAbundances);

            public static Settings FromSrmSettings(SrmSettings srmSettings)
            {
                return DEFAULT.ChangeIsotopeAbundances(srmSettings.TransitionSettings.FullScan.IsotopeAbundances);
            }
            public double MassResolution { get; private set; }

            public Settings ChangeMassResolution(double massResolution)
            {
                return ChangeProp(ImClone(this), im => im.MassResolution = massResolution);
            }
            public double MinAbundance { get; private set; }

            public Settings ChangeMinAbundance(double minAbundance)
            {
                return ChangeProp(ImClone(this), im => im.MinAbundance = minAbundance);
            }
            public IsotopeAbundances IsotopeAbundances { get; private set; }

            public Settings ChangeIsotopeAbundances(IsotopeAbundances isotopeAbundances)
            {
                return ChangeProp(ImClone(this), im => im.IsotopeAbundances = isotopeAbundances ?? DEFAULT.IsotopeAbundances);
            }

            public MassDistribution GetMassDistribution(MoleculeMassOffset moleculeMassOffset, MassType massType,
                int charge)
            {
                return GetMassDistribution(moleculeMassOffset.Molecule,
                    massType.IsMonoisotopic()
                        ? moleculeMassOffset.MonoMassOffset
                        : moleculeMassOffset.AverageMassOffset, charge);
            }

            public MassDistribution GetMassDistribution(Molecule molecule, double massShift, int charge)
            {
                var emptyMassDistribution = new MassDistribution(MassResolution, MinAbundance);
                var massDistribution = emptyMassDistribution;
                foreach (var entry in molecule)
                {
                    massDistribution = massDistribution.Add(emptyMassDistribution.Add(IsotopeAbundances[entry.Key]).Multiply(entry.Value));
                }
                if (charge != 0 || massShift != 0)
                {
                    massDistribution = massDistribution.OffsetAndDivide(massShift - charge * BioMassCalc.MassElectron,
                        Math.Max(1, Math.Abs(charge)));
                }
                return massDistribution;
            }

            public double GetMonoMass(Molecule molecule)
            {
                double totalMass = 0;
                foreach (var entry in molecule)
                {
                    totalMass += IsotopeAbundances[entry.Key].MostAbundanceMass * entry.Value;
                }
                return totalMass;
            }

            public double GetMonoMass(MoleculeMassOffset moleculeMassOffset)
            {
                return GetMonoMass(moleculeMassOffset.Molecule) + moleculeMassOffset.MonoMassOffset;
            }

            public double GetAverageMass(Molecule molecule)
            {
                double totalMass = 0;
                foreach (var entry in molecule)
                {
                    totalMass += IsotopeAbundances[entry.Key].AverageMass * entry.Value;
                }

                return totalMass;
            }

            public double GetAverageMass(MoleculeMassOffset moleculeMassOffset)
            {
                return GetAverageMass(moleculeMassOffset.Molecule) + moleculeMassOffset.AverageMassOffset;
            }

            public MoleculeMassOffset ReplaceMoleculeWithMassOffset(MoleculeMassOffset moleculeMassOffset)
            {
                double monoMass = GetMonoMass(moleculeMassOffset.Molecule) + moleculeMassOffset.MonoMassOffset;
                double averageMass = GetAverageMass(moleculeMassOffset.Molecule) + moleculeMassOffset.AverageMassOffset;
                return MoleculeMassOffset.Create(null, monoMass, averageMass);
            }

            protected bool Equals(Settings other)
            {
                return MassResolution.Equals(other.MassResolution) && MinAbundance.Equals(other.MinAbundance) &&
                       Equals(IsotopeAbundances, other.IsotopeAbundances);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Settings) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = MassResolution.GetHashCode();
                    hashCode = (hashCode * 397) ^ MinAbundance.GetHashCode();
                    hashCode = (hashCode * 397) ^ (IsotopeAbundances != null ? IsotopeAbundances.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        /// <summary>
        /// Returns a MoleculeMassOffset representing the chemical formula of the FragmentLoss.
        /// Returns the chemical formula of the loss times minus one.
        /// </summary>
        public static MoleculeMassOffset LossAsMoleculeMassOffset(FragmentLoss fragmentLoss)
        {
            if (ParsedMoleculeMassOffset.IsNullOrEmpty(fragmentLoss.ParsedMoleculeMassOffset))
            {
                return MoleculeMassOffset.Create(null, -fragmentLoss.MonoisotopicMass, -fragmentLoss.AverageMass);
            }
            return ParsedMoleculeMassOffset.EMPTY.Difference(fragmentLoss.ParsedMoleculeMassOffset);
        }
    }
}
