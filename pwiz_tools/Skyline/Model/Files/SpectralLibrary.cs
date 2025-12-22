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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Files
{
    public class SpectralLibrary : FileModel, IFileRenameable
    {
        private readonly bool _includeTypePrefix;

        public static SpectralLibrary Create(string documentFilePath, LibrarySpec librarySpec, bool includeTypePrefix = false)
        {
            var identityPath = new IdentityPath(librarySpec.Id);
            var libraryName = librarySpec.Name ?? string.Empty;
            var filePath = librarySpec.FilePath ?? string.Empty;

            return new SpectralLibrary(documentFilePath, identityPath, libraryName, filePath, includeTypePrefix);
        }

        internal SpectralLibrary(string documentFilePath, IdentityPath identityPath, string name, string filePath, bool includeTypePrefix) :
            base(documentFilePath, identityPath)
        {
            Name = name;
            FilePath = filePath;
            _includeTypePrefix = includeTypePrefix;
        }

        public override bool IsBackedByFile => true;
        public override string Name { get; }
        public override string FilePath { get; }
        public static string TypeText => FileResources.FileModel_Libraries;
        protected override string FileTypeText => _includeTypePrefix ? TypeText : string.Empty;
        public override ImageId ImageAvailable => ImageId.peptide_library;

        public static LibrarySpec LoadLibrarySpecFromDocument(SrmDocument document, SpectralLibrary library)
        {
            return document.Settings.PeptideSettings.Libraries.FindLibrarySpec(library.IdentityPath.GetIdentity(0));
        }

        public static ModifiedDocument Edit(SrmDocument doc, LibrarySpec oldLibrarySpec, LibrarySpec newLibrarySpec)
        {
            var libraries = doc.Settings.PeptideSettings.Libraries;
            // Update the library specs and LibraryManager will handle reloading the libraries as needed
            // Maintaining the order of the libraries is important
            var librarySpecs = libraries.LibrarySpecs.ToArray();
            int libraryIndex = librarySpecs.IndexOf(l => ReferenceEquals(l.Id, oldLibrarySpec.Id));
            if (libraryIndex == -1)
            {
                throw new InvalidOperationException(string.Format(FileResources.SpectralLibrary_Edit_Library___0___not_found_in_the_document_, newLibrarySpec.Name));
            }
            librarySpecs[libraryIndex] = newLibrarySpec;

            var newDocument = doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(l =>
                l.ChangeLibrarySpecs(librarySpecs)));
            var entry = AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_spectral_library_update, doc.DocumentType, newLibrarySpec.Name);
            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }

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

        #region IFileRenameable implementation

        public bool HasItemWithName(SrmDocument document, string newName)
        {
            Assume.IsNotNull(document);

            var librarySpecs = document.Settings.PeptideSettings.Libraries.LibrarySpecs;
            return librarySpecs.Any(item => string.Equals(item.Name, newName, StringComparison.CurrentCulture));
        }

        public bool ValidateNewName(SrmDocument document, string newName, out string errorMessage)
        {
            if (HasItemWithName(document, newName))
            {
                errorMessage = string.Format(Controls.FilesTree.FilesTreeResources.FilesTreeForm_Error_Renaming_SpectralLibrary, newName);
                return false;
            }
            errorMessage = null;
            return true;
        }

        public ModifiedDocument PerformRename(SrmDocument document, SrmSettingsChangeMonitor monitor, string newName)
        {
            var librarySpec = LoadLibrarySpecFromDocument(document, this);

            var oldName = librarySpec.Name;
            var newLibrarySpec = (LibrarySpec)librarySpec.ChangeName(newName);
            var libraries = document.Settings.PeptideSettings.Libraries;

            var librarySpecs = libraries.LibrarySpecs.ToArray();
            var libs = libraries.Libraries.ToArray();

            for (var i = 0; i < librarySpecs.Length; i++)
            {
                if (ReferenceEquals(librarySpecs[i].Id, newLibrarySpec.Id))
                {
                    librarySpecs[i] = newLibrarySpec;
                    if (libs[i] != null)
                    {
                        libs[i] = (Library)libs[i].ChangeName(newName);
                    }
                }
            }

            var newPepLibraries = libraries.ChangeLibraries(librarySpecs.ToList(), libs);
            var newPepSettings = document.Settings.PeptideSettings.ChangeLibraries(newPepLibraries);
            var newSettings = document.Settings.ChangePeptideSettings(newPepSettings);
            var newDocument = document.ChangeSettings(newSettings, monitor);

            var entry = AuditLogEntry.CreateSimpleEntry(
                MessageType.files_tree_node_renamed,
                document.DocumentType,
                oldName,
                newName);

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }

        public string AuditLogMessageResource => Controls.FilesTree.FilesTreeResources.Change_SpectralLibraryName;

        #endregion
    }
}
