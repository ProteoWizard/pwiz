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

namespace pwiz.Skyline.Model.Files
{
    public class SkylineAuditLog : FileModel
    {
        private static readonly IdentityPath IDENTITY_PATH = new IdentityPath(new StaticFolderId());

        public static SkylineAuditLog Create(SrmDocument document, string documentFilePath)
        {
            var name = FileResources.FilesTree_AuditLog;
            var filePath = SrmDocument.GetAuditLogPath(documentFilePath);

            return new SkylineAuditLog(documentFilePath, name, filePath);
        }

        internal SkylineAuditLog(string documentFilePath, string name, string filePath) : base(documentFilePath, IDENTITY_PATH)
        {
            Name = name;
            FilePath = filePath;
        }

        public override bool IsBackedByFile => true;
        public override bool RequiresSavedDocument => false;

        public override string Name { get; }
        public override string FilePath { get; }
        public override ImageId ImageAvailable => ImageId.audit_log;
    }
}
