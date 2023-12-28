/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

namespace AssortResources
{
    /// <summary>
    /// Holds information about text in a source file that uses a resource from a resource file
    /// </summary>
    public class ResourceReference
    {
        public ResourceReference(string resourceFileName, int resourceFileNameOffset, string resourceIdentifier,
            int resourceIdentifierOffset)
        {
            ResourceFileName = resourceFileName;
            ResourceFileNameOffset = resourceFileNameOffset;
            ResourceIdentifier = resourceIdentifier;
            ResourceIdentifierOffset = resourceIdentifierOffset;
        }

        public string ResourceFileName { get; }
        /// <summary>
        /// Character offset of ResourceFileName within the source file
        /// </summary>
        public int ResourceFileNameOffset { get; }
        public string ResourceIdentifier { get; }
        public int ResourceIdentifierOffset { get; }
    }
}
