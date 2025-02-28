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

namespace pwiz.Skyline.Model.FilesView
{
    public class SpectralLibrariesFolder : FileNode
    {
        private static readonly Identity SPECTRAL_LIBRARIES = new StaticFolderId();

        public SpectralLibrariesFolder(SrmDocument document) : base(document, new IdentityPath(SPECTRAL_LIBRARIES), ImageId.folder)
        {
        }

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.Settings.PeptideSettings == null || !Document.Settings.PeptideSettings.HasLibraries)
                {
                    return Array.Empty<Replicate>().ToList<FileNode>();
                }

                var files = Document.Settings.PeptideSettings.Libraries.LibrarySpecs.Select(library => new SpectralLibrary(Document, library.Id)).ToList<FileNode>();
                return ImmutableList.ValueOf(files);
            }
        }

        public override string Name => FilesView.FilesTree_TreeNodeLabel_Libraries;
        public override string FilePath => string.Empty;
    }
}