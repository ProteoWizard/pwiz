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
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Files
{
    public class ReplicateSampleFile : FileNode
    {
        public ReplicateSampleFile(IDocumentContainer documentContainer, ChromatogramSetId chromatogramSetId, ChromFileInfoId chromFileInfoId)
            : base(documentContainer, new IdentityPath(chromatogramSetId, chromFileInfoId), ImageId.replicate_sample_file)
        {
        }

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => ChromFileInfo;
        public override string Name => ChromFileInfo?.Name ?? string.Empty;
        public override string FilePath => ChromFileInfo?.FilePath.GetFilePath() ?? string.Empty;
        public override string FileName => ChromFileInfo?.FilePath.GetFileName() ?? string.Empty;

        internal ChromFileInfo ChromFileInfo
        {
            get
            {
                var chromSetId = (ChromatogramSetId)IdentityPath.GetIdentity(0);
                if (DocumentContainer.Document.MeasuredResults.TryGetChromatogramSet(chromSetId.GlobalIndex, out var chromSet, out _))
                {
                    var chromFileInfoId = (ChromFileInfoId)IdentityPath.GetIdentity(1);
                    return chromSet.GetFileInfo(chromFileInfoId);
                }
                else return null;
            }
        }

        public override GlobalizedObject GetProperties(string localFilePath)
        {
            return new ReplicateSampleFileProperties(this, localFilePath);
        }

        public override bool ModelEquals(FileNode nodeDoc)
        {
            if (nodeDoc == null) return false;
            if (!(nodeDoc is ReplicateSampleFile sampleFile)) return false;

            return ReferenceEquals(ChromFileInfo, sampleFile.ChromFileInfo);
        }
    }
}
