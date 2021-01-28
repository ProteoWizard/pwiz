using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public abstract class ProteomicSequence
    {
        public static ProteomicSequence GetProteomicSequence(SrmSettings settings, Peptide peptide,
            ExplicitMods explicitMods, IsotopeLabelType labelType)
        {
            if (explicitMods == null || !explicitMods.HasCrosslinks)
            {
                return ModifiedSequence.GetModifiedSequence(settings, peptide.Sequence, explicitMods, labelType);
            }

            return CrosslinkedSequence.GetCrosslinkedSequence(settings, explicitMods.GetPeptideStructure(), labelType);
        }

        public static ProteomicSequence GetProteomicSequence(SrmSettings settings, PeptideDocNode peptideDocNode, IsotopeLabelType labelType)
        {
            return GetProteomicSequence(settings, peptideDocNode.Peptide, peptideDocNode.ExplicitMods, labelType);
        }

        public abstract string MonoisotopicMasses { get; }
        public abstract string AverageMasses { get; }
        public abstract string ThreeLetterCodes { get; }
        public abstract string FullNames { get; }
        public abstract string UnimodIds { get; }
        public abstract string FormatDefault();

        public static string FormatThreeLetterCode(ModifiedSequence.Modification modification)
        {
            if (string.IsNullOrEmpty(modification.ShortName))
            {
                return FormatFullName(modification);
            }
            return modification.ShortName;
        }

        public static string FormatMassModification(IEnumerable<ModifiedSequence.Modification> mods, MassType massType, bool fullPrecision)
        {
            double mass = 0;
            foreach (var mod in mods)
            {
                double? modMass = (massType & MassType.Average) != 0 ? mod.AverageMass : mod.MonoisotopicMass;
                mass += modMass.GetValueOrDefault();
            }
            if (mass == 0)
            {
                return String.Empty;
            }
            return Bracket(FormatMassDelta(mass, fullPrecision));
        }

        private static string FormatMassDelta(double mass, bool fullPrecision)
        {
            int precision = fullPrecision ? MassModification.MAX_PRECISION_TO_KEEP : 1;
            string strMod = Math.Round(mass, precision).ToString(CultureInfo.InvariantCulture);
            if (mass >= 0)
            {
                strMod = @"+" + strMod;
            }

            return strMod;
        }

        public static string FormatFullName(ModifiedSequence.Modification mod)
        {
            if (string.IsNullOrEmpty(mod.Name))
            {
                return FormatFallback(mod);
            }
            return mod.Name;
        }

        public static string FormatUnimodIds(IEnumerable<ModifiedSequence.Modification> mods)
        {
            return string.Join(String.Empty, mods.Select<ModifiedSequence.Modification, string>(mod =>
            {
                if (mod.UnimodId.HasValue)
                {
                    return @"(" + ModifiedSequence.UnimodPrefix + mod.UnimodId.Value + @")";
                }
                return Bracket(FormatFallback(mod));
            }));
        }

        public static string FormatFallback(IEnumerable<ModifiedSequence.Modification> mods)
        {
            return string.Join(string.Empty, mods.Select<ModifiedSequence.Modification, string>(mod => Bracket(FormatFallback(mod))));
        }

        /// <summary>
        /// Converts the modification to a string in the safest way possible. This is used
        /// when the requested modification format (e.g. Three Letter Code) is not available
        /// for this modification.
        /// </summary>
        public static string FormatFallback(ModifiedSequence.Modification mod)
        {
            if (!string.IsNullOrEmpty(mod.Name))
            {
                return mod.Name;
            }
            if (!string.IsNullOrEmpty(mod.ShortName))
            {
                return mod.ShortName;
            }
            if (mod.MonoisotopicMass != 0)
            {
                return mod.MonoisotopicMass.ToString(CultureInfo.CurrentCulture);
            }
            if (!string.IsNullOrEmpty(mod.Formula))
            {
                return mod.Formula;
            }
            return @"#UNKNOWNMODIFICATION#";
        }

        /// <summary>
        /// Adds brackets around a modification. If the modification itself contains the close
        /// bracket character, then uses either parentheses or braces.
        /// </summary>
        public static string Bracket(string str)
        {
            // ReSharper disable LocalizableElement
            if (!str.Contains("]")) 
            {
                return "[" + str + "]";
            }
            if (!str.Contains(")"))
            {
                return "(" + str + ")";
            }
            if (!str.Contains("}"))
            {
                return "{" + str + "}";
            }
            // We could not find a safe type of bracket to use.
            // Just replace all the close brackets with underscores.
            return "[" + str.Replace("]", "_") + "]";
            // ReSharper restore LocalizableElement
        }

        public static Modification MakeModification(string unmodifiedSequence, ExplicitMod explicitMod)
        {
            var staticMod = explicitMod.Modification;
            int i = explicitMod.IndexAA;
            var monoMass = staticMod.MonoisotopicMass ??
                           SrmSettings.MonoisotopicMassCalc.GetAAModMass(unmodifiedSequence[i], i,
                               unmodifiedSequence.Length);
            var avgMass = staticMod.AverageMass ??
                          SrmSettings.AverageMassCalc.GetAAModMass(unmodifiedSequence[i], i,
                              unmodifiedSequence.Length);
            if (monoMass == 0 && avgMass == 0)
            {
                char aa = unmodifiedSequence[i];
                if ((staticMod.LabelAtoms & LabelAtoms.LabelsAA) != LabelAtoms.None && AminoAcid.IsAA(aa))
                {
                    string heavyFormula = SequenceMassCalc.GetHeavyFormula(aa, staticMod.LabelAtoms);
                    monoMass = SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, heavyFormula,
                        SequenceMassCalc.MassPrecision);
                    avgMass = SequenceMassCalc.FormulaMass(BioMassCalc.AVERAGE, heavyFormula,
                        SequenceMassCalc.MassPrecision);
                }
            }
            return new ModifiedSequence.Modification(explicitMod, monoMass, avgMass);
        }

        public class Modification : Immutable
        {
            public Modification(ExplicitMod explicitMod, double monoMass, double avgMass)
            {
                ExplicitMod = explicitMod;
                MonoisotopicMass = monoMass;
                AverageMass = avgMass;
            }

            public ExplicitMod ExplicitMod { get; private set; }
            public StaticMod StaticMod { get { return ExplicitMod.Modification; } }
            public int IndexAA {get { return ExplicitMod.IndexAA; } }
            public Modification ChangeIndexAa(int newIndexAa)
            {
                return ChangeProp(ImClone(this),
                    im => { im.ExplicitMod = new ExplicitMod(newIndexAa, ExplicitMod.Modification); });
            }

            public Modification ChangeLinkedPeptideSequence(ModifiedSequence linkedPeptideSequence)
            {
                return ChangeProp(ImClone(this), im => im.LinkedPeptideSequence = linkedPeptideSequence);
            }

            public string Name { get { return StaticMod.Name; } }
            public string ShortName { get { return StaticMod.ShortName; } }
            public string Formula { get { return StaticMod.Formula; } }
            public int? UnimodId { get { return StaticMod.UnimodId; } }
            public double MonoisotopicMass { get; private set; }
            public double AverageMass { get; private set; }

            public ModifiedSequence LinkedPeptideSequence { get; private set; }

            protected bool Equals(Modification other)
            {
                return ExplicitMod.Equals(other.ExplicitMod) && MonoisotopicMass.Equals(other.MonoisotopicMass) &&
                       AverageMass.Equals(other.AverageMass);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Modification) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = ExplicitMod.GetHashCode();
                    hashCode = (hashCode * 397) ^ MonoisotopicMass.GetHashCode();
                    hashCode = (hashCode * 397) ^ AverageMass.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}