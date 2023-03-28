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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// The set of peptides and modifications which are attached to a main peptide with crosslinkers.
    /// This class does not include the main peptide itself, or the modifications which are on the main peptide.
    /// </summary>
    public class CrosslinkStructure : Immutable
    {
        public static readonly CrosslinkStructure EMPTY = new CrosslinkStructure(ImmutableList<Peptide>.EMPTY,
            ImmutableList<ExplicitMods>.EMPTY, ImmutableList<Crosslink>.EMPTY);

        public CrosslinkStructure(ICollection<Peptide> peptides, IEnumerable<Crosslink> crosslinks)
            : this(peptides, peptides.Select(peptide => new ExplicitMods(peptide, new ExplicitMod[0], null)),
                crosslinks)
        {

        }
        public CrosslinkStructure(IEnumerable<Peptide> peptides, IEnumerable<ExplicitMods> explicitModsList, IEnumerable<Crosslink> crosslinks)
        {
            LinkedPeptides = ImmutableList.ValueOf(peptides);
            if (explicitModsList == null)
            {
                explicitModsList = LinkedPeptides.Select(MakeEmptyExplicitMods);
            }
            LinkedExplicitMods = ImmutableList.ValueOf(explicitModsList);
            if (LinkedExplicitMods.Count != LinkedPeptides.Count)
            {
                throw new ArgumentException(@"Peptide count does not match");
            }

            if (LinkedExplicitMods.Contains(null))
            {
                LinkedExplicitMods = ImmutableList.ValueOf(Enumerable.Range(0, LinkedPeptides.Count)
                    .Select(i => LinkedExplicitMods[i] ?? MakeEmptyExplicitMods(LinkedPeptides[i])));
            }
            if (LinkedExplicitMods.Any(mod => mod != null && mod.HasCrosslinks))
            {
                throw new ArgumentException(@"Cannot nest crosslinks");
            }
            Crosslinks = ImmutableList.ValueOfOrEmpty(crosslinks);
        }

        public static CrosslinkStructure ToPeptide(Peptide peptide, ExplicitMods explicitMods, StaticMod mod, int aaIndex1, int aaIndex2)
        {
            return new CrosslinkStructure(ImmutableList.Singleton(peptide), ImmutableList.Singleton(explicitMods),
                ImmutableList.Singleton(
                    new Crosslink(mod,
                        new[] {new CrosslinkSite(0, aaIndex1), new CrosslinkSite(1, aaIndex2),})
                ));
        }

        public bool IsEmpty
        {
            get
            {
                return LinkedPeptides.Count == 0 && Crosslinks.Count == 0;
            }
        }

        /// <summary>
        /// List of peptides that are linked to the main peptide.
        /// Not this this list does not include the main peptide itself, so you should
        /// never try indexing into this list using a <see cref="CrosslinkSite.PeptideIndex"/>.
        /// The class <see cref="PeptideStructure"/> is designed to work better with crosslink sites.
        /// </summary>
        [TrackChildren]
        public ImmutableList<Peptide> LinkedPeptides { get; private set; }
        /// <summary>
        /// The modifications on each of the LinkedPeptides.
        /// LinkedPeptides.Count is always equal to LinkedExplicitMods.Count
        /// </summary>
        [TrackChildren]
        public ImmutableList<ExplicitMods> LinkedExplicitMods { get; private set; }
        [TrackChildren]
        public ImmutableList<Crosslink> Crosslinks { get; private set; }

        protected bool Equals(CrosslinkStructure other)
        {
            return LinkedPeptides.Equals(other.LinkedPeptides) && LinkedExplicitMods.Equals(other.LinkedExplicitMods) && Crosslinks.Equals(other.Crosslinks);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CrosslinkStructure) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = LinkedPeptides.GetHashCode();
                hashCode = (hashCode * 397) ^ LinkedExplicitMods.GetHashCode();
                hashCode = (hashCode * 397) ^ Crosslinks.GetHashCode();
                return hashCode;
            }
        }

        public bool HasCrosslinks
        {
            get
            {
                return Crosslinks.Count > 0;
            }
        }

        public MoleculeMassOffset GetNeutralFormula(SrmSettings settings, IsotopeLabelType labelType)
        {
            List<MoleculeMassOffset> parts = new List<MoleculeMassOffset>();
            for (int i = 0; i < LinkedPeptides.Count; i++)
            {
                IPrecursorMassCalc massCalc = settings.GetPrecursorCalc(labelType, LinkedExplicitMods[i]);
                parts.Add(new MoleculeMassOffset(Molecule.Parse(massCalc.GetMolecularFormula(LinkedPeptides[i].Sequence))));
            }

            foreach (var crosslink in Crosslinks)
            {
                parts.Add(crosslink.Crosslinker.GetMoleculeMassOffset());
            }

            return FragmentedMolecule.SumMoleculeMassOffsets(parts);
        }

        public bool IsConnected()
        {
            var connectedPeptideIndexes = ConnectedPeptideIndexes();
            return Enumerable.Range(1, LinkedPeptides.Count).All(connectedPeptideIndexes.Contains);
        }

        private HashSet<int> ConnectedPeptideIndexes()
        {
            var connectedPeptides = new HashSet<int>{0};
            IList<Crosslink> crosslinks = Crosslinks;
            while (true)
            {
                var nextCrosslinks = new List<Crosslink>();
                foreach (var crosslink in crosslinks)
                {
                    if (crosslink.Sites.PeptideIndexes.Any(connectedPeptides.Contains))
                    {
                        connectedPeptides.UnionWith(crosslink.Sites.PeptideIndexes);
                    }
                    else
                    {
                        nextCrosslinks.Add(crosslink);
                    }
                }

                if (nextCrosslinks.Count == crosslinks.Count)
                {
                    return connectedPeptides;
                }

                crosslinks = nextCrosslinks;
            }
        }

        public CrosslinkStructure RemoveCrosslinksAtSite(CrosslinkSite site)
        {
            return new CrosslinkStructure(LinkedPeptides, LinkedExplicitMods, Crosslinks.Where(link=>!link.Sites.Contains(site)));
        }

        public CrosslinkStructure RemoveDisconnectedPeptides()
        {
            int totalPeptideCount = LinkedPeptides.Count + 1;
            var retainedPeptideIndexes = ConnectedPeptideIndexes().OrderBy(i => i).ToList();
            var newIndexMap = new int?[totalPeptideCount];
            for (int newPeptideIndex = 0; newPeptideIndex < retainedPeptideIndexes.Count; newPeptideIndex++)
            {
                newIndexMap[retainedPeptideIndexes[newPeptideIndex]] = newPeptideIndex;
            }

            var newCrosslinks = new List<Crosslink>();
            foreach (var crosslink in Crosslinks)
            {
                var newSites = ImmutableList.ValueOf(crosslink.Sites.Sites
                    .Where(site => newIndexMap[site.PeptideIndex].HasValue).Select(site =>
                        new CrosslinkSite(newIndexMap[site.PeptideIndex].Value, site.AaIndex)));
                if (newSites.Count == 0)
                {
                    continue;
                }
                Assume.AreEqual(newSites.Count, crosslink.Sites.Count);
                newCrosslinks.Add(new Crosslink(crosslink.Crosslinker, newSites));
            }

            var newLinkedPeptides = new List<Peptide>();
            var newExplicitModsList = new List<ExplicitMods>();
            for (int i = 0; i < LinkedPeptides.Count; i++)
            {
                if (!newIndexMap[i].HasValue)
                {
                    continue;
                }
                newLinkedPeptides.Add(LinkedPeptides[i]);
                newExplicitModsList.Add(LinkedExplicitMods[i]);
            }
            return new CrosslinkStructure(newLinkedPeptides, newExplicitModsList, newCrosslinks);
        }

        public CrosslinkStructure ChangeGlobalMods(SrmSettings settingsNew)
        {
            if (IsEmpty)
            {
                return this;
            }
            var crosslinkModifications = settingsNew.PeptideSettings.Modifications.StaticModifications
                .Where(mod => mod.IsCrosslinker).ToDictionary(mod => mod.Name);
            var newCrosslinks = new List<Crosslink>();
            foreach (var crosslink in Crosslinks)
            {
                if (!crosslinkModifications.TryGetValue(crosslink.Crosslinker.Name, out var newCrosslinker))
                {
                    continue;
                }

                if (crosslink.Crosslinker.Equivalent(newCrosslinker))
                {
                    newCrosslinks.Add(crosslink);
                }
                else
                {
                    newCrosslinks.Add(new Crosslink(newCrosslinker, crosslink.Sites));
                }
            }

            var newExplicitMods = new List<ExplicitMods>();
            foreach (var explicitMods in LinkedExplicitMods)
            {
                newExplicitMods.Add(explicitMods.ChangeGlobalMods(settingsNew));
            }

            if (ArrayUtil.ReferencesEqual(newCrosslinks, Crosslinks) &&
                ArrayUtil.ReferencesEqual(newExplicitMods, LinkedExplicitMods))
            {
                return this;
            }

            return new CrosslinkStructure(LinkedPeptides, newExplicitMods, newCrosslinks).RemoveDisconnectedPeptides();
        }

        public static ExplicitMods MakeEmptyExplicitMods(Peptide peptide)
        {
            return new ExplicitMods(peptide, ImmutableList.Empty<ExplicitMod>(), null);
        }

        public class DefaultValuesEmpty : DefaultValues
        {
            protected override IEnumerable<object> _values
            {
                get
                {
                    yield return EMPTY;
                }
            }
        }
    }
}