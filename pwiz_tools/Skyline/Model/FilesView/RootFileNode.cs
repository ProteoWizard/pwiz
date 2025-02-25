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
using pwiz.Common.Collections;

// TODO: support project files

namespace pwiz.Skyline.Model.FilesView
{
    public class RootFileNode : FileNode
    {
        private string _documentPath;

        public RootFileNode(SrmDocument document, string documentPath) :
            base(document, IdentityPath.ROOT, ImageId.skyline)
        {
            _documentPath = documentPath;
        }

        public override string Name => null;
        public override string FilePath => null;
        public override string FileName => null;

        public override IList<FileNode> Files
        {
            get
            {
                var list = new List<FileNode>();

                // CONSIDER: add a "HasFiles" check to avoid eagerly loading everything
                //           from SrmDocument with a file shown in the tree
                FileNode files = new ReplicatesFolder(Document);
                if(files.Files.Count > 0) 
                    list.Add(files);

                files = new SpectralLibrariesFolder(Document);
                if (files.Files.Count > 0)
                    list.Add(files);

                files = new BackgroundProteomeFolder(Document);
                if (files.Files.Count > 0)
                    list.Add(files);

                files = new RTCalcFolder(Document);
                if (files.Files.Count > 0)
                    list.Add(files);

                files = new IonMobilityLibraryFolder(Document);
                if (files.Files.Count > 0)
                    list.Add(files);

                files = new OptimizationLibraryFolder(Document);
                if (files.Files.Count > 0)
                    list.Add(files);

                files = new ProjectFilesFolder(Document, _documentPath);
                list.Add(files);

                return ImmutableList.ValueOf(list);
            }
        }
    }
}