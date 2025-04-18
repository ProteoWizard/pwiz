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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Files
{
    // .irtdb
    public class RTCalc : FileNode
    {
        private readonly Lazy<RetentionScoreCalculatorSpec> _lazy;

        public RTCalc(SrmDocument document, string documentPath, Identity id) : base(document, documentPath, new IdentityPath(id))
        {
            _lazy = new Lazy<RetentionScoreCalculatorSpec>(FindRTCalcSpec);
        }

        public override bool IsBackedByFile => true;
        public override Immutable Immutable => _lazy.Value;
        public override string Name => _lazy.Value.Name;
        public override string FilePath => _lazy.Value.FilePath;

        private RetentionScoreCalculatorSpec FindRTCalcSpec()
        {
            return Document.Settings.PeptideSettings.Prediction.RetentionTime.Calculator;
        }
    }
}