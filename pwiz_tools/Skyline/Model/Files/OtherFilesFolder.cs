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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;

namespace pwiz.Skyline.Model.Files
{
    public class OtherFilesFolder : FileNode
    {
        private static readonly Identity OTHER_FILES_FOLDER = new StaticFolderId();

        public OtherFilesFolder(IDocumentContainer documentContainer) :
            base(documentContainer, new IdentityPath(OTHER_FILES_FOLDER), ImageId.folder)
        {
        }

        public override Immutable Immutable => Document.Settings.Files;
        public override string Name => FileResources.FileModel_OtherFilesFolder;
        public override string FilePath => string.Empty;
        public override string FileName => string.Empty;

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.Settings.Files == null || Document.Settings.Files.IsEmpty)
                {
                    return ImmutableList.Empty<FileNode>();
                }
                else
                {
                    var nodeFiles = Document.Settings.Files.FileList.Select(item => new BasicFile(DocumentContainer, item));

                    return ImmutableList.ValueOf(nodeFiles.ToList<FileNode>());
                }
            }
        }

        public ModifiedDocument Delete(SrmDocument doc, BasicFile file)
        {
            var deleteId = file.IdentityPath.Child;
            var deleteName = doc.Settings.Files.FindById(deleteId); 

            if (deleteId == null || deleteName == null)
            {
                return new ModifiedDocument(doc);
            }

            var remainingFiles = doc.Settings.Files.FileList.Where(f => !f.Id.Equals(deleteId)).ToList();
            var newFiles = doc.Settings.Files.ChangeFileList(remainingFiles);
            var newSettings = doc.Settings.ChangeFiles(newFiles);
            var newDoc = doc.ChangeSettings(newSettings);

            var entry = AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_otherfiles_remove_one, doc.DocumentType, deleteName);

            return new ModifiedDocument(newDoc).ChangeAuditLogEntry(entry);
        }
    }
}
