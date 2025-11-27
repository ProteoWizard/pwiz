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
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Files
{
    public class OptimizationLibrary : FileModel
    {
        public static IList<OptimizationLibrary> Create(string documentFilePath, Optimization.OptimizationLibrary predictionOptimizedLibrary)
        {
            var identityPath = new IdentityPath(predictionOptimizedLibrary.Id);
            var model = new OptimizationLibrary(documentFilePath, identityPath, predictionOptimizedLibrary.Name, predictionOptimizedLibrary.FilePath);

            return ImmutableList<OptimizationLibrary>.Singleton(model);
        }

        private OptimizationLibrary(string documentFilePath, IdentityPath identityPath, string name, string filePath) : 
            base(documentFilePath, identityPath)
        {
            Name = name;
            FilePath = filePath;
        }

        public override bool IsBackedByFile => true;
        public override string Name { get; }
        public override string FilePath { get; }
    }
}