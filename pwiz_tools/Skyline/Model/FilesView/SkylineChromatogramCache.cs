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

using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.FilesView
{
    public class SkylineChromatogramCache : FileNode
    {
        // TODO: make a temporary Id for ChromatogramCache, which doesn't have one of its own
        private class ChromatogramCacheId : Identity { }

        private readonly string _documentPath;
        private readonly string _cachePath;

        public SkylineChromatogramCache(SrmDocument document, string documentPath, string cachePath) : 
            base(document, new IdentityPath(new ChromatogramCacheId()), ImageId.cache_file)
        {
            _documentPath = documentPath;
            _cachePath = cachePath;
        }

        public override string Name => FilesView.FilesTree_TreeNodeLabel_ChromatogramCache;

        public override string FilePath => ChromatogramCache.FinalPathForName(_documentPath, null);
    }
}