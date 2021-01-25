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
        public CrosslinkStructure(IEnumerable<Peptide> peptides, IEnumerable<ExplicitMods> explicitModsList, IEnumerable<Crosslink> crosslinks)
        {
            LinkedPeptides = ImmutableList.ValueOf(peptides);
            if (explicitModsList == null)
            {
                LinkedExplicitMods = ImmutableList.ValueOf(new ExplicitMods[LinkedPeptides.Count]);
            }
            else
            {
                LinkedExplicitMods = ImmutableList.ValueOf(explicitModsList);
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
        public ImmutableList<Peptide> LinkedPeptides { get; private set; }
        /// <summary>
        /// The modifications on each of the LinkedPeptides.
        /// LinkedPeptides.Count is always equal to LinkedExplicitMods.Count
        /// </summary>
        public ImmutableList<ExplicitMods> LinkedExplicitMods { get; private set; }
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
            MoleculeMassOffset result = MoleculeMassOffset.EMPTY;
            for (int i = 0; i < LinkedPeptides.Count; i++)
            {
                IPrecursorMassCalc massCalc = settings.GetPrecursorCalc(labelType, LinkedExplicitMods[i]);
                result = result.Plus(new MoleculeMassOffset(Molecule.Parse(massCalc.GetMolecularFormula(LinkedPeptides[i].Sequence)), 0, 0));
            }

            foreach (var crosslink in Crosslinks)
            {
                result = result.Plus(crosslink.Crosslinker.GetMoleculeMassOffset());
            }

            return result;
        }

        public bool IsConnected()
        {
            var allPeptideIndexes = Crosslinks.SelectMany(link => link.Sites.PeptideIndexes).ToHashSet();
            if (allPeptideIndexes.Count == 0)
            {
                return false;
            }

            if (allPeptideIndexes.Count != allPeptideIndexes.Max() + 1)
            {
                return false;
            }

            if (allPeptideIndexes.Min() != 0)
            {
                return false;
            }
            var visitedPeptides = new HashSet<int> { 0 };
            IReadOnlyList<Crosslink> remainingCrosslinks = Crosslinks;
            while (true)
            {
                var nextQueue = new List<Crosslink>();
                foreach (var crosslink in remainingCrosslinks)
                {
                    if (crosslink.Sites.PeptideIndexes.Any(visitedPeptides.Contains))
                    {
                        visitedPeptides.UnionWith(crosslink.Sites.PeptideIndexes);
                    }
                    else
                    {
                        nextQueue.Add(crosslink);
                    }
                }

                if (nextQueue.Count == 0)
                {
                    return true;
                }

                if (nextQueue.Count == remainingCrosslinks.Count)
                {
                    return false;
                }

                remainingCrosslinks = nextQueue;
            }
        }
    }
}