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
using System.Linq;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Files
{
    public class ReplicatesFolder : FileModel
    {
        private static readonly IdentityPath IDENTITY_PATH = new IdentityPath(new StaticFolderId());

        public ReplicatesFolder(string documentFilePath, IList<Replicate> files) : 
            base(documentFilePath, IDENTITY_PATH)
        {
            Files = files.Cast<FileModel>().ToList();
        }

        public override string Name => FileResources.FileModel_Replicates;
        public override string FilePath => string.Empty;
        public override string FileName => string.Empty;
        protected override string FileTypeText => string.Empty; // Folders don't use type prefix
        public override ImageId ImageAvailable => ImageId.folder;
        public override ImageId ImageMissing => ImageId.folder_missing;
        public override IList<FileModel> Files { get; }

        public int SampleFileCount()
        {
            return Files.Sum(replicate => replicate.Files.Count);
        }

        public ModifiedDocument DeleteAll(SrmDocument document, SrmSettingsChangeMonitor monitor)
        {
            var newDocument = document.ChangeMeasuredResults(null, monitor);
            newDocument.ValidateResults();

            var entry = AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_replicates_remove_all, document.DocumentType);

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }
    }
}
