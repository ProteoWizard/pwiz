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

using pwiz.Common.Collections;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Skyline.Model.Files
{
    public class SpectralLibrary : FileModel
    {
        public static SpectralLibrary Create(string documentFilePath, LibrarySpec librarySpec)
        {
            var identityPath = new IdentityPath(librarySpec.Id);
            var name = librarySpec.Name ?? string.Empty;
            var filePath = librarySpec.FilePath ?? string.Empty;

            return new SpectralLibrary(documentFilePath, identityPath, name, filePath);
        }

        internal SpectralLibrary(string documentFilePath, IdentityPath identityPath, string name, string filePath) :
            base(documentFilePath, identityPath)
        {
            Name = name;
            FilePath = filePath;
        }

        public override bool IsBackedByFile => true;
        public override string Name { get; }
        public override string FilePath { get; }
        public override ImageId ImageAvailable => ImageId.peptide_library;

        public static LibrarySpec LoadLibrarySpecFromDocument(SrmDocument document, SpectralLibrary library)
        {
            return document.Settings.PeptideSettings.Libraries.FindLibrarySpec(library.IdentityPath.GetIdentity(0));
        }

        public override Func<SkylineDataSchema, RootSkylineObject> PropertyObjectInstancer =>
            dataSchema => new Databinding.Entities.SpectralLibrary(dataSchema, IdentityPath);

        public static ModifiedDocument Delete(SrmDocument document, SrmSettingsChangeMonitor monitor, List<FileModel> models)
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

        public static ModifiedDocument Rearrange(SrmDocument document, SrmSettingsChangeMonitor monitor, List<FileModel> draggedModels, FileModel dropModel, MoveType moveType)
        {
            var draggedLibraries = draggedModels.Cast<SpectralLibrary>().Select(model => LoadLibrarySpecFromDocument(document, model)).ToList();

            var newLibraries = document.Settings.PeptideSettings.Libraries.LibrarySpecs.Except(draggedLibraries).ToList();

            switch (moveType)
            {
                case MoveType.move_to:
                {
                    var dropLibrarySpec = LoadLibrarySpecFromDocument(document, (SpectralLibrary)dropModel);
                    var insertAt = newLibraries.IndexOf(dropLibrarySpec);
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
                document.DocumentType,
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
    }
}