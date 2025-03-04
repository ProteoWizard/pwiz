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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.FilesView
{
    public class Replicate : FileNode
    {
        private readonly ChromatogramSet _chromatogramSet;

        public Replicate(SrmDocument document, string documentPath, ChromatogramSetId chromSetId) : 
            base(document, documentPath, new IdentityPath(chromSetId), ImageId.replicate)
        {
            _chromatogramSet = document.MeasuredResults?.FindChromatogramSet(chromSetId);
        }

        public override Immutable Immutable => _chromatogramSet;

        public override string Name => _chromatogramSet.Name ?? string.Empty;
        public override string FilePath => null;
        public override string FileName => null;

        public override IList<FileNode> Files
        {
            get
            {
                return _chromatogramSet.MSDataFileInfos.Select(fileInfo => new ReplicateSampleFile(Document, DocumentPath,
                    (ChromatogramSetId)IdentityPath.GetIdentity(0), 
                    (ChromFileInfoId)  fileInfo.Id)).ToList<FileNode>();
            }
        }
    }
}
