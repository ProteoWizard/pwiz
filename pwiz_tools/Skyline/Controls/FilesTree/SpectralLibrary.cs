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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Controls.FilesTree
{
    public class SpectralLibrary : FileNode
    {
        private readonly Lazy<LibrarySpec> _librarySpec;

        public SpectralLibrary(SrmDocument document, string documentPath, Identity libraryId) : 
            base(document, documentPath, new IdentityPath(libraryId), ImageId.peptide)
        {
            _librarySpec = new Lazy<LibrarySpec>(FindLibrarySpec);
        }

        public override Immutable Immutable => _librarySpec.Value;

        public override string Name => _librarySpec.Value.Name;
        public override string FilePath => _librarySpec.Value.FilePath;

        public override bool IsBackedByFile => true;

        private LibrarySpec FindLibrarySpec()
        {
            return Document.Settings.PeptideSettings.Libraries.FindLibrarySpec(IdentityPath.GetIdentity(0));
        }
    }
}