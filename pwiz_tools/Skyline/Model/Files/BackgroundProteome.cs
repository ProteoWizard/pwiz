/*
 * Copyright 2025 University of Washington - Seattle, WA
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

using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Proteome;

namespace pwiz.Skyline.Model.Files
{
    public class BackgroundProteome : FileNode
    {
        public BackgroundProteome(IDocumentContainer documentContainer, Identity backgroundProteomeId) : 
            base(documentContainer, new IdentityPath(backgroundProteomeId))
        {
        }

        private Proteome.BackgroundProteome BgProteome => Document.Settings.PeptideSettings.BackgroundProteome;

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => BgProteome;
        public override string Name => BgProteome.Name;
        public override string FilePath => BgProteome.FilePath;
        public override string FileName => BgProteome.Name;

        public override bool ModelEquals(FileNode nodeDoc)
        {
            if (nodeDoc == null) return false;
            if (!(nodeDoc is BackgroundProteome library)) return false;

            return ReferenceEquals(BgProteome, library.BgProteome);
        }

        public ModifiedDocument Edit(SrmDocument doc, BackgroundProteomeSpec bgProteomeSpec)
        {
            var newBgProteome = new Proteome.BackgroundProteome(bgProteomeSpec);

            var newPeptideSettings = doc.Settings.PeptideSettings.ChangeBackgroundProteome(newBgProteome);
            var newSettings = doc.Settings.ChangePeptideSettings(newPeptideSettings);
            var newDocument = doc.ChangeSettings(newSettings);

            var entry = AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_background_proteome_update, doc.DocumentType, bgProteomeSpec.Name);

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }
    }
}