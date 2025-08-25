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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Files
{
    public class SpectralLibrary : FileNode
    {
        public SpectralLibrary(IDocumentContainer documentContainer, Identity libraryId) : 
            base(documentContainer, new IdentityPath(libraryId), ImageId.peptide_library)
        {
        }

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => LibrarySpec;
        public override string Name => LibrarySpec?.Name ?? string.Empty;
        public override string FilePath => LibrarySpec?.FilePath ?? string.Empty;

        public ModifiedDocument Delete(SrmDocument document, SrmSettingsChangeMonitor monitor, List<FileNode> models)
        {
            var deleteIds = models.Select(model => ReferenceValue.Of(model.IdentityPath.Child)).ToHashSet();
            var deleteNames = models.Select(item => item.Name).ToList();

            var remainingLibraries = 
                document.Settings.PeptideSettings.Libraries.LibrarySpecs.Where(lib => !deleteIds.Contains(lib.Id));

            var newPepLibraries = document.Settings.PeptideSettings.Libraries.ChangeLibrarySpecs(remainingLibraries.ToList());
            var newPepSettings = document.Settings.PeptideSettings.ChangeLibraries(newPepLibraries);
            var newSettings = document.Settings.ChangePeptideSettings(newPepSettings);
            var newDocument = document.ChangeSettings(newSettings, monitor);

            var entry = AuditLogEntry.CreateCountChangeEntry(
                MessageType.files_tree_libraries_remove_one,
                MessageType.files_tree_libraries_remove_several,
                document.DocumentType,
                deleteNames
            );

            if (deleteNames.Count > 1)
            {
                entry = entry.AppendAllInfo(deleteNames.Select(name => new MessageInfo(MessageType.removed_library, document.DocumentType, name)).ToList());
            }

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }

        public ModifiedDocument Rearrange(SrmDocument document, SrmSettingsChangeMonitor monitor, List<FileNode> draggedModels, FileNode dropModel, MoveType moveType)
        {
            var draggedLibraries = draggedModels.Cast<SpectralLibrary>().Select(model => model.LibrarySpec).ToList();

            var newLibraries = document.Settings.PeptideSettings.Libraries.LibrarySpecs.Except(draggedLibraries).ToList();

            switch (moveType)
            {
                case MoveType.move_to:
                {
                    var insertAt = newLibraries.IndexOf(((SpectralLibrary)dropModel).LibrarySpec);
                    newLibraries.InsertRange(insertAt, draggedLibraries);
                    break;
                }
                case MoveType.move_last:
                default:
                    newLibraries.AddRange(draggedLibraries);
                    break;
            }

            var newLibs = new Library[newLibraries.Count]; // Required by PeptideSettings.Validate() 
            var newPepLibraries = document.Settings.PeptideSettings.Libraries.ChangeLibraries(newLibraries, newLibs);
            var newPepSettings = document.Settings.PeptideSettings.ChangeLibraries(newPepLibraries);
            var newSettings = document.Settings.ChangePeptideSettings(newPepSettings);
            var newDocument = document.ChangeSettings(newSettings, monitor);

            var readableNames = draggedLibraries.Select(item => item.Name).ToList();

            var entry = AuditLogEntry.CreateCountChangeEntry(
                MessageType.files_tree_node_drag_and_drop,
                MessageType.files_tree_nodes_drag_and_drop,
                Document.DocumentType,
                readableNames,
                readableNames.Count,
                str => MessageArgs.Create(str, dropModel.Name),
                MessageArgs.Create(readableNames.Count, dropModel.Name)
            );

            if (readableNames.Count > 1)
            {
                entry = entry.ChangeAllInfo(readableNames.
                    Select(node => new MessageInfo(MessageType.files_tree_node_drag_and_drop, newDocument.DocumentType, node, dropModel.Name)).
                    ToList());
            }

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }

        public override bool ModelEquals(FileNode nodeDoc)
        {
            if (nodeDoc == null) return false;
            if (!(nodeDoc is SpectralLibrary library)) return false;

            return ReferenceEquals(LibrarySpec, library.LibrarySpec);
        }

        private LibrarySpec LibrarySpec => Document.Settings.PeptideSettings.Libraries.FindLibrarySpec(IdentityPath.GetIdentity(0));
    }
}