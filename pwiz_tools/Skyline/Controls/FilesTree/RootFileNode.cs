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
using System.IO;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;


namespace pwiz.Skyline.Controls.FilesTree
{
    public class RootFileNode : FileNode
    {
        public RootFileNode(SrmDocument document, string documentPath) :
            base(document, documentPath, IdentityPath.ROOT, ImageId.skyline)
        {
            if (documentPath == null)
            {
                Name = FilesTreeResources.FilesTree_TreeNodeLabel_NewDocument;
                FilePath = null;
                FileName = null;
            }
            else
            {
                Name = Path.GetFileName(documentPath);
                FilePath = documentPath;
                FileName = Path.GetFileName(documentPath);
            }
        }

        public override Immutable Immutable => Document;
        public override bool IsBackedByFile => FilePath != null;
        public override string Name { get; }
        public override string FilePath { get; }
        public override string FileName { get; }

        public override IList<FileNode> Files
        {
            get
            {
                var list = new List<FileNode>();

                // CONSIDER: add a "HasFiles" check to avoid eagerly loading everything
                //           from SrmDocument for files shown in the tree
                FileNode files = new ReplicatesFolder(Document, DocumentPath);
                if(files.Files.Count > 0) 
                    list.Add(files);

                files = new SpectralLibrariesFolder(Document, DocumentPath);
                if (files.Files.Count > 0)
                    list.Add(files);

                files = new BackgroundProteomeFolder(Document, DocumentPath);
                if (files.Files.Count > 0)
                    list.Add(files);
                
                files = new RTCalcFolder(Document, DocumentPath);
                if (files.Files.Count > 0)
                    list.Add(files);
                
                files = new IonMobilityLibraryFolder(Document, DocumentPath);
                if (files.Files.Count > 0)
                    list.Add(files);
                
                files = new OptimizationLibraryFolder(Document, DocumentPath);
                if (files.Files.Count > 0)
                    list.Add(files);

                files = new ProjectFilesFolder(Document, DocumentPath);
                list.Add(files);

                return ImmutableList.ValueOf(list);
            }
        }
    }
}