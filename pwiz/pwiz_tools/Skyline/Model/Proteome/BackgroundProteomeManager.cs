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
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Proteome
{
    public sealed class BackgroundProteomeManager : BackgroundLoader
    {
        private readonly object _lockLoadBackgroundProteome = new object();

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

        protected override bool IsLoaded(SrmDocument document)
        {
            return IsLoaded(document, GetBackgroundProteome(document));
        }

        private static bool IsLoaded(SrmDocument document, BackgroundProteome backgroundProteome)
        {
            if (backgroundProteome.IsNone)
            {
                return true;
            }
            if (!backgroundProteome.DatabaseValidated)
            {
                return false;
            }
            if (backgroundProteome.DatabaseInvalid)
            {
                return true;
            }
            var peptideSettings = document.Settings.PeptideSettings;
            return backgroundProteome.GetDigestion(peptideSettings) != null;
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
            lock (_lockLoadBackgroundProteome)
            {
                BackgroundProteome originalBackgroundProteome = GetBackgroundProteome(docCurrent);
                // Check to see whether the Digestion already exists but has not been queried yet.
                BackgroundProteome backgroundProteomeWithDigestions = new BackgroundProteome(originalBackgroundProteome, true);
                if (IsLoaded(docCurrent, backgroundProteomeWithDigestions))
                {
                    CompleteProcessing(container, backgroundProteomeWithDigestions);
                    return true;
                }

                string name = originalBackgroundProteome.Name;
                ProgressStatus progressStatus = new ProgressStatus(string.Format("Digesting {0} proteome", name));
                try
                {
                    using (FileSaver fs = new FileSaver(originalBackgroundProteome.DatabasePath, StreamManager))
                    {
                        File.Copy(originalBackgroundProteome.DatabasePath, fs.SafeName, true);
                        var digestHelper = new DigestHelper(this, container, docCurrent, name, fs.SafeName, true);
                        var digestion = digestHelper.Digest(ref progressStatus);

                        if (digestion == null)
                        {
                            // Processing was canceled
                            EndProcessing(docCurrent);
                            UpdateProgress(progressStatus.Cancel());
                            return false;
                        }
                        if (!fs.Commit())
                        {
                            EndProcessing(docCurrent);
                            throw new IOException(string.Format("Unable to rename temporary file to {0}.", fs.RealName));
                        }

                        CompleteProcessing(container, new BackgroundProteome(originalBackgroundProteome, true));
                        UpdateProgress(progressStatus.Complete());
                        return true;
                    }
                }
                catch (Exception x)
                {
                    string message = string.Format(string.Format("Failed updating background proteome {0}.\n{1}", name, x.Message));
                    UpdateProgress(progressStatus.ChangeErrorException(new IOException(message, x)));
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
            private readonly bool _isTempPath;

            private ProgressStatus _progressStatus;

            public DigestHelper(BackgroundProteomeManager manager,
                                IDocumentContainer container,
                                SrmDocument document,
                                string nameProteome,
                                string pathProteome,
                                bool isTempPath)
            {
                _manager = manager;
                _container = container;
                _document = document;
                _nameProteome = nameProteome;
                _pathProteome = pathProteome;
                _isTempPath = isTempPath;
            }

// ReSharper disable RedundantAssignment
            public Digestion Digest(ref ProgressStatus progressStatus)
// ReSharper restore RedundantAssignment
            {
                ProteomeDb proteomeDb = null;
                try
                {
                    proteomeDb = ProteomeDb.OpenProteomeDb(_pathProteome, _isTempPath);
                    var enzyme = _document.Settings.PeptideSettings.Enzyme;

                    _progressStatus = new ProgressStatus(
                        string.Format("Digesting {0} proteome with {1}", _nameProteome, enzyme.Name));
                    var digestion = proteomeDb.Digest(new ProteaseImpl(enzyme), Progress);
                    progressStatus = _progressStatus;

                    return digestion;
                }
                finally
                {
                    if (proteomeDb != null && _isTempPath)
                        proteomeDb.Dispose();
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
