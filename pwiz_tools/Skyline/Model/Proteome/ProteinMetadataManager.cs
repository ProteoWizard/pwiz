﻿/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;


namespace pwiz.Skyline.Model.Proteome
{
    /// <summary>
    /// Crawls the Skyline doc looking for unresolved protein metdata, and
    /// accesses background proteome db and web services as needed.
    /// Will use the background proteome db's protein info by preference,
    /// but will use web services if that's not available.  This way the user
    /// gets a quick answer for accession numbers etc without waiting for the
    /// entire background proteome to update its own protein metadata.
    /// </summary>
    public sealed class ProteinMetadataManager : BackgroundLoader
    {
        private readonly Dictionary<int, ProteinMetadata> _processedNodes = new Dictionary<int, ProteinMetadata>(); // Preserves our efforts if we're interrupted
        private WebEnabledFastaImporter _fastaImporter = new WebEnabledFastaImporter(); // Tests may swap this out for other fake interfaces
        public WebEnabledFastaImporter FastaImporter
        {
            get { return _fastaImporter; }
            set { _fastaImporter = value; }  // Tests may override with an object that simulates web access, bad web access, etc
        }

        public override void ClearCache()
        {
            lock (_processedNodes)
            {
                _processedNodes.Clear();
            }
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            // We're interested if the document has unresolved protein metadata.
            return document.IsProteinMetadataPending;
        }

        /// <summary>
        /// Returns null if the document's PeptideGroupDocNodes have fully resolved
        /// protein metadata (as evidenced by them claiming not to need a web search).
        /// Otherwise, a string describing how the doc is not loaded in this regard.
        /// </summary>
        /// <param name="document">the document to inspect</param>
        /// <returns>null if document contains no PeptideGroupDocNodes needing a web search for protein metadata</returns>
        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            return IsNotLoadedExplainedHelper(document);
        }
      
        private static string IsNotLoadedExplainedHelper(SrmDocument document)
        {
            if (document == null)
                return @"no document";
            return !document.IsProteinMetadataPending ? null : @"ProteinMetadataManager: document.IsProteinMetadataPending";
        }

        public static bool IsLoadedDocument(SrmDocument document)
        {
            return IsNotLoadedExplainedHelper(document) == null;
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            return new IPooledStream[0];
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            // For our purposes, a better name for this would be "IsInterrupted": if the
            // doc changes out from under us, we just preserve the searches done so far 
            // then get out, and use them in the OnDocumentChange which must be coming.
            if (ReferenceEquals(container.Document, tag)) // "tag" is the doc we started working on
                return false; // We are still working on the same doc

            // If the doc changed, then our work so far may be useless now
            CleanupProcessedNodesDict(container.Document); // Does any of our completed search work apply to the current document?

            return true;
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document,
            SrmDocument docCurrent)
        {

            if (!FastaImporter.HasWebAccess()) // Do we even have web access?
            {
                EndProcessing(container.Document);
                return false; // Return silently rather than flashing the progress bar
            }
            SrmDocument docNew, docOrig;
            do
            {
                docOrig = container.Document;
                var loadMonitor = new LoadMonitor(this, container, docOrig);
                docNew = LookupProteinMetadata(docOrig, loadMonitor);
                if (null == docNew)
                {
                    EndProcessing(docOrig);
                    return false;
                }

            } while (!CompleteProcessing(container, docNew, docOrig));

            CleanupProcessedNodesDict(docNew);  // Drop any completed work, we're done with it

            return true;
        }

        private SrmDocument LookupProteinMetadata(SrmDocument docOrig, IProgressMonitor progressMonitor)
        {
            lock (_processedNodes)
            {
                // Check to make sure this operation was not canceled while this thread was
                // waiting to acquire the lock.  This also cleans up pending work.
                if (progressMonitor.IsCanceled)
                    return null;

                IProgressStatus progressStatus = new ProgressStatus(Resources.ProteinMetadataManager_LookupProteinMetadata_resolving_protein_details);
                int nResolved = 0;
                int nUnresolved = docOrig.PeptideGroups.Select(pg => pg.ProteinMetadata.NeedsSearch()).Count();

                if ((nUnresolved > 0) && !docOrig.Settings.PeptideSettings.BackgroundProteome.IsNone)
                {
                    // Do a quick check to see if background proteome already has the info
                    if (!docOrig.Settings.PeptideSettings.BackgroundProteome.NeedsProteinMetadataSearch)
                    {
                        try
                        {
                            using (var proteomeDb = docOrig.Settings.PeptideSettings.BackgroundProteome.OpenProteomeDb())
                            {
                                foreach (PeptideGroupDocNode nodePepGroup in docOrig.PeptideGroups)
                                {
                                    if (_processedNodes.ContainsKey(nodePepGroup.Id.GlobalIndex))
                                    {
                                        // We did this before we were interrupted
                                        progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * nResolved++ / nUnresolved));
                                    }
                                    else if (nodePepGroup.ProteinMetadata.NeedsSearch())
                                    {
                                        var proteinMetadata = proteomeDb.GetProteinMetadataByName(nodePepGroup.Name);
                                        if ((proteinMetadata == null) && !Equals(nodePepGroup.Name, nodePepGroup.OriginalName))
                                            proteinMetadata = proteomeDb.GetProteinMetadataByName(nodePepGroup.OriginalName); // Original name might hit
                                        if ((proteinMetadata == null) && !String.IsNullOrEmpty(nodePepGroup.ProteinMetadata.Accession))
                                            proteinMetadata = proteomeDb.GetProteinMetadataByName(nodePepGroup.ProteinMetadata.Accession); // Parsed accession might hit
                                        if ((proteinMetadata != null) && !proteinMetadata.NeedsSearch())
                                        {
                                            // Background proteome has already resolved this
                                            _processedNodes.Add(nodePepGroup.Id.GlobalIndex, proteinMetadata);
                                            progressMonitor.UpdateProgress(
                                                progressStatus =
                                                    progressStatus.ChangePercentComplete(100*nResolved++/nUnresolved));
                                        }
                                    }
                                    if (progressMonitor.IsCanceled)
                                    {
                                        progressMonitor.UpdateProgress(progressStatus.Cancel());
                                        return null;
                                    }
                                }
                            }
                        }
                        // ReSharper disable once EmptyGeneralCatchClause
                        catch
                        {
                            // The protDB file is busy, or some other issue - just go directly to web
                        }
                    }
                }
                if (nResolved != nUnresolved)
                {
                    try
                    {
                        // Now go to the web for more protein metadata (or pretend to, depending on WebEnabledFastaImporter.DefaultWebAccessMode)
                        var docNodesWithUnresolvedProteinMetadata = new Dictionary<ProteinSearchInfo,PeptideGroupDocNode>(); 
                        var proteinsToSearch = new List<ProteinSearchInfo>(); 
                        foreach (PeptideGroupDocNode node in docOrig.PeptideGroups)
                        {
                            if (node.ProteinMetadata.NeedsSearch() && !_processedNodes.ContainsKey(node.Id.GlobalIndex)) // Did we already process this?
                            {
                                var proteinMetadata = node.ProteinMetadata;
                                if (proteinMetadata.WebSearchInfo.IsEmpty()) // Never even been hit with regex
                                {
                                    // Use Regexes to get some metadata, and a search term
                                    var parsedProteinMetaData = FastaImporter.ParseProteinMetaData(proteinMetadata);
                                    if ((parsedProteinMetaData == null) || Equals(parsedProteinMetaData.Merge(proteinMetadata),proteinMetadata.SetWebSearchCompleted()))
                                    {
                                        // That didn't parse well enough to make a search term, or didn't add any new info - just set it as searched so we don't keep trying
                                        _processedNodes.Add(node.Id.GlobalIndex, proteinMetadata.SetWebSearchCompleted());
                                        if (progressMonitor.IsCanceled)
                                        {
                                            progressMonitor.UpdateProgress(progressStatus.Cancel());
                                            return null;
                                        }
                                        progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * nResolved++ / nUnresolved));
                                        proteinMetadata = null;  // No search to be done
                                    }
                                    else
                                    {
                                        proteinMetadata = proteinMetadata.Merge(parsedProteinMetaData);  // Fill in any gaps with parsed info
                                    }
                                }
                                if (proteinMetadata != null)
                                {
                                    // We note the sequence length because it's useful in disambiguating search results
                                    proteinsToSearch.Add(new ProteinSearchInfo(new DbProteinName(null, proteinMetadata), 
                                        node.PeptideGroup.Sequence == null ? 0 : node.PeptideGroup.Sequence.Length)); 
                                    docNodesWithUnresolvedProteinMetadata.Add(proteinsToSearch.Last(), node);
                                }
                            }
                        }
                        if (progressMonitor.IsCanceled)
                        {
                            progressMonitor.UpdateProgress(progressStatus.Cancel());
                            return null;
                        }
                        progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * nResolved / nUnresolved));

                        // Now we actually hit the internet
                        if (proteinsToSearch.Any())
                        {
                            foreach (var result in FastaImporter.DoWebserviceLookup(proteinsToSearch, progressMonitor, false)) // Resolve them all, now
                            {
                                Debug.Assert(!result.GetProteinMetadata().NeedsSearch());
                                _processedNodes.Add(docNodesWithUnresolvedProteinMetadata[result].Id.GlobalIndex, result.GetProteinMetadata());
                                if (progressMonitor.IsCanceled)
                                {
                                    progressMonitor.UpdateProgress(progressStatus.Cancel());
                                    return null;
                                }
                                progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * nResolved++ / nUnresolved));
                            }
                        }                        
                    }
                    catch (OperationCanceledException)
                    {
                        progressMonitor.UpdateProgress(progressStatus.Cancel());
                        return null;
                    }

                }

                // And finally write back to the document
                var listProteins = new List<PeptideGroupDocNode>();
                foreach (PeptideGroupDocNode node in docOrig.MoleculeGroups)
                {
                    if (_processedNodes.ContainsKey(node.Id.GlobalIndex))
                    {
                        listProteins.Add(node.ChangeProteinMetadata(_processedNodes[node.Id.GlobalIndex]));
                    }
                    else
                    {
                        listProteins.Add(node);
                    }
                }
                var docNew = docOrig.ChangeChildrenChecked(listProteins.Cast<DocNode>().ToArray());
                progressMonitor.UpdateProgress(progressStatus.Complete());
                return (SrmDocument)docNew;
            }
        }

        private void CleanupProcessedNodesDict(SrmDocument doc)
        {
            // Clean out any old results we can't use with this doc
            lock (_processedNodes)
            {
                if (_processedNodes.Any())
                {
                    var oldProcessedNodesDict = new Dictionary<int, ProteinMetadata>(_processedNodes);
                    _processedNodes.Clear();
                    foreach (PeptideGroupDocNode nodePepGroup in doc.PeptideGroups)
                    {
                        ProteinMetadata metadata;
                        if (oldProcessedNodesDict.TryGetValue(nodePepGroup.Id.GlobalIndex, out metadata) &&
                            nodePepGroup.ProteinMetadata.NeedsSearch())
                            _processedNodes.Add(nodePepGroup.Id.GlobalIndex, metadata); // That node's Id is still in doc, and we have its metadata
                    }
                }
            }
        }

        /// <summary>
        /// helpful for the many places where user might prefer to think of a protein
        /// in terms of something other than its name
        /// </summary>
        public enum ProteinDisplayMode
        {
            ByName,
            ByAccession,
            ByPreferredName,
            ByGene
        };

        public static ProteinDisplayMode ProteinsDisplayMode(string displayProteinsMode)
        {
            return Helpers.ParseEnum(displayProteinsMode, ProteinDisplayMode.ByName);
        }

        public static string ProteinModalDisplayText(ProteinMetadata metadata, string displayProteinsMode)
        {
            return ProteinModalDisplayText(metadata, ProteinsDisplayMode(displayProteinsMode));
        }

        public static string ProteinModalDisplayText(PeptideGroupDocNode node)
        {
            return ProteinModalDisplayText(node.ProteinMetadata, Settings.Default.ShowPeptidesDisplayMode);
        }

        public static string ProteinModalDisplayText(ProteinMetadata metadata, ProteinDisplayMode displayProteinsMode)
        {
            switch (displayProteinsMode)
            {
                case ProteinDisplayMode.ByAccession:
                case ProteinDisplayMode.ByPreferredName:
                case ProteinDisplayMode.ByGene:
                    break;
                default:
                    return metadata.Name;
            }

            // If the desired field is not populated because it's not yet searched, say so
            if (metadata.NeedsSearch())
                return Resources.ProteinMetadataManager_LookupProteinMetadata_resolving_protein_details;

            // If the desired field is not populated, return something like "<name: YAL01234>"
            var failsafe = String.Format(Resources.PeptideGroupTreeNode_ProteinModalDisplayText__name___0__, metadata.Name);
            switch (displayProteinsMode)
            {
                case ProteinDisplayMode.ByAccession:
                    return metadata.Accession ?? failsafe;
                case ProteinDisplayMode.ByPreferredName:
                    return metadata.PreferredName ?? failsafe;
                case ProteinDisplayMode.ByGene:
                    return metadata.Gene ?? failsafe;
            }
            return failsafe;
        }
    }
}
