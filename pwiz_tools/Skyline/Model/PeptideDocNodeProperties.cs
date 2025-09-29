/*
 * Original author: Aaron Banse <acbanse .at. icloud dot com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
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

using JetBrains.Annotations;
using pwiz.Skyline.Model.DocSettings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Resources;

namespace pwiz.Skyline.Model
{
    public class PeptideDocNodeProperties : GlobalizedObject, IAnnotatable
    {
        protected override ResourceManager GetResourceManager()
        {
            return PropertyGridDocNodeResources.ResourceManager;
        }

        public PeptideDocNodeProperties(SrmDocument document, Skyline.Model.Databinding.Entities.Peptide peptide)
        {
            Sequence = peptide.Sequence;
            ProteinSequence = peptide.Protein.Sequence;
            ModifiedSequence = peptide.ModifiedSequence.ToString();
            FirstPosition = peptide.FirstPosition;
            LastPosition = peptide.LastPosition;
            MissedCleavages = peptide.MissedCleavages;
            PredictedRetentionTime = peptide.PredictedRetentionTime;
            AvgMeasuredRetentionTime = peptide.AverageMeasuredRetentionTime;
            StandardType = peptide.StandardType.ToString();

            
        }

        [Category("Peptide")] public string Sequence { get; }
        [Category("Peptide")] public string ProteinSequence { get; }
        [Category("Peptide")] public string ModifiedSequence { get; }
        [Category("Peptide")] public int? FirstPosition { get; }
        [Category("Peptide")] public int? LastPosition { get; }
        [Category("Peptide")] public int MissedCleavages { get; }
        [Category("Peptide")] public double? PredictedRetentionTime { get; }
        [Category("Peptide")] public double? AvgMeasuredRetentionTime { get; }
        [UseCustomHandling]
        [Category("Peptide")] public string StandardType { get; set; }
        private readonly Func<SrmDocument, SrmSettingsChangeMonitor, object, ModifiedDocument> _editStandardTypeFunc;

        // Test Support - enforced by code check
        // Invoked via reflection in InspectPropertySheetResources in CodeInspectionTest
        [UsedImplicitly]
        private static ResourceManager ResourceManager() => PropertyGridDocNodeResources.ResourceManager;

        #region IAnnotatable Implementation

        [UseCustomHandling]
        [Category("Annotations")] public List<Annotations.Annotation> Annotations { get; }

        [UseCustomHandling]
        public Dictionary<Annotations.Annotation, Func<SrmDocument, SrmSettingsChangeMonitor, object, ModifiedDocument>> EditAnnotationFuncDictionary { get; }

        #endregion
    }
}
