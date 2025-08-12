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

using System.IO;
using pwiz.Common.SystemUtil;
using File = pwiz.Skyline.Model.Files.Doc.File;

namespace pwiz.Skyline.Model.Files
{
    public class BasicFile : FileNode
    {
        public BasicFile(IDocumentContainer documentContainer, File docFile) : 
            base(documentContainer, new IdentityPath(docFile.Id))
        {
            File = docFile;
        }

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => File;
        public override string Name => File.Name;
        public override string FilePath => PathEx.GetDirectoryName(DocumentPath) + Path.DirectorySeparatorChar + Name;

        private File File { get; }

        public override bool ModelEquals(FileNode nodeDoc)
        {
            if (nodeDoc == null) return false;
            if (!(nodeDoc is BasicFile basicFile)) return false;

            return ReferenceEquals(File, basicFile.File);
        }
    }
}