/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;


namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Matches document peptides to library peptides.
    /// </summary>
    public class ViewLibraryPepMatching
    {
        private readonly SrmDocument _document;
        private readonly Library _selectedLibrary;
        private readonly LibrarySpec _selectedSpec;
        private readonly byte[] _lookupPool;
        private readonly ViewLibraryPepInfo[] _libraryPepInfos;
        private readonly LibKeyModificationMatcher _matcher;
        private SrmSettings[] _chargeSettingsMap;
        private BackgroundProteome _backgroundProteome;
        
        public Dictionary<PeptideSequenceModKey, PeptideMatch> PeptideMatches { get; private set; }

        public int MatchedPeptideCount { get; private set; }
        public int SkippedPeptideCount { get; private set; }

        public SrmSettings Settings { get { return _document.Settings; } }

        public SrmDocument DocAllPeptides { get; set; }
        public IdentityPath AddAllPeptidesSelectedPath { get; set; }

        public ViewLibraryPepMatching(SrmDocument document,
                                      Library library,
                                      LibrarySpec spec,
                                      byte[] lookupPool,
                                      LibKeyModificationMatcher matcher,
                                      ViewLibraryPepInfo[] peptides)
        {
            _document = document;
            _selectedLibrary = library;
            _selectedSpec = spec;
            _lookupPool = lookupPool;
            _matcher = matcher;
            _libraryPepInfos = peptides;
            _chargeSettingsMap = new SrmSettings[128];
        }

        public void SetBackgroundProteome(BackgroundProteome backgroundProteome)
        {
            _backgroundProteome = backgroundProteome;
       }

        /// <summary>
        /// Matches library peptides to the current document settings and adds them to the document.
        /// This needs to be one function so that we can use one LongWaitDlg. 
        /// </summary>
        public void AddAllPeptidesToDocument(ILongWaitBroker broker)
        {
            MatchAllPeptides(broker);
            if (broker.IsCanceled)
                return;

            if (MatchedPeptideCount == 0)
                return;

            if (broker.ShowDialog(EnsureDuplicateProteinFilter) == DialogResult.Cancel)
                return;

            IdentityPath selectedPath;
            IdentityPath toPath = AddAllPeptidesSelectedPath;

             DocAllPeptides = AddPeptides(_document, broker, toPath, out selectedPath);
            AddAllPeptidesSelectedPath = selectedPath;
        }

        public DialogResult EnsureDuplicateProteinFilter(IWin32Window parent)
        {
            return EnsureDuplicateProteinFilter(parent, false);
        }

        /// <summary>
        /// If peptides match to multiple proteins, ask the user what they want to do with these
        /// peptides. 
        /// </summary>
        public DialogResult EnsureDuplicateProteinFilter(IWin32Window parent, bool single)
        {
            var result = DialogResult.OK;
            var multipleProteinsPerPeptideCount = PeptideMatches.Values.Count(
                pepMatch => pepMatch.Proteins != null && pepMatch.Proteins.Count > 1);
            var unmatchedPeptidesCount =
                PeptideMatches.Values.Count(pepMatch => pepMatch.Proteins != null && pepMatch.Proteins.Count == 0);
            var filteredPeptidesCount = PeptideMatches.Values.Count(pepMatch => !pepMatch.MatchesFilterSettings);
            if(multipleProteinsPerPeptideCount > 0 || unmatchedPeptidesCount > 0 || filteredPeptidesCount > 0)
            {
                using (var peptideProteinsDlg =
                    new FilterMatchedPeptidesDlg(multipleProteinsPerPeptideCount, unmatchedPeptidesCount, filteredPeptidesCount, single))
                {
                    result = peptideProteinsDlg.ShowDialog(parent);
                }
            }
            return result;
        }

        private const int PERCENT_PEPTIDE_MATCH = 50;

        /// <summary>
        /// Tries to match each library peptide to document settings.
        /// </summary>
        public void MatchAllPeptides(ILongWaitBroker broker)
        {
            _chargeSettingsMap = new SrmSettings[128];

            // Build a dictionary mapping sequence to proteins because getting this information is slow.
            var dictSequenceProteins = new Dictionary<string, IList<ProteinInfo>>();
            var dictNewNodePeps = new Dictionary<PeptideSequenceModKey, PeptideMatch>();

            PeptideMatches = null;
            MatchedPeptideCount = 0;

            int peptides = 0;
            int totalPeptides = _libraryPepInfos.Length;

            foreach (ViewLibraryPepInfo pepInfo in _libraryPepInfos)
            {
                if (broker.IsCanceled)
                    return;

                int charge = pepInfo.Key.Charge;
                // Find the matching peptide.
                var nodePepMatched = AssociateMatchingPeptide(pepInfo, charge).PeptideNode;
                if (nodePepMatched != null)
                {
                    MatchedPeptideCount++;

                    PeptideMatch peptideMatchInDict;
                    // If peptide is already in the dictionary of peptides to add, merge the children.
                    if (!dictNewNodePeps.TryGetValue(nodePepMatched.SequenceKey, out peptideMatchInDict))
                    {
                        IList<ProteinInfo> matchedProteins = null;

                        var sequence = nodePepMatched.Peptide.Sequence;
                        // This is only set if the user has checked the associate peptide box. 
                        if (_backgroundProteome != null)
                        {
                            // We want to query the background proteome as little as possible,
                            // so sequences are mapped to protein lists in a dictionary.
                            if (!dictSequenceProteins.TryGetValue(sequence, out matchedProteins))
                            {
                                using (var proteomeDb = _backgroundProteome.OpenProteomeDb())
                                {
                                    var digestion = _backgroundProteome.GetDigestion(proteomeDb, Settings.PeptideSettings);
                                    if (digestion != null)
                                    {
                                        matchedProteins = digestion.GetProteinsWithSequence(sequence).Select(protein=>new ProteinInfo(protein)).ToList();
                                        dictSequenceProteins.Add(sequence, matchedProteins);
                                    }
                                }
                            }
                            
                        }
                        dictNewNodePeps.Add(nodePepMatched.SequenceKey, 
                            new PeptideMatch(nodePepMatched, matchedProteins, 
                                MatchesFilter(sequence, charge)));
                    }
                    else
                    {
                        PeptideDocNode nodePepInDictionary = peptideMatchInDict.NodePep;
                        if (!nodePepInDictionary.HasChildCharge(charge))
                        {
                            List<DocNode> newChildren = nodePepInDictionary.Children.ToList();
                            newChildren.AddRange(nodePepMatched.Children);
                            newChildren.Sort(Peptide.CompareGroups);
                            var key = nodePepMatched.SequenceKey;
                            dictNewNodePeps.Remove(key);
                            dictNewNodePeps.Add(key, 
                                new PeptideMatch((PeptideDocNode)nodePepInDictionary.ChangeChildren(newChildren),
                                    peptideMatchInDict.Proteins, peptideMatchInDict.MatchesFilterSettings));
                        }
                    }
                }
                peptides++;
                int progressValue = (int)((peptides + 0.0) / totalPeptides * PERCENT_PEPTIDE_MATCH);
                broker.ProgressValue = progressValue;
            }
            PeptideMatches = dictNewNodePeps;
        }

        public bool MatchesFilter(string sequence, int charge)
        {
            int missedCleavages = Settings.PeptideSettings.Enzyme.CountCleavagePoints(sequence);
            return missedCleavages <= Settings.PeptideSettings.DigestSettings.MaxMissedCleavages
                && Settings.TransitionSettings.Filter.PrecursorCharges.Contains(charge);
        }

        public PeptideDocNode MatchSinglePeptide(ViewLibraryPepInfo pepInfo)
        {
            _chargeSettingsMap = new SrmSettings[128];
            var nodePep = AssociateMatchingPeptide(pepInfo, pepInfo.Key.Charge).PeptideNode;
            if (nodePep == null)
                return null;

            IList<ProteinInfo> matchedProteins = null;

            // This is only set if the user has checked the associate peptide box. 
            var sequence = nodePep.Peptide.Sequence;
            if (_backgroundProteome != null)
            {
                using (var proteomeDb = _backgroundProteome.OpenProteomeDb())
                {
                    var digestion = _backgroundProteome.GetDigestion(proteomeDb, Settings.PeptideSettings);
                    if (digestion != null)
                    {
                        matchedProteins = digestion.GetProteinsWithSequence(sequence).Select(protein=>new ProteinInfo(protein)).ToArray();
                    }
                }
            }
            
            PeptideMatches = new Dictionary<PeptideSequenceModKey, PeptideMatch>
                                 {{nodePep.SequenceKey, new PeptideMatch(nodePep, matchedProteins, 
                                     MatchesFilter(sequence, pepInfo.Key.Charge))}};
            return nodePep;
        }

        public ViewLibraryPepInfo AssociateMatchingPeptide(ViewLibraryPepInfo pepInfo, int charge)
        {
            return AssociateMatchingPeptide(pepInfo, charge, null);
        }

        public ViewLibraryPepInfo AssociateMatchingPeptide(ViewLibraryPepInfo pepInfo, int charge, SrmSettingsDiff settingsDiff)
        {
            var key = pepInfo.Key;

            var settings = _chargeSettingsMap[charge];
            // Change current document settings to match the current library and change the charge filter to
            // match the current peptide.
            if (settings == null)
            {
                settings = _document.Settings;
                var rankId = settings.PeptideSettings.Libraries.RankId;
                if (rankId != null && !_selectedSpec.PeptideRankIds.Contains(rankId))
                    settings = settings.ChangePeptideLibraries(lib => lib.ChangeRankId(null));

                settings = settings.ChangePeptideLibraries(
                        lib => lib.ChangeLibraries(new[] { _selectedSpec }, new[] { _selectedLibrary })
                                  .ChangePick(PeptidePick.library))
                    .ChangeTransitionFilter(
                        filter => filter.ChangePrecursorCharges(new[] { charge }).ChangeAutoSelect(true))
                    .ChangeMeasuredResults(null);

                _chargeSettingsMap[charge] = settings;
            }
           
            var nodePep = _matcher.GetModifiedNode(key, pepInfo.GetAASequence(_lookupPool), settings, settingsDiff);
            if (nodePep != null)
            {
                pepInfo.PeptideNode = nodePep;
            }
            return pepInfo;
        }

        /// <summary>
        /// Adds a list of PeptideDocNodes found in the library to the current document.
        /// </summary>
        public SrmDocument AddPeptides(SrmDocument document, ILongWaitBroker broker, IdentityPath toPath, out IdentityPath selectedPath)
        {
            if (toPath != null &&
                toPath.Depth == (int)SrmDocument.Level.MoleculeGroups &&
                ReferenceEquals(toPath.GetIdentity((int)SrmDocument.Level.MoleculeGroups), SequenceTree.NODE_INSERT_ID))
            {
                toPath = null;
            }
            
            SkippedPeptideCount = 0;
            var dictCopy = new Dictionary<PeptideSequenceModKey, PeptideMatch>();

            // Make heavy mods explicit
            if (PeptideMatches.Values.Contains(match => match.NodePep.HasExplicitMods 
                    && match.NodePep.ExplicitMods.HeavyModifications != null))
            {
                _matcher.ConvertAllHeavyModsExplicit();
            }

            // Call ensure mods on all peptides to be added to the document.
            var listDefStatMods = new MappedList<string, StaticMod>();
            listDefStatMods.AddRange(Properties.Settings.Default.StaticModList);
            listDefStatMods.AddRange(document.Settings.PeptideSettings.Modifications.StaticModifications);

            var listDefHeavyMods = new MappedList<string, StaticMod>();
            listDefHeavyMods.AddRange(Properties.Settings.Default.HeavyModList);
            listDefHeavyMods.AddRange(document.Settings.PeptideSettings.Modifications.HeavyModifications);

            foreach (var key in PeptideMatches.Keys)
            {
                var match = PeptideMatches[key];
                var nodePepDocSet = match.NodePep;
                if (_matcher.MatcherPepMods != null)
                    nodePepDocSet = match.NodePep.EnsureMods(_matcher.MatcherPepMods,
                        document.Settings.PeptideSettings.Modifications,
                        listDefStatMods, listDefHeavyMods);
                if (!dictCopy.ContainsKey(nodePepDocSet.SequenceKey))
                    dictCopy.Add(nodePepDocSet.SequenceKey, 
                        new PeptideMatch(nodePepDocSet, match.Proteins, 
                        match.MatchesFilterSettings));
            }

            if (!Properties.Settings.Default.LibraryPeptidesKeepFiltered)
            {
                // TODO: This removes entire peptides where only a single
                //       precursor does not match.  e.g. the library contains
                //       a singly charged precursor match, but also doubly charged
                dictCopy = dictCopy.Where(match => match.Value.MatchesFilterSettings)
                                   .ToDictionary(match => match.Key, match => match.Value);
            }
            SrmDocument newDocument = UpdateExistingPeptides(document, dictCopy, toPath, out selectedPath);
            toPath = selectedPath;

            // If there is an associated background proteome, add peptides that can be
            // matched to the proteins from the background proteom.
            if (_backgroundProteome != null)
            {
                newDocument = AddProteomePeptides(newDocument, dictCopy, broker,
                    toPath, out selectedPath);
            }
            toPath = selectedPath;

            // Add all remaining peptides as a peptide list.
            if (_backgroundProteome == null ||  Properties.Settings.Default.LibraryPeptidesAddUnmatched)
            {
                var listPeptidesToAdd = dictCopy.Values.ToList();
                listPeptidesToAdd.RemoveAll(match => match.Proteins != null && match.Proteins.Count > 0);
                if (listPeptidesToAdd.Count > 0)
                {
                    newDocument = AddPeptidesToLibraryGroup(newDocument, listPeptidesToAdd, broker,
                                                            toPath, out selectedPath);
                }
            }

            return newDocument;
        }

        /// <summary>
        /// Enumerate all document peptides. If a library peptide already exists in the
        /// current document, update the transition groups for that document peptide and
        /// remove the peptide from the list to add.
        /// </summary>
        /// <param name="document">The starting document</param>
        /// <param name="dictCopy">A dictionary of peptides to peptide matches. All added
        /// peptides are removed</param>
        /// <param name="toPath">Currently selected path.</param>
        /// <param name="selectedPath">Selected path after the nodes have been added</param>
        /// <returns>A new document with precursors for existing petides added</returns>
        private SrmDocument UpdateExistingPeptides(SrmDocument document,
            Dictionary<PeptideSequenceModKey, PeptideMatch> dictCopy,
            IdentityPath toPath, out IdentityPath selectedPath)
        {
            selectedPath = toPath;
            IList<DocNode> nodePepGroups = new List<DocNode>();
            foreach (PeptideGroupDocNode nodePepGroup in document.PeptideGroups)
            {
                IList<DocNode> nodePeps = new List<DocNode>();
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    var key = nodePep.SequenceKey;
                    PeptideMatch peptideMatch;
                    // If this peptide is not in our list of peptides to add, 
                    // or if we are in a peptide list and this peptide has been matched to protein(s),
                    // then we don't touch this particular node.
                    if (!dictCopy.TryGetValue(key, out peptideMatch) ||
                        (nodePepGroup.IsPeptideList && 
                        (peptideMatch.Proteins != null && peptideMatch.Proteins.Any()))) 
                        nodePeps.Add(nodePep);
                    else
                    {
                        var proteinName = nodePepGroup.PeptideGroup.Name;
                        int indexProtein = -1;
                        if (peptideMatch.Proteins != null)
                        {
                            indexProtein =
                                peptideMatch.Proteins.IndexOf(protein => Equals(protein.ProteinMetadata.Name, proteinName));
                            // If the user has opted to filter duplicate peptides, remove this peptide from the list to
                            // add and continue.
                            if(FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.NoDuplicates && peptideMatch.Proteins.Count > 1)
                            {
                                dictCopy.Remove(key);
                                nodePeps.Add(nodePep);
                                continue;
                            }
                            // [1] If this protein is not the first match, and the user has opted to add only the first occurence,  
                            // [2] or if this protein is not one of the matches, and [2a] we are either not in a peptide list
                            // [2b] or the user has opted to filter unmatched peptides, ignore this particular node.
                            if((indexProtein > 0 && FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.FirstOccurence) || 
                               (indexProtein == -1 && 
                               (!nodePepGroup.IsPeptideList || !Properties.Settings.Default.LibraryPeptidesAddUnmatched)))
                            {
                                nodePeps.Add(nodePep);
                                continue;
                            }
                        }
                        // Update the children of the peptide in the document to include the charge state of the peptide we are adding.
                        PeptideDocNode nodePepMatch = peptideMatch.NodePep;
                        PeptideDocNode nodePepSettings = null;
                        var newChildren = nodePep.Children.ToList();
                        Identity nodeGroupChargeId = newChildren.Count > 0 ? newChildren[0].Id : null; 
                        foreach (TransitionGroupDocNode nodeGroup in nodePepMatch.Children)
                        {
                            int chargeGroup = nodeGroup.TransitionGroup.PrecursorCharge;
                            if (nodePep.HasChildCharge(chargeGroup))
                                SkippedPeptideCount++;
                            else
                            {
                                if (nodePepSettings == null)
                                    nodePepSettings = nodePepMatch.ChangeSettings(document.Settings, SrmSettingsDiff.ALL);
                                TransitionGroupDocNode nodeGroupCharge = (TransitionGroupDocNode) nodePepSettings.FindNode(nodeGroup.TransitionGroup);
                                if (nodeGroupCharge == null)
                                {
                                    continue;                                    
                                }
                                if(peptideMatch.Proteins != null && peptideMatch.Proteins.Count() > 1)
                                {
                                    // If we may be adding this specific node to the document more than once, create a copy of it so that
                                    // we don't have two nodes with the same global id.
                                    nodeGroupCharge = (TransitionGroupDocNode) nodeGroupCharge.CopyId();
                                    nodeGroupCharge = (TransitionGroupDocNode) nodeGroupCharge.ChangeChildren(
                                        nodeGroupCharge.Children.ToList().ConvertAll(child => child.CopyId()));
                                }
                                nodeGroupChargeId = nodeGroupCharge.Id;
                                newChildren.Add(nodeGroupCharge);
                            }
                        }
                        // Sort the new peptide children.
                        newChildren.Sort(Peptide.CompareGroups);
                        var nodePepAdd = nodePep.ChangeChildrenChecked(newChildren);
                        // If we have changed the children, need to set automanage children to false.
                        if (nodePep.AutoManageChildren && !ReferenceEquals(nodePep, nodePepAdd))
                            nodePepAdd = nodePepAdd.ChangeAutoManageChildren(false);
                        // Change the selected path.
                        if (PeptideMatches.Count == 1)
                        {
                            selectedPath = nodeGroupChargeId == null
                                                ? new IdentityPath(new[] { nodePepGroup.Id, nodePepAdd.Id })
                                                : new IdentityPath(new[] { nodePepGroup.Id, nodePepAdd.Id, nodeGroupChargeId });
                        }
                        nodePeps.Add(nodePepAdd);
                        // Remove this peptide from the list of peptides we need to add to the document
                        dictCopy.Remove(key);
                        if (peptideMatch.Proteins != null)
                        {
                            if (indexProtein != -1)
                                // Remove this protein from the list of proteins associated with the peptide.
                                peptideMatch.Proteins.RemoveAt(indexProtein);
                            // If this peptide has not yet been added to all matched proteins,
                            // put it back in the list of peptides to add.
                            if (peptideMatch.Proteins.Count != 0 && FilterMultipleProteinMatches != BackgroundProteome.DuplicateProteinsFilter.FirstOccurence)
                                dictCopy.Add(key, peptideMatch);
                        }
                    }
                }
                nodePepGroups.Add(nodePepGroup.ChangeChildrenChecked(nodePeps));
            }
            return (SrmDocument) document.ChangeChildrenChecked(nodePepGroups);
        }

        /// <summary>
        /// Adds all peptides which can be matched to a background proteome to the
        /// proteins in the background proteins, and returns a new document with those
        /// proteins and peptides added.
        /// </summary>
        /// <param name="document">The starting document</param>
        /// <param name="dictCopy">A dictionary of peptides to peptide matches. All added
        /// peptides are removed</param>
        /// <param name="broker">For reporting long wait status</param>
        /// <param name="toPath">Path to the location in the document to add new items</param>
        /// <param name="selectedPath">Path to item in the document that should be selected
        /// after this operation is complete</param>
        /// <returns>A new document with matching peptides and their proteins addded</returns>
        private SrmDocument AddProteomePeptides(SrmDocument document,
                                                Dictionary<PeptideSequenceModKey, PeptideMatch> dictCopy,
                                                ILongWaitBroker broker,
                                                IdentityPath toPath,
                                                out IdentityPath selectedPath)
        {
            // Build a list of new PeptideGroupDocNodes to add to the document.
            var dictPeptideGroupsNew = new Dictionary<string, PeptideGroupDocNode>();

            // Get starting progress values
            int startPercent = (broker != null ? broker.ProgressValue : 0);
            int processedPercent = 0;
            int processedCount = 0;
            int totalMatches = dictCopy.Count;

            // Just to make sure this is set
            selectedPath = toPath;

            foreach (PeptideMatch pepMatch in dictCopy.Values)
            {
                // Show progress, if in a long wait
                if (broker != null)
                {
                    if (broker.IsCanceled)
                    {
                        selectedPath = toPath;
                        return document;
                    }
                    // All peptides with protein get processed in this loop.  Peptides
                    // without proteins get added later.
                    if (pepMatch.Proteins != null)
                        processedCount++;
                    int processPercentNow = processedCount * (100 - startPercent) / totalMatches;
                    if (processedPercent != processPercentNow)
                    {
                        processedPercent = processPercentNow;
                        broker.ProgressValue = startPercent + processedPercent;
                    }
                }
                // Peptide should be added to the document,
                // unless the NoDuplicates radio was selected and the peptide has more than 1 protein associated with it.
                if (pepMatch.Proteins == null ||
                    (FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.NoDuplicates && pepMatch.Proteins.Count > 1))
                    continue;                    
                

                foreach (ProteinInfo protein in pepMatch.Proteins)
                {
                    // Look for the protein in the document.
                    string name = protein.ProteinMetadata.Name;
                    var peptideGroupDocNode = FindPeptideGroupDocNode(document, name);
                    bool foundInDoc = peptideGroupDocNode != null;
                    bool foundInList = false;
                    if (!foundInDoc)
                    {
                        // If the protein is not already in the document, 
                        // check to see if we have already created a PeptideGroupDocNode for it. 
                        if (dictPeptideGroupsNew.TryGetValue(name, out peptideGroupDocNode))
                            foundInList = true;
                        // If not, create a new PeptideGroupDocNode.
                        else
                        {
                            List<ProteinMetadata> alternativeProteins = new List<ProteinMetadata>(protein.Alternatives);
                            peptideGroupDocNode = new PeptideGroupDocNode(
                                    new FastaSequence(name, protein.ProteinMetadata.Description, alternativeProteins, protein.Sequence),
                                    null, null, new PeptideDocNode[0]);
                        }
                    }
                    // Create a new peptide that matches this protein.
                    var fastaSequence = peptideGroupDocNode.PeptideGroup as FastaSequence;
                    var peptideSequence = pepMatch.NodePep.Peptide.Sequence;
                    // ReSharper disable PossibleNullReferenceException
                    var begin = fastaSequence.Sequence.IndexOf(peptideSequence, StringComparison.Ordinal);
                    // ReSharper restore PossibleNullReferenceException
                    // Create a new PeptideDocNode using this peptide.
                    var newPeptide = new Peptide(fastaSequence, peptideSequence, begin, begin + peptideSequence.Length,
                                                 Settings.PeptideSettings.Enzyme.CountCleavagePoints(peptideSequence));
                    // Make sure we keep the same children. 
                    PeptideMatch match = pepMatch;
                    var newNodePep = ((PeptideDocNode) new PeptideDocNode(newPeptide, pepMatch.NodePep.ExplicitMods, pepMatch.NodePep.ExplicitRetentionTime)
                            .ChangeChildren(pepMatch.NodePep.Children.ToList().ConvertAll(nodeGroup =>
                                {
                                    // Create copies of the children in order to prevent transition groups with the same 
                                    // global indices.
                                    var nodeTranGroup = (TransitionGroupDocNode) nodeGroup;
                                    if(match.Proteins != null && match.Proteins.Count() > 1)
                                    {
                                        nodeTranGroup = (TransitionGroupDocNode) nodeTranGroup.CopyId();
                                        nodeTranGroup = (TransitionGroupDocNode) nodeTranGroup.ChangeChildren(
                                            nodeTranGroup.Children.ToList().ConvertAll(nodeTran => nodeTran.CopyId()));
                                    }
                                    return (DocNode) nodeTranGroup;
                                })).ChangeAutoManageChildren(false)).ChangeSettings(document.Settings, SrmSettingsDiff.ALL);
                    // If this PeptideDocNode is already a child of the PeptideGroupDocNode,
                    // ignore it.
                    if (peptideGroupDocNode.Children.Contains(nodePep => Equals(((PeptideDocNode) nodePep).Key, newNodePep.Key)))
                    {
                        Console.WriteLine(Resources.ViewLibraryPepMatching_AddProteomePeptides_Skipping__0__already_present, newNodePep.Peptide.Sequence);
                        continue;
                    }
                    // Otherwise, add it to the list of children for the PeptideGroupNode.
                    var newChildren = peptideGroupDocNode.Children.Cast<PeptideDocNode>().ToList();
                    newChildren.Add(newNodePep);
                    newChildren.Sort(FastaSequence.ComparePeptides);

                    // Store modified proteins by global index in a HashSet for second pass.
                    var newPeptideGroupDocNode = peptideGroupDocNode.ChangeChildren(newChildren.Cast<DocNode>().ToArray())
                        .ChangeAutoManageChildren(false);
                    // If the protein was already in the document, replace with the new PeptideGroupDocNode.
                    if (foundInDoc)
                        document = (SrmDocument)document.ReplaceChild(newPeptideGroupDocNode);
                    // Otherwise, update the list of new PeptideGroupDocNodes to add.
                    else
                    {
                        if (foundInList)
                            dictPeptideGroupsNew.Remove(peptideGroupDocNode.Name);
                        dictPeptideGroupsNew.Add(peptideGroupDocNode.Name, (PeptideGroupDocNode) newPeptideGroupDocNode);
                    }
                    // If we are only adding a single node, select it.
                    if (PeptideMatches.Count == 1)
                        selectedPath = new IdentityPath(new[] {peptideGroupDocNode.Id, newNodePep.Peptide});
                    // If the user only wants to add the first protein found, 
                    // we break the foreach loop after peptide has been added to its first protein.)
                    if (FilterMultipleProteinMatches == BackgroundProteome.DuplicateProteinsFilter.FirstOccurence)
                        break;
                }
            }

            if (dictPeptideGroupsNew.Count == 0)
            {
                return document;
            }

            // Sort the peptides.
            var nodePepGroupsSortedChildren = new List<PeptideGroupDocNode>();
            foreach(PeptideGroupDocNode nodePepGroup in dictPeptideGroupsNew.Values)
            {
                var newChildren = nodePepGroup.Children.ToList();
                // Have to cast all children to PeptideDocNodes in order to sort.
                var newChildrenNodePeps = newChildren.Cast<PeptideDocNode>().ToList();
                newChildrenNodePeps.Sort(FastaSequence.ComparePeptides);
                nodePepGroupsSortedChildren.Add((PeptideGroupDocNode) 
                    nodePepGroup.ChangeChildren(newChildrenNodePeps.Cast<DocNode>().ToArray()));
            }
            // Sort the proteins.
            nodePepGroupsSortedChildren.Sort((node1, node2) => Comparer<string>.Default.Compare(node1.Name, node2.Name));
            IdentityPath selPathTemp = selectedPath, nextAdd;
            document = document.AddPeptideGroups(nodePepGroupsSortedChildren, false,
                toPath, out selectedPath, out nextAdd);
            selectedPath = PeptideMatches.Count == 1 ? selPathTemp : selectedPath;
            return document;
        }

        private static SrmDocument AddPeptidesToLibraryGroup(SrmDocument document,
                                                             ICollection<PeptideMatch> listMatches,
                                                             ILongWaitBroker broker,
                                                             IdentityPath toPath,
                                                             out IdentityPath selectedPath)
        {
            // Get starting progress values
            int startPercent = (broker != null ? broker.ProgressValue : 0);
            int processedPercent = 0;
            int processedCount = 0;
            int totalMatches = listMatches.Count;

            var listPeptides = new List<PeptideDocNode>();
            foreach (var match in listMatches)
            {
                // Show progress, if in a long wait
                if (broker != null)
                {
                    if (broker.IsCanceled)
                    {
                        selectedPath = null;
                        return document;
                    }
                    processedCount++;
                    int processPercentNow = processedCount * (100 - startPercent) / totalMatches;
                    if (processedPercent != processPercentNow)
                    {
                        processedPercent = processPercentNow;
                        broker.ProgressValue = startPercent + processedPercent;
                    }
                }

                listPeptides.Add(match.NodePep.ChangeSettings(document.Settings, SrmSettingsDiff.ALL));
            }

            bool hasVariable =
                listPeptides.Contains(nodePep => nodePep.HasExplicitMods && nodePep.ExplicitMods.IsVariableStaticMods);

            // Use existing group by this name, if present.
            var nodePepGroupNew = FindPeptideGroupDocNode(document, Resources.ViewLibraryPepMatching_AddPeptidesToLibraryGroup_Library_Peptides);
            if(nodePepGroupNew != null)
            {
                var newChildren = nodePepGroupNew.Children.ToList();
                newChildren.AddRange(listPeptides.ConvertAll(nodePep => (DocNode) nodePep));
                selectedPath = (listPeptides.Count == 1 ? new IdentityPath(nodePepGroupNew.Id, listPeptides[0].Id) : toPath);
                nodePepGroupNew = (PeptideGroupDocNode) nodePepGroupNew.ChangeChildren(newChildren);
                if (hasVariable)
                    nodePepGroupNew = (PeptideGroupDocNode) nodePepGroupNew.ChangeAutoManageChildren(false);
                return (SrmDocument) document.ReplaceChild(nodePepGroupNew);   
            }  
            else
            {
                nodePepGroupNew = new PeptideGroupDocNode(new PeptideGroup(), 
                                                          Resources.ViewLibraryPepMatching_AddPeptidesToLibraryGroup_Library_Peptides,
                                                          string.Empty, listPeptides.ToArray());
                if (hasVariable)
                    nodePepGroupNew = (PeptideGroupDocNode) nodePepGroupNew.ChangeAutoManageChildren(false);
                IdentityPath nextAdd;
                document = document.AddPeptideGroups(new[] { nodePepGroupNew }, true,
                    toPath, out selectedPath, out nextAdd);
                selectedPath = new IdentityPath(selectedPath, nodePepGroupNew.Children[0].Id);
                return document;
            }
        }

        private static PeptideGroupDocNode FindPeptideGroupDocNode(SrmDocument document, String name)
        {
            foreach (PeptideGroupDocNode peptideGroupDocNode in document.MoleculeGroups)
            {
                if (peptideGroupDocNode.Name == name)
                {
                    return peptideGroupDocNode;
                }
            }
            return null;
        }

        public struct PeptideMatch
        {
            public PeptideMatch(PeptideDocNode nodePep, IEnumerable<ProteinInfo> proteins, bool matchesFilterSettings) : this()
            {
                NodePep = nodePep;
                Proteins = proteins == null ? null : proteins.ToList();
                MatchesFilterSettings = matchesFilterSettings;
            }

            public PeptideDocNode NodePep { get; set; }

            public List<ProteinInfo> Proteins { get; set; }
            public bool MatchesFilterSettings { get; private set; }

        }
        public class ProteinInfo
        {
            public ProteinInfo(Protein protein)
            {
                ProteinMetadata = protein.ProteinMetadata;
                Sequence = protein.Sequence;
                Alternatives = ImmutableList.ValueOf(protein.AlternativeNames);
            }
            public string Sequence { get; private set; }
            public ProteinMetadata ProteinMetadata { get; private set; }
            public IList<ProteinMetadata> Alternatives { get; private set; }
        }



        public static BackgroundProteome.DuplicateProteinsFilter FilterMultipleProteinMatches
        {
            get
            {
                return Helpers.ParseEnum(Properties.Settings.Default.LibraryPeptidesAddDuplicatesEnum,
                                         BackgroundProteome.DuplicateProteinsFilter.AddToAll);
            }
        }
    }    
}
