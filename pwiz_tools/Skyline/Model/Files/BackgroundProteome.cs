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

using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Proteome;

namespace pwiz.Skyline.Model.Files
{
    public class BackgroundProteome : FileModel
    {
        public static BackgroundProteome Create(string documentFilePath, Proteome.BackgroundProteome proteome)
        {
            var identityPath = new IdentityPath(proteome.Id);
            return new BackgroundProteome(documentFilePath, identityPath, proteome.Name, proteome.FilePath);
        }

        private BackgroundProteome(string documentFilePath, IdentityPath identityPath, string name, string filePath) :
            base(documentFilePath, identityPath)
        {
            Name = name;
            FilePath = filePath;
        }

        public override bool IsBackedByFile => true;
        public override string Name { get; }
        public override string FilePath { get; }
        public static string TypeText => FileResources.FileModel_BackgroundProteome;
        protected override string FileTypeText => TypeText;
        public override ImageId ImageAvailable => ImageId.prot_db;

        public static ModifiedDocument Edit(SrmDocument doc, BackgroundProteomeSpec bgProteomeSpec)
        {
            var newDocument = doc.ChangeSettings(doc.Settings.ChangePeptideSettings(ps =>
                ps.ChangeBackgroundProteome(new Proteome.BackgroundProteome(bgProteomeSpec))));
            var entry = AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_background_proteome_update, doc.DocumentType, bgProteomeSpec.Name);
            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }
    }
}
