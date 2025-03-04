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

namespace pwiz.Skyline.Model.FilesView
{
    public class SkylineChromatogramCache : FileNode
    {
        // TODO: this is a temporary Id for ChromatogramCache, which doesn't have one of its own
        private class ChromatogramCacheId : Identity { }

        public SkylineChromatogramCache(SrmDocument document, string documentPath) : 
            base(document, documentPath, new IdentityPath(new ChromatogramCacheId()), ImageId.cache_file)
        {
        }

        public override Immutable Immutable => Document.Settings.MeasuredResults;

        public override string Name => FilesView.FilesTree_TreeNodeLabel_ChromatogramCache;

        public override string FilePath => ChromatogramCache.FinalPathForName(DocumentPath, null);

        public override bool IsBackedByFile => true;
    }
}