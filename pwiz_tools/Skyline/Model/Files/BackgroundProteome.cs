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

using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Proteome;

namespace pwiz.Skyline.Model.Files
{
    public class BackgroundProteome : FileNode
    {
        public static IList<BackgroundProteome> Create(string documentFilePath, Proteome.BackgroundProteome proteome)
        {
            var identityPath = new IdentityPath(proteome.Id);
            var model = new BackgroundProteome(documentFilePath, identityPath, proteome.Name, proteome.Name, proteome.FilePath);

            return ImmutableList.Singleton(model);
        }

        private BackgroundProteome(string documentFilePath, IdentityPath identityPath, string name, string fileName, string filePath) : 
            base(documentFilePath, identityPath)
        {
            Name = name;
            FileName = fileName;
            FilePath = filePath;
        }

        public override bool IsBackedByFile => true;
        public override string Name { get; }
        public override string FilePath { get; }
        public override string FileName { get; }

        public static ModifiedDocument Edit(SrmDocument doc, BackgroundProteomeSpec bgProteomeSpec)
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