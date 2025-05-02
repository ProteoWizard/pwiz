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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Files
{
    public class ProjectFilesFolder : FileNode
    {
        private static readonly Identity PROJECT_FILES_FOLDER = new StaticFolderId();

        public ProjectFilesFolder(IDocumentContainer documentContainer) :
            base(documentContainer, new IdentityPath(PROJECT_FILES_FOLDER), ImageId.folder)
        {
        }

        public override Immutable Immutable => new Immutable();
        public override string Name => FileResources.FileModel_ProjectFiles;
        public override string FilePath => string.Empty;

        public override IList<FileNode> Files
        {
            get
            {
                var files = new List<FileNode>();

                if (Document.Settings.DataSettings.IsAuditLoggingEnabled) 
                    files.Add(new SkylineAuditLog(DocumentContainer));

                if(IsDocumentSavedToDisk())
                    files.Add(new SkylineViewFile(DocumentContainer));

                // Chromatogram Caches (.skyd)
                // CONSIDER: is this correct? See more where Cache files are created in MeasuredResults @ line 1640
                // CONSIDER: does this also need to check if the file exists?
                var cachePaths = Document.Settings.MeasuredResults?.CachePaths;
                if (cachePaths != null)
                {
                    files.AddRange(cachePaths.Select(_ => new SkylineChromatogramCache(DocumentContainer)));
                }

                return ImmutableList.ValueOf(files);
            }
        }
    }
}
