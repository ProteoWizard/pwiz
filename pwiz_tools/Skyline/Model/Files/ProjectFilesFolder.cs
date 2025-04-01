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
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Files
{
    public class ProjectFilesFolder : FileNode
    {
        private static readonly Identity PROJECT_FILES_FOLDER = new StaticFolderId();

        public ProjectFilesFolder(SrmDocument document, string documentPath) : 
            base(document, documentPath, new IdentityPath(PROJECT_FILES_FOLDER), ImageId.folder)
        {
        }

        public override Immutable Immutable => Document;
        public override string Name => FileResources.FileModel_ProjectFiles;
        public override string FilePath => string.Empty;

        public override IList<FileNode> Files
        {
            get
            {
                IList<FileNode> files = new List<FileNode>();

                if (Document.Settings.DataSettings.IsAuditLoggingEnabled) 
                    files.Add(new SkylineAuditLog(Document, DocumentPath));

                // CONSIDER: does this need to check for whether the Skyline Document is saved to disk?
                files.Add(new SkylineViewFile(Document, DocumentPath));

                // Chromatogram Caches (.skyd)
                // CONSIDER: is this correct? See more where Cache files are created in MeasuredResults @ line 1640
                // CONSIDER: does this also need to check if the file exists?
                var cachePaths = Document.Settings.MeasuredResults?.CachePaths;
                if (cachePaths != null)
                {
                    foreach (var _ in cachePaths)
                    {
                        files.Add(new SkylineChromatogramCache(Document, DocumentPath));
                    }
                }

                return ImmutableList.ValueOf(files);
            }
        }

    }
}
