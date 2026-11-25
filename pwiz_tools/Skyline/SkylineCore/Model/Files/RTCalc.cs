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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;

namespace pwiz.Skyline.Model.Files
{
    public class RTCalc : FileModel
    {
        public static RTCalc Create(string documentFilePath, RetentionScoreCalculatorSpec irtDb)
        {
            var identityPath = new IdentityPath(irtDb.Id);
            return new RTCalc(documentFilePath, identityPath, irtDb.Name, irtDb.FilePath);
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
        public static string TypeText => SkylineResources.SkylineWindow_FindIrtDatabase_iRT_Calculator;
        protected override string FileTypeText => TypeText;
        public override ImageId ImageAvailable => ImageId.irt_calculator;

        public static ModifiedDocument Edit(SrmDocument doc, RetentionScoreCalculatorSpec newCalc)
        {
            var newDocument = doc.ChangeSettings(doc.Settings.ChangePeptidePrediction(prediction =>
                prediction.ChangeRetentionTime(prediction.RetentionTime.ChangeCalculator(newCalc))));
            var entry = AuditLogEntry.CreateSimpleEntry(MessageType.files_tree_rt_calculator_update, doc.DocumentType, newCalc.Name);
            return new ModifiedDocument(newDocument).ChangeAuditLogEntry(entry);
        }
    }
}
