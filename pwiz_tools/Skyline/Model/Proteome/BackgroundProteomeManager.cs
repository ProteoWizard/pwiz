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
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Proteome
{
    public sealed class BackgroundProteomeManager : BackgroundLoader
    {
        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            if (previous == null)
            {
                return true;
            }
            if (!Equals(GetBackgroundProteome(document), GetBackgroundProteome(previous)))
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

        private bool IsLoaded(SrmDocument document, BackgroundProteome backgroundProteome)
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
            var enzyme = document.Settings.PeptideSettings.Enzyme;
            var digestSettings = document.Settings.PeptideSettings.DigestSettings;
            return backgroundProteome.GetDigestion(enzyme, digestSettings) != null;
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
            return new List<IPooledStream>();
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            return false;
        }

        private SrmDocument ChangeBackgroundProteome(SrmDocument document, BackgroundProteome backgroundProteome)
        {
            return document.ChangeSettings(
                document.Settings.ChangePeptideSettings(
                    document.Settings.PeptideSettings.ChangeBackgroundProteome(backgroundProteome)));
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            BackgroundProteome originalBackgroundProteome = GetBackgroundProteome(docCurrent);
            // Check to see whether the Digestion already exists but has not been queried yet.
            BackgroundProteome backgroundProteomeWithDigestions = new BackgroundProteome(originalBackgroundProteome, true);
            if (IsLoaded(docCurrent, backgroundProteomeWithDigestions))
            {
                return CompleteProcessing(container, ChangeBackgroundProteome(docCurrent, backgroundProteomeWithDigestions), docCurrent);
            }
            using (FileSaver fs = new FileSaver(originalBackgroundProteome.DatabasePath, StreamManager))
            {
                File.Copy(originalBackgroundProteome.DatabasePath, fs.SafeName, true);
                var proteomeDb = ProteomeDb.OpenProteomeDb(fs.SafeName);
                var enzyme = docCurrent.Settings.PeptideSettings.Enzyme;
                var protease = new ProteaseImpl(enzyme);
                string name = originalBackgroundProteome.Name;
                string nameEnzyme = enzyme.Name;
                ProgressStatus progressStatus = new ProgressStatus(
                    string.Format("Digesting {0} proteome with {1}", name, nameEnzyme));
                var digestion = proteomeDb.Digest(protease, (s, i) =>
                {
                    UpdateProgress(progressStatus.ChangePercentComplete(i));
                    return true;
                });
                if (digestion != null)
                {
                    if (!fs.Commit())
                    {
                        return false;
                    }
                }
                return CompleteProcessing(container, 
                    ChangeBackgroundProteome(docCurrent, new BackgroundProteome(originalBackgroundProteome, true)), 
                    docCurrent);
            }
        }
    }
}
