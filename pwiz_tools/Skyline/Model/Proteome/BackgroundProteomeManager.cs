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
        private static readonly object _lockLoadBackgroundProteome = new object();

        private SrmSettingsChangeMonitor _monitor; // Used with PeptideSettingsUI

        public bool ForegroundLoadRequested { get; private set; }  // Used with PeptideSettingsUI

        private WebEnabledFastaImporter _fastaImporter = new WebEnabledFastaImporter(); // Default is to actually go to the web
        public WebEnabledFastaImporter FastaImporter
        { 
            get { return _fastaImporter; }
            set { _fastaImporter = value; }  // Tests may override with an object that simulates web access
        }

        public override void ClearCache()
        {
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
            return IsNotLoadedExplained(document.Settings.PeptideSettings, GetBackgroundProteome(document), true);
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
            return IsNotLoadedExplained(document.Settings.PeptideSettings, GetBackgroundProteome(document), requireResolvedProteinMetadata) == null;
        }

        public static string IsNotLoadedExplained(PeptideSettings settings, BackgroundProteome backgroundProteome, bool requireResolvedProteinMetadata)
        {
            if (backgroundProteome.IsNone)
            {
                return null;
            }
            if (!backgroundProteome.DatabaseValidated)
            {
                return @"BackgroundProteomeManager: !backgroundProteome.DatabaseValidated";
            }
            if (backgroundProteome.DatabaseInvalid)
            {
                return null;
            }
            if (!requireResolvedProteinMetadata || !backgroundProteome.NeedsProteinMetadataSearch)
            {
                return null;
            }
            return @"BackgroundProteomeManager: NeedsProteinMetadataSearch";
        }


        private static BackgroundProteome GetBackgroundProteome(SrmDocument document)
        {
            return document.Settings.PeptideSettings.BackgroundProteome;
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            yield break;
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return _monitor == null ? ForegroundLoadRequested : _monitor.IsCanceled(); // Foreground task has monitor, background task may be interrupted by foreground
        }

        private static SrmDocument ChangeBackgroundProteome(SrmDocument document, BackgroundProteome backgroundProteome)
        {
            return document.ChangeSettings(
                document.Settings.ChangePeptideSettings(setP => setP.ChangeBackgroundProteome(backgroundProteome)));
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            return Load(container, docCurrent.Settings.PeptideSettings, docCurrent, true) != null;
        }

        private BackgroundProteome Load(IDocumentContainer container, PeptideSettings settings, SrmDocument docCurrent, bool isBackgroundLoad)
        {
            // Only allow one background proteome to load at a time.  This can
            // get tricky, if the user performs an undo and then a redo across
            // a change in background proteome.
            // Our only priority is accessing web services to add missing protein metadata.
            // There may also be a load initiation by the Peptide Settings dialog as foreground task,
            // it takes priority over the background task.

            lock (_lockLoadBackgroundProteome)
            {
                BackgroundProteome originalBackgroundProteome = settings.BackgroundProteome;
                BackgroundProteome validatedBackgroundProtome = originalBackgroundProteome.DatabaseValidated 
                    ? originalBackgroundProteome 
                    : new BackgroundProteome(originalBackgroundProteome.BackgroundProteomeSpec);
                if (IsNotLoadedExplained(settings, validatedBackgroundProtome, true) == null)
                {
                    // protein metadata is resolved
                    CompleteProcessing(container, validatedBackgroundProtome);
                    Helpers.AssignIfEquals(ref validatedBackgroundProtome, originalBackgroundProteome);
                    return validatedBackgroundProtome; // No change needed
                }
                // we are here to resolve the protein metadata
                string name = originalBackgroundProteome.Name;
                IProgressStatus progressStatus =
                    new ProgressStatus(string.Format(Resources.BackgroundProteomeManager_LoadBackground_Resolving_protein_details_for__0__proteome, name));
                try
                {
                    // The transaction commit for writing the digestion info can be very lengthy, avoid lock timeouts
                    // by doing that work in a tempfile that no other thread knows aboout
                    using (FileSaver fs = new FileSaver(originalBackgroundProteome.DatabasePath, StreamManager))
                    {
                        File.Copy(originalBackgroundProteome.DatabasePath, fs.SafeName, true);
                        var digestHelper = new DigestHelper(this, container, docCurrent, name, fs.SafeName, true);

                        bool success = digestHelper.LookupProteinMetadata(ref progressStatus);
                        if (digestHelper.IsCanceled || !success)
                        {
                            // Processing was canceled
                            if (docCurrent != null)
                                EndProcessing(docCurrent);
                            UpdateProgress(progressStatus.Cancel());
                            return null;
                        }
                        using (var proteomeDb = ProteomeDb.OpenProteomeDb(originalBackgroundProteome.DatabasePath))
                        {
                            proteomeDb.DatabaseLock.AcquireWriterLock(int.MaxValue); // Wait for any existing readers to complete, prevent any new ones
                            try
                            {
                                if (File.GetLastWriteTime(fs.RealName) <= File.GetLastWriteTime(fs.SafeName)) // Don't overwrite if foreground task has already updated
                                {
                                    proteomeDb.CloseDbConnection(); // Get rid of any file handles
                                    if (!fs.Commit())
                                    {
                                        if (docCurrent != null)
                                            EndProcessing(docCurrent);
                                        throw new IOException(string.Format(Resources.BackgroundProteomeManager_LoadBackground_Unable_to_rename_temporary_file_to__0__,
                                                fs.RealName));
                                    }
                                }
                            }
                            finally
                            {
                                proteomeDb.DatabaseLock.ReleaseWriterLock();
                            }
                        }

                        var updatedProteome = new BackgroundProteome(originalBackgroundProteome);
                        using (var proteomeDb = originalBackgroundProteome.OpenProteomeDb())
                        {
                            proteomeDb.AnalyzeDb(); // Now it's safe to start this potentially lengthy indexing operation
                        }
                        CompleteProcessing(container, updatedProteome);
                        UpdateProgress(progressStatus.Complete());
                        return updatedProteome;
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
                    return null;
                }
            }
        }

        private void CompleteProcessing(IDocumentContainer container, BackgroundProteome backgroundProteomeWithDigestions)
        {
            if (container == null)
                return;  // We're using this in the PeptideSettingsUI thread
            SrmDocument docCurrent;
            SrmDocument docNew;
            do
            {
                docCurrent = container.Document;
                docNew = ChangeBackgroundProteome(docCurrent, backgroundProteomeWithDigestions);
            }
            while (!CompleteProcessing(container, docNew, docCurrent));
        }

        private void UpdateProcessingProgress(IProgressStatus progress)
        {
            if (_monitor == null) // Using this with PeptideSettingsUI?
            {
                UpdateProgress(progress);
            }
            else
            {
                _monitor.ChangeProgress(status => progress);
            }
        }


        // For use in PeptideSettingsUI, where we may need to force completion for peptide uniqueness constraints
        public BackgroundProteome LoadForeground(PeptideSettings settings, SrmSettingsChangeMonitor monitor)
        {
            if (monitor.IsCanceled())
                return null;
            Assume.IsTrue(ForegroundLoadRequested); // Caller should have called BeginForegroundLoad()
            lock (_lockLoadBackgroundProteome) // Wait for background loader, if any, to notice
            {
                _monitor = monitor;
                try
                {
                    return Load(null, settings, null, false);
                }
                finally
                {
                    _monitor = null;
                }
            }
        }

        public void BeginForegroundLoad()
        {
            ForegroundLoadRequested = true; // We are overriding the background loader, if it is running this will eventually halt it
        }

        public void EndForegroundLoad()
        {
            ForegroundLoadRequested = false; // We are done overriding the background loader
        }

        private sealed class DigestHelper : IProgressMonitor
        {
            private readonly BackgroundProteomeManager _manager;
            private readonly IDocumentContainer _container;
            private readonly SrmDocument _document;
            private readonly string _nameProteome;
            private readonly string _pathProteome;
            private readonly bool _isTemporary;  // Are we doing this work on a temporary copy of the DB?

            private IProgressStatus _progressStatus;

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

            public bool LookupProteinMetadata(ref IProgressStatus progressStatus)
            {

                using (var proteomeDb = ProteomeDb.OpenProteomeDb(_pathProteome, _isTemporary))
                {
                    _progressStatus = progressStatus.ChangeMessage(
                        string.Format(Resources.BackgroundProteomeManager_LoadBackground_Resolving_protein_details_for__0__proteome,_nameProteome));
                    bool result = false;
                    // Well formatted Uniprot headers don't require web access, so do an inital pass in hopes of finding those, then a second pass that requires web access
                    for (var useWeb = 0; useWeb <=1; useWeb++)
                    {
                        if (_progressStatus.IsCanceled)
                            break;
                        if (useWeb == 1 && !_manager.FastaImporter.HasWebAccess()) // Do we even have web access?
                        {
                            _progressStatus =
                                _progressStatus.ChangeMessage(Resources.DigestHelper_LookupProteinMetadata_Unable_to_access_internet_to_resolve_protein_details_)
                                    .ChangeWarningMessage(Resources.DigestHelper_LookupProteinMetadata_Unable_to_access_internet_to_resolve_protein_details_).Cancel();
                            result = false;
                        }
                        else
                        {
                            bool done;
                            result |= proteomeDb.LookupProteinMetadata(this, ref _progressStatus, _manager.FastaImporter, useWeb == 0, out done); // first pass, just parse descriptions, second pass use web.
                            if (done)
                                break;
                        }
                    }
                    progressStatus = _progressStatus;
                    return result;
                }
            }

            public bool IsCanceled
            {
                // Cancel if the document state has changed since the digestion started, of if a foreground load has started
                get { return _container != null && (_manager.StateChanged(_container.Document, _document) || _manager.ForegroundLoadRequested) || _manager.IsCanceled(_container, _document); }
            }

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                if (IsCanceled)
                {
                    return UpdateProgressResponse.cancel;
                }
                // Pass through to the manager maintaining the same original progress status ID
                _progressStatus = _progressStatus.ChangeMessage(status.Message).ChangePercentComplete(status.PercentComplete);
                _manager.UpdateProcessingProgress(_progressStatus);
                return UpdateProgressResponse.normal;
            }

            public bool HasUI
            {
                get { return false; }
            }
        }
    }
}
