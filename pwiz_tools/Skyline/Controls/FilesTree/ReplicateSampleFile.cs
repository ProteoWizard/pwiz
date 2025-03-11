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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Controls.FilesTree
{
    public class ReplicateSampleFile : FileNode
    {
        private readonly Lazy<ChromFileInfo> _chromFileInfo;

        public ReplicateSampleFile(SrmDocument document, string documentPath, ChromatogramSetId chromatogramSetId, ChromFileInfoId chromFileInfoId)
            : base(document, documentPath, new IdentityPath(chromatogramSetId, chromFileInfoId), ImageId.replicate_sample_file)
        {
            _chromFileInfo = new Lazy<ChromFileInfo>(FindChromFileInfo);
        }

        public override Immutable Immutable => _chromFileInfo.Value;

        public override string Name => _chromFileInfo.Value.Name;
        public override string FilePath => _chromFileInfo.Value.FilePath.GetFilePath();
        public override string FileName => _chromFileInfo.Value.FilePath.GetFileName();

        public override bool IsBackedByFile => true;

        private ChromFileInfo FindChromFileInfo()
        {
            return Document.MeasuredResults?.FindChromatogramSet(
                (ChromatogramSetId)IdentityPath.GetIdentity(0)).FindChromFileInfo(
                (ChromFileInfoId)  IdentityPath.GetIdentity(1));
        }
    }
}
