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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.peptide)]
    public class Peptide : SkylineDocNode<PeptideDocNode>
    {
        private readonly CachedValue<IDictionary<ResultKey, PeptideResult>> _results;
        public Peptide(SkylineDataSchema dataSchema, IdentityPath identityPath)
            : base(dataSchema, identityPath)
        {
            _results = CachedValue.Create(dataSchema, MakeResults);
        }

        private Precursors _precursors;
        [OneToMany(ForeignKey = "Peptide")]
        public Precursors Precursors
        {
            get { return _precursors = _precursors ?? new Precursors(this); }
        }

        [InvariantDisplayName("PeptideResults")]
        [OneToMany(ForeignKey = "Peptide", ItemDisplayName = "PeptideResult")]
        public IDictionary<ResultKey, PeptideResult> Results
        {
            get
            {
                return _results.Value;
            }
        }

        private IDictionary<ResultKey, PeptideResult> MakeResults()
        {
            return MakeChromInfoResultsMap(DocNode.Results, file => new PeptideResult(this, file));
        }

        protected override PeptideDocNode CreateEmptyNode()
        {
            return new PeptideDocNode(new Model.Peptide(null, "X", null, null, 0)); // Not L10N
        }

        [HideWhen(AncestorOfType = typeof(Protein))]
        public Protein Protein
        {
            get { return new Protein(DataSchema, IdentityPath.Parent); }
        }

        [InvariantDisplayName("PeptideSequence")]
        public string Sequence
        {
            get { return ToString(); } // Will show custom ion name if this isn't actually a peptide proper
        }

        [InvariantDisplayName("PeptideModifiedSequence")]
        public string ModifiedSequence
        {
            get
            {
                return DocNode.Peptide.IsCustomIon
                    ? TextUtil.EXCEL_NA
                    : SrmDocument.Settings.GetPrecursorCalc(IsotopeLabelType.light, DocNode.ExplicitMods)
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

        public char PreviousAa
        {
            get { return DocNode.Peptide.PrevAA; }
        }

        public char NextAa
        {
            get { return DocNode.Peptide.NextAA; }
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
                return retentionTimeRegression.GetRetentionTime(SrmDocument.Settings.GetSourceTextId(DocNode));
            }
        }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? AverageMeasuredRetentionTime 
        {
            get { return DocNode.AverageMeasuredRetentionTime; }
        }

        [InvariantDisplayName("PeptideNote")]
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
            var peptide = DocNode.Peptide;
            return peptide.IsCustomIon
                ? peptide.CustomIon.ToString()
                : peptide.Sequence;
        }

        [InvariantDisplayName("PeptideDocumentLocation")]
        public DocumentLocation DocumentLocation
        {
            get
            {
                return new DocumentLocation(IdentityPath.ToGlobalIndexList());
            }
        }
    }
}
