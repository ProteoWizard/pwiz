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
    public class OptimizationLibraryFolder : FileNode
    {
        // ReSharper disable once IdentifierTypo
        private static readonly Identity OPTDB_FOLDER = new StaticFolderId();

        public OptimizationLibraryFolder(SrmDocument document) : base(document, new IdentityPath(OPTDB_FOLDER), ImageId.folder)
        {
        }

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.Settings.TransitionSettings == null || !Document.Settings.TransitionSettings.HasOptimizationLibraryPersisted)
                {
                    return Array.Empty<Replicate>().ToList<FileNode>();
                }

                return ImmutableList.Singleton<FileNode>(new OptimizationLibrary(Document, Document.Settings.TransitionSettings.Prediction.OptimizedLibrary.Id));
            }
        }

        public override string Name => FilesView.FilesTree_TreeNodeLabel_OptimizationLibrary;
        public override string FilePath => string.Empty;
    }
}