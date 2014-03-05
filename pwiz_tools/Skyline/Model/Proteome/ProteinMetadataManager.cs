/*
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
    /// If background proteome is under construction, or having its own
    /// protein metadata resolved, politely waits for that to complete.
    /// </summary>
    public sealed class ProteinMetadataManager : BackgroundLoader
    {
        private WebEnabledFastaImporter _fastaImporter = new WebEnabledFastaImporter(); // Default is to actually go to the web
        public WebEnabledFastaImporter FastaImporter
        {
            get { return _fastaImporter; }
            set { _fastaImporter = value; }  // Tests may override with an object that simulates web access
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            // The only way this manager can go from loaded to not loaded is if the peptide groups change,
            // or the background proteome has finished loading and has protein metadata for us
            return  (previous == null) || !ReferenceEquals(document.Children, previous.Children) ||
                !ReferenceEquals(document.Settings.PeptideSettings.BackgroundProteome,
                    previous.Settings.PeptideSettings.BackgroundProteome);
        }

        /// <summary>
        /// Returns true if the document's PeptideGroupDocNodes have fully resolved
        /// protein metadata (as evidenced by them claiming not to need a web search)
        /// </summary>
        /// <param name="document">the document to inspect</param>
        /// <returns>true if document contains no PeptideGroupDocNodes needing a web search for protein metadata</returns>
        public static bool IsLoadedDocument(SrmDocument document)
        {
            if (document == null)
                return false;
            // Any PeptideGroupDocNodes having PeptideGroup IDs that need the rest of their protein metadata updated?
            foreach (PeptideGroupDocNode nodePepGroup in document.PeptideGroups)
            {
                if (nodePepGroup.ProteinMetadata.NeedsSearch())
                    return false; // not loaded - we need to do a web search
            }
            return true;
        }
      

        protected override bool IsLoaded(SrmDocument document)
        {
            if (document == null)
                return true; // no work needed

            if (IsLoadedDocument(document))
                return true; // no work needed

            // We don't want to proceed if the background proteome (if any) isn't complete,
            // with its protein metadata resolved, since we prefer to pull our protein metadata from there
            if (!BackgroundProteomeManager.DocumentHasLoadedBackgroundProteomeOrNone(document,true))
                return true; // no work at this moment, thanks - we'll wait for background proteome

            return false; // there is work to do
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            return new IPooledStream[0];
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return !ReferenceEquals(container.Document, tag);
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document,
            SrmDocument docCurrent)
        {
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
            return true;
        }

        private SrmDocument LookupProteinMetadata(SrmDocument documentIn, IProgressMonitor progressMonitor)
        {

            var progressStatus = new ProgressStatus(Resources.ProteinMetadataManager_LookupProteinMetadata_resolving_protein_details);
            int nResolved = 0;
            int nUnresolved = documentIn.PeptideGroups.Select(pg => pg.ProteinMetadata.NeedsSearch()).Count();

            var docNew = documentIn;
            if ((nUnresolved>0) && !documentIn.Settings.PeptideSettings.BackgroundProteome.IsNone)
            {
                // Do a quick check to see if background proteome already has the info
                if (documentIn.Settings.PeptideSettings.BackgroundProteome.NeedsProteinMetadataSearch)
                {
                    // Background proteome loader needs to catch up - get out of the way
                    progressMonitor.UpdateProgress(progressStatus.Complete());
                    return docNew;
                }
                try
                {
                    using (var proteomeDb = documentIn.Settings.PeptideSettings.BackgroundProteome.OpenProteomeDb())
                    {
                        foreach (PeptideGroupDocNode nodePepGroup in documentIn.PeptideGroups)
                        {
                            bool needsSearch = nodePepGroup.ProteinMetadata.NeedsSearch();
                            ProteinMetadata p = null;
                            if (needsSearch)
                            {
                                var dbProtein = proteomeDb.GetProteinByName(nodePepGroup.Name);
                                if ((dbProtein == null) && !String.IsNullOrEmpty(nodePepGroup.ProteinMetadata.Accession))
                                    // possibly renamed, parsed accession might hit
                                    dbProtein = proteomeDb.GetProteinByName(nodePepGroup.ProteinMetadata.Accession);
                                if (dbProtein != null)
                                    p = dbProtein.ProteinMetadata;
                            }
                            if (p != null)
                            {
                                if (!p.NeedsSearch())
                                {
                                    // Background proteome has already resolved this
                                    docNew = (SrmDocument) docNew.ReplaceChild(nodePepGroup.ChangeProteinMetadata(p));
                                    progressMonitor.UpdateProgress(
                                        progressStatus =
                                            progressStatus.ChangePercentComplete(100*nResolved++/nUnresolved));
                                }
                                else
                                {
                                    // Background proteome loader needs to catch up - get out of the way
                                    progressMonitor.UpdateProgress(progressStatus.Complete());
                                    return docNew;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // DB file is busy, just try again later
                    progressMonitor.UpdateProgress(progressStatus.Complete());
                    return docNew;
                }
            }

            if (nResolved == nUnresolved)
            {
                progressMonitor.UpdateProgress(progressStatus.Complete());
                return docNew; // Nothing more to do 
            }

            try
            {
                // Now go to the web for missing metadata
                // Does everybody have a search term yet?
                bool foundUnparsedProteins = false; 
                foreach (PeptideGroupDocNode node in docNew.PeptideGroups)
                {
                    if (node.ProteinMetadata.WebSearchInfo.IsEmpty()) // Never even been hit with regex
                    {
                        var parsedProteinMetaData = FastaImporter.ParseProteinMetaData(node.ProteinMetadata); // Use Regexes to get some metadata, and a search term
                        if (parsedProteinMetaData == null) // Didn't match anything
                        {
                            parsedProteinMetaData = node.ProteinMetadata;
                            parsedProteinMetaData = parsedProteinMetaData.SetWebSearchCompleted(); // At a minimum take this out of consideration for next time
                        }
                        progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * nResolved++ / nUnresolved));
                        docNew = (SrmDocument)docNew.ReplaceChild(node.ChangeProteinMetadata(parsedProteinMetaData));
                        foundUnparsedProteins = true;
                    }
                }
                if (foundUnparsedProteins)
                {
                    progressMonitor.UpdateProgress(progressStatus.Complete());
                    return docNew; // An excellent intermediate step - we added some intelligence inexpensively
                }

                // Now go to the web for more protein metadata (or pretend to, depending on WebEnabledFastaImporter.DefaultWebAccessMode)
                var docNodesWithUnresolvedProteinMetadata = new Dictionary<DbProteinName,PeptideGroupDocNode>(); 
                var results = new List<DbProteinName>(); // DbProteinName is a convenient container for immutable ProteinMetadata objects
                foreach (PeptideGroupDocNode node in docNew.PeptideGroups)
                {
                    if (node.ProteinMetadata.NeedsSearch())
                    {
                        results.Add(new DbProteinName(null, node.ProteinMetadata)); // Make a copy of the node's protein metadata
                        docNodesWithUnresolvedProteinMetadata.Add(results.Last(), node);
                    }
                }
                progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * nResolved / nUnresolved));
                foreach (var result in FastaImporter.DoWebserviceLookup(results, true)) // Just do one batch, come back later for more
                {
                    Debug.Assert(!result.GetProteinMetadata().NeedsSearch());
                    docNew = (SrmDocument)docNew.ReplaceChild(docNodesWithUnresolvedProteinMetadata[result].ChangeProteinMetadata(result.GetProteinMetadata()));
                    progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * nResolved++ / nUnresolved));
                }
            }
            catch (OperationCanceledException)
            {
                progressMonitor.UpdateProgress(progressStatus.Cancel());
                return null;
            }

            progressMonitor.UpdateProgress(progressStatus.Complete());
            return docNew;
        }
    }

}
