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
    public class BackgroundProteomeFolder : FileNode
    {
        private static readonly Identity BACKGROUND_PROTEOME = new StaticFolderId();

        public BackgroundProteomeFolder(SrmDocument document, string documentPath) : 
            base(document, documentPath, new IdentityPath(BACKGROUND_PROTEOME), ImageId.folder)
        {
        }

        public override Immutable Immutable => Document.Settings.PeptideSettings;
        public override string Name => FileResources.FileModel_BackgroundProteome;
        public override string FilePath => string.Empty;
        public override string FileName => string.Empty;

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.Settings.PeptideSettings is { HasBackgroundProteome: true })
                {
                    return ImmutableList<FileNode>.Singleton(new BackgroundProteome(Document, DocumentPath, Document.Settings.PeptideSettings.BackgroundProteome.Id));
                }
                else {
                    return ImmutableList.Empty<FileNode>();
                }
            }
        }
    }
}