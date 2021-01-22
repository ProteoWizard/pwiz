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
using JetBrains.Annotations;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class LinkedPeptide : Immutable
    {
        public static readonly ImmutableSortedList<ModificationSite, LinkedPeptide> EMPTY_CROSSLINK_STRUCTURE 
            = ImmutableSortedList<ModificationSite, LinkedPeptide>.EMPTY;

        public LinkedPeptide(Peptide peptide, int indexAa, ExplicitMods explicitMods)
        {
            Peptide = peptide;
            IndexAa = indexAa;
            ExplicitMods = explicitMods;
        }

        public Peptide Peptide { get; private set; }
        public int IndexAa { get; private set; }

        public int Ordinal
        {
            get { return IndexAa + 1; }
        }

        [CanBeNull]
        public ExplicitMods ExplicitMods { get; private set; }

        public ImmutableSortedList<ModificationSite, LinkedPeptide> CrosslinkStructure
        {
            get
            {
                return null; // DONTCHECKIN ExplicitMods?.Crosslinks ?? EMPTY_CROSSLINK_STRUCTURE;
            }
        }

        public LinkedPeptide ChangeExplicitMods(ExplicitMods explicitMods)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitMods = explicitMods);
        }

        public TransitionGroup GetTransitionGroup(IsotopeLabelType labelType, Adduct adduct)
        {
            return new TransitionGroup(Peptide, adduct, labelType);
        }

        public TransitionGroupDocNode GetTransitionGroupDocNode(SrmSettings settings, IsotopeLabelType labelType, Adduct adduct) {
            var transitionGroup = GetTransitionGroup(labelType, adduct);
            var transitionGroupDocNode = new TransitionGroupDocNode(transitionGroup, Annotations.EMPTY, settings, ExplicitMods, null, ExplicitTransitionGroupValues.EMPTY, null, null, false);
            return transitionGroupDocNode;
        }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            var transitionGroupDocNode = GetTransitionGroupDocNode(settings, labelType, Adduct.SINGLY_PROTONATED);
            return transitionGroupDocNode.GetNeutralFormula(settings, ExplicitMods);
        }

        public IEnumerable<LegacyComplexFragmentIon> ListComplexFragmentIons(SrmSettings settings, int maxFragmentEventCount, bool useFilter)
        {
            IEnumerable<LegacyComplexFragmentIon> result = ListSimpleFragmentIons(settings, useFilter);
            result = PermuteComplexFragmentIons(ExplicitMods, settings, maxFragmentEventCount, useFilter, result);
            return result;
        }

        public IEnumerable<LegacyComplexFragmentIon> PermuteFragmentIons(SrmSettings settings, int maxFragmentationCount, bool useFilter,
            ModificationSite modificationSite, IEnumerable<LegacyComplexFragmentIon> startingFragmentIons)
        {
            var linkedFragmentIonList = ImmutableList.ValueOf(ListComplexFragmentIons(settings, maxFragmentationCount, useFilter));
            return startingFragmentIons.SelectMany(cfi =>
                PermuteFragmentIon(settings, maxFragmentationCount, cfi, modificationSite, linkedFragmentIonList));

        }
        private IEnumerable<LegacyComplexFragmentIon> PermuteFragmentIon(SrmSettings settings, 
            int maxFragmentationCount,
            LegacyComplexFragmentIon fragmentIon,
            ModificationSite modificationSite,
            IList<LegacyComplexFragmentIon> linkedFragmentIons)
        {
            if (fragmentIon.IsOrphan && !fragmentIon.IsEmptyOrphan
                || !fragmentIon.IncludesAaIndex(modificationSite.IndexAa))
            {
                yield return fragmentIon;
                yield break;
            }
            int fragmentCountRemaining = maxFragmentationCount - fragmentIon.GetFragmentationEventCount();
            foreach (var linkedFragmentIon in linkedFragmentIons)
            {
                if (linkedFragmentIon.GetFragmentationEventCount() > fragmentCountRemaining)
                {
                    continue;
                }

                if (fragmentIon.IsOrphan)
                {
                    if (linkedFragmentIon.IncludesAaIndex(IndexAa))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!linkedFragmentIon.IncludesAaIndex(IndexAa))
                    {
                        continue;
                    }
                }

                yield return fragmentIon.AddChild(modificationSite, linkedFragmentIon);
            }
        }

        public IEnumerable<LegacyComplexFragmentIon> ListSimpleFragmentIons(SrmSettings settings, bool useFilter)
        {
            var transitionGroupDocNode =
                GetTransitionGroupDocNode(settings, IsotopeLabelType.light, Adduct.SINGLY_PROTONATED);
            yield return LegacyComplexFragmentIon.NewOrphanFragmentIon(transitionGroupDocNode.TransitionGroup, ExplicitMods, Adduct.SINGLY_PROTONATED);
            foreach (var transitionDocNode in transitionGroupDocNode.TransitionGroup.GetTransitions(settings,
                transitionGroupDocNode, ExplicitMods, transitionGroupDocNode.PrecursorMz,
                transitionGroupDocNode.IsotopeDist, null, null, useFilter, false))
            {
                if (transitionDocNode.Transition.MassIndex != 0)
                {
                    continue;
                }
                yield return new LegacyComplexFragmentIon(transitionDocNode.Transition, transitionDocNode.Losses, CrosslinkStructure);
            }
        }

        protected bool Equals(LinkedPeptide other)
        {
            return Equals(Peptide, other.Peptide) && IndexAa == other.IndexAa &&
                   Equals(ExplicitMods, other.ExplicitMods);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LinkedPeptide) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Peptide != null ? Peptide.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IndexAa;
                hashCode = (hashCode * 397) ^ (ExplicitMods != null ? ExplicitMods.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static IEnumerable<LegacyComplexFragmentIon> PermuteComplexFragmentIons(
            [CanBeNull] ExplicitMods mods, 
            SrmSettings settings, int maxFragmentationCount, bool useFilter, IEnumerable<LegacyComplexFragmentIon> startingFragmentIons)
        {
            var result = FilterImpossibleCleavages(mods, startingFragmentIons);
            if (mods != null)
            {
                // DONTCHECKIN
                // foreach (var crosslinkMod in mods.LinkedCrossslinks)
                // {
                //     result = crosslinkMod.Value.PermuteFragmentIons(settings, maxFragmentationCount, useFilter,
                //         crosslinkMod.Key, result);
                // }
            }

            return result.Where(cfi => !cfi.IsEmptyOrphan);
        }

        /// <summary>
        /// Remove ions where fragmentation is occurring between two ends of a looplink.
        /// </summary>
        public static IEnumerable<LegacyComplexFragmentIon> FilterImpossibleCleavages(ExplicitMods mods,
            IEnumerable<LegacyComplexFragmentIon> startingFragmentIons)
        {
            throw new NotImplementedException();
#if false
            if (mods == null)
            {
                return startingFragmentIons;
            }
            var looplinks = new List<Tuple<int, int>>();
            foreach (var crosslinkMod in mods.Crosslinks)
            {
                if (crosslinkMod.Value.Peptide == null)
                {
                    int index1 = crosslinkMod.Key.IndexAa;
                    int index2 = crosslinkMod.Value.IndexAa;
                    if (index1 == index2)
                    {
                        continue;
                    }
                    looplinks.Add(Tuple.Create(Math.Min(index1, index2), Math.Max(index1, index2)));
                }
            }

            if (!looplinks.Any())
            {
                return startingFragmentIons;
            }

            return startingFragmentIons.Where(cfi =>
            {
                if (cfi.Transition.IonType == IonType.precursor || cfi.IsOrphan)
                {
                    return true;
                }

                int cleavageOffset = cfi.Transition.CleavageOffset;
                return !looplinks.Any(looplink => looplink.Item1 <= cleavageOffset && looplink.Item2 > cleavageOffset);
            });
#endif
        }

        public LegacyComplexFragmentIon MakeComplexFragmentIon(SrmSettings settings, IsotopeLabelType labelType, ComplexFragmentIonName complexFragmentIonName)
        {
            var transitionGroup = GetTransitionGroup(labelType, Adduct.SINGLY_PROTONATED);
            Transition transition;
            if (complexFragmentIonName.IonType == IonType.precursor || complexFragmentIonName.IonType == IonType.custom)
            {
                transition = new Transition(transitionGroup, IonType.precursor, Peptide.Length - 1, 0, Adduct.SINGLY_PROTONATED);
            }
            else
            {
                transition = new Transition(transitionGroup, complexFragmentIonName.IonType,
                    Transition.OrdinalToOffset(complexFragmentIonName.IonType, complexFragmentIonName.Ordinal, Peptide.Length), 
                    0, Adduct.SINGLY_PROTONATED);
            }

            var result = new LegacyComplexFragmentIon(transition, null, CrosslinkStructure, complexFragmentIonName.IsOrphan);
            // DONTCHECKIN
            // if (ExplicitMods != null)
            // {
            //     foreach (var child in complexFragmentIonName.Children)
            //     {
            //         LinkedPeptide linkedPeptide;
            //         if (!ExplicitMods.Crosslinks.TryGetValue(child.Item1, out linkedPeptide))
            //         {
            //             throw new InvalidOperationException(@"No crosslink at " + child.Item1);
            //         }
            //         result = result.AddChild(child.Item1,
            //             linkedPeptide.MakeComplexFragmentIon(settings, labelType, child.Item2));
            //     }
            // }
            //
            return result;
        }

        /// <summary>
        /// Returns the number of linked peptides in this tree (including this linked peptide, unless it is a looplink).
        /// </summary>
        public int CountDescendents()
        {
            if (Peptide == null)
            {
                return 0;
            }
            int result = 1;
            if (ExplicitMods != null)
            {
                // DONTCHECKIN
                // result += ExplicitMods.Crosslinks.Values.Sum(linkedPeptide => linkedPeptide.CountDescendents());
            }

            return result;
        }

        [TrackChildren(defaultValues:typeof(DefaultValuesNullOrEmpty))]
        [UsedImplicitly]
        public IList<LoggableExplicitMod> ExplicitModsStatic
        {
            get
            {
                if (ExplicitMods != null)
                {
                    return ImmutableList.ValueOf(ExplicitMods.StaticModifications.Select(
                        mod => new LoggableExplicitMod(mod, Peptide.Sequence)));
                }

                return ImmutableList.Empty<LoggableExplicitMod>();
            }
        }

        [Track]
        [UsedImplicitly]
        public string PeptideSequence
        {
            get { return Peptide?.Sequence; }
        }

        [Track]
        [UsedImplicitly]
        public int Position
        {
            get { return IndexAa + 1; }
        }
    }
}
