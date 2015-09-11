/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Proteome
{
    public sealed class BackgroundProteomeManager : BackgroundLoader
    {

        private readonly object _lockLoadBackgroundProteome = new object();

        private WebEnabledFastaImporter _fastaImporter = new WebEnabledFastaImporter(); // Default is to actually go to the web
        public WebEnabledFastaImporter FastaImporter
        { 
            get { return _fastaImporter; }
            set { _fastaImporter = value; }  // Tests may override with an object that simulates web access
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            if (previous == null)
            {
                return true;
            }
            if (!ReferenceEquals(GetBackgroundProteome(document), GetBackgroundProteome(previous)))
            {
                return true;
            }
            if (!Equals(GetEnzyme(document), GetEnzyme(previous)))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns null if the background proteome is loaded -
        /// this means either that the doc does not have one, or
        /// the one it has exists and has the digest completed 
        /// and that the proteins in the db have
        /// had their protein metadata (accession, gene etc) resolved.
        /// Otherwise returns a string describing the unloaded state.
        /// </summary>
        /// <param name="document">the document whose background proteome we're checking</param>
        /// <returns>null if doc has no background proteome, or if the one 
        /// it does have is loaded and digested with all proten metadata ready to go.
        /// Otherwise, a string describing the current unloaded state.</returns>
        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            return IsNotLoadedExplained(document, GetBackgroundProteome(document), true);
        }

        private static string IsNotLoadedExplained(SrmDocument document, BackgroundProteome backgroundProteome, bool requireResolvedProteinMetadata)
        {
            return DocumentHasLoadedBackgroundProteomeOrNoneExplained(document, backgroundProteome, requireResolvedProteinMetadata);
        }

        /// <summary>
        /// returns true if the background proteome is loaded -
        /// this means either that the doc does not have one, or
        /// the one it has exists and has the digest completed -
        /// and optionally that the proteins in the db have
        /// had their protein metadata (accession, gene etc) resolved.
        /// </summary>
        /// <param name="document">the document whose background proteome we're checking</param>
        /// <param name="requireResolvedProteinMetadata">if true, require that any proteins
        /// in the db have their metadata (accession, gene etc) resolved</param>
        /// <returns>true if doc has no background proteome, or if the one 
        /// it does have is loaded and digested (and optionally with all 
        /// proten metadata ready to go</returns>
        public static bool DocumentHasLoadedBackgroundProteomeOrNone(SrmDocument document, bool requireResolvedProteinMetadata)
        {
            return DocumentHasLoadedBackgroundProteomeOrNoneExplained(document, GetBackgroundProteome(document), requireResolvedProteinMetadata) == null;
        }

        private static string DocumentHasLoadedBackgroundProteomeOrNoneExplained(SrmDocument document, BackgroundProteome backgroundProteome, bool requireResolvedProteinMetadata)
        {
            if (backgroundProteome.IsNone)
            {
                return null;
            }
            if (!backgroundProteome.DatabaseValidated)
            {
                return "BackgroundProteomeManager: !backgroundProteome.DatabaseValidated"; // Not L10N
            }
            if (backgroundProteome.DatabaseInvalid)
            {
                return null;
            }
            var peptideSettings = document.Settings.PeptideSettings;

            if (!backgroundProteome.HasDigestion(peptideSettings))
            {
                return "BackgroundProteomeManager: !backgroundProteome.HasDigestion(peptideSettings)"; // Not L10N
            }
            if (!requireResolvedProteinMetadata || (!backgroundProteome.NeedsProteinMetadataSearch))
            {
                return null;
            }
            if (backgroundProteome.NeedsProteinMetadataSearch)
            {
                return "BackgroundProteomeManager: NeedsProteinMetadataSearch"; // Not L10N
            }
            return "BackgroundProteomeManager: requireResolvedProteinMetadata"; // Not L10N

        }


        private static BackgroundProteome GetBackgroundProteome(SrmDocument document)
        {
            return document.Settings.PeptideSettings.BackgroundProteome;
        }

        private static Enzyme GetEnzyme(SrmDocument document)
        {
            return document.Settings.PeptideSettings.Enzyme;
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            yield break;
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return false;
        }

        private static SrmDocument ChangeBackgroundProteome(SrmDocument document, BackgroundProteome backgroundProteome)
        {
            return document.ChangeSettings(
                document.Settings.ChangePeptideSettings(setP => setP.ChangeBackgroundProteome(backgroundProteome)));
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            // Only allow one background proteome to load at a time.  This can
            // get tricky, if the user performs an undo and then a redo across
            // a change in background proteome.
            // Our first priority is doing the digestions, the second is accessing web
            // services to add missing protein metadata.
            lock (_lockLoadBackgroundProteome)
            {
                BackgroundProteome originalBackgroundProteome = GetBackgroundProteome(docCurrent);
                // Check to see whether the Digestion already exists but has not been queried yet.
                BackgroundProteome backgroundProteomeWithDigestions = new BackgroundProteome(originalBackgroundProteome, true);
                if (IsNotLoadedExplained(docCurrent, backgroundProteomeWithDigestions, true) == null)
                {
                    // digest is ready, and protein metdata is resolved
                    CompleteProcessing(container, backgroundProteomeWithDigestions);
                    return true;
                }
                // are we here to do the digest, or to resolve the protein metadata?
                bool getMetadata = (IsNotLoadedExplained(docCurrent, backgroundProteomeWithDigestions, false) == null) &&
                    backgroundProteomeWithDigestions.NeedsProteinMetadataSearch; 

                string name = originalBackgroundProteome.Name;
                ProgressStatus progressStatus =
                    new ProgressStatus(string.Format(getMetadata?Resources.BackgroundProteomeManager_LoadBackground_Resolving_protein_details_for__0__proteome:Resources.BackgroundProteomeManager_LoadBackground_Digesting__0__proteome, name));
                try
                {
                    using (FileSaver fs = new FileSaver(originalBackgroundProteome.DatabasePath, StreamManager))
                    {
                        File.Copy(originalBackgroundProteome.DatabasePath, fs.SafeName, true);
                        var digestHelper = new DigestHelper(this, container, docCurrent, name, fs.SafeName, true);
                        bool success;
                        if (getMetadata)
                            success = digestHelper.LookupProteinMetadata(ref progressStatus);
                        else
                            success = (digestHelper.Digest(ref progressStatus) != null);

                        if (!success)
                        {
                            // Processing was canceled
                            EndProcessing(docCurrent);
                            UpdateProgress(progressStatus.Cancel());
                            return false;
                        }
                        using (var proteomeDb = ProteomeDb.OpenProteomeDb(originalBackgroundProteome.DatabasePath))
                        {
                            proteomeDb.DatabaseLock.AcquireWriterLock(int.MaxValue);
                            try
                            {
                                if (!fs.Commit())
                                {
                                    EndProcessing(docCurrent);
                                    throw new IOException(
                                        string.Format(
                                            Resources
                                                .BackgroundProteomeManager_LoadBackground_Unable_to_rename_temporary_file_to__0__,
                                            fs.RealName));
                                }
                            }
                            finally
                            {
                                proteomeDb.DatabaseLock.ReleaseWriterLock();
                            }
                        }


                        CompleteProcessing(container, new BackgroundProteome(originalBackgroundProteome, true));
                        UpdateProgress(progressStatus.Complete());
                        return true;
                    }
                }
                catch (Exception x)
                {
                    var message = new StringBuilder();
                    message.AppendLine(
                        string.Format(Resources.BackgroundProteomeManager_LoadBackground_Failed_updating_background_proteome__0__,
                                      name));
                    message.Append(x.Message); 
                    UpdateProgress(progressStatus.ChangeErrorException(new IOException(message.ToString(), x)));
                    return false;
                }
            }
        }

        private void CompleteProcessing(IDocumentContainer container, BackgroundProteome backgroundProteomeWithDigestions)
        {
            SrmDocument docCurrent;
            SrmDocument docNew;
            do
            {
                docCurrent = container.Document;
                docNew = ChangeBackgroundProteome(docCurrent, backgroundProteomeWithDigestions);
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
        }

        private sealed class DigestHelper
        {
            private readonly BackgroundProteomeManager _manager;
            private readonly IDocumentContainer _container;
            private readonly SrmDocument _document;
            private readonly string _nameProteome;
            private readonly string _pathProteome;
            private readonly bool _isTemporary;  // Are we doing this work on a temporary copy of the DB?

            private ProgressStatus _progressStatus;

            public DigestHelper(BackgroundProteomeManager manager,
                                IDocumentContainer container,
                                SrmDocument document,
                                string nameProteome,
                                string pathProteome,
                                bool isTemporary)
            {
                _manager = manager;
                _container = container;
                _document = document;
                _nameProteome = nameProteome;
                _pathProteome = pathProteome;
                _isTemporary = isTemporary;
            }

// ReSharper disable RedundantAssignment
            public Digestion Digest(ref ProgressStatus progressStatus)
// ReSharper restore RedundantAssignment
            {
                using (var proteomeDb = ProteomeDb.OpenProteomeDb(_pathProteome,_isTemporary))
                {
                    var enzyme = _document.Settings.PeptideSettings.Enzyme;

                    _progressStatus = new ProgressStatus(
                        string.Format(Resources.DigestHelper_Digest_Digesting__0__proteome_with__1__, _nameProteome, enzyme.Name));
                    var digestion = proteomeDb.Digest(new ProteaseImpl(enzyme), Progress);
                    progressStatus = _progressStatus;

                    return digestion;
                }
            }

            // ReSharper disable RedundantAssignment
            public bool LookupProteinMetadata(ref ProgressStatus progressStatus)
            // ReSharper restore RedundantAssignment
            {
                if (!_manager.FastaImporter.HasWebAccess()) // Do we even have web access?
                    return false; // Return silently rather than flashing the progress bar

                using (var proteomeDb = ProteomeDb.OpenProteomeDb(_pathProteome, _isTemporary))
                {
                    _progressStatus = new ProgressStatus(
                        string.Format(Resources.BackgroundProteomeManager_LoadBackground_Resolving_protein_details_for__0__proteome,_nameProteome));
                    var result = proteomeDb.LookupProteinMetadata(Progress,_manager.FastaImporter,true); // true means be polite, don't try to resolve all in one go
                    progressStatus = _progressStatus;
                    return result;
                }
            }

            private bool Progress(string taskname, int progress)
            {
                // Cancel if the document state has changed since the digestion started.
                if (_manager.StateChanged(_container.Document, _document))
                    return false;

                _manager.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(progress));
                return true;
            }
        }
    }
}
