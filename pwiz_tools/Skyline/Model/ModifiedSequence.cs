﻿/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{

    public interface ISequenceContainer
    {
        Target Target { get; }
        Target ModifiedTarget { get; }
        ExplicitMods ExplicitMods { get; }
    }

    /// <summary>
    /// Holds an unmodified sequence and a list of explicit modifications (i.e. StatidMod and AminoAcid index).
    /// This enables the sequence to be formatted in a number of ways, including mass deltas, or modification names.
    /// </summary>
    public class ModifiedSequence
    {
        public const string UnimodPrefix = "unimod:";

        /// <summary>
        /// Constructs a ModifiedSequence from SrmSettings and PeptideDocNode.
        /// </summary>
        public static ModifiedSequence GetModifiedSequence(SrmSettings settings, ISequenceContainer peptide,
            IsotopeLabelType labelType)
        {
            if (!peptide.Target.IsProteomic)
            {
                return null;
            }

            return GetModifiedSequence(settings, peptide.Target.Sequence, peptide.ExplicitMods, labelType);
        }

        public static ModifiedSequence GetModifiedSequence(SrmSettings settings, string unmodifiedSequence, ExplicitMods peptideExplicitMods, IsotopeLabelType labelType) 
        {
            List<IsotopeLabelType> implicitLabelTypes = new List<IsotopeLabelType>();
            implicitLabelTypes.Add(IsotopeLabelType.light);
            if (!labelType.IsLight)
            {
                implicitLabelTypes.Add(labelType);
            }
            List<Modification> explicitMods = new List<Modification>();
            if (null != peptideExplicitMods)
            {
                var staticBaseMods = peptideExplicitMods.GetStaticBaseMods(labelType);
                if (staticBaseMods != null)
                {
                    implicitLabelTypes.Clear();
                }
                var labelMods = peptideExplicitMods.GetModifications(labelType);
                if (labelMods != null)
                {
                    if (!peptideExplicitMods.IsVariableStaticMods)
                    {
                        implicitLabelTypes.Remove(labelType);
                    }
                }
                else if (!labelType.IsLight)
                {
                    labelMods = peptideExplicitMods.GetModifications(IsotopeLabelType.light);
                    if (labelMods != null && !peptideExplicitMods.IsVariableStaticMods)
                    {
                        implicitLabelTypes.Remove(IsotopeLabelType.light);
                    }
                }
                if (labelMods != null || staticBaseMods != null)
                {
                    IEnumerable<ExplicitMod> modsToAdd = (labelMods ?? Enumerable.Empty<ExplicitMod>())
                        .Concat(staticBaseMods ?? Enumerable.Empty<ExplicitMod>());
                    foreach (var mod in modsToAdd)
                    {
                        explicitMods.Add(ResolveModification(settings, labelType, unmodifiedSequence, mod));
                    }
                }
            }

            List<StaticMod> implicitMods = new List<StaticMod>();
            var peptideModifications = settings.PeptideSettings.Modifications;
            foreach (var implicitLabelType in implicitLabelTypes)
            {
                implicitMods.AddRange(peptideModifications.GetModifications(implicitLabelType));
            }

            for (int i = 0; i < unmodifiedSequence.Length; i++)
            {
                foreach (var staticMod in implicitMods)
                {
                    if (staticMod.IsExplicit || staticMod.IsVariable)
                    {
                        continue;
                    }
                    if (staticMod.Terminus.HasValue)
                    {
                        if (staticMod.Terminus == ModTerminus.N && i != 0)
                        {
                            continue;
                        }
                        if (staticMod.Terminus == ModTerminus.C && i != unmodifiedSequence.Length - 1)
                        {
                            continue;
                        }
                    }
                    if (!string.IsNullOrEmpty(staticMod.AAs) && !staticMod.AAs.Contains(unmodifiedSequence[i]))
                    {
                        continue;
                    }
                    explicitMods.Add(ResolveModification(settings, labelType, unmodifiedSequence, new ExplicitMod(i, staticMod)));
                }
            }
            return new ModifiedSequence(unmodifiedSequence, explicitMods, settings.TransitionSettings.Prediction.PrecursorMassType);
        }
        
        private readonly string _unmodifiedSequence;
        private readonly ImmutableList<Modification> _explicitMods;
        private readonly MassType _defaultMassType;
        public ModifiedSequence(string unmodifiedSequence, IEnumerable<Modification> explicitMods, MassType defaultMassType)
        {
            _unmodifiedSequence = unmodifiedSequence;
            _explicitMods = ImmutableList.ValueOf(explicitMods.OrderBy(mod=>mod.IndexAA, SortOrder.Ascending));
            _defaultMassType = defaultMassType;
        }

        [Browsable(false)]
        public ImmutableList<Modification> ExplicitMods
        {
            get { return _explicitMods; }
        }

       public string MonoisotopicMasses
        {
            get { return Format(mods => FormatMassModification(mods, MassType.Monoisotopic, true)); }
        }

        public string AverageMasses
        {
            get { return Format(mods => FormatMassModification(mods, MassType.Average, true)); }
        }

        public string ThreeLetterCodes
        {
            get { return FormatModsIndividually(FormatThreeLetterCode); }
        }

        public string FullNames
        {
            get { return FormatModsIndividually(FormatFullName); }
        }

        public string UnimodIds
        {
            get { return Format(FormatUnimodIds); }
        }

        public override string ToString()
        {
            return Format(mods=>FormatMassModification(mods, _defaultMassType, false));
        }

        private string FormatModsIndividually(Func<Modification, string> modFormatter)
        {
            return Format(mods =>string.Join(string.Empty, mods.Select(mod => Bracket(modFormatter(mod)))));
        }

        private string Format(Func<IEnumerable<Modification>, string> modFormatter)
        {
            return FormatSelf(modFormatter) + FormatLinkedPeptides(modFormatter);
        }

        private string FormatSelf(Func<IEnumerable<Modification>, string> modFormatter)
        {
            StringBuilder result = new StringBuilder();
            int seqCharsReturned = 0;
            foreach (var modGroup in _explicitMods.Where(mod => null == mod.ExplicitMod.LinkedPeptide).GroupBy(mod => mod.IndexAA))
            {
                result.Append(_unmodifiedSequence.Substring(seqCharsReturned,
                    modGroup.Key + 1 - seqCharsReturned));
                seqCharsReturned = modGroup.Key + 1;
                result.Append(modFormatter(modGroup.ToArray()));
            }
            result.Append(_unmodifiedSequence.Substring(seqCharsReturned));
            return result.ToString();
        }

        private string FormatLinkedPeptides(Func<IEnumerable<Modification>, string> modFormatter)
        {
            List<Tuple<int, Modification>> linkedModifications = new List<Tuple<int, Modification>>();

            StringBuilder peptideSequences = new StringBuilder();
            StringBuilder crosslinks = new StringBuilder();
            int totalPeptides = 1;
            foreach (var mod in GetModifications().Where(mod => null != mod.ExplicitMod.LinkedPeptide))
            {
                totalPeptides += mod.ExplicitMod.LinkedPeptide.CountDescendents();
                if (mod.LinkedPeptideSequence == null)
                {
                    crosslinks.Append(FormatCrosslinkMod(modFormatter, totalPeptides, mod, 0, 0));
                }
                else
                {
                    linkedModifications.Add(Tuple.Create(0, mod));
                }
            }

            int peptideIndex = 0;
            while (linkedModifications.Any())
            {
                peptideIndex++;
                var linkedModTuple = linkedModifications[0];
                linkedModifications.RemoveAt(0);
                var linkedModification = linkedModTuple.Item2;
                if (linkedModTuple.Item2.LinkedPeptideSequence != null)
                {
                    foreach (var mod in linkedModTuple.Item2.LinkedPeptideSequence.GetModifications()
                        .Where(mod => null != mod.ExplicitMod.LinkedPeptide))
                    {
                        if (mod.LinkedPeptideSequence == null)
                        {
                            crosslinks.Append(FormatCrosslinkMod(modFormatter, totalPeptides, mod, peptideIndex,
                                peptideIndex));
                        }
                        else
                        {
                            linkedModifications.Add(Tuple.Create(peptideIndex, mod));
                        }
                    }
                }

                peptideSequences.Append(@"-");
                peptideSequences.Append(linkedModification.LinkedPeptideSequence.FormatSelf(modFormatter));
                crosslinks.Append(FormatCrosslinkMod(modFormatter, totalPeptides, linkedModification,
                    linkedModTuple.Item1, peptideIndex));
            }
            if (crosslinks.Length == 0)
            {
                Assume.AreEqual(0, peptideSequences.Length);
                return string.Empty;
            }

            return peptideSequences + @"-" + crosslinks;
        }

        private string FormatCrosslinkMod(Func<IEnumerable<Modification>, string> modFormatter, 
            int totalPeptideCount,
            Modification linkedModification,
            int peptideIndex1, int peptideIndex2)
        {
            Assume.IsTrue(peptideIndex1 <= peptideIndex2);
            var strMod = modFormatter(new[] { linkedModification });
            if (strMod.Length == 0)
            {
                strMod = @"[]";
            }


            var indexes = new List<string>(totalPeptideCount);
            indexes.AddRange(Enumerable.Repeat(@"*", peptideIndex1));
            if (peptideIndex1 == peptideIndex2)
            {
                indexes.Add((linkedModification.IndexAA + 1) + @"-" + (linkedModification.ExplicitMod.LinkedPeptide.IndexAa + 1));
            }
            else
            {
                indexes.Add((linkedModification.IndexAA + 1).ToString());
                indexes.AddRange(Enumerable.Repeat(@"*", peptideIndex2 - peptideIndex1 - 1));
                indexes.Add((linkedModification.ExplicitMod.LinkedPeptide.IndexAa + 1).ToString());
            }
            indexes.AddRange(Enumerable.Repeat(@"*", totalPeptideCount - peptideIndex2 - 1));
            return strMod.Substring(0, strMod.Length - 1) + @"@" + string.Join(@",", indexes) +
                   strMod.Substring(strMod.Length - 1);
        }

        public IEnumerable<Modification> GetModifications()
        {
            return _explicitMods;
        }

        public string GetUnmodifiedSequence()
        {
            return _unmodifiedSequence;
        }

        /// <summary>
        /// Replace all of the crosslinked peptide with the mass of the crosslinker plus the mass of the linked peptide.
        /// </summary>
        public ModifiedSequence ReplaceCrosslinksWithMasses(SrmSettings settings, IsotopeLabelType labelType)
        {
            if (ExplicitMods.All(mod => null == mod.LinkedPeptideSequence))
            {
                return this;
            }

            var newModifications = new List<Modification>(ExplicitMods.Count);
            foreach (var modification in ExplicitMods)
            {
                if (null == modification.LinkedPeptideSequence)
                {
                    newModifications.Add(modification);
                    continue;
                }
                var formula = modification.StaticMod.Formula;
                MoleculeMassOffset moleculeMassOffset;
                if (string.IsNullOrEmpty(formula))
                {
                    moleculeMassOffset = new MoleculeMassOffset(Molecule.Empty, modification.StaticMod.MonoisotopicMass ?? 0, modification.StaticMod.AverageMass ?? 0);
                }
                else
                {
                    moleculeMassOffset = new MoleculeMassOffset(Molecule.ParseExpression(formula), 0, 0);
                }
                moleculeMassOffset = moleculeMassOffset.Plus(modification.ExplicitMod.LinkedPeptide.GetNeutralFormula(settings, labelType));
                var fragmentedMoleculeSettings = FragmentedMolecule.Settings.FromSrmSettings(settings);
                moleculeMassOffset = fragmentedMoleculeSettings.ReplaceMoleculeWithMassOffset(moleculeMassOffset);
                Assume.IsTrue(0 == moleculeMassOffset.Molecule.Count);
                newModifications.Add(new Modification(modification.ExplicitMod, moleculeMassOffset.MonoMassOffset, moleculeMassOffset.AverageMassOffset));
            }
            return new ModifiedSequence(_unmodifiedSequence, newModifications, _defaultMassType);
        }

        public ModifiedSequence SeverCrosslinks()
        {
            return new ModifiedSequence(_unmodifiedSequence, _explicitMods.Select(mod=>mod.ChangeLinkedPeptideSequence(null)), _defaultMassType);
        }

        public static string FormatThreeLetterCode(Modification modification)
        {
            if (string.IsNullOrEmpty(modification.ShortName))
            {
                return FormatFullName(modification);
            }
            return modification.ShortName;
        }

        public static string FormatMassModification(IEnumerable<Modification> mods, MassType massType, bool fullPrecision)
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

        public static string FormatFullName(Modification mod)
        {
            if (string.IsNullOrEmpty(mod.Name))
            {
                return FormatFallback(mod);
            }
            return mod.Name;
        }

        public static string FormatUnimodIds(IEnumerable<Modification> mods)
        {
            return string.Join(String.Empty, mods.Select(mod =>
            {
                if (mod.UnimodId.HasValue)
                {
                    return @"(" + UnimodPrefix + mod.UnimodId.Value + @")";
                }
                return Bracket(FormatFallback(mod));
            }));
        }

        public static string FormatFallback(IEnumerable<Modification> mods)
        {
            return string.Join(string.Empty, mods.Select(mod => Bracket(FormatFallback(mod))));
        }

        /// <summary>
        /// Converts the modification to a string in the safest way possible. This is used
        /// when the requested modification format (e.g. Three Letter Code) is not available
        /// for this modification.
        /// </summary>
        public static string FormatFallback(Modification mod)
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

        protected bool Equals(ModifiedSequence other)
        {
            return string.Equals(_unmodifiedSequence, other._unmodifiedSequence) &&
                   _explicitMods.Equals(other._explicitMods) && _defaultMassType == other._defaultMassType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ModifiedSequence) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _unmodifiedSequence.GetHashCode();
                hashCode = (hashCode * 397) ^ _explicitMods.GetHashCode();
                hashCode = (hashCode * 397) ^ (int) _defaultMassType;
                return hashCode;
            }
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
            return new Modification(explicitMod, monoMass, avgMass);
        }

        public static Modification ResolveModification(SrmSettings settings, IsotopeLabelType labelType, string unmodifiedSequence,
            ExplicitMod explicitMod)
        {
            var modification = MakeModification(unmodifiedSequence, explicitMod);
            if (explicitMod?.LinkedPeptide?.Peptide == null)
            {
                return modification;
            }

            return modification.ChangeLinkedPeptideSequence(GetModifiedSequence(settings,
                explicitMod.LinkedPeptide.Peptide.Sequence, explicitMod.LinkedPeptide.ExplicitMods, labelType));
        }
    }
}
