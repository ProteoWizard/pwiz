/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// Represents a crosslinked fragment ion, but does not include the Adduct.
    /// </summary>
    public class NeutralFragmentIon : Immutable, IComparable<NeutralFragmentIon>
    {
        public NeutralFragmentIon(IEnumerable<IonOrdinal> parts, TransitionLosses losses)
        {
            IonChain = IonChain.FromIons(parts);
            Losses = losses;
        }

        public IonChain IonChain { get; private set; }

        public TransitionLosses Losses { get; private set; }

        public bool? IncludesSite(PeptideStructure peptideStructure, CrosslinkSite site)
        {
            if (site.PeptideIndex >= IonChain.Count)
            {
                return null;
            }

            return IonChain[site.PeptideIndex]
                .IncludesAaIndex(peptideStructure.Peptides[site.PeptideIndex], site.AaIndex);
        }

        public static NeutralFragmentIon Simple(Transition transition, TransitionLosses losses)
        {
            return new NeutralFragmentIon(ImmutableList.Singleton(IonOrdinal.FromTransition(transition)), losses);
        }

        public bool IsMs1
        {
            get
            {
                return IsIonTypePrecursor && Losses == null;
            }
        }

        public bool IsIonTypePrecursor
        {
            get
            {
                return IonChain.IsPrecursor;
            }
        }

        /// <summary>
        /// Returns true if all of the peptides in this fragment ion are connected to each other by
        /// at least one crosslinker.
        /// </summary>
        public bool IsConnected(PeptideStructure peptideStructure)
        {
            var peptideIndexes =
                Enumerable.Range(0, IonChain.Count).Where(i => !IonChain[i].IsEmpty).ToHashSet();
            if (peptideIndexes.Count == 0)
            {
                // Empty
                return false;
            }

            var containedCrosslinks = new List<Crosslink>();
            var cleavedCrosslinks = new List<Crosslink>();
            foreach (var crosslink in peptideStructure.Crosslinks)
            {
                bool? isContained = ContainsCrosslink(peptideStructure, crosslink.Sites);
                if (!isContained.HasValue)
                {
                    cleavedCrosslinks.Add(crosslink);
                } 
                else if (isContained.Value)
                {
                    containedCrosslinks.Add(crosslink);
                }
            }

            // Verify that the number of cleaved crosslinkers with a particular name is the same as
            // the number of neutral losses with that name
            if (Losses == null)
            {
                if (cleavedCrosslinks.Count > 0)
                {
                    return false;
                }
            }
            else
            {
                var lossCountsByName = Losses.Losses.GroupBy(loss => loss.PrecursorMod.Name)
                    .ToDictionary(group => group.Key, group => group.Count());
                var cleavedCrosslinksByName = cleavedCrosslinks.GroupBy(crosslink => crosslink.Crosslinker.Name)
                    .ToDictionary(group => group.Key, group => group.Count());
                foreach (var crosslinkerName in peptideStructure.Crosslinks
                             .Select(crosslink => crosslink.Crosslinker.Name).Distinct())
                {
                    lossCountsByName.TryGetValue(crosslinkerName, out int lossCount);
                    cleavedCrosslinksByName.TryGetValue(crosslinkerName, out int cleavedCrosslinkCount);
                    if (lossCount != cleavedCrosslinkCount)
                    {
                        return false;
                    }
                }
            }

            var peptideIndexQueue = new Queue<int>();
            var visitedPeptideIndexes = new HashSet<int>();
            peptideIndexQueue.Enqueue(peptideIndexes.Min());
            while (peptideIndexQueue.Count > 0)
            {
                var peptideIndex = peptideIndexQueue.Dequeue();
                visitedPeptideIndexes.Add(peptideIndex);
                foreach (var crosslink in containedCrosslinks)
                {
                    if (crosslink.Sites.PeptideIndexes.Contains(peptideIndex))
                    {
                        foreach (var site in crosslink.Sites)
                        {
                            if (visitedPeptideIndexes.Add(site.PeptideIndex))
                            {
                                peptideIndexQueue.Enqueue(site.PeptideIndex);
                            }
                        }
                    }
                }
            }

            return visitedPeptideIndexes.SetEquals(peptideIndexes);
        }

        public IEnumerable<Crosslink> GetCleavedCrosslinks(PeptideStructure peptideStructure)
        {
            return peptideStructure.Crosslinks.Where(crosslink =>
                !ContainsCrosslink(peptideStructure, crosslink.Sites).HasValue);
        }

        public bool IsOrphan
        {
            get
            {
                return IonChain.Count > 0 && IonChain[0].IsEmpty;
            }
        }

        public IonChain GetName()
        {
            return IonChain;
        }

        /// <summary>
        /// Returns the text that should be displayed for this in the Targets tree.
        /// </summary>
        public string GetLabel(PeptideStructure peptideStructure, bool includeLossText, bool includeResidues)
        {
            if (IsIonTypePrecursor)
            {
                string label = IonTypeExtension.GetLocalizedString(IonType.precursor);
                if (includeLossText)
                {
                    label += GetTransitionLossesText();
                }

                return label;
            }

            StringBuilder stringBuilder = new StringBuilder();
            // Simple case of two peptides linked together
            if (includeResidues)
            {
                var aminoAcid = IonChain.First().GetAminoAcid(peptideStructure.Peptides[0].Sequence);
                if (aminoAcid.HasValue)
                {
                    stringBuilder.Append(aminoAcid);
                    stringBuilder.Append(@" ");
                }
            }

            stringBuilder.Append(@"[");
            stringBuilder.Append(IonChain);
            stringBuilder.Append(@"]");
            if (includeLossText)
            {
                stringBuilder.Append(GetTransitionLossesText());
            }
            if (includeResidues && IonChain.Count > 1)
            {
                var lastAminoAcid = IonChain[IonChain.Count - 1]
                    .GetAminoAcid(peptideStructure.Peptides[peptideStructure.Peptides.Count - 1].Sequence);
                if (lastAminoAcid.HasValue)
                {
                    stringBuilder.Append(@" ");
                    stringBuilder.Append(lastAminoAcid);
                }
            }
            return stringBuilder.ToString();
        }
        private string GetTransitionLossesText()
        {
            if (Losses == null)
            {
                return string.Empty;
            }

            return @" -" + Math.Round(Losses.Mass, 1);
        }

        public int CountFragmentationEvents()
        {
            int result = 0;
            if (Losses != null)
            {
                result += Losses.Losses.Count;
            }

            foreach (var part in IonChain)
            {
                switch (part.Type)
                {
                    case IonType.precursor:
                    case null:
                        break;
                    default:
                        result++;
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns true if this ion is compatible with the set of crosslinks in the PeptideStructure.
        /// In order to be compatible, all crosslinks must either be completely contained within this ion, or
        /// completely excluded.
        /// This method only considers CrosslinkSites whose PeptideIndex is less than IonType.Count.
        /// This is to enable filtering out impossible partial ions while constructing larger ions using
        /// <see cref="SingleFragmentIon.Prepend"/>.
        /// Note that this does not check whether all of the fragment ions are actually connected to each other.
        /// For that, you would need to also check <see cref="IsConnected"/>.
        /// </summary>
        public bool IsAllowed(PeptideStructure peptideStructure)
        {
            foreach (var crosslink in peptideStructure.Crosslinks)
            {
                if (crosslink.Crosslinker.HasLoss)
                {
                    // A crosslinker with losses is cleavable to assume it is valid for now
                    continue;
                }
                if (!ContainsCrosslink(peptideStructure, crosslink.Sites).HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks how many of the sites are attached to this fragment ion.
        /// Possible return values:
        /// False: None of the sites are contained in this ion
        /// True: All of the sites are contained in this ion
        /// Null: Some of the sites are contained in this ion, and some are not.
        /// 
        /// This method only considers sites whose <see cref="CrosslinkSite.PeptideIndex"/> is less than
        /// the length of this <see cref="IonChain"/>.
        /// </summary>
        public bool? ContainsCrosslink(PeptideStructure peptideStructure, IEnumerable<CrosslinkSite> crosslinkSites)
        {
            int countIncluded = 0;
            int countExcluded = 0;
            foreach (var site in crosslinkSites)
            {
                switch (IncludesSite(peptideStructure, site))
                {
                    case true:
                        countIncluded++;
                        break;
                    case false:
                        countExcluded++;
                        break;
                }
            }

            if (countIncluded == 0)
            {
                return false;
            }

            if (countExcluded == 0)
            {
                return true;
            }

            return null;
        }

        public int CompareTo(NeutralFragmentIon other)
        {
            if (IsIonTypePrecursor)
            {
                if (!other.IsIonTypePrecursor)
                {
                    return -1;
                }
            }
            else if (other.IsIonTypePrecursor)
            {
                return 1;
            }

            for (int i = 0; i < Math.Min(IonChain.Count, other.IonChain.Count); i++)
            {
                int result = IonChain[i].CompareTo(other.IonChain[i]);
                if (result == 0)
                {
                    result = Comparer<double?>.Default.Compare(Losses?.Mass, other.Losses?.Mass);
                }
                if (result != 0)
                {
                    return result;
                }
            }

            return IonChain.Count.CompareTo(other.IonChain.Count);
        }

        public Transition MakeTransition(TransitionGroup group, Adduct adduct)
        {
            var firstPart = IonChain[0];
            IonType ionType = firstPart.Type ?? IonType.precursor;
            int cleavageOffset;
            if (ionType == IonType.precursor)
            {
                cleavageOffset = group.Peptide.Sequence.Length - 1;
            }
            else
            {
                cleavageOffset = Transition.OrdinalToOffset(ionType, firstPart.Ordinal, group.Peptide.Sequence.Length);
            }
            return new Transition(group, ionType, cleavageOffset, 0, adduct, null);
        }

        public ComplexFragmentIon MakeChargedIon(TransitionGroup group, Adduct adduct, ExplicitMods explicitMods)
        {
            return new ComplexFragmentIon(MakeTransition(group, adduct), this, explicitMods);
        }

        public NeutralFragmentIon AddLosses(TransitionLosses losses)
        {
            if (losses == null)
            {
                return this;
            }
            TransitionLosses newLosses;
            if (Losses == null)
            {
                newLosses = losses;
            }
            else
            {
                newLosses = new TransitionLosses(Losses.Losses.Concat(losses.Losses).ToList(), losses.MassType);
            }

            return ChangeProp(ImClone(this), im => im.Losses = newLosses);
        }
    }
}
