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
using System.IO;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Files
{
    public class SkylineFile : FileNode
    {
        private readonly IDictionary<Type, FileNode> _filesByFolder;

        public SkylineFile(SrmDocument document, string documentPath) :
            base(document, documentPath, IdentityPath.ROOT, ImageId.skyline)
        {
            // DocumentPath is null when a .sky document is new and has not been saved to disk yet
            if (documentPath == null)
            {
                Name = FileResources.FileModel_NewDocument;
                FilePath = null;
                FileName = null;
            }
            else
            {
                Name = Path.GetFileName(documentPath);
                FilePath = documentPath;
                FileName = Path.GetFileName(documentPath);
            }

            BuildFileList(out var files, out var dict);

            Files = ImmutableList.ValueOf(files);
            _filesByFolder = new ImmutableDictionary<Type, FileNode>(dict);
        }

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => Document;
        public override string Name { get; }
        public override string FilePath { get; }
        public override string FileName { get; }
        public override IList<FileNode> Files { get; }

        public FileNode Folder<T>() where T : FileNode
        {
            _filesByFolder.TryGetValue(typeof(T), out var files);
            return files;
        } 

        private void BuildFileList(out IList<FileNode> list, out IDictionary<Type, FileNode> dict)
        {
            list = new List<FileNode>();
            dict = new Dictionary<Type, FileNode>();

            FileNode files = new ReplicatesFolder(Document, DocumentPath);
            dict[typeof(ReplicatesFolder)] = files;
            list.Add(files); // Always show the Replicates folder

            files = new SpectralLibrariesFolder(Document, DocumentPath);
            if (files.Files.Count > 0)
            {
                dict[typeof(SpectralLibrariesFolder)] = files;
                list.Add(files);
            }

            files = new BackgroundProteomeFolder(Document, DocumentPath);
            if (files.Files.Count > 0)
            {
                dict[typeof(BackgroundProteomeFolder)] = files;
                list.Add(files);
            }

            files = new RTCalcFolder(Document, DocumentPath);
            if (files.Files.Count > 0)
            {
                dict[typeof(RTCalcFolder)] = files;
                list.Add(files);
            }

            files = new IonMobilityLibraryFolder(Document, DocumentPath);
            if (files.Files.Count > 0)
            {
                dict[typeof(IonMobilityLibraryFolder)] = files;
                list.Add(files);
            }

            files = new OptimizationLibraryFolder(Document, DocumentPath);
            if (files.Files.Count > 0)
            {
                dict[typeof(OptimizationLibraryFolder)] = files;
                list.Add(files);
            }

            files = new ProjectFilesFolder(Document, DocumentPath);
            dict[typeof(ProjectFilesFolder)] = files;
            list.Add(files); // Always show project files - .sky, .sky.view, etc
        }
    }
}