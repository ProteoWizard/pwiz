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
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Files
{
    public class RTCalc : FileModel
    {
        public static IList<RTCalc> Create(string documentFilePath, RetentionScoreCalculatorSpec irtDb)
        {
            var identityPath = new IdentityPath(irtDb.Id);
            return ImmutableList<RTCalc>.Singleton(new RTCalc(documentFilePath, identityPath, irtDb.Name, irtDb.FilePath));
        }

        private RTCalc(string documentFilePath, IdentityPath identityPath, string name, string filePath) : 
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