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
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.Files
{
    public class SpectralLibrariesFolder : FileNode
    {
        private static readonly Identity SPECTRAL_LIBRARIES = new StaticFolderId();

        public SpectralLibrariesFolder(IDocumentContainer documentContainer) :
            base(documentContainer, new IdentityPath(SPECTRAL_LIBRARIES), ImageId.folder, ImageId.folder_missing)
        {
        }

        public override Immutable Immutable => Document.Settings.PeptideSettings;
        public override string Name => FileResources.FileModel_Libraries;
        public override string FilePath => string.Empty;

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.Settings.PeptideSettings is { HasLibraries: true })
                {
                    var model =
                        Document.Settings.PeptideSettings.Libraries.LibrarySpecs.
                            Select(library => new SpectralLibrary(DocumentContainer, library.Id)).
                            ToList<FileNode>();

                    return ImmutableList.ValueOf(model);
                }
                else
                {
                    return ImmutableList.Empty<FileNode>();
                }
            }
        }

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