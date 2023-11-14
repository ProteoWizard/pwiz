﻿/*
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
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Proteome
{
    public class ProteinAssociation
    {
        private SrmDocument _document;
        private StringSearch _peptideTrie;
        private Dictionary<string, List<PeptideDocNode>> _peptideToPath;
        private Dictionary<PeptideDocNode, List<IProteinRecord>> _peptideToProteins, _peptideToProteinGroups;
        private MappingResultsInternal _results, _finalResults, _proteinGroupResults;
        private IDictionary<IProteinRecord, PeptideAssociationGroup> _proteinGroupAssociations;
        private HashSet<PeptideDocNode> _peptidesRemovedByFilters;

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
            _proteinGroupAssociations = null;
            _proteinGroupResults = null;
            _finalResults = null;
            _peptideToProteinGroups = null;
            _peptideToProteins = null;
            _peptidesRemovedByFilters = null;
        }

        public void UseFastaFile(string file, Func<FastaSequence, IEnumerable<Peptide>> digestProteinToPeptides, ILongWaitBroker broker)
        {
            if (!File.Exists(file))
                return;

            ResetMapping();
            using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var fastaSource = new FastaSource(stream);
            var proteinAssociations = FindProteinMatches(fastaSource, digestProteinToPeptides, broker);
            if (proteinAssociations != null)
            {
                AssociatedProteins = proteinAssociations;
            }
        }

        // find matches using the background proteome
        public void UseBackgroundProteome(BackgroundProteome backgroundProteome, Func<FastaSequence, IEnumerable<Peptide>> digestProteinToPeptides, ILongWaitBroker broker)
        {
            if (backgroundProteome.Equals(BackgroundProteome.NONE))
                throw new InvalidOperationException(Resources.AssociateProteinsDlg_UseBackgroundProteome_No_background_proteome_defined);

            ResetMapping();
            var proteome = backgroundProteome;
            var proteinSource = new BackgroundProteomeSource(broker.CancellationToken, proteome);
            var proteinAssociations = FindProteinMatches(proteinSource, digestProteinToPeptides, broker);
            if (proteinAssociations != null)
            {
                AssociatedProteins = proteinAssociations;
            }
        }

        private Dictionary<IProteinRecord, PeptideAssociationGroup> FindProteinMatches(IProteinSource proteinSource, Func<FastaSequence, IEnumerable<Peptide>> digestProteinToPeptides, ILongWaitBroker broker)
        {
            var localResults = new MappingResultsInternal();
            var peptideToProteins = new Dictionary<PeptideDocNode, List<IProteinRecord>>();

            var proteinAssociations = new Dictionary<IProteinRecord, PeptideAssociationGroup>();
            int maxProgressValue = 0;
            broker.Message = Resources.AssociateProteinsDlg_FindProteinMatchesWithFasta_Finding_peptides_in_FASTA_file;

            ParallelEx.ForEach(proteinSource.Proteins, fastaRecord =>
            {
                int progressValue = fastaRecord.Progress;
                var fasta = fastaRecord.Sequence;
                var trieResults = _peptideTrie.FindAll(fasta.Sequence);
                var matches = new List<PeptideDocNode>();

                // don't count the same peptide twice in a protein
                var peptidesMatched = new HashSet<string>();

                IList<Peptide> digestedPeptides = null;

                foreach (var result in trieResults)
                {
                    if (!peptidesMatched.Add(result.Keyword))
                        continue;

                    // check that peptide is in the digest of the protein (if the result is non-empty)
                    digestedPeptides ??= digestProteinToPeptides(fastaRecord.Sequence).ToList();
                    if (!digestedPeptides.Contains(p => p.Sequence == result.Keyword))
                        continue;

                    matches.AddRange(_peptideToPath[result.Keyword]);
                }

                var peptideAssociationGroup = new PeptideAssociationGroup(matches);

                lock (localResults)
                {
                    if (broker.IsCanceled)
                        return;

                    if (progressValue > maxProgressValue && progressValue <= 100)
                    {
                        broker.ProgressValue = progressValue;
                        maxProgressValue = Math.Max(maxProgressValue, progressValue);
                    }

                    if (matches.Count > 0)
                    {
                        proteinAssociations[fastaRecord] = peptideAssociationGroup;
                        ++localResults.ProteinsMapped;
                        localResults.FinalPeptideCount += matches.Count;

                        foreach (var match in matches)
                        {
                            if (!peptideToProteins.ContainsKey(match))
                                peptideToProteins.Add(match, new List<IProteinRecord> { fastaRecord });
                            else
                                peptideToProteins[match].Add(fastaRecord);
                        }
                    }
                    else
                        ++localResults.ProteinsUnmapped;
                }
            });
            
            Assume.IsTrue(localResults.ProteinsMapped + localResults.ProteinsUnmapped > 0);

            var distinctPeptideDocNodes = _peptideToPath.SelectMany(kvp => kvp.Value);
            int distinctTargetPeptideCount = distinctPeptideDocNodes.Where(p => !p.IsDecoy).Select(p => p.Peptide.Target).Distinct().Count();
            _peptideToProteins = peptideToProteins;
            _results = localResults;
            _results.PeptidesMapped = peptideToProteins.Keys.Select(p => p.Peptide.Target).Distinct().Count();
            _results.PeptidesUnmapped = distinctTargetPeptideCount - _results.PeptidesMapped;
            _results.FinalProteinCount = proteinAssociations.Count;

            return proteinAssociations;
        }

        [XmlRoot("protein_association")]
        public class ParsimonySettings : Immutable, IXmlSerializable, IValidating
        {
            public static ParsimonySettings DEFAULT = new ParsimonySettings() { MinPeptidesPerProtein = 1 };

            public ParsimonySettings(bool groupProteins, bool findMinimalProteinList, bool removeSubsetProteins, SharedPeptides sharedPeptides, int minPeptidesPerProtein)
            {
                GroupProteins = groupProteins;
                FindMinimalProteinList = findMinimalProteinList;
                RemoveSubsetProteins = removeSubsetProteins;
                SharedPeptides = sharedPeptides;
                MinPeptidesPerProtein = minPeptidesPerProtein;
            }

            [Track(ignoreDefaultParent:true)]
            public bool GroupProteins { get; private set; }

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
                    MinPeptidesPerProtein = MinPeptidesPerProtein
                };
            }

            public int ProteinsMapped { get; set; }
            public int ProteinsUnmapped { get; set; }
            public int PeptidesMapped { get; set; }
            public int PeptidesUnmapped { get; set; }

            public bool GroupProteins { get; set; }
            public bool FindMinimalProteinList { get; set; }
            public bool RemoveSubsetProteins { get; set; }
            public SharedPeptides SharedPeptides { get; set; }
            public int MinPeptidesPerProtein { get; set; }

            public ParsimonySettings ParsimonySettings => new ParsimonySettings(GroupProteins, FindMinimalProteinList,
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
        }

        public void ApplyParsimonyOptions(bool groupProteins, bool findMinimalProteinList, bool removeSubsetProteins, SharedPeptides sharedPeptides, int minPeptidesPerProtein, ILongWaitBroker broker)
        {
            Dictionary<PeptideDocNode, List<IProteinRecord>> peptideToProteinGroups = _peptideToProteins;

            broker.Message = Resources.AssociateProteinsDlg_UpdateParsimonyResults_Applying_parsimony_options;

            _peptidesRemovedByFilters = new HashSet<PeptideDocNode>();

            if (groupProteins)
            {
                if (_proteinGroupAssociations == null)
                {
                    _proteinGroupResults = _results.Clone();
                    _proteinGroupResults.GroupProteins = true;
                    _proteinGroupAssociations = CalculateProteinGroups(_proteinGroupResults, broker);

                    if (_proteinGroupAssociations == null)
                        return;
                }

                _finalResults = _proteinGroupResults.Clone();
                ParsimoniousProteins = _proteinGroupAssociations;
                peptideToProteinGroups = _peptideToProteinGroups;
            }
            else
            {
                _finalResults = _results;
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

        private Dictionary<IProteinRecord, PeptideAssociationGroup> CalculateProteinGroups(MappingResultsInternal results, ILongWaitBroker broker)
        {
            var _peptideGroupToProteins = new Dictionary<PeptideAssociationGroup, List<IProteinRecord>>();

            foreach(var kvp in AssociatedProteins)
                if (!_peptideGroupToProteins.ContainsKey(kvp.Value))
                    _peptideGroupToProteins.Add(kvp.Value, new List<IProteinRecord> { kvp.Key });
                else
                    _peptideGroupToProteins[kvp.Value].Add(kvp.Key);

            results.FinalPeptideCount = 0;
            results.FinalProteinCount = _peptideGroupToProteins.Count;

            broker.Message = Resources.ProteinAssociation_CalculateProteinGroups_Calculating_protein_groups;
            var proteinGroupAssociations = new Dictionary<IProteinRecord, PeptideAssociationGroup>();

            _peptideToProteinGroups = new Dictionary<PeptideDocNode, List<IProteinRecord>>();
            Action<IProteinRecord, PeptideAssociationGroup> addPeptideAssociations =
                (protein, peptides) =>
                {
                    foreach (var peptide in peptides.Peptides)
                    {
                        if (!_peptideToProteinGroups.ContainsKey(peptide))
                            _peptideToProteinGroups.Add(peptide, new List<IProteinRecord> {protein});
                        else
                            _peptideToProteinGroups[peptide].Add(protein);
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

                var proteinsByRecordIndex = kvp.Value.OrderBy(p => p.RecordIndex).ToList();
                var proteinGroupName = string.Join(ProteinGroupMetadata.GROUP_SEPARATOR, proteinsByRecordIndex.Select(p => p.Sequence.Name));
                var proteinFastaSequence = new FastaSequenceGroup(proteinGroupName, proteinsByRecordIndex.Select(r => r.Sequence).ToList());
                var proteinGroup = new FastaRecord(kvp.Value[0].RecordIndex, 0, proteinFastaSequence);
                proteinGroupAssociations[proteinGroup] = kvp.Key;
                addPeptideAssociations(proteinGroup, kvp.Key);
            }

            return proteinGroupAssociations;
        }

        /// <summary>
        /// Calculate clusters (connected components) for protein/peptide associations
        /// </summary>
        private Dictionary<int, IEnumerable<IProteinRecord>> CalculateClusters(Dictionary<PeptideDocNode, List<IProteinRecord>> peptideToProteinGroups, ILongWaitBroker broker)
        {
            var clusterByProteinGroup = new Dictionary<IProteinRecord, int>();
            int clusterId = 0;
            var clusterStack = new Stack<KeyValuePair<IProteinRecord, PeptideAssociationGroup>>();

            int proteinsProcessed = 0;

            broker.Message = Resources.ProteinAssociation_Calculating_protein_clusters;
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

        private ISet<IProteinRecord> FindMinimalProteinSet(Dictionary<PeptideDocNode, List<IProteinRecord>> peptideToProteinGroups, ILongWaitBroker broker)
        {
            var proteinsByCluster = CalculateClusters(peptideToProteinGroups, broker);

            broker.Message = Resources.ProteinAssociation_Finding_minimal_protein_list;
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

        private ISet<IProteinRecord> FindSubsetProteins(Dictionary<PeptideDocNode, List<IProteinRecord>> peptideToProteinGroups, ILongWaitBroker broker)
        {
            var proteinsByCluster = CalculateClusters(peptideToProteinGroups, broker);

            broker.Message = Resources.ProteinAssociation_Removing_subset_proteins;
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

            broker.Message = Resources.ProteinAssociation_ListPeptidesForMatching_Building_peptide_prefix_tree;

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

                _peptideToPath = peptidesForMatching.GroupBy(node => GetPeptideSequence(node.Peptide)).ToDictionary(k => k.Key, g => g.ToList());
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
            var status = new ProgressStatus(Resources.ProteinAssociation_CreateDocTree_Creating_protein_targets_and_assigning_their_peptides);
            monitor.UpdateProgress(status);

            // Protein associations may be out of order because of multi-threading, so put them back in order.
            var proteinAssociationsList = ParsimoniousProteins.OrderBy(kvp => kvp.Key.RecordIndex).ToList();

            var newPeptideGroups = new List<PeptideGroupDocNode>(); // all groups that will be added in the new document
            var appendPeptideLists = new List<PeptideGroupDocNode>();

            // Move unmapped peptides from FastaSequence node to "Unmapped Peptides" list
            var unmappedPeptideNodes = _peptideToPath.SelectMany(kvp => kvp.Value).Where(p => !p.IsDecoy).ToHashSet();
            unmappedPeptideNodes.ExceptWith(ParsimoniousProteins.Values.SelectMany(pag => pag.Peptides));
            unmappedPeptideNodes.ExceptWith(_peptidesRemovedByFilters);

            // Modifies and adds old groups that still contain unmatched peptides to newPeptideGroups
            foreach (var nodePepGroup in current.MoleculeGroups)
            {
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
                    unmappedPeptideNodes.ExceptWith(peptideDocNodes); // do not count iRT peptides as unmapped
                    continue;
                }

                // Keep peptide lists that contain unmapped peptides
                if (nodePepGroup.IsProteomic && nodePepGroup.IsPeptideList)
                {
                    // If a peptide list peptide is unmapped, leave it in the peptide list but remove it from the global unmapped list
                    var peptidesByMappedStatus = peptideDocNodes.ToLookup(node => _peptideToProteins.Contains(kvp => kvp.Key.Target == node.Target), node => node);
                    var unmappedPeptides = peptidesByMappedStatus[false].ToHashSet();
                    unmappedPeptideNodes.ExceptWith(unmappedPeptides);

                    // If it was mapped, remove it from the peptide list
                    var mappedPeptides = peptidesByMappedStatus[true];
                    var mappedPeptideIndexes = mappedPeptides.Select(node => node.Peptide.GlobalIndex);
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
                var unmappedNakedPeptides = unmappedPeptideNodes.Select(node => node.RemoveFastaSequence());
                var unmappedPeptideList = new PeptideGroupDocNode(new PeptideGroup(), Annotations.EMPTY,
                    Resources.ProteinAssociation_CreateDocTree_Unmapped_Peptides, string.Empty, unmappedNakedPeptides.ToArray());
                appendPeptideLists.Add(unmappedPeptideList);
            }

            int totalPeptideGroups = newPeptideGroups.Count + appendPeptideLists.Count + proteinAssociationsList.Count;

            // Adds all new groups/proteins to newPeptideGroups
            foreach (var keyValuePair in proteinAssociationsList)
            {
                var protein = keyValuePair.Key.Sequence;
                var children = new List<PeptideDocNode>();
                foreach (PeptideDocNode peptideDocNode in keyValuePair.Value.Peptides)
                {
                    // do not reassociate iRT list peptides to proteins
                    if (!children.Contains(p => p.ModifiedTarget.Equals(peptideDocNode.ModifiedTarget)))
                        children.Add(peptideDocNode.ChangeFastaSequence(protein));
                }

                var proteinOrGroupMetadata = protein is FastaSequenceGroup
                    ? new ProteinGroupMetadata((protein as FastaSequenceGroup).FastaSequenceList.Select(s => new ProteinMetadata(s.Name, s.Description)).ToList())
                    : new ProteinMetadata(protein.Name, protein.Description);
                var peptideGroupDocNode = new PeptideGroupDocNode(protein, proteinOrGroupMetadata, children.ToArray());
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
            foreach (var fastaData in FastaData.ParseFastaFile(new StreamReader(stream)))
            {
                yield return new FastaRecord(index, (int) (stream.Position * 100 / streamLength), fastaData);
                index++;
            }
        }

        public interface IProteinRecord
        {
            int RecordIndex { get; }
            FastaSequence Sequence { get; }
            int Progress { get; }
        }

        public interface IProteinSource
        {
            IEnumerable<IProteinRecord> Proteins { get; }
        }

        private class BackgroundProteomeRecord : IProteinRecord
        {
            public BackgroundProteomeRecord(int index, FastaSequence sequence, int progress)
            {
                RecordIndex = index;
                Sequence = sequence;
                Progress = progress;
            }

            public int RecordIndex { get; }
            public FastaSequence Sequence { get; }
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
                        yield return new BackgroundProteomeRecord(i, Proteome.MakeFastaSequence(DbProteins[i]), i * 100 / DbProteins.Count);
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
            public FastaRecord(int recordIndex, int progress, FastaSequence fastaSequence)
            {
                RecordIndex = recordIndex;
                Progress = progress;
                Sequence = fastaSequence;
            }

            public int RecordIndex { get; }
            public int Progress { get; }
            public FastaSequence Sequence { get; }
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
