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
    public class SkylineAuditLog : FileNode
    {
        private static readonly Identity AUDIT_LOG_FILE = new StaticFolderId();

        public SkylineAuditLog(SrmDocument document, string documentPath) : 
            base(document, documentPath, new IdentityPath(AUDIT_LOG_FILE), ImageId.audit_log)
        {
        }

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => new Immutable();
        public override string Name => FileResources.FilesTree_AuditLog;
        public override string FilePath => SrmDocument.GetAuditLogPath(DocumentPath);
    }
}
