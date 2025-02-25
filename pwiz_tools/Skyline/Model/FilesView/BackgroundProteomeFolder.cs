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
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.FilesView
{
    public class BackgroundProteomeFolder : FileNode
    {
        private static readonly Identity BACKGROUND_PROTEOME = new StaticFolderId();

        public BackgroundProteomeFolder(SrmDocument document) : 
            base(document, new IdentityPath(BACKGROUND_PROTEOME), ImageId.folder)
        {
        }

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.Settings.PeptideSettings == null || !Document.Settings.PeptideSettings.HasBackgroundProteome)
                {
                    return Array.Empty<Replicate>().ToList<FileNode>();
                }

                return ImmutableList<FileNode>.Singleton(new BackgroundProteome(Document, Document.Settings.PeptideSettings.BackgroundProteome.Id));
            }
        }

        public override string Name => FilesView.FilesTree_TreeNodeLabel_BackgroundProteome;
        public override string FilePath => string.Empty;
        public override string FileName => string.Empty;
    }
}