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
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
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
        private readonly List<ViewLibraryPepInfo> _listPepInfos;
        private SrmSettings[] _chargeSettingsMap;
        private BackgroundProteome _backgroundProteome;
        private Digestion _digestion;

        public Dictionary<PeptideSequenceModKey, PeptideMatch> PeptideMatches { get; private set; }

        public int MatchedPeptideCount { get; private set; }
        public int SkippedPeptideCount { get; private set; }

        public SrmSettings Settings { get { return _document.Settings; } }

        public DuplicateProteinsFilter HandleDuplicateProteins { get; set; }

        public SrmDocument DocAllPeptides { get; set; }
        public IdentityPath AddAllPeptidesSelectedPath { get; set; }

        public ViewLibraryPepMatching(SrmDocument document,
                                      Library library,
                                      LibrarySpec spec,
                                      byte[] lookupPool,
                                      List<ViewLibraryPepInfo> peptides)
        {
            _document = document;
            _selectedLibrary = library;
            _selectedSpec = spec;
            _lookupPool = lookupPool;
            _listPepInfos = peptides;
            _chargeSettingsMap = new SrmSettings[128];
        }

        public void SetBackgroundProteome(BackgroundProteome backgroundProteome)
        {
            _backgroundProteome = backgroundProteome;
            _digestion = _backgroundProteome.GetDigestion(_document.Settings.PeptideSettings);
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
        // TODO: Make this also ask about any peptides without a match, and maybe change function and dialog name
        public DialogResult EnsureDuplicateProteinFilter(IWin32Window parent, bool single)
        {
            var result = DialogResult.OK;
            var multipleProteinsPerPeptideCount = PeptideMatches.Values.Count(
                pepMatch => pepMatch.Proteins != null && pepMatch.Proteins.Count > 1);
            if(multipleProteinsPerPeptideCount > 0)
            {
                var peptideProteinsDlg = new FilterMatchedPeptidesDlg(multipleProteinsPerPeptideCount, single);

                result = peptideProteinsDlg.ShowDialog(parent);
                if (result != DialogResult.Cancel)
                    HandleDuplicateProteins = peptideProteinsDlg.PeptidesDuplicateProteins;
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
            var dictSequenceProteins = new Dictionary<string, IList<Protein>>();
            var dictNewNodePeps = new Dictionary<PeptideSequenceModKey, PeptideMatch>();

            PeptideMatches = null;
            MatchedPeptideCount = 0;

            int peptides = 0;
            int totalPeptides = _listPepInfos.Count();

            foreach (ViewLibraryPepInfo pepInfo in _listPepInfos)
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
                    if (nodePepMatched.HasExplicitMods)
                        Console.Write("Explict mods on {0}", nodePepMatched.Peptide.Sequence);
                    if (!dictNewNodePeps.TryGetValue(nodePepMatched.SequenceKey, out peptideMatchInDict))
                    {
                        IList<Protein> matchedProteins = null;
                        // This is only set if the user has checked the associate peptide box. 
                        if (_backgroundProteome != null)
                        {
                            // We want to query the background proteome as little as possible,
                            // so sequences are mapped to protein lists in a dictionary.
                            var sequence = nodePepMatched.Peptide.Sequence;
                            if (!dictSequenceProteins.TryGetValue(sequence, out matchedProteins))
                            {
                                matchedProteins = _digestion.GetProteinsWithSequence(sequence);
                                dictSequenceProteins.Add(sequence, matchedProteins);
                            }
                        }
                        dictNewNodePeps.Add(nodePepMatched.SequenceKey, new PeptideMatch(nodePepMatched, matchedProteins));
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
                                    peptideMatchInDict.Proteins));
                        }
                    }
                }
                peptides++;
                int progressValue = (int)((peptides + 0.0) / totalPeptides * PERCENT_PEPTIDE_MATCH);
                if (progressValue != broker.ProgressValue)
                    broker.ProgressValue = progressValue;
            }

            PeptideMatches = dictNewNodePeps;
        }

        public PeptideDocNode MatchSinglePeptide(ViewLibraryPepInfo pepInfo)
        {
            _chargeSettingsMap = new SrmSettings[128];
            var nodePep = AssociateMatchingPeptide(pepInfo, pepInfo.Key.Charge).PeptideNode;
            if (nodePep == null)
                return null;

            IList<Protein> matchedProteins = null;

            // This is only set if the user has checked the associate peptide box. 
            if (_backgroundProteome != null)
            {
                var sequence = nodePep.Peptide.Sequence;
                matchedProteins = _digestion.GetProteinsWithSequence(sequence);
            }

            PeptideMatches = new Dictionary<PeptideSequenceModKey, PeptideMatch>
                                 {{nodePep.SequenceKey, new PeptideMatch(nodePep, matchedProteins)}};

            return nodePep;
        }

        public ViewLibraryPepInfo AssociateMatchingPeptide(ViewLibraryPepInfo pepInfo, int charge)
        {
            return AssociateMatchingPeptide(pepInfo, charge, null);
        }

        public ViewLibraryPepInfo AssociateMatchingPeptide(ViewLibraryPepInfo pepInfo, int charge, SrmSettingsDiff settingsDiff)
        {
            var settings = _chargeSettingsMap[charge];
            // Change current document settings to match the current library and change the charge filter to
            // match the current peptide.
            if (settings == null)
            {
                settings = _document.Settings.ChangePeptideLibraries(lib =>
                    lib.ChangeLibraries(new[] { _selectedSpec }, new[] { _selectedLibrary })
                    .ChangePick(PeptidePick.library))
                    .ChangeTransitionFilter(filter =>
                filter.ChangePrecursorCharges(new[] { charge })
                    .ChangeAutoSelect(true))
                .ChangeMeasuredResults(null);

                _chargeSettingsMap[charge] = settings;
            }
            var diff = settingsDiff ?? SrmSettingsDiff.ALL;
            var sequence = pepInfo.GetAASequence(_lookupPool);
            var key = pepInfo.Key;
            Peptide peptide = new Peptide(null, sequence, null, null, 0);
            // Create all variations of this peptide matching the settings.
            foreach (var nodePep in peptide.CreateDocNodes(settings, settings))
            {
                PeptideDocNode nodePepMod = nodePep.ChangeSettings(settings, diff, false);
                foreach (TransitionGroupDocNode nodeGroup in nodePepMod.Children)
                {
                    var calc = settings.GetPrecursorCalc(nodeGroup.TransitionGroup.LabelType, nodePepMod.ExplicitMods);
                    if (calc == null)
                        continue;
                    string modSequence = calc.GetModifiedSequence(nodePep.Peptide.Sequence, false);
                    // If this sequence matches the sequence of the library peptide, a match has been found.
                    if (!Equals(key.Sequence, modSequence))
                        continue;

                    if (settingsDiff == null)
                    {
                        nodePepMod = (PeptideDocNode)nodePepMod.ChangeAutoManageChildren(false);
                    }
                    else
                    {
                        // Keep only the matching transition group, so that modifications
                        // will be highlighted differently for light and heavy forms.
                        // Only performed when getting peptides for display in the explorer.
                        nodePepMod = (PeptideDocNode)nodePep.ChangeChildrenChecked(
                                                         new DocNode[] { nodeGroup });
                    }
                    pepInfo.PeptideNode = nodePepMod;
                    return pepInfo;
                }
            }
            return pepInfo;
        }

        /// <summary>
        /// Adds a list of PeptideDocNodes found in the library to the current document.
        /// </summary>
        public SrmDocument AddPeptides(SrmDocument document, ILongWaitBroker broker, IdentityPath toPath, out IdentityPath selectedPath)
        {
            SkippedPeptideCount = 0;
            var dictCopy = new Dictionary<PeptideSequenceModKey, PeptideMatch>(PeptideMatches);

            SrmDocument newDocument = UpdateExistingPeptides(document, dictCopy);

            selectedPath = toPath;
            if (toPath != null &&
                toPath.Depth == (int)SrmDocument.Level.PeptideGroups &&
                toPath.GetIdentity((int)SrmDocument.Level.PeptideGroups) == SequenceTree.NODE_INSERT_ID)
            {
                toPath = null;
            }

            // If there is an associated background proteome, add peptides that can be
            // matched to the proteins from the background proteom.
            if (_backgroundProteome != null)
            {
                newDocument = AddProteomePeptides(newDocument, dictCopy, broker,
                    toPath, out selectedPath);
            }

            // Add all remaining peptides as a peptide list. 
            var listPeptidesToAdd = dictCopy.Values.ToList();
            listPeptidesToAdd.RemoveAll(match => match.Proteins != null);
            if (listPeptidesToAdd.Count > 0)
            {
                newDocument = AddPeptidesToLibraryGroup(newDocument, listPeptidesToAdd, broker,
                    toPath, out selectedPath);
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
        /// <returns>A new document with precursors for existing petides added</returns>
        // TODO: Add logic to respect HandleDuplicateProteins attribute
        private SrmDocument UpdateExistingPeptides(SrmDocument document,
            IDictionary<PeptideSequenceModKey, PeptideMatch> dictCopy)
        {
            IList<DocNode> nodePepGroups = new List<DocNode>();
            foreach (PeptideGroupDocNode nodePepGroup in document.PeptideGroups)
            {
                IList<DocNode> nodePeps = new List<DocNode>();
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    var key = nodePep.SequenceKey;
                    PeptideMatch peptideMatch;
                    // Peptide is not in our list of peptides to add, so we don't touch it.
                    if (!dictCopy.TryGetValue(key, out peptideMatch))
                        nodePeps.Add(nodePep);
                    else
                    {
                        PeptideDocNode nodePepMatch = peptideMatch.NodePep;
                        PeptideDocNode nodePepSettings = null;
                        var newChildren = nodePep.Children.ToList();
                        foreach (TransitionGroupDocNode nodeGroup in nodePepMatch.Children)
                        {
                            int chargeGroup = nodeGroup.TransitionGroup.PrecursorCharge;
                            if (nodePep.HasChildCharge(chargeGroup))
                                SkippedPeptideCount++;
                            else
                            {
                                if (nodePepSettings == null)
                                    nodePepSettings = nodePepMatch.ChangeSettings(document.Settings, SrmSettingsDiff.ALL);
                                newChildren.Add(nodePepSettings.FindNode(nodeGroup.TransitionGroup));
                            }
                        }
                        newChildren.Sort(Peptide.CompareGroups);
                        var nodePepAdd = nodePep.ChangeChildrenChecked(newChildren);
                        if (nodePep.AutoManageChildren && !ReferenceEquals(nodePep, nodePepAdd))
                            nodePepAdd = nodePepAdd.ChangeAutoManageChildren(false);
                        nodePeps.Add(nodePepAdd);
                        // Remove this peptide from the list of peptides we need to add to the document.
                        dictCopy.Remove(key);
                        if (peptideMatch.Proteins != null)
                        {
                            if (!nodePepGroup.IsPeptideList)
                            {
                                // Remove this protein from the list of proteins associated with the peptide.
                                var proteinName = nodePepGroup.PeptideGroup.Name;
                                peptideMatch.Proteins.RemoveAt(
                                    peptideMatch.Proteins.IndexOf(protein => Equals(protein.Name, proteinName)));
                            }
                            // If this peptide has not yet been added to all matched proteins,
                            // we must put it back in the list of peptides to add.
                            if (peptideMatch.Proteins.Count != 0)
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
            List<PeptideGroupDocNode> nodePepGroupsNew = new List<PeptideGroupDocNode>();

            // Get starting progress values
            int startPercent = (broker != null ? broker.ProgressValue : 0);
            int processedPercent = 0;
            int processedCount = 0;
            int totalMatches = dictCopy.Count;

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
                    (HandleDuplicateProteins == DuplicateProteinsFilter.NoDuplicates && pepMatch.Proteins.Count > 1))
                {
                    continue;                    
                }

                foreach (Protein protein in pepMatch.Proteins)
                {
                    string name = protein.Name;

                    // Look for the protein in the document.
                    var peptideGroupDocNode = FindPeptideGroupDocNode(document, name);
                    bool foundInDoc = peptideGroupDocNode != null;
                    bool foundInList = false;
                    if (!foundInDoc)
                    {
                        // If the protein is not already in the document, 
                        // check to see if we have already created a PeptideGroupDocNode for it. 
                        peptideGroupDocNode = nodePepGroupsNew.Find(nodePepGroup => Equals(nodePepGroup.Name, name));
                        if (peptideGroupDocNode != null)
                            foundInList = true;
                        // If not, create a new PeptideGroupDocNode.
                        else
                        {
                            List<AlternativeProtein> alternativeProteins = new List<AlternativeProtein>();
                            foreach (var alternativeName in protein.AlternativeNames)
                            {
                                alternativeProteins.Add(new AlternativeProtein(alternativeName.Name,
                                                                               alternativeName.Description));
                            }
                            peptideGroupDocNode =
                                new PeptideGroupDocNode(
                                    new FastaSequence(name, protein.Description, alternativeProteins,
                                                      protein.Sequence),
                                    SkylineWindow.GetPeptideGroupId(document, true),
                                    null, new PeptideDocNode[0]);
                        }
                    }

                    // Create a new peptide that matches this protein.
                    var fastaSequence = peptideGroupDocNode.PeptideGroup as FastaSequence;
                    var peptideSequence = pepMatch.NodePep.Peptide.Sequence;
                    // ReSharper disable PossibleNullReferenceException
                    var begin = fastaSequence.Sequence.IndexOf(peptideSequence);
                    // ReSharper restore PossibleNullReferenceException
                    // Create a new PeptideDocNode using this peptide.
                    var newPeptide = new Peptide(fastaSequence,
                                                 peptideSequence,
                                                 begin,
                                                 begin + peptideSequence.Length,
                                                 Settings.PeptideSettings.Enzyme.CountCleavagePoints(
                                                     peptideSequence));
                    // Make sure we keep the same children. 
                    var newNodePep = ((PeptideDocNode) new PeptideDocNode(newPeptide, pepMatch.NodePep.ExplicitMods)
                            .ChangeChildren(pepMatch.NodePep.Children)
                            .ChangeAutoManageChildren(false))
                        .ChangeSettings(document.Settings, SrmSettingsDiff.ALL);
                    // If this PeptideDocNode is already a child of the PeptideGroupDocNode,
                    // ignore it.
                    if (peptideGroupDocNode.Children.Contains(
                        nodePep => Equals(((PeptideDocNode) nodePep).Key, newNodePep.Key)))
                    {
                        Console.WriteLine("Skipping {0} already present", newNodePep.Peptide.Sequence);
                        continue;
                    }
                    // Otherwise, add it to the list of children for the PeptideGroupNode.
                    var newChildren = peptideGroupDocNode.Children.ToList();
                    newChildren.Add(newNodePep);
                    // TODO: Sort only once in a separate loop to avoid O(n^2*log(n)) algorithm
                    //       Store modified proteins by global index in a HashSet for second pass.
                    // Have to convert DocNodes into PeptideDocNodes in order to sort.
                    var newChildrenNodePeps = newChildren.ConvertAll(docNode => (PeptideDocNode) docNode);
                    newChildrenNodePeps.Sort(FastaSequence.ComparePeptides);
                    newChildren = newChildrenNodePeps.ConvertAll(nodePep => (DocNode) nodePep);
                    var newPeptideGroupDocNode = peptideGroupDocNode.ChangeChildren(newChildren)
                        .ChangeAutoManageChildren(false);
                    selectedPath = new IdentityPath(peptideGroupDocNode.Id);
                    // If the protein was already in the document, replace with the new PeptideGroupDocNode.
                    if (foundInDoc)
                        document = (SrmDocument) document.ReplaceChild(newPeptideGroupDocNode);
                    // Otherwise, update the list of new PeptideGroupDocNodes to add.
                    else
                    {
                        if (foundInList)
                            nodePepGroupsNew.Remove(peptideGroupDocNode);
                        nodePepGroupsNew.Add((PeptideGroupDocNode) newPeptideGroupDocNode);
                    }
                    // If the user only wants to add the first protein found, 
                    // we break the foreach loop after peptide has been added to its first protein.
                    if (HandleDuplicateProteins == DuplicateProteinsFilter.FirstOccurence)
                        break;
                }
            }

            return document.AddPeptideGroups(nodePepGroupsNew, false, toPath, out selectedPath);
        }

        private static SrmDocument AddPeptidesToLibraryGroup(SrmDocument document,
                                                             IList<PeptideMatch> listMatches,
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

            // TODO: Use existing group by this name, if present.
            PeptideGroupDocNode nodePepGroupNew = new PeptideGroupDocNode(
                new PeptideGroup(), "Library Peptides", "", listPeptides.ToArray());

            return document.AddPeptideGroups(new[] { nodePepGroupNew }, true, toPath, out selectedPath);
        }

        private static PeptideGroupDocNode FindPeptideGroupDocNode(SrmDocument document, String name)
        {
            foreach (PeptideGroupDocNode peptideGroupDocNode in document.PeptideGroups)
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
            public PeptideMatch(PeptideDocNode nodePep, IList<Protein> proteins) : this()
            {
                NodePep = nodePep;
                Proteins = proteins;
            }

            public PeptideDocNode NodePep { get; private set; }
            public IList<Protein> Proteins { get; private set; }
        }

        // ReSharper disable InconsistentNaming
        public enum DuplicateProteinsFilter
        {
            NoDuplicates,
            FirstOccurence,
            AddToAll
        }
        // ReSharper restore InconsistentNaming
    }    
}
