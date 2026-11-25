/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Lib
{
    /// <summary>
    /// The level a global (experiment-wide) q-value applies to.
    /// </summary>
    public enum GlobalQValueLevel
    {
        protein,
        peptide,
        precursor
    }

    /// <summary>
    /// A protein group a library entry belongs to, with its global statistics when the library has them.
    /// </summary>
    public class LibraryProteinGroup : Immutable
    {
        public LibraryProteinGroup(string name, double? qValue, double? pep)
        {
            Name = name;
            QValue = qValue;
            Pep = pep;
        }

        /// <summary>
        /// The group's accessions, joined by ';' when the group has more than one.
        /// </summary>
        public string Name { get; }
        public double? QValue { get; }
        public double? Pep { get; }
    }

    /// <summary>
    /// Experiment-wide statistics for one library entry. A null value is one the library does not report.
    /// </summary>
    public class LibraryGlobalScores : Immutable
    {
        public static readonly LibraryGlobalScores EMPTY =
            new LibraryGlobalScores(null, null, null, null, Enumerable.Empty<LibraryProteinGroup>());

        /// <summary>
        /// The scores of an entry known only by its protein (or molecule list) name: that one
        /// group without values, or <see cref="EMPTY"/> for no name.
        /// </summary>
        public static LibraryGlobalScores ForProtein(string protein)
        {
            if (string.IsNullOrEmpty(protein))
                return EMPTY;
            return new LibraryGlobalScores(null, null, null, null,
                ImmutableList.Singleton(new LibraryProteinGroup(protein, null, null)));
        }

        public LibraryGlobalScores(double? precursorQValue, double? precursorPep, double? peptideQValue,
            double? peptidePep, IEnumerable<LibraryProteinGroup> proteinGroups)
        {
            PrecursorQValue = precursorQValue;
            PrecursorPep = precursorPep;
            PeptideQValue = peptideQValue;
            PeptidePep = peptidePep;
            ProteinGroups = ImmutableList.ValueOf(proteinGroups);
            BestProteinGroup = ProteinGroups.Where(group => group.QValue.HasValue)
                .OrderBy(group => group.QValue.Value).FirstOrDefault();
        }

        public double? PrecursorQValue { get; }
        public double? PrecursorPep { get; }
        public double? PeptideQValue { get; }
        public double? PeptidePep { get; }
        public ImmutableList<LibraryProteinGroup> ProteinGroups { get; }

        /// <summary>
        /// The protein group with the lowest q-value, which is the one that decides whether the
        /// entry passes a protein-level cutoff. Found once: the filters ask for it per entry.
        /// </summary>
        public LibraryProteinGroup BestProteinGroup { get; }

        /// <summary>
        /// The q-value at <paramref name="level"/>, or null when the library does not report one.
        /// An entry with no protein group has no protein q-value, so it fails any protein cutoff.
        /// </summary>
        public double? GetQValue(GlobalQValueLevel level)
        {
            switch (level)
            {
                case GlobalQValueLevel.precursor:
                    return PrecursorQValue;
                case GlobalQValueLevel.peptide:
                    return PeptideQValue;
                default:
                    return BestProteinGroup?.QValue;
            }
        }

        public bool PassesCutoff(GlobalQValueLevel level, double cutoff)
        {
            var qValue = GetQValue(level);
            return qValue.HasValue && qValue.Value <= cutoff;
        }
    }

    /// <summary>
    /// Accepts the library entries whose global q-value at <see cref="Level"/> is at or below <see cref="Cutoff"/>.
    /// </summary>
    public class GlobalQValueFilter : Immutable
    {
        public GlobalQValueFilter(GlobalQValueLevel level, double cutoff)
        {
            Level = level;
            Cutoff = cutoff;
        }

        [Track]
        public GlobalQValueLevel Level { get; }
        [Track]
        public double Cutoff { get; }

        public bool Accepts(LibraryGlobalScores globalScores)
        {
            return globalScores.PassesCutoff(Level, Cutoff);
        }

        /// <summary>
        /// Removes the precursors whose entry in a library reporting <see cref="Level"/> fails the
        /// cutoff, and the peptides left with no precursors. A node that loses children stops
        /// auto-managing them, so a later settings change does not add them back. A precursor that
        /// no such library contains follows its peptide. Only <paramref name="peptideGroups"/> are filtered.
        /// </summary>
        public SrmDocument FilterDocument(SrmDocument document, IEnumerable<PeptideGroupDocNode> peptideGroups)
        {
            var settings = document.Settings;
            var libraries = settings.PeptideSettings.Libraries.Libraries
                .Where(library => library != null && library.GlobalQValueLevels.Contains(Level)).ToList();
            if (libraries.Count == 0)
                return document;

            var groupIndexes = new HashSet<int>(peptideGroups.Select(nodeGroup => nodeGroup.Id.GlobalIndex));
            bool anyChanged = false;
            var newPeptideGroups = new List<DocNode>();
            foreach (var nodePepGroup in document.MoleculeGroups)
            {
                if (!groupIndexes.Contains(nodePepGroup.Id.GlobalIndex))
                {
                    newPeptideGroups.Add(nodePepGroup);
                    continue;
                }
                bool peptideRemoved = false;
                bool peptideChanged = false;
                var newPeptides = new List<DocNode>();
                foreach (var nodePep in nodePepGroup.Molecules)
                {
                    var keptGroups = GetKeptTransitionGroups(libraries, settings, nodePep);
                    if (keptGroups.Count == nodePep.Children.Count)
                    {
                        newPeptides.Add(nodePep);
                    }
                    else if (keptGroups.Count == 0)
                    {
                        peptideRemoved = true;
                    }
                    else
                    {
                        newPeptides.Add(nodePep.ChangeChildren(keptGroups).ChangeAutoManageChildren(false));
                        peptideChanged = true;
                    }
                }
                if (!peptideRemoved && !peptideChanged)
                {
                    newPeptideGroups.Add(nodePepGroup);
                    continue;
                }
                var newPeptideGroup = nodePepGroup.ChangeChildren(newPeptides);
                if (peptideRemoved)
                    newPeptideGroup = newPeptideGroup.ChangeAutoManageChildren(false);
                newPeptideGroups.Add(newPeptideGroup);
                anyChanged = true;
            }
            return anyChanged ? (SrmDocument) document.ChangeChildren(newPeptideGroups) : document;
        }

        /// <summary>
        /// The precursors of <paramref name="nodePep"/> to keep. A precursor no library contains,
        /// such as a heavy-labeled one, follows its peptide: it is kept when one of the peptide's
        /// library precursors passes, or when none of them is in a library.
        /// </summary>
        private List<DocNode> GetKeptTransitionGroups(IList<Library> libraries, SrmSettings settings,
            PeptideDocNode nodePep)
        {
            var verdicts = nodePep.TransitionGroups
                .Select(nodeGroup => Tuple.Create(nodeGroup, Accepts(libraries, nodeGroup.GetLibKey(settings, nodePep))))
                .ToList();
            bool keepUnlisted = verdicts.All(verdict => !verdict.Item2.HasValue) ||
                                verdicts.Any(verdict => verdict.Item2 == true);
            return verdicts.Where(verdict => verdict.Item2 ?? keepUnlisted)
                .Select(verdict => (DocNode) verdict.Item1).ToList();
        }

        /// <summary>
        /// Whether a library that reports <see cref="Level"/> has an entry for the key that passes,
        /// or null when none of those libraries contains the key.
        /// </summary>
        private bool? Accepts(IList<Library> libraries, LibKey key)
        {
            bool inLibrary = false;
            foreach (var library in libraries)
            {
                if (!library.Contains(key))
                    continue;
                if (Accepts(library.GetGlobalScores(key)))
                    return true;
                inLibrary = true;
            }
            return inLibrary ? false : null;
        }
    }
}
