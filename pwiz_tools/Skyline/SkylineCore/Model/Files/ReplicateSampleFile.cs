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

namespace pwiz.Skyline.Model.Files
{
    public class ReplicateSampleFile : FileModel
    {
        public static ReplicateSampleFile Create(string documentFilePath, IdentityPath chromSetId, ChromFileInfo chromFileInfo)
        {
            var identityPath = new IdentityPath(chromSetId.GetIdentity(0), chromFileInfo.Id); 

            var name = chromFileInfo.Name;
            var filePath = chromFileInfo.FilePath?.GetFilePath() ?? string.Empty;
            var fileName = chromFileInfo.FilePath?.GetFileName() ?? string.Empty;

            return new ReplicateSampleFile(documentFilePath, identityPath, name, fileName, filePath);
        }

        public ReplicateSampleFile(string documentFilePath, IdentityPath identityPath, string name, string fileName, string filePath) : 
            base(documentFilePath, identityPath) 
        {
            Name = name;
            FileName = fileName;
            FilePath = filePath;
        }

        public override bool IsBackedByFile => true;
        public override string Name { get; }
        public override string FilePath { get; }
        public override string FileName { get; }
        protected override string FileTypeText => string.Empty; // Replicate sample files don't use type prefix
        public override ImageId ImageAvailable => ImageId.replicate_sample_file;
    }
}
