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
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Files
{
    public class SpectralLibrariesFolder : FileModel
    {
        private static readonly IdentityPath IDENTITY_PATH = new IdentityPath(new StaticFolderId());

        public static SpectralLibrariesFolder Create(string documentFilePath, IList<SpectralLibrary> libraries)
        {
            return new SpectralLibrariesFolder(documentFilePath, libraries);
        }

        internal SpectralLibrariesFolder(string documentFilePath, IList<SpectralLibrary> libraries) : 
            base(documentFilePath, IDENTITY_PATH)
        {
            Files = libraries.Cast<FileModel>().ToList();
        }

        public override string Name => FileResources.FileModel_Libraries;
        public override string FilePath => string.Empty;
        protected override string FileTypeText => string.Empty; // Folders don't use type prefix
        public override ImageId ImageAvailable => ImageId.folder;
        public override ImageId ImageMissing => ImageId.folder_missing;
        public override IList<FileModel> Files { get; }

        public ModifiedDocument DeleteAll(SrmDocument document, SrmSettingsChangeMonitor monitor)
        {
            var newPepLibraries = document.Settings.PeptideSettings.Libraries.ChangeLibraries(Array.Empty<LibrarySpec>(), Array.Empty<Library>());
            var newPepSettings = document.Settings.PeptideSettings.ChangeLibraries(newPepLibraries);
            var newSettings = document.Settings.ChangePeptideSettings(newPepSettings);
            var newDocument = document.ChangeSettings(newSettings, monitor);

            var entry = AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_libraries_remove_all, document.DocumentType);

            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }
    }
}