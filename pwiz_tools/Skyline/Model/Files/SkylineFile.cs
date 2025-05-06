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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Files
{
    public class SkylineFile : FileNode
    {
        public SkylineFile(IDocumentContainer documentContainer) :
            base(documentContainer, IdentityPath.ROOT, ImageId.skyline)
        {
            // DocumentPath is null when a .sky document is new and has not been saved to disk yet
            if (IsDocumentSavedToDisk())
            {
                Name = FileName = Path.GetFileName(documentContainer.DocumentFilePath);
                FilePath = documentContainer.DocumentFilePath;
            }
            else
            {
                Name = FileResources.FileModel_NewDocument;
                FilePath = null;
                FileName = null;
            }

            Files = ImmutableList.ValueOf(BuildFileList());
        }

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => new Immutable();
        public override string Name { get; }
        public override string FilePath { get; }
        public override string FileName { get; }
        public override IList<FileNode> Files { get; }

        public FileNode Folder<T>() where T : FileNode
        {
            return Files.OfType<T>().FirstOrDefault();
        } 

        private IEnumerable<FileNode> BuildFileList()
        {
            var list = new List<FileNode>();

            if (Document.Settings.DataSettings.IsAuditLoggingEnabled)
                list.Add(new SkylineAuditLog(DocumentContainer));
            
            if (IsDocumentSavedToDisk())
                list.Add(new SkylineViewFile(DocumentContainer));

            // TODO: put Chromatogram Cache (.skyd) back in the tree - maybe in the Replicates\ folder?
            // CONSIDER: is this correct? See more where Cache files are created in MeasuredResults @ line 1640
            // CONSIDER: does this also need to check if the file exists?
            // Chromatogram Caches (.skyd)
            // var cachePaths = Document.Settings.MeasuredResults?.CachePaths;
            // if (cachePaths != null)
            // {
            //     list.AddRange(cachePaths.Select(_ => new SkylineChromatogramCache(DocumentContainer)));
            // }

            FileNode files = new ReplicatesFolder(DocumentContainer);
            list.Add(files); // Always show the Replicates folder

            files = new SpectralLibrariesFolder(DocumentContainer);
            if (files.Files.Count > 0)
            {
                list.Add(files);
            }

            files = new BackgroundProteomeFolder(DocumentContainer);
            if (files.Files.Count > 0)
            {
                list.Add(files);
            }

            files = new RTCalcFolder(DocumentContainer);
            if (files.Files.Count > 0)
            {
                list.Add(files);
            }

            files = new IonMobilityLibraryFolder(DocumentContainer);
            if (files.Files.Count > 0)
            {
                list.Add(files);
            }

            files = new OptimizationLibraryFolder(DocumentContainer);
            if (files.Files.Count > 0)
            {
                list.Add(files);
            }

            return list;
        }

        public static SkylineFile Create(SrmDocument document, string documentFilePath) 
        {
            var documentContainer = CreateDocumentContainer(document, documentFilePath);
            return new SkylineFile(documentContainer);
        }
    }
}