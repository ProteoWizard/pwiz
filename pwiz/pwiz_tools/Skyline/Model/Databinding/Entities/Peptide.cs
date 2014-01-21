/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    // ReSharper disable LocalizableElement
    [AnnotationTarget(AnnotationDef.AnnotationTarget.peptide)]
    public class Peptide : SkylineDocNode<PeptideDocNode>
    {
        public Peptide(SkylineDataSchema dataSchema, IdentityPath identityPath)
            : base(dataSchema, identityPath)
        {
        }

        private Precursors _precursors;
        [OneToMany(ForeignKey = "Peptide")]
        public Precursors Precursors
        {
            get { return _precursors = _precursors ?? new Precursors(this); }
        }

        private IDictionary<ResultKey, PeptideResult> _results;
        [DisplayName("PeptideResults")]
        [OneToMany(ForeignKey = "Peptide", ItemDisplayName = "PeptideResult")]
        public IDictionary<ResultKey, PeptideResult> Results
        {
            get
            {
                return _results = _results ?? MakeChromInfoResultsMap(DocNode.Results, file => new PeptideResult(this, file));
            }
        }

        protected override void OnDocumentChanged()
        {
            _results = null;
            base.OnDocumentChanged();
        }

        protected override PeptideDocNode CreateEmptyNode()
        {
            return new PeptideDocNode(new Model.Peptide(null, "X", null, null, 0));
        }

        [HideWhen(AncestorOfType = typeof(SkylineDocument))]
        public Protein Protein
        {
            get { return new Protein(DataSchema, IdentityPath.Parent); }
        }

        [DisplayName("PeptideSequence")]
        public string Sequence
        {
            get { return DocNode.Peptide.Sequence; }
        }

        [DisplayName("PeptideModifiedSequence")]
        public string ModifiedSequence
        {
            get
            {
                return SrmDocument.Settings.GetPrecursorCalc(IsotopeLabelType.light, DocNode.ExplicitMods)
                                  .GetModifiedSequence(Sequence, true);
            }
        }

        public string StandardType
        {
            get
            {
                return DocNode.GlobalStandardType;
            }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? BeginPos
        {
            get { return DocNode.Peptide.Begin; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? EndPos
        {
            get { return DocNode.Peptide.End; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int MissedCleavages
        {
            get { return DocNode.Peptide.MissedCleavages; }
        }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? RetentionTimeCalculatorScore
        {
            get
            {
                var retentionTimeRegression = GetRetentionTimeRegression();
                if (retentionTimeRegression == null)
                {
                    return null;
                }
                return retentionTimeRegression.Calculator.ScoreSequence(ModifiedSequence);
            }
        }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? PredictedRetentionTime
        {
            get
            {
                var retentionTimeRegression = GetRetentionTimeRegression();
                if (null == retentionTimeRegression)
                {
                    return null;
                }
                return retentionTimeRegression.GetRetentionTime(SrmDocument.Settings.GetModifiedSequence(DocNode));
            }
        }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? AverageMeasuredRetentionTime 
        {
            get { return DocNode.AverageMeasuredRetentionTime; }
        }

        [DisplayName("PeptideNote")]
        public string Note
        {
            get { return DocNode.Note; }
            set
            {
                var docNode = DocNode;
                ChangeDocNode(docNode.ChangeAnnotations(docNode.Annotations.ChangeNote(value)));
            }
        }

        private RetentionTimeRegression GetRetentionTimeRegression()
        {
            return SrmDocument.Settings.PeptideSettings.Prediction.RetentionTime;
        }

        public override string ToString()
        {
            return Sequence;
        }
    }
}
