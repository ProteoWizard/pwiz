/*
 * Original author: Matt Chambers <matt.chambers42 .@. gmail.com>
 *
 * Copyright 2022
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Proteome
{
    public class ProteinAssociation
    {
        private SrmDocument _document;
        private StringSearch _peptideTrie;
        private Dictionary<string, List<PeptideDocNode>> _peptideToPath;
        private Dictionary<ReferenceValue<PeptideDocNode>, List<IProteinRecord>> _peptideToProteins;
        private MappingResultsInternal _results, _finalResults;
        private HashSet<ReferenceValue<PeptideDocNode>> _peptidesRemovedByFilters;

        private static IEqualityComparer<PeptideDocNode> ReferenceEqualityComparer = ReferenceValue.EQUALITY_COMPARER;
        private IDictionary<string, ProteinMetadata> _proteinToMetadata { get; set; }

        internal class ProteinOrGeneGroupResultCache
        {
            public MappingResultsInternal Results { get; set; }
            public Dictionary<IProteinRecord, PeptideAssociationGroup> PeptideGroupByProteinOrGeneGroup { get; set; }
            public Dictionary<ReferenceValue<PeptideDocNode>, List<IProteinRecord>> PeptideToProteinOrGeneGroup { get; set; }
        }

        private Dictionary<bool, ProteinOrGeneGroupResultCache> _proteinOrGeneGroupResultCacheByGeneLevel;

        public IDictionary<IProteinRecord, PeptideAssociationGroup> AssociatedProteins { get; private set; }
        public IDictionary<IProteinRecord, PeptideAssociationGroup> ParsimoniousProteins { get; private set; }
        public int PeptidesRemovedByFiltersCount => _peptideToProteins?.Count ?? 0;

        public IMappingResults Results
        {
            get => _results;
        }

        public IMappingResults FinalResults
        {
            get => _finalResults;
        }

        public enum SharedPeptides
        {
            DuplicatedBetweenProteins,
            AssignedToFirstProtein,
            AssignedToBestProtein, // with the most unique peptides, or all best proteins in case of ties
            Removed, // (peptides must be unique to a single protein)
        }

        public ProteinAssociation(SrmDocument document, ILongWaitBroker broker)
        {
            _document = document;

            ListPeptidesForMatching(broker);
        }

        private void ResetMapping()
        {
            _proteinOrGeneGroupResultCacheByGeneLevel = new Dictionary<bool, ProteinOrGeneGroupResultCache>()
            {
                { false, null },
                { true, null }
            };
            _finalResults = null;
            _peptideToProteins = null;
            _peptidesRemovedByFilters = null;
            _proteinToMetadata = null;
            AssociatedProteins = null;
            ParsimoniousProteins = null;
        }

        public void UseFastaFile(string file, ILongWaitBroker broker)
        {
            if (!File.Exists(file))
                return;

            using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fastaSource = new FastaSource(stream);
            UseProteinSource(fastaSource, _document.Settings.PeptideSettings.Enzyme, broker);
        }

        // find matches using the background proteome
        public void UseBackgroundProteome(BackgroundProteome backgroundProteome, ILongWaitBroker broker)
        {
            if (backgroundProteome.Equals(BackgroundProteome.NONE))
                throw new InvalidOperationException(Resources.AssociateProteinsDlg_UseBackgroundProteome_No_background_proteome_defined);

            var proteome = backgroundProteome;
            UseProteinSource(new BackgroundProteomeSource(broker.CancellationToken, proteome), _document.Settings.PeptideSettings.Enzyme, broker);
        }

        public void UseProteinSource(IProteinSource proteinSource, Enzyme enzyme, ILongWaitBroker broker)
        {
            ResetMapping();
            var proteinAssociations = FindProteinMatches(proteinSource, enzyme, broker);
            if (proteinAssociations != null)
            {
                AssociatedProteins = proteinAssociations;
            }
        }

        private Dictionary<IProteinRecord, PeptideAssociationGroup> FindProteinMatches(IProteinSource proteinSource, Enzyme enzyme, ILongWaitBroker broker)
        {
            var localResults = new MappingResultsInternal();
            var peptideToProteins = new Dictionary<ReferenceValue<PeptideDocNode>, List<IProteinRecord>>();

            var proteinAssociations = new Dictionary<IProteinRecord, PeptideAssociationGroup>();
            int maxProgressValue = 0;
            broker.Message = ProteomeResources.AssociateProteinsDlg_FindProteinMatchesWithFasta_Finding_peptides_in_FASTA_file;
            var proteinPeptideMatchesDictionary = new Dictionary<int, ProteinPeptideMatches>();
            var allEnzymaticPeptides = new HashSet<string>();

            ParallelEx.ForEach(proteinSource.Proteins.Select(Tuple.Create<IProteinRecord, int>), fastaRecordIndex =>
            {
                var fastaRecord = fastaRecordIndex.Item1;
                int progressValue = fastaRecord.Progress;
                var fasta = fastaRecord.Sequence;
                ProteinPeptideMatches proteinPeptideMatches = new ProteinPeptideMatches(fastaRecord, enzyme,
                    _peptideTrie.FindAll(fasta.Sequence).Select(result => result.Keyword).Distinct());
                lock (localResults)
                {
                    if (broker.IsCanceled)
                        return;

                    if (progressValue > maxProgressValue && progressValue <= 100)
                    {
                        broker.ProgressValue = progressValue;
                        maxProgressValue = Math.Max(maxProgressValue, progressValue);
                    }

                    proteinPeptideMatchesDictionary.Add(fastaRecordIndex.Item2, proteinPeptideMatches);
                    allEnzymaticPeptides.UnionWith(proteinPeptideMatches.EnzymaticPeptides);
                }
            });

            var proteinPeptideMatchesList = proteinPeptideMatchesDictionary
                .OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
            var peptideAssociationGroups = new PeptideAssociationGroup[proteinPeptideMatchesList.Count];
            ParallelEx.For(0, proteinPeptideMatchesList.Count, iProtein =>
            {
                var proteinPeptideMatches = proteinPeptideMatchesList[iProtein];
                var matches = new List<PeptideDocNode>();

                foreach (var peptideSequence in proteinPeptideMatches.CandidatePeptides)
                {
                    if (!proteinPeptideMatches.EnzymaticPeptides.Contains(peptideSequence))
                    {
                        // The peptide could not have been digested by the enzyme from this protein sequence.
                        // Only skip it if there is at least one protein that could produce the digested peptide
                        if (allEnzymaticPeptides.Contains(peptideSequence))
                        {
                            continue;
                        }
                    }

                    matches.AddRange(_peptideToPath[peptideSequence]);
                }
                if (matches.Count > 0)
                {
                    peptideAssociationGroups[iProtein] = new PeptideAssociationGroup(matches);
                }
            });

            for (int iProtein = 0; iProtein < proteinPeptideMatchesList.Count; iProtein++)
            {
                var fastaRecord = proteinPeptideMatchesList[iProtein].ProteinRecord;
                var peptideAssociationGroup = peptideAssociationGroups[iProtein];
                if (peptideAssociationGroup == null)
                {
                    ++localResults.ProteinsUnmapped;
                }
                else
                {
                    ++localResults.ProteinsMapped;
                    localResults.FinalPeptideCount += peptideAssociationGroup.Peptides.Count;
                    proteinAssociations[fastaRecord] = peptideAssociationGroup;
                    foreach (var match in peptideAssociationGroup.Peptides)
                    {
                        if (peptideToProteins.TryGetValue(match, out var list))
                        {
                            list.Add(fastaRecord);
                        }
                        else
                        {
                            peptideToProteins.Add(match, new List<IProteinRecord>{fastaRecord});
                        }
                    }
                }
            }

            if (broker.IsCanceled)
                return null;

            Assume.IsTrue(localResults.ProteinsMapped + localResults.ProteinsUnmapped > 0);

            var distinctPeptideDocNodes = _peptideToPath.SelectMany(kvp => kvp.Value);
            int distinctTargetPeptideCount = distinctPeptideDocNodes.Where(p => !p.IsDecoy).Select(p => p.Peptide.Target).Distinct().Count();
            _peptideToProteins = peptideToProteins;
            _results = localResults;
            _results.PeptidesMapped = peptideToProteins.Keys.Select(p => p.Value.Peptide.Target).Distinct().Count();
            _results.PeptidesUnmapped = distinctTargetPeptideCount - _results.PeptidesMapped;
            _results.FinalProteinCount = proteinAssociations.Count;

            _proteinToMetadata = new Dictionary<string, ProteinMetadata>();
            foreach (var kvp in proteinAssociations)
                _proteinToMetadata[kvp.Key.Sequence.Name] = kvp.Key.Metadata;

            return proteinAssociations;
        }

        private class ProteinPeptideMatches
        {
            private static readonly DigestSettings lenientDigestSettings = new DigestSettings(int.MaxValue, false);
            public ProteinPeptideMatches(IProteinRecord proteinRecord, Enzyme enzyme, IEnumerable<string> candidatePeptides)
            {
                ProteinRecord = proteinRecord;
                CandidatePeptides = ImmutableList.ValueOf(candidatePeptides);
                if (CandidatePeptides.Count > 0)
                {
                    var maxPeptideLength = CandidatePeptides.Max(peptide => peptide.Length);
                    EnzymaticPeptides = enzyme
                        .Digest(proteinRecord.Sequence, lenientDigestSettings, maxPeptideLength)
                        .Select(peptide => peptide.Sequence).Intersect(CandidatePeptides).ToHashSet();
                }
                else
                {
                    EnzymaticPeptides = Array.Empty<string>();
                }
            }

            public IProteinRecord ProteinRecord { get; }
            public ImmutableList<string> CandidatePeptides { get; }
            public ICollection<string> EnzymaticPeptides { get; }
        }

        [XmlRoot("protein_association")]
        public class ParsimonySettings : Immutable, IXmlSerializable, IValidating
        {
            public static ParsimonySettings DEFAULT = new ParsimonySettings() { MinPeptidesPerProtein = 1 };

            public ParsimonySettings(bool groupProteins, bool geneLevel, bool findMinimalProteinList, bool removeSubsetProteins, SharedPeptides sharedPeptides, int minPeptidesPerProtein)
            {
                GroupProteins = groupProteins;
                GeneLevelParsimony = geneLevel;
                FindMinimalProteinList = findMinimalProteinList;
                RemoveSubsetProteins = removeSubsetProteins;
                SharedPeptides = sharedPeptides;
                MinPeptidesPerProtein = minPeptidesPerProtein;
            }

            [Track(ignoreDefaultParent:true)]
            public bool GroupProteins { get; private set; }

            [Track(defaultValues: typeof(DefaultValuesFalse))]
            public bool GeneLevelParsimony { get; private set; }

            [Track(ignoreDefaultParent: true)]
            public bool FindMinimalProteinList { get; private set; }

            [Track(ignoreDefaultParent: true)]
            public bool RemoveSubsetProteins { get; private set; }

            [Track(ignoreDefaultParent: true)]
            public SharedPeptides SharedPeptides { get; private set; }

            [Track(ignoreDefaultParent: true)]
            public int MinPeptidesPerProtein { get; private set; }

            #region object overrides
            public bool Equals(ParsimonySettings obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj.GroupProteins == GroupProteins &&
                       obj.GeneLevelParsimony == GeneLevelParsimony &&
                       obj.FindMinimalProteinList == FindMinimalProteinList &&
                       obj.RemoveSubsetProteins == RemoveSubsetProteins &&
                       obj.SharedPeptides == SharedPeptides &&
                       obj.MinPeptidesPerProtein == MinPeptidesPerProtein;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != typeof(ParsimonySettings)) return false;
                return Equals((ParsimonySettings)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = MinPeptidesPerProtein;
                    result = (result * 397) ^ GroupProteins.GetHashCode();
                    result = (result * 397) ^ GeneLevelParsimony.GetHashCode();
                    result = (result * 397) ^ FindMinimalProteinList.GetHashCode();
                    result = (result * 397) ^ RemoveSubsetProteins.GetHashCode();
                    result = (result * 397) ^ SharedPeptides.GetHashCode();
                    return result;
                }
            }

            public void Validate()
            {
                if (MinPeptidesPerProtein < 0)
                    throw new InvalidDataException(string.Format(
                        Resources.ParsimonySettings_Validate_The_value__0__for__1__is_not_valid__it_must_be_greater_than_or_equal_to__2__,
                        MinPeptidesPerProtein, PropertyNames.ParsimonySettings_MinPeptidesPerProtein, 0));
            }

            #endregion

            #region Implementation of IXmlSerializable
            private enum Attr
            {
                min_peptides_per_protein,
                group_proteins,
                gene_level_parsimony,
                find_minimal_protein_list,
                remove_subset_proteins,
                shared_peptides
            }

            public XmlSchema GetSchema()
            {
                return null;
            }

            public void ReadXml(XmlReader reader)
            {
                MinPeptidesPerProtein = reader.GetIntAttribute(Attr.min_peptides_per_protein, 1);
                GroupProteins = reader.GetBoolAttribute(Attr.group_proteins);
                GeneLevelParsimony = reader.GetBoolAttribute(Attr.gene_level_parsimony);
                FindMinimalProteinList = reader.GetBoolAttribute(Attr.find_minimal_protein_list);
                RemoveSubsetProteins = reader.GetBoolAttribute(Attr.remove_subset_proteins);
                SharedPeptides = reader.GetEnumAttribute(Attr.shared_peptides, SharedPeptides.DuplicatedBetweenProteins);

                bool empty = reader.IsEmptyElement;
                reader.Read();
                if (!empty)
                {
                    reader.ReadEndElement();
                }

                Validate();
            }

            public void WriteXml(XmlWriter writer)
            {
                writer.WriteAttribute(Attr.min_peptides_per_protein, MinPeptidesPerProtein, 1);
                writer.WriteAttribute(Attr.group_proteins, GroupProteins, false);
                writer.WriteAttribute(Attr.gene_level_parsimony, GeneLevelParsimony, false);
                writer.WriteAttribute(Attr.find_minimal_protein_list, FindMinimalProteinList, false);
                writer.WriteAttribute(Attr.remove_subset_proteins, RemoveSubsetProteins, false);
                writer.WriteAttribute(Attr.shared_peptides, SharedPeptides, SharedPeptides.DuplicatedBetweenProteins);
            }
            private ParsimonySettings()
            {
            }

            public static ParsimonySettings Deserialize(XmlReader reader)
            {
                return reader.Deserialize(new ParsimonySettings());
            }
            #endregion
        }

        public interface IMappingResults
        {
            int ProteinsMapped { get; }
            int ProteinsUnmapped { get; }
            int PeptidesMapped { get; }
            int PeptidesUnmapped { get; }

            bool GroupProteins { get; }
            bool GeneLevelParsimony { get; }
            bool FindMinimalProteinList { get; }
            bool RemoveSubsetProteins { get; }
            SharedPeptides SharedPeptides { get; }
            int MinPeptidesPerProtein { get; }
            ParsimonySettings ParsimonySettings { get; }

            int FinalProteinCount { get; }
            int FinalPeptideCount { get; }

            int TotalSharedPeptideCount { get; }
            int FinalSharedPeptideCount { get; }
        }

        public class MappingResultsInternal : IMappingResults
        {
            public MappingResultsInternal Clone()
            {
                return new MappingResultsInternal
                {
                    PeptidesMapped = PeptidesMapped,
                    PeptidesUnmapped = PeptidesUnmapped,
                    ProteinsMapped = ProteinsMapped,
                    ProteinsUnmapped = ProteinsUnmapped,
                    FinalPeptideCount = FinalPeptideCount,
                    FinalProteinCount = FinalProteinCount,
                    TotalSharedPeptideCount = TotalSharedPeptideCount,
                    FinalSharedPeptideCount = FinalSharedPeptideCount,
                    FindMinimalProteinList = FindMinimalProteinList,
                    RemoveSubsetProteins = RemoveSubsetProteins,
                    SharedPeptides = SharedPeptides,
                    GroupProteins = GroupProteins,
                    GeneLevelParsimony = GeneLevelParsimony,
                    MinPeptidesPerProtein = MinPeptidesPerProtein
                };
            }

            public int ProteinsMapped { get; set; }
            public int ProteinsUnmapped { get; set; }
            public int PeptidesMapped { get; set; }
            public int PeptidesUnmapped { get; set; }

            public bool GroupProteins { get; set; }
            public bool GeneLevelParsimony { get; set; }
            public bool FindMinimalProteinList { get; set; }
            public bool RemoveSubsetProteins { get; set; }
            public SharedPeptides SharedPeptides { get; set; }
            public int MinPeptidesPerProtein { get; set; }

            public ParsimonySettings ParsimonySettings => new ParsimonySettings(GroupProteins, GeneLevelParsimony, FindMinimalProteinList,
                RemoveSubsetProteins, SharedPeptides, MinPeptidesPerProtein);

            public int FinalProteinCount { get; set; }
            public int FinalPeptideCount { get; set; }

            public int TotalSharedPeptideCount { get; set; }
            public int FinalSharedPeptideCount { get; set; }
        }

        public class PeptideAssociationGroup
        {
            public List<PeptideDocNode> Peptides { get; }

            private int _hash;

            public PeptideAssociationGroup(List<PeptideDocNode> peptides)
            {
                Peptides = peptides;

                _hash = 397;
                foreach(var peptide in peptides)
                    _hash = (_hash * 397) ^ peptide.Peptide.Sequence.GetHashCode();
            }

            public override bool Equals(object x)
            {
                if (!(x is PeptideAssociationGroup))
                    return Peptides == null;
                if (_hash != ((PeptideAssociationGroup)x)._hash)
                    return false;
                return Peptides.SequenceEqual(((PeptideAssociationGroup) x).Peptides);
            }

            public override int GetHashCode()
            {
                return _hash;
            }

            public override string ToString()
            {
                return string.Join(TextUtil.SEPARATOR_CSV.ToString(), Peptides.Select(p => p.ModifiedSequenceDisplay));
            }
        }

        public void ApplyParsimonyOptions(bool groupProteins, bool geneLevel, bool findMinimalProteinList, bool removeSubsetProteins, SharedPeptides sharedPeptides, int minPeptidesPerProtein, ILongWaitBroker broker)
        {
            Dictionary<ReferenceValue<PeptideDocNode>, List<IProteinRecord>> peptideToProteinGroups = _peptideToProteins;

            broker.Message = ProteomeResources.AssociateProteinsDlg_UpdateParsimonyResults_Applying_parsimony_options;

            _peptidesRemovedByFilters = new HashSet<ReferenceValue<PeptideDocNode>>();

            if (groupProteins || geneLevel)
            {
                if (_proteinOrGeneGroupResultCacheByGeneLevel[geneLevel] == null)
                {
                    var cache = _proteinOrGeneGroupResultCacheByGeneLevel[geneLevel] = new ProteinOrGeneGroupResultCache();
                    cache.Results = _results.Clone();
                    cache.Results.GroupProteins = true;
                    cache.Results.GeneLevelParsimony = geneLevel;
                    cache.PeptideGroupByProteinOrGeneGroup = CalculateProteinOrGeneGroups(cache.Results, geneLevel, broker);

                    if (cache.PeptideGroupByProteinOrGeneGroup == null)
                        return;
                }

                _finalResults = _proteinOrGeneGroupResultCacheByGeneLevel[geneLevel].Results.Clone();
                ParsimoniousProteins = _proteinOrGeneGroupResultCacheByGeneLevel[geneLevel].PeptideGroupByProteinOrGeneGroup;
                peptideToProteinGroups = _proteinOrGeneGroupResultCacheByGeneLevel[geneLevel].PeptideToProteinOrGeneGroup;
            }
            else
            {
                _finalResults = _results;
                _finalResults.GeneLevelParsimony = false;
                ParsimoniousProteins = AssociatedProteins;
            }

            if (broker.IsCanceled)
                return;

            // count shared peptides remaining
            var allPeptidesRemaining = new HashSet<string>();
            var sharedPeptidesRemaining = new Dictionary<string, int>();
            foreach (var kvp in ParsimoniousProteins)
            foreach (var peptide in kvp.Value.Peptides.GroupBy(p => p.ModifiedSequence))
                if (!allPeptidesRemaining.Add(peptide.Key))
                {
                    if (!sharedPeptidesRemaining.ContainsKey(peptide.Key))
                        sharedPeptidesRemaining[peptide.Key] = 2;
                    else
                        sharedPeptidesRemaining[peptide.Key] += 1;
                }
            _finalResults.TotalSharedPeptideCount = sharedPeptidesRemaining.Values.Sum();

            // FindProteinMatches already duplicates results between proteins
            if (sharedPeptides != SharedPeptides.DuplicatedBetweenProteins)
            {
                var filteredProteinAssociations = new Dictionary<IProteinRecord, List<PeptideDocNode>>();
                _finalResults = _finalResults.Clone();
                _finalResults.FinalPeptideCount = 0;
                foreach (var kvp in peptideToProteinGroups)
                {
                    if (sharedPeptides == SharedPeptides.Removed && kvp.Value.Count > 1)
                    {
                        _peptidesRemovedByFilters.Add(kvp.Key);
                        continue;
                    }
                    if (broker.IsCanceled)
                        return;

                    IEnumerable<IProteinRecord> filteredProteins = null;
                    if (sharedPeptides == SharedPeptides.AssignedToFirstProtein) // pick the protein with the lowest RecordIndex
                        filteredProteins = new []{ kvp.Value.Aggregate((p1, p2) => p1.RecordIndex < p2.RecordIndex ? p1 : p2) };
                    else if (sharedPeptides == SharedPeptides.AssignedToBestProtein) // pick the protein(s) with the most peptides
                    {
                        int topCount = 0;
                        var filteredProteinsList = new List<IProteinRecord>();
                        foreach (var protein in kvp.Value.OrderByDescending(p => ParsimoniousProteins[p].Peptides.Count))
                        {
                            if (topCount == 0)
                                topCount = ParsimoniousProteins[protein].Peptides.Count;
                            else if (ParsimoniousProteins[protein].Peptides.Count < topCount)
                                break;
                            filteredProteinsList.Add(protein);
                        }
                        filteredProteins = filteredProteinsList;
                    }
                    else if (sharedPeptides == SharedPeptides.Removed)
                        filteredProteins = kvp.Value;
                    else
                        throw new InvalidOperationException(@"SharedPeptides mode " +
                                                            Enum.GetName(typeof(SharedPeptides), sharedPeptides) +
                                                            @" not handled in ApplyParsimonyOptions");

                    foreach (var protein in filteredProteins)
                    {
                        ++_finalResults.FinalPeptideCount;
                        if (!filteredProteinAssociations.ContainsKey(protein))
                            filteredProteinAssociations.Add(protein, new List<PeptideDocNode> {kvp.Key});
                        else
                            filteredProteinAssociations[protein].Add(kvp.Key);
                    }
                }

                ParsimoniousProteins = filteredProteinAssociations.ToDictionary(kvp => kvp.Key, kvp => new PeptideAssociationGroup(kvp.Value));
                _finalResults.SharedPeptides = sharedPeptides;
                _finalResults.FinalProteinCount = ParsimoniousProteins.Count;
            }

            if (findMinimalProteinList)
            {
                var minimalProteinSet = FindMinimalProteinSet(peptideToProteinGroups, broker);
                ParsimoniousProteins = ParsimoniousProteins.Where(p => minimalProteinSet.Contains(p.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                _finalResults = _finalResults.Clone();
                _finalResults.FindMinimalProteinList = true;
                _finalResults.RemoveSubsetProteins = true;
                _finalResults.FinalProteinCount = ParsimoniousProteins.Count;
                _finalResults.FinalPeptideCount = ParsimoniousProteins.Sum(kvp => kvp.Value.Peptides.Count);
            }
            else if (removeSubsetProteins)
            {
                var subsetProteins = FindSubsetProteins(peptideToProteinGroups, broker);
                ParsimoniousProteins = ParsimoniousProteins.Where(p => !subsetProteins.Contains(p.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                _finalResults = _finalResults.Clone();
                _finalResults.FindMinimalProteinList = false;
                _finalResults.RemoveSubsetProteins = true;
                _finalResults.FinalProteinCount = ParsimoniousProteins.Count;
                _finalResults.FinalPeptideCount = ParsimoniousProteins.Sum(kvp => kvp.Value.Peptides.Count);

            }

            if (minPeptidesPerProtein > 1)
            {
                foreach (var kvp in ParsimoniousProteins.Where(p => p.Value.Peptides.Count < minPeptidesPerProtein))
                foreach (var peptide in kvp.Value.Peptides)
                    _peptidesRemovedByFilters.Add(peptide);
                ParsimoniousProteins = ParsimoniousProteins.Where(p => p.Value.Peptides.Count >= minPeptidesPerProtein).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                _finalResults = _finalResults.Clone();
                _finalResults.FinalProteinCount = ParsimoniousProteins.Count;
                _finalResults.FinalPeptideCount = ParsimoniousProteins.Sum(kvp => kvp.Value.Peptides.Count);
            }
            _finalResults.MinPeptidesPerProtein = minPeptidesPerProtein;

            // count shared peptides remaining
            allPeptidesRemaining.Clear();
            sharedPeptidesRemaining.Clear();
            foreach(var kvp in ParsimoniousProteins)
            foreach(var peptide in kvp.Value.Peptides.GroupBy(p => p.ModifiedSequence))
                if (!allPeptidesRemaining.Add(peptide.Key))
                {
                    if (!sharedPeptidesRemaining.ContainsKey(peptide.Key))
                        sharedPeptidesRemaining[peptide.Key] = 2;
                    else
                        sharedPeptidesRemaining[peptide.Key] += 1;
                }
            _finalResults.FinalSharedPeptideCount = sharedPeptidesRemaining.Values.Sum();
        }

        public class GeneLevelEqualityComparer : EqualityComparer<IProteinRecord>
        {
            public override bool Equals(IProteinRecord x, IProteinRecord y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(null, x)) return false;
                if (ReferenceEquals(null, y)) return false;
                if (x.Metadata.Gene.IsNullOrEmpty() && y.Metadata.Gene.IsNullOrEmpty()) return x.Sequence.Equals(y.Sequence);
                if (x.Metadata.Gene.IsNullOrEmpty() != y.Metadata.Gene.IsNullOrEmpty()) return false;

                return x.Metadata.Gene.Equals(y.Metadata.Gene);
            }

            public override int GetHashCode(IProteinRecord obj)
            {
                if (obj.Metadata.Gene == null)
                    return obj.Sequence.GetHashCode();
                return obj.Metadata.Gene.GetHashCode();
            }
        }
        
        public static IProteinRecord GenerateConcatenatedSequenceIfNecessary(Dictionary<IProteinRecord, List<PeptideDocNode>> proteinToPeptides)
        {
            if (proteinToPeptides.Count == 1)
                return proteinToPeptides.Keys.First();

            var longestProtein = proteinToPeptides.OrderByDescending(kvp2 => kvp2.Key.Sequence.Sequence.Length).First().Key;
            var allPeptides = proteinToPeptides.Values.SelectMany(o => o).Distinct(ReferenceEqualityComparer).ToList();
            if (allPeptides.All(node => longestProtein.Sequence.Sequence.Contains(node.Peptide.Sequence)))
                return longestProtein;

            // each protein's individual metadata is kept, but all protein sequences are replaced by the concatenated sequence
            var concatenatedSequence = string.Concat(proteinToPeptides.Keys.Select(p => p.Sequence.Sequence));
            return new FastaRecord(longestProtein.RecordIndex, 0,
                new FastaSequenceGroup(longestProtein.Sequence.Name,
                    proteinToPeptides.Keys.Select(p => new FastaSequence(p.Sequence.Name,
                        p.Sequence.Description, p.Sequence.Alternatives, concatenatedSequence)).ToList()),
                new ProteinGroupMetadata(proteinToPeptides.Keys.Select(p => p.Metadata).ToList()));
        }

        private Dictionary<IProteinRecord, PeptideAssociationGroup> CalculateProteinOrGeneGroups(MappingResultsInternal results, bool geneLevel, ILongWaitBroker broker)
        {
            var _peptideGroupToProteins = new Dictionary<PeptideAssociationGroup, List<IProteinRecord>>();

            var proteinOrGeneToPeptideGroup = AssociatedProteins;
            if (geneLevel)
            {
                // gene to protein to peptides; the top level dictionary uses the GeneLevelEqualityComparer
                var proteinsByGene = AssociatedProteins.GroupBy(kvp => kvp.Key, new GeneLevelEqualityComparer());
                var geneToPeptides = new Dictionary<IProteinRecord, Dictionary<IProteinRecord, List<PeptideDocNode>>>(new GeneLevelEqualityComparer());
                foreach (var group in proteinsByGene)
                    geneToPeptides.Add(group.Key, group.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Peptides));

                // now pick the protein with the longest sequence if it contains all the peptides, or a concatenation of all of the sequences if not
                proteinOrGeneToPeptideGroup = geneToPeptides.ToDictionary(kvp => GenerateConcatenatedSequenceIfNecessary(kvp.Value),
                    kvp => new PeptideAssociationGroup(kvp.Value.Values.SelectMany(o => o).Distinct(ReferenceEqualityComparer).ToList()));
            }

            foreach(var kvp in proteinOrGeneToPeptideGroup)
                if (!_peptideGroupToProteins.ContainsKey(kvp.Value))
                    _peptideGroupToProteins.Add(kvp.Value, new List<IProteinRecord> { kvp.Key });
                else
                    _peptideGroupToProteins[kvp.Value].Add(kvp.Key);

            results.FinalPeptideCount = 0;
            results.FinalProteinCount = _peptideGroupToProteins.Count;

            if (geneLevel)
                broker.Message = ProteomeResources.ProteinAssociation_CalculateProteinOrGeneGroups_Calculating_gene_groups;
            else
                broker.Message = ProteomeResources.ProteinAssociation_CalculateProteinGroups_Calculating_protein_groups;
            var proteinGroupAssociations = new Dictionary<IProteinRecord, PeptideAssociationGroup>(geneLevel ? new GeneLevelEqualityComparer() : EqualityComparer<IProteinRecord>.Default);

            var peptideToProteinOrGeneGroups = _proteinOrGeneGroupResultCacheByGeneLevel[geneLevel].PeptideToProteinOrGeneGroup = new Dictionary<ReferenceValue<PeptideDocNode>, List<IProteinRecord>>();
            Action<IProteinRecord, PeptideAssociationGroup> addPeptideAssociations =
                (protein, peptides) =>
                {
                    foreach (var peptide in peptides.Peptides)
                    {
                        if (!peptideToProteinOrGeneGroups.ContainsKey(peptide))
                            peptideToProteinOrGeneGroups.Add(peptide, new List<IProteinRecord> {protein});
                        else
                            peptideToProteinOrGeneGroups[peptide].Add(protein);
                    }
                };

            int index = 0;
            foreach (var kvp in _peptideGroupToProteins)
            {
                if (broker.IsCanceled)
                    return null;
                ++index;
                broker.ProgressValue = index * 100 / _peptideGroupToProteins.Count;

                results.FinalPeptideCount += kvp.Key.Peptides.Count;

                if (kvp.Value.Count == 1)
                {
                    proteinGroupAssociations[kvp.Value[0]] = kvp.Key;
                    addPeptideAssociations(kvp.Value[0], kvp.Key);
                    continue;
                }

                string ProteinOrGeneGroupName(IProteinRecord p)
                {
                    if (geneLevel && !p.Metadata.Gene.IsNullOrEmpty()) return p.Metadata.Gene;
                    return p.Sequence.Name;
                }

                var proteinsByRecordIndex = kvp.Value.OrderBy(p => p.RecordIndex).ToList();
                var proteinGroupName = string.Join(ProteinGroupMetadata.GROUP_SEPARATOR, proteinsByRecordIndex.Select(ProteinOrGeneGroupName).Distinct());
                var proteinFastaSequence = new FastaSequenceGroup(proteinGroupName, proteinsByRecordIndex.Select(r => r.Sequence).ToList());
                var proteinGroup = new FastaRecord(kvp.Value[0].RecordIndex, 0, proteinFastaSequence, kvp.Value[0].Metadata);
                proteinGroupAssociations[proteinGroup] = kvp.Key;
                addPeptideAssociations(proteinGroup, kvp.Key);
            }

            return proteinGroupAssociations;
        }

        /// <summary>
        /// Calculate clusters (connected components) for protein/peptide associations
        /// </summary>
        private Dictionary<int, IEnumerable<IProteinRecord>> CalculateClusters(Dictionary<ReferenceValue<PeptideDocNode>, List<IProteinRecord>> peptideToProteinGroups, ILongWaitBroker broker)
        {
            var clusterByProteinGroup = new Dictionary<IProteinRecord, int>();
            int clusterId = 0;
            var clusterStack = new Stack<KeyValuePair<IProteinRecord, PeptideAssociationGroup>>();

            int proteinsProcessed = 0;

            broker.Message = ProteomeResources.ProteinAssociation_Calculating_protein_clusters;
            broker.ProgressValue = 0;

            foreach (var kvp in ParsimoniousProteins.OrderBy(kvp => kvp.Key.Sequence.Name))
            {
                var proteinGroup = kvp.Key;

                if (clusterByProteinGroup.ContainsKey(proteinGroup))
                    continue;

                broker.SetProgressCheckCancel(proteinsProcessed, ParsimoniousProteins.Count);

                // for each protein without a cluster assignment, make a new cluster
                ++clusterId;
                clusterStack.Push(kvp);
                while (clusterStack.Count > 0)
                {
                    var kvpTop = clusterStack.Pop();
                    var peptideGroup = kvpTop.Value;

                    // add all "cousin" proteins to the current cluster
                    foreach(var peptide in peptideGroup.Peptides)
                    foreach (var cousinProteinGroup in peptideToProteinGroups[peptide])
                    {
                        if (clusterByProteinGroup.ContainsKey(cousinProteinGroup) || !ParsimoniousProteins.ContainsKey(cousinProteinGroup))
                            continue;
                        ++proteinsProcessed;
                        clusterByProteinGroup.Add(cousinProteinGroup, clusterId);

                        clusterStack.Push(new KeyValuePair<IProteinRecord, PeptideAssociationGroup>(cousinProteinGroup, ParsimoniousProteins[cousinProteinGroup]));
                    }
                }
            }

            // group proteins by cluster for return
            return clusterByProteinGroup.GroupBy(kvp => kvp.Value, kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => (IEnumerable<IProteinRecord>) kvp);
        }

        private ISet<IProteinRecord> FindMinimalProteinSet(Dictionary<ReferenceValue<PeptideDocNode>, List<IProteinRecord>> peptideToProteinGroups, ILongWaitBroker broker)
        {
            var proteinsByCluster = CalculateClusters(peptideToProteinGroups, broker);

            broker.Message = ProteomeResources.ProteinAssociation_Finding_minimal_protein_list;
            broker.ProgressValue = 0;
            int clustersProcessed = 0;

            var minimalProteinList = new ConcurrentDictionary<IProteinRecord, bool>();
            ParallelEx.ForEach(proteinsByCluster.Values, cluster =>
            {
                Interlocked.Increment(ref clustersProcessed);
                broker.SetProgressCheckCancel(clustersProcessed, proteinsByCluster.Count);

                var clusterProteins = cluster.ToList();

                var unexplainedPeptideSetByCluster = clusterProteins.SelectMany(p => ParsimoniousProteins[p].Peptides.Select(p2 => p2.Peptide.Sequence)).ToHashSet();
                var peptidesExplainedByProtein = new Dictionary<IProteinRecord, int>();

                while (unexplainedPeptideSetByCluster.Count > 0) // stop once all peptides are explained
                {
                    // find cluster protein(s) with most unexplained peptides
                    foreach (var protein in clusterProteins)
                        peptidesExplainedByProtein[protein] = ParsimoniousProteins[protein].Peptides.Count(p => unexplainedPeptideSetByCluster.Contains(p.Peptide.Sequence));
                    var proteinsWithMostUnexplainedPeptides = peptidesExplainedByProtein.Where(kvp => kvp.Value == peptidesExplainedByProtein.Values.Max()).Select(kvp => kvp.Key);

                    // add this protein(s) to the minimal set
                    foreach (var protein in proteinsWithMostUnexplainedPeptides)
                    {
                        unexplainedPeptideSetByCluster.ExceptWith(ParsimoniousProteins[protein].Peptides.Select(p2 => p2.Peptide.Sequence));
                        minimalProteinList[protein] = true;
                    }
                }
            });

            return minimalProteinList.Keys.ToHashSet();
        }

        private ISet<IProteinRecord> FindSubsetProteins(Dictionary<ReferenceValue<PeptideDocNode>, List<IProteinRecord>> peptideToProteinGroups, ILongWaitBroker broker)
        {
            var proteinsByCluster = CalculateClusters(peptideToProteinGroups, broker);

            broker.Message = ProteomeResources.ProteinAssociation_Removing_subset_proteins;
            broker.ProgressValue = 0;
            int proteinsProcessed = 0;

            var subsetProteins = new ConcurrentDictionary<IProteinRecord, bool>();
            foreach (var cluster in proteinsByCluster.Values)
            {
                broker.SetProgressCheckCancel(proteinsProcessed, ParsimoniousProteins.Count);

                // order proteins by number of peptides
                var clusterProteins = cluster.OrderByDescending(p => ParsimoniousProteins[p].Peptides.Count).ToList();

                // check if each protein is a subset of the proteins with more peptides than it
                Action<int> loopBody = i =>
                {
                    Interlocked.Increment(ref proteinsProcessed);
                    broker.SetProgressCheckCancel(proteinsProcessed, ParsimoniousProteins.Count);

                    var potentialSupersetProtein = clusterProteins[i];
                    var potentialSupersetPeptides = ParsimoniousProteins[potentialSupersetProtein];
                    for (int j = i + 1; j < clusterProteins.Count; j++)
                    {
                        var potentialSubsetProtein = clusterProteins[j];
                        var potentialSubsetPeptides = ParsimoniousProteins[potentialSubsetProtein];

                        // if the potential subset has any peptides not in the potential superset, it's not a subset
                        if (potentialSubsetPeptides.Peptides.Any(p => !potentialSupersetPeptides.Peptides.Contains(p)))
                            continue;

                        subsetProteins[potentialSubsetProtein] = true;
                    }
                };

                // in some cases one or two clusters will have the vast majority of proteins, so do the subset calculation in parallel for those big clusters
                if (clusterProteins.Count > 20)
                    ParallelEx.For(0, clusterProteins.Count - 1, loopBody);
                else
                    for (int i = 0; i < clusterProteins.Count - 1; i++)
                        loopBody(i);
            }

            return subsetProteins.Keys.ToHashSet();
        }

        private static string GetPeptideSequence(Peptide peptide)
        {
            return peptide.Target.Sequence;
        }

        private void ListPeptidesForMatching(ILongWaitBroker broker)
        {
            if (_peptideTrie != null)
                return;

            broker.Message = ProteomeResources.ProteinAssociation_ListPeptidesForMatching_Building_peptide_prefix_tree;

            if (_peptideToPath == null)
            {
                var peptidesForMatching = new HashSet<PeptideDocNode>(new PeptideComparer());

                var doc = _document;
                foreach (var nodePepGroup in doc.PeptideGroups)
                {
                    // if is already a FastaSequence we don't want to mess with it
                    /*if (nodePepGroup.PeptideGroup is FastaSequence)
                    {
                        continue;
                    }*/

                    peptidesForMatching.UnionWith(nodePepGroup.Peptides);
                }

                if (peptidesForMatching.Count == 0)
                {
                    _peptideToPath = null;
                    _peptideTrie = null;
                    throw new InvalidOperationException(Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_peptides_);
                }

                _peptideToPath = peptidesForMatching.GroupBy(node => GetPeptideSequence(node.Peptide))
                    .ToDictionary(grouping => grouping.Key, grouping => grouping.ToList());
            }

            _peptideTrie = new StringSearch(_peptideToPath.Keys, broker.CancellationToken);
            if (broker.IsCanceled)
                _peptideTrie = null;
        }

        public class PeptideComparer : IEqualityComparer<PeptideDocNode>
        {
            public bool Equals(PeptideDocNode x, PeptideDocNode y)
            {
                return Equals(x?.SequenceKey, y?.SequenceKey);
            }

            public int GetHashCode(PeptideDocNode obj)
            {
                return obj.SequenceKey.GetHashCode();
            }
        }

        // Given the current SRMDocument and a dictionary with associated proteins this method will run the the document tree and
        // build a new document.  The new document will contain all pre-existing FastaSequence nodes and will add the newly matches 
        // FastaSequence nodes.  The peptides that were matched to a FastaSequence are removed from their old group.
        public SrmDocument CreateDocTree(SrmDocument current, IProgressMonitor monitor)
        {
            var status = new ProgressStatus(ProteomeResources.ProteinAssociation_CreateDocTree_Creating_protein_targets_and_assigning_their_peptides);
            monitor.UpdateProgress(status);

            // Protein associations may be out of order because of multi-threading, so put them back in order.
            var proteinAssociationsList = ParsimoniousProteins.OrderBy(kvp => kvp.Key.RecordIndex).ToList();

            var newPeptideGroups = new List<PeptideGroupDocNode>(); // all groups that will be added in the new document
            var appendPeptideLists = new List<PeptideGroupDocNode>();

            // Move unmapped peptides from FastaSequence node to "Unmapped Peptides" list
            var unmappedPeptideNodes = _peptideToPath.Values.SelectMany(list => list).Where(p => !p.IsDecoy)
                .Select(ReferenceValue.Of).ToHashSet();
            unmappedPeptideNodes.ExceptWith(ParsimoniousProteins.Values.SelectMany(pag => pag.Peptides.Select(ReferenceValue.Of)));
            unmappedPeptideNodes.ExceptWith(_peptidesRemovedByFilters);

            // Modifies and adds old groups that still contain unmatched peptides to newPeptideGroups
            foreach (var nodePepGroup in current.MoleculeGroups)
            {
                if (monitor.IsCanceled)
                    return null;

                var peptideDocNodes = nodePepGroup.Children.Where(node => node is PeptideDocNode).Cast<PeptideDocNode>().ToList();

                // Drop empty peptide lists
                if (peptideDocNodes.Count == 0)
                    continue;

                // Drop old Unmapped Peptides group
                if (nodePepGroup.Name == Resources.ProteinAssociation_CreateDocTree_Unmapped_Peptides)
                    continue;

                // Keep decoy and iRT lists
                if (nodePepGroup.IsDecoy)
                {
                    appendPeptideLists.Add(nodePepGroup);
                    continue;
                }

                if (peptideDocNodes.All(node => node.GlobalStandardType == StandardType.IRT))
                {
                    newPeptideGroups.Add(nodePepGroup);
                    unmappedPeptideNodes.ExceptWith(peptideDocNodes.Select(ReferenceValue.Of)); // do not count iRT peptides as unmapped
                    continue;
                }

                // Keep peptide lists that contain unmapped peptides
                if (nodePepGroup.IsProteomic && nodePepGroup.IsPeptideList)
                {
                    var mappedTargets = _peptideToProteins.Select(node => node.Key.Value.Target).ToHashSet();

                    // If a peptide list peptide is unmapped, leave it in the peptide list but remove it from the global unmapped list
                    var peptidesByMappedStatus = new Dictionary<bool, IList<ReferenceValue<PeptideDocNode>>>
                    {
                        { false, new List<ReferenceValue<PeptideDocNode>>() },
                        { true, new List<ReferenceValue<PeptideDocNode>>() }
                    };

                    foreach (var node in peptideDocNodes)
                    {
                        if (monitor.IsCanceled)
                            return null;

                        peptidesByMappedStatus[mappedTargets.Contains(node.Target)].Add(node);
                    }
                    var unmappedPeptides = peptidesByMappedStatus[false].ToHashSet();
                    unmappedPeptideNodes.ExceptWith(unmappedPeptides);

                    // If it was mapped, remove it from the peptide list
                    var mappedPeptides = peptidesByMappedStatus[true];
                    var mappedPeptideIndexes = mappedPeptides.Select(node => node.Value.Peptide.GlobalIndex);
                    var newPeptideList = (PeptideGroupDocNode) nodePepGroup.RemoveAll(mappedPeptideIndexes.ToList());

                    // Only keep the list if it still has peptides
                    if (newPeptideList.Children.Count != 0) 
                        appendPeptideLists.Add(newPeptideList);
                    continue;
                }

                // Get non-peptide children
                var nonPeptideNodes = nodePepGroup.Children.Where(node => (node as PeptideDocNode)?.Peptide.Target.IsProteomic == false).ToList();

                // Ignore old groups with no non-peptide children
                if (nonPeptideNodes.Count == 0)
                    continue;

                // Not a protein
                var newNodePepGroup = nonPeptideNodes;

                // If the count of items in the group has not changed then it can be assumed that the group is the same
                // otherwise if there is a different count and it is not 0 then we want to add the modified group to the
                // set of new groups that will be added to the tree
                if (newNodePepGroup.Count == nodePepGroup.Children.Count)
                {
                    newPeptideGroups.Add(nodePepGroup);  // No change
                }
                else if (newNodePepGroup.Any())
                {
                    newPeptideGroups.Add((PeptideGroupDocNode)nodePepGroup.ChangeChildren(newNodePepGroup.ToArray()));
                }
            }

            if (unmappedPeptideNodes.Count > 0)
            {
                var unmappedNakedPeptides = unmappedPeptideNodes.Select(node => node.Value.RemoveFastaSequence());
                var unmappedPeptideList = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY,
                    Resources.ProteinAssociation_CreateDocTree_Unmapped_Peptides, string.Empty, unmappedNakedPeptides.ToArray());
                appendPeptideLists.Add(unmappedPeptideList);
            }

            int totalPeptideGroups = newPeptideGroups.Count + appendPeptideLists.Count + proteinAssociationsList.Count;

            // Adds all new groups/proteins to newPeptideGroups
            foreach (var keyValuePair in proteinAssociationsList)
            {
                var protein = keyValuePair.Key.Sequence;
                var metadata = keyValuePair.Key.Metadata;
                var children = new List<PeptideDocNode>();
                foreach (PeptideDocNode peptideDocNode in keyValuePair.Value.Peptides)
                {
                    // do not reassociate iRT list peptides to proteins
                    if (!children.Contains(p => p.ModifiedTarget.Equals(peptideDocNode.ModifiedTarget)))
                        children.Add(peptideDocNode.ChangeFastaSequence(protein));
                }

                var proteinOrGroupMetadata = protein is FastaSequenceGroup group
                    ? new ProteinGroupMetadata(group.FastaSequenceList.Select(s =>
                    {
                        var proteinMetadata = _proteinToMetadata[s.Name];
                        return new ProteinMetadata(s.Name, s.Description, proteinMetadata.PreferredName,
                            proteinMetadata.Accession, proteinMetadata.Gene, proteinMetadata.Species);
                    }).ToList())
                    : new ProteinMetadata(protein.Name, protein.Description, metadata.PreferredName, metadata.Accession,
                        metadata.Gene, metadata.Species);
                var peptideGroupDocNode = new PeptideGroupDocNode(protein, proteinOrGroupMetadata, children.ToArray());
                //peptideGroupDocNode = peptideGroupDocNode.ChangeName(protein.Name).ChangeDescription(protein.Description);
                newPeptideGroups.Add(peptideGroupDocNode);

                if (monitor.IsCanceled)
                    return null;
                monitor.UpdateProgress(status.ChangePercentComplete(newPeptideGroups.Count * 100 / totalPeptideGroups));
            }

            newPeptideGroups.AddRange(appendPeptideLists);

            var newPeptideSettings = current.Settings.PeptideSettings.ChangeParsimonySettings(FinalResults.ParsimonySettings);
            if (!Equals(newPeptideSettings, current.Settings.PeptideSettings))
            {
                current = current.ChangeSettings(current.Settings.ChangePeptideSettings(newPeptideSettings));
            }

            return (SrmDocument)current.ChangeChildrenChecked(newPeptideGroups.ToArray());
        }

        /// <summary>
        /// Returns tuples of FastaData along with the position in the Stream that the fasta record was found.
        /// This method returns an IEnumerable because it is intended to be passed to <see cref="ParallelEx.ForEach{TSource}" />
        /// which starts processing in parallel before the last record has been fetched.
        /// </summary>
        private static IEnumerable<FastaRecord> ParseFastaWithFilePositions(Stream stream)
        {
            int index = 0;
            long streamLength = stream.Length;
            var wefi = new WebEnabledFastaImporter();
            foreach (var fastaData in wefi.Import(new StreamReader(stream)))
            {
                yield return new FastaRecord(index, (int) (stream.Position * 100 / streamLength), fastaData);
                index++;
            }
        }

        public interface IProteinRecord
        {
            int RecordIndex { get; }
            FastaSequence Sequence { get; }
            ProteinMetadata Metadata { get; }
            int Progress { get; }
        }

        public interface IProteinSource
        {
            IEnumerable<IProteinRecord> Proteins { get; }
        }

        private class BackgroundProteomeRecord : IProteinRecord
        {
            public BackgroundProteomeRecord(int index, FastaSequence sequence, ProteinMetadata metadata, int progress)
            {
                RecordIndex = index;
                Sequence = sequence;
                Progress = progress;
                Metadata = metadata;
            }

            public int RecordIndex { get; }
            public FastaSequence Sequence { get; }
            public ProteinMetadata Metadata { get; }
            public int Progress { get; }
        }

        public class BackgroundProteomeSource : IProteinSource
        {
            public BackgroundProteomeSource(CancellationToken cancelToken, BackgroundProteome proteome)
            {
                Proteome = proteome;

                using (var proteomeDb = proteome.OpenProteomeDb(cancelToken))
                {
                    DbProteins = proteomeDb.ListProteinSequences();
                }
            }

            public IEnumerable<IProteinRecord> Proteins
            {
                get
                {
                    for (int i = 0; i < DbProteins.Count; ++i)
                        yield return new BackgroundProteomeRecord(i, Proteome.MakeFastaSequence(DbProteins[i]), DbProteins[i].ProteinMetadata, i * 100 / DbProteins.Count);
                }
            }

            private BackgroundProteome Proteome;
            private IList<Protein> DbProteins;
        }

        /// <summary>
        /// Contains the fasta sequences read from a fasta file, along with properties indicating
        /// the order that the records were found in the file, and the byte offset where the records
        /// were found. (In theory, "RecordIndex" is redundant, and "FilePosition" could be used to order these
        /// things, but, it's conceivable the fasta parsing code might not be careful about where the stream position
        /// is as each record is returned).
        /// </summary>
        private class FastaRecord : IProteinRecord
        {
            public FastaRecord(int recordIndex, int progress, FastaSequence fastaSequence, ProteinMetadata metadata)
            {
                RecordIndex = recordIndex;
                Progress = progress;
                Sequence = fastaSequence;
                Metadata = metadata;
            }

            public FastaRecord(int recordIndex, int progress, DbProtein protein)
            {
                RecordIndex = recordIndex;
                Progress = progress;

                var firstName = protein.Names.First();
                var alternatives = protein.Names.Skip(1).Select(o => o.GetProteinMetadata()).ToList();
                Sequence = new FastaSequence(firstName.Name, firstName.Description, alternatives, protein.Sequence);
                Metadata = firstName.GetProteinMetadata();
            }

            public int RecordIndex { get; }
            public int Progress { get; }
            public FastaSequence Sequence { get; }
            public ProteinMetadata Metadata { get; }

            public override string ToString()
            {
                if (Metadata.Gene.IsNullOrEmpty())
                    return $@"{Sequence.Name}:{Helpers.TruncateString(Sequence.Sequence, 30)}";
                return $@"{Sequence.Name} ({Metadata.Gene}):{Helpers.TruncateString(Sequence.Sequence, 30)}";
            }
        }

        public class FastaSource : IProteinSource
        {
            public FastaSource(Stream fastaStream)
            {
                FastaRecords = ParseFastaWithFilePositions(fastaStream);
            }

            public IEnumerable<IProteinRecord> Proteins
            {
                get
                {
                    foreach (var protein in FastaRecords)
                        yield return protein;
                }
            }

            private IEnumerable<FastaRecord> FastaRecords;
        }
    }

    public class AssociateProteinsSettings : AuditLogOperationSettings<AssociateProteinsSettings>, IAuditLogComparable
    {
        public static AssociateProteinsSettings DEFAULT = new AssociateProteinsSettings(null, null, null);

        public AssociateProteinsSettings(ProteinAssociation proteinAssociation, string fasta, string backgroundProteome)
        {
            Results = proteinAssociation?.FinalResults ?? proteinAssociation?.Results;
            FASTA = fasta;
            BackgroundProteome = backgroundProteome;

            ParsimonySettings = Results?.ParsimonySettings ?? ProteinAssociation.ParsimonySettings.DEFAULT;
        }

        protected override AuditLogEntry CreateEntry(SrmDocumentPair docPair)
        {
            var messageType = GroupProteins
                ? MessageType.associated_peptides_with_protein_groups
                : MessageType.associated_peptides_with_proteins;
            var entry = AuditLogEntry.CreateSimpleEntry(messageType, SrmDocument.DOCUMENT_TYPE.proteomic);

            return entry.Merge(base.CreateEntry(docPair));
        }

        public ProteinAssociation.IMappingResults Results { get; private set; }

        [TrackChildren(ignoreName: true)]
        public ProteinAssociation.ParsimonySettings ParsimonySettings { get; private set; }

        [Track]
        public string FASTA { get; private set; }

        [Track]
        public string BackgroundProteome { get; private set; }

        public bool GroupProteins => ParsimonySettings?.GroupProteins ?? false;

        [Track(ignoreDefaultParent: true)]
        public int MappedProteins => Results?.ProteinsMapped ?? 0;

        [Track(ignoreDefaultParent: true)]
        public int MappedPeptides => Results?.PeptidesMapped ?? 0;

        [Track]
        public int UnmappedProteins => Results?.ProteinsUnmapped ?? 0;

        [Track]
        public int UnmappedPeptides => Results?.PeptidesUnmapped ?? 0;

        [Track]
        public int TargetProteins => GroupProteins ? 0 : Results?.FinalProteinCount ?? 0;

        [Track]
        public int TargetProteinGroups => !GroupProteins ? 0 : Results?.FinalProteinCount ?? 0;

        [Track(ignoreDefaultParent: true)]
        public int TargetPeptides => Results?.FinalPeptideCount ?? 0;

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return DEFAULT;
        }
    }
}

