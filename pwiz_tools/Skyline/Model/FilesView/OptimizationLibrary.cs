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

namespace pwiz.Skyline.Model.FilesView
{
    public class OptimizationLibrary : FileNode
    {
        private readonly Lazy<Optimization.OptimizationLibrary> _lazy;

        public OptimizationLibrary(SrmDocument document, Identity optimizedLibraryId) : base(document, new IdentityPath(optimizedLibraryId))
        {
            _lazy = new Lazy<Optimization.OptimizationLibrary>(FindOptimizationLibrary);
        }

        private Optimization.OptimizationLibrary FindOptimizationLibrary()
        {
            return Document.Settings.TransitionSettings.Prediction.OptimizedLibrary;
        }

        public override string Name => _lazy.Value.Name;
        public override string FilePath => _lazy.Value.FilePath;
    }
}