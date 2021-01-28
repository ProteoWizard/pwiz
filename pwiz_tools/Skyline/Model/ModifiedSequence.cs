/*
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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{

    /// <summary>
    /// Holds an unmodified sequence and a list of explicit modifications (i.e. StatidMod and AminoAcid index).
    /// This enables the sequence to be formatted in a number of ways, including mass deltas, or modification names.
    /// </summary>
    public class ModifiedSequence : ProteomicSequence
    {
        public const string UnimodPrefix = "unimod:";

        /// <summary>
        /// Constructs a ModifiedSequence from SrmSettings and PeptideDocNode.
        /// </summary>
        public static ModifiedSequence GetModifiedSequence(SrmSettings settings, PeptideDocNode peptideDocNode,
            IsotopeLabelType labelType)
        {
            if (!peptideDocNode.IsProteomic)
            {
                return null;
            }

            return GetModifiedSequence(settings, peptideDocNode.Peptide.Sequence, peptideDocNode.ExplicitMods, labelType);
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
                    if (staticMod.IsExplicit || staticMod.IsVariable || null != staticMod.CrosslinkerSettings)
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

       public override string MonoisotopicMasses
        {
            get { return Format(mods => FormatMassModification(mods, MassType.Monoisotopic, true)); }
        }

        public override string AverageMasses
        {
            get { return Format(mods => FormatMassModification(mods, MassType.Average, true)); }
        }

        public override string ThreeLetterCodes
        {
            get { return FormatModsIndividually(FormatThreeLetterCode); }
        }

        public override string FullNames
        {
            get { return FormatModsIndividually(FormatFullName); }
        }

        public override string UnimodIds
        {
            get { return Format(FormatUnimodIds); }
        }

        public override string FormatDefault()
        {
            return Format(mods => FormatMassModification(mods, _defaultMassType, false));
        }

        protected string FormatModsIndividually(Func<Modification, string> modFormatter)
        {
            return Format(mods => string.Join(string.Empty, Enumerable.Select(mods, mod => Bracket(modFormatter(mod)))));
        }

        protected string Format(Func<IEnumerable<Modification>, string> modFormatter)
        {
            return FormatSelf(modFormatter);
        }

        private string FormatSelf(Func<IEnumerable<Modification>, string> modFormatter)
        {
            StringBuilder result = new StringBuilder();
            int seqCharsReturned = 0;
            foreach (var modGroup in Enumerable.Where(_explicitMods, mod => null == mod.ExplicitMod.LinkedPeptide).GroupBy(mod => mod.IndexAA))
            {
                result.Append(_unmodifiedSequence.Substring(seqCharsReturned,
                    modGroup.Key + 1 - seqCharsReturned));
                seqCharsReturned = modGroup.Key + 1;
                result.Append(modFormatter(modGroup.ToArray()));
            }
            result.Append(_unmodifiedSequence.Substring(seqCharsReturned));
            return result.ToString();
        }



        public override string ToString()
        {
            return FormatDefault();
        }

        public IEnumerable<Modification> GetModifications()
        {
            return _explicitMods;
        }

        public string GetUnmodifiedSequence()
        {
            return _unmodifiedSequence;
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
