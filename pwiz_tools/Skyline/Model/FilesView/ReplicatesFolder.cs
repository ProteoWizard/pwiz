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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.FilesView
{
    public class ReplicatesFolder : FileNode
    {
        private static readonly Identity REPLICATES = new StaticFolderId();

        public ReplicatesFolder(SrmDocument document, string documentPath) : 
            base(document, documentPath, new IdentityPath(REPLICATES), ImageId.folder)
        {
        }

        public override Immutable Immutable => Document.Settings.MeasuredResults;

        public override IList<FileNode> Files
        {
            get
            {
                if (Document.MeasuredResults == null)
                {
                    return Array.Empty<Replicate>().ToList<FileNode>();
                }

                var files = Document.MeasuredResults.Chromatograms.Select(chromatogramSet => 
                    new Replicate(Document, DocumentPath, (ChromatogramSetId)chromatogramSet.Id)).ToList<FileNode>();

                return ImmutableList.ValueOf(files);
            }
        }

        public override string Name => FilesView.FilesTree_TreeNodeLabel_Replicates;
        public override string FilePath => string.Empty;
        public override string FileName => string.Empty;
    }
}
