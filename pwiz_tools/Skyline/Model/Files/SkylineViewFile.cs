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

using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Files
{
    public class SkylineViewFile : FileNode
    {
        private static readonly Identity VIEW_FILE = new StaticFolderId();

        public SkylineViewFile(SrmDocument document, string documentPath) : 
            base(document, documentPath, new IdentityPath(VIEW_FILE), ImageId.view_file)
        { 
        }

        public override Immutable Immutable => Document.Settings;

        public override string Name => FileResources.FileModel_ViewFile;
        public override string FilePath => SkylineWindow.GetViewFile(DocumentPath);
        public override bool IsBackedByFile => true;
    }
}
