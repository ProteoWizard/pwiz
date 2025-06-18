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
    public class OptimizationLibrary : FileNode
    {
        public OptimizationLibrary(IDocumentContainer documentContainer, Identity optimizedLibraryId) : 
            base(documentContainer, new IdentityPath(optimizedLibraryId))
        {
        }

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => OptLibrary;
        public override string Name => OptLibrary.Name;
        public override string FilePath => OptLibrary.FilePath;

        public override bool ModelEquals(FileNode nodeDoc)
        {
            if (nodeDoc == null) return false;
            if (!(nodeDoc is OptimizationLibrary library)) return false;

            return ReferenceEquals(OptLibrary, library.OptLibrary);
        }

        private Optimization.OptimizationLibrary OptLibrary => 
            Document.Settings.TransitionSettings.Prediction.OptimizedLibrary;
    }
}