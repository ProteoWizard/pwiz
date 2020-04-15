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
        public static readonly MassDistribution EMPTY_MASSDISTRIBUTION = new MassDistribution(.001, 0.00001);

        private FragmentedMolecule()
        {
            PrecursorFormula = FragmentFormula = Molecule.Empty;
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
        
        public Molecule PrecursorFormula { get; private set; }

        public FragmentedMolecule ChangePrecursorFormula(Molecule precursorFormula)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.PrecursorFormula = precursorFormula;
                im.ModifiedSequence = null;
            });
        }

        public double PrecursorMassShift { get; private set; }
        public MassType PrecursorMassType { get; private set; }

        public FragmentedMolecule ChangePrecursorMassShift(double precursorMassShift, MassType precursorMassType)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.PrecursorMassShift = precursorMassShift;
                im.PrecursorMassType = precursorMassType;
            });
        }
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

        public Molecule FragmentFormula { get; private set; }

        public FragmentedMolecule ChangeFragmentFormula(Molecule fragmentFormula)
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
        public double FragmentMassShift { get; private set; }
        public MassType FragmentMassType { get; private set; }

        public FragmentedMolecule ChangeFragmentMassShift(double massShift, MassType massType)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.FragmentMassShift = massShift;
                im.FragmentMassType = massType;
            });
        }

        private void UpdateFormulas()
        {
            if (ModifiedSequence == null)
            {
                return;
            }
            double precursorMassShift;
            var precursorNeutralFormula = FormulaForIonType(GetSequenceFormula(ModifiedSequence, PrecursorMassType, out precursorMassShift), IonType.precursor);
            PrecursorFormula = SetCharge(precursorNeutralFormula, PrecursorCharge);
            PrecursorMassShift = precursorMassShift;
            if (FragmentIonType == IonType.custom)
            {
                return;
            }
            if (FragmentIonType == IonType.precursor)
            {
                FragmentOrdinal = UnmodifiedSequence.Length;
                FragmentFormula = SetCharge(precursorNeutralFormula, FragmentCharge);
                FragmentMassShift = PrecursorMassShift;
            }
            else
            {
                FragmentOrdinal = Math.Max(1, Math.Min(UnmodifiedSequence.Length, FragmentOrdinal));
                ModifiedSequence fragmentSequence = GetFragmentSequence(ModifiedSequence, FragmentIonType, FragmentOrdinal);
                double fragmentMassShift;
                FragmentFormula = GetSequenceFormula(fragmentSequence, FragmentMassType, out fragmentMassShift);
                FragmentFormula = AddFragmentLosses(FragmentFormula, FragmentLosses, FragmentMassType, ref fragmentMassShift);
                FragmentFormula = FormulaForIonType(FragmentFormula, FragmentIonType);
                FragmentFormula = SetCharge(FragmentFormula, FragmentCharge);
                FragmentMassShift = fragmentMassShift;
            }
        }

        public IDictionary<double, double> GetFragmentDistribution(Settings settings, double? precursorMinMz, double? precursorMaxMz)
        {
            var fragmentDistribution = settings.GetMassDistribution(FragmentFormula, FragmentMassShift, FragmentCharge);
            var otherFragmentFormula = GetComplementaryProductFormula();
            var otherFragmentDistribution = settings.GetMassDistribution(otherFragmentFormula, PrecursorMassShift - FragmentMassShift, PrecursorCharge);
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

        public Molecule GetComplementaryProductFormula()
        {
            var difference = new Dictionary<string, int>(PrecursorFormula);
            foreach (var entry in FragmentFormula)
            {
                int count;
                difference.TryGetValue(entry.Key, out count);
                count -= entry.Value;
                difference[entry.Key] = count;
            }
            var negative = difference.FirstOrDefault(entry => entry.Value < 0);
            if (null != negative.Key)
            {
                string message = string.Format(
                    "Unable to calculate expected distribution because the fragment contains more '{0}' atoms than the precursor.",
                    negative.Key);
                throw new InvalidOperationException(message);
            }
            return Molecule.FromDict(difference);
        }

        private static Molecule GetSequenceFormula(ModifiedSequence modifiedSequence, MassType massType, out double unexplainedMassShift)
        {
            unexplainedMassShift = 0;
            var molecule = new Dictionary<string, int>();
            string unmodifiedSequence = modifiedSequence.GetUnmodifiedSequence();
            var modifications = modifiedSequence.GetModifications().ToLookup(mod => mod.IndexAA);
            for (int i = 0; i < unmodifiedSequence.Length; i++)
            {
                char aminoAcid = unmodifiedSequence[i];
                var aminoAcidFormula = Molecule.Parse(AminoAcidFormulas.Default.Formulas[aminoAcid]);
                Add(molecule, aminoAcidFormula);
                foreach (var mod in modifications[i])
                {
                    string formula = mod.Formula;
                    if (formula == null)
                    {
                        var staticMod = mod.StaticMod;
                        var aa = unmodifiedSequence[i];
                        if ((staticMod.LabelAtoms & LabelAtoms.LabelsAA) != LabelAtoms.None && AminoAcid.IsAA(aa))
                        {
                            formula = SequenceMassCalc.GetHeavyFormula(aa, staticMod.LabelAtoms);
                        }
                    }
                    if (formula != null)
                    {
                        var modFormula = Molecule.ParseExpression(formula);
                        Add(molecule, modFormula);
                    }
                    else
                    {
                        unexplainedMassShift += massType.IsMonoisotopic() ? mod.MonoisotopicMass : mod.AverageMass;
                    }
                }
            }
            return Molecule.FromDict(molecule);
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

        private static Molecule SetCharge(Molecule neutralFormula, int charge)
        {
            if (charge <= 0)
            {
                return neutralFormula;
            }
            return neutralFormula.SetElementCount("H", neutralFormula.GetElementCount("H") + charge);
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

        public static Molecule FormulaForIonType(Molecule molecule, IonType ionType)
        {
            IList<Tuple<string, int>> deltas;
            switch (ionType)
            {
                case IonType.precursor:
                    deltas = new[] {Tuple.Create("H", 2), Tuple.Create("O", 1)};
                    break;
                case IonType.a:
                    deltas = new[] {Tuple.Create("H", 1), Tuple.Create("C", -1), Tuple.Create("O", -1)};
                    break;
                case IonType.b:
                    deltas = new[] {Tuple.Create("H", 0)};
                    break;
                case IonType.c:
                    deltas = new[] {Tuple.Create("H", 3), Tuple.Create("N", 1)};
                    break;
                case IonType.x:
                    deltas = new[] {Tuple.Create("H", 0), Tuple.Create("O", 2), Tuple.Create("C", 1)};
                    break;
                case IonType.y:
                    deltas = new[] {Tuple.Create("H", 2), Tuple.Create("O", 1)};
                    break;
                case IonType.z:
                    deltas = new[] {Tuple.Create("H", -1), Tuple.Create("O", 1), Tuple.Create("N", -1)};
                    break;
                default:
                    throw new ArgumentException();
            }
            foreach (var delta in deltas)
            {
                molecule = molecule.SetElementCount(delta.Item1, molecule.GetElementCount(delta.Item1) + delta.Item2);
            }
            return molecule;
        }

        public static Molecule AddFragmentLosses(Molecule molecule, IList<FragmentLoss> fragmentLosses, 
            MassType massType, ref double unexplainedMass)
        {
            foreach (var fragmentLoss in fragmentLosses)
            {
                if (string.IsNullOrEmpty(fragmentLoss.Formula))
                {
                    unexplainedMass += massType.IsMonoisotopic() ? fragmentLoss.MonoisotopicMass : fragmentLoss.AverageMass;
                    continue;
                }
                Molecule lossFormula;
                int ichMinus = fragmentLoss.Formula.IndexOf('-');
                if (ichMinus < 0)
                {
                    lossFormula = Molecule.Parse(fragmentLoss.Formula);
                }
                else
                {
                    lossFormula = Molecule.Parse(fragmentLoss.Formula.Substring(0, ichMinus));
                    lossFormula = lossFormula.Difference(Molecule.Parse(fragmentLoss.Formula.Substring(ichMinus + 1)));
                }
                foreach (var entry in lossFormula)
                {
                    molecule = molecule.SetElementCount(entry.Key, molecule.GetElementCount(entry.Key) - entry.Value);
                }
            }
            return molecule;
        }

        public static FragmentedMolecule GetFragmentedMolecule(SrmSettings settings, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode, TransitionDocNode transitionDocNode)
        {

            FragmentedMolecule fragmentedMolecule = EMPTY
                .ChangePrecursorMassShift(0, settings.TransitionSettings.Prediction.PrecursorMassType)
                .ChangeFragmentMassShift(0, settings.TransitionSettings.Prediction.FragmentMassType);
            if (peptideDocNode == null)
            {
                return fragmentedMolecule;
            }
            var labelType = transitionGroupDocNode == null
                ? IsotopeLabelType.light
                : transitionGroupDocNode.TransitionGroup.LabelType;
            if (peptideDocNode.IsProteomic)
            {
                fragmentedMolecule = fragmentedMolecule.ChangeModifiedSequence(
                    ModifiedSequence.GetModifiedSequence(settings, peptideDocNode, labelType));
                if (transitionGroupDocNode != null)
                {
                    fragmentedMolecule = fragmentedMolecule
                        .ChangePrecursorCharge(transitionGroupDocNode.PrecursorCharge);
                }
                if (transitionDocNode == null || transitionDocNode.IsMs1)
                {
                    return fragmentedMolecule;
                }
                var transition = transitionDocNode.Transition;
                fragmentedMolecule = fragmentedMolecule
                    .ChangeFragmentIon(transition.IonType, transition.Ordinal)
                    .ChangeFragmentCharge(transition.Charge);
                var transitionLosses = transitionDocNode.Losses;
                if (transitionLosses != null)
                {
                    var fragmentLosses = transitionLosses.Losses.Select(transitionLoss => transitionLoss.Loss);
                    fragmentedMolecule = fragmentedMolecule.ChangeFragmentLosses(fragmentLosses);
                }
                return fragmentedMolecule;
            }
            if (transitionGroupDocNode == null)
            {
                return fragmentedMolecule
                    .ChangePrecursorFormula(
                        Molecule.Parse(peptideDocNode.CustomMolecule.Formula ?? string.Empty));
            }
            var customMolecule = transitionGroupDocNode.CustomMolecule;
            fragmentedMolecule =
                fragmentedMolecule.ChangePrecursorCharge(transitionGroupDocNode.TransitionGroup
                    .PrecursorCharge);
            if (customMolecule.Formula != null)
            {
                var ionInfo = new IonInfo(customMolecule.Formula,
                    transitionGroupDocNode.PrecursorAdduct);
                fragmentedMolecule = fragmentedMolecule
                    .ChangePrecursorFormula(Molecule.Parse(ionInfo.FormulaWithAdductApplied));
            }
            else
            {

                fragmentedMolecule = fragmentedMolecule.ChangePrecursorMassShift(
                    transitionGroupDocNode.PrecursorAdduct.MassFromMz(
                        transitionGroupDocNode.PrecursorMz, transitionGroupDocNode.PrecursorMzMassType), 
                        transitionGroupDocNode.PrecursorMzMassType);
            }
            if (transitionDocNode == null || transitionDocNode.IsMs1)
            {
                return fragmentedMolecule;
            }
            var customIon = transitionDocNode.Transition.CustomIon;
            if (customIon.Formula != null)
            {
                fragmentedMolecule = fragmentedMolecule.ChangeFragmentFormula(
                    Molecule.Parse(customIon.FormulaWithAdductApplied));
            }
            else
            {
                fragmentedMolecule = fragmentedMolecule.ChangeFragmentMassShift(
                    transitionDocNode.Transition.Adduct.MassFromMz(
                        transitionDocNode.Mz, transitionDocNode.MzMassType), 
                        transitionDocNode.MzMassType);
            }
            fragmentedMolecule = fragmentedMolecule
                .ChangeFragmentCharge(transitionDocNode.Transition.Charge);
            return fragmentedMolecule;
        }

        public class Settings : Immutable
        {
            public static readonly Settings DEFAULT = new Settings().ChangeMassResolution(.01).ChangeMinAbundance(.00001)
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

            public MassDistribution GetMassDistribution(Molecule molecule, double massShift, int charge)
            {
                var massDistribution = new MassDistribution(MassResolution, MinAbundance);
                foreach (var entry in molecule)
                {
                    massDistribution = massDistribution.Add(IsotopeAbundances[entry.Key].Multiply(entry.Value));
                }
                if (charge != 0)
                {
                    massDistribution = massDistribution.OffsetAndDivide(massShift - charge * BioMassCalc.MassElectron,
                        Math.Abs(charge));
                }
                return massDistribution;
            }

            public double GetMonoMass(Molecule molecule, double massShift, int charge)
            {
                var massDistribution = ChangeIsotopeAbundances(GetMonoisotopicAbundances(IsotopeAbundances))
                    .GetMassDistribution(molecule, massShift, charge);
                return massDistribution.MostAbundanceMass;
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

        private static IsotopeAbundances GetMonoisotopicAbundances(IsotopeAbundances isotopeAbundances)
        {
            var newAbundances = new Dictionary<string, MassDistribution>();
            foreach (var entry in isotopeAbundances)
            {
                newAbundances.Add(entry.Key, new MassDistribution(entry.Value.MassResolution, entry.Value.MinimumAbundance)
                    .SetAbundance(entry.Value.MostAbundanceMass, 1));
            }
            return isotopeAbundances.SetAbundances(newAbundances);
        }
    }
}
