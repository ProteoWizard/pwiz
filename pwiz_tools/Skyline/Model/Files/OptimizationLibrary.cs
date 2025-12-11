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

using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings.Extensions;

namespace pwiz.Skyline.Model.Files
{
    public class OptimizationLibrary : FileModel
    {
        public static OptimizationLibrary Create(string documentFilePath, Optimization.OptimizationLibrary predictionOptimizedLibrary)
        {
            var identityPath = new IdentityPath(predictionOptimizedLibrary.Id);
            return new OptimizationLibrary(documentFilePath, identityPath, predictionOptimizedLibrary.Name, predictionOptimizedLibrary.FilePath);
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
        public static string TypeText => FileResources.FileModel_OptimizationLibrary;
        protected override string FileTypeText => TypeText;
        public override ImageId ImageAvailable => ImageId.opt_db;

        public static ModifiedDocument Edit(SrmDocument doc, Optimization.OptimizationLibrary newLib)
        {
            var newDocument = doc.ChangeSettings(doc.Settings.ChangeTransitionPrediction(prediction =>
                prediction.ChangeOptimizationLibrary(newLib)));
            var entry = AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_optimization_library_update, doc.DocumentType, newLib.Name);
            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }
    }
}
