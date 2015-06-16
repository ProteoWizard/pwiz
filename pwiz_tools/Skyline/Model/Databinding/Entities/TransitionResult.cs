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

using System.ComponentModel;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.transition_result)]
    public class TransitionResult : Result
    {
        private readonly CachedValue<TransitionChromInfo> _chromInfo;
        public TransitionResult(Transition transition, ResultFile resultFile) : base(transition, resultFile)
        {
            _chromInfo = CachedValue.Create(DataSchema, () => GetResultFile().FindChromInfo(transition.DocNode.Results));
        }

        [Browsable(false)]
        public TransitionChromInfo ChromInfo { get { return _chromInfo.Value; } }

        public void ChangeChromInfo(EditDescription editDescription, TransitionChromInfo newChromInfo)
        {
            var newDocNode = Transition.DocNode.ChangeResults(GetResultFile().ChangeChromInfo(Transition.DocNode.Results, newChromInfo));
            Transition.ChangeDocNode(editDescription, newDocNode);
        }
        [HideWhen(AncestorOfType = typeof(Transition))]
        public Transition Transition { get { return (Transition)SkylineDocNode; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? RetentionTime { get { return ChromInfo.IsEmpty ? (double?) null : ChromInfo.RetentionTime; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? Fwhm { get { return ChromInfo.IsEmpty ? (double?) null : ChromInfo.Fwhm; } }
        public bool FwhmDegenerate { get { return ChromInfo.IsFwhmDegenerate; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? StartTime { get { return ChromInfo.IsEmpty ? (double?)null : ChromInfo.StartRetentionTime; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? EndTime { get { return ChromInfo.IsEmpty ? (double?) null : ChromInfo.EndRetentionTime; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? Area { get { return ChromInfo.IsEmpty ? (double?) null : ChromInfo.Area; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? Background { get { return ChromInfo.IsEmpty ? (double?)null : ChromInfo.BackgroundArea; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? AreaRatio { get { return ChromInfo.Ratio; } }
        [Format(Formats.PEAK_AREA_NORMALIZED, NullValue = TextUtil.EXCEL_NA)]
        public double? AreaNormalized
        {
            get { return Area / GetResultFile().GetTotalArea(Transition.Precursor.IsotopeLabelType); }
        }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? Height { get { return ChromInfo.IsEmpty ? (double?) null : ChromInfo.Height; } }
        [Format(Formats.MASS_ERROR, NullValue = TextUtil.EXCEL_NA)]
        public double? MassErrorPPM { get { return ChromInfo.MassError; } }
        public bool? Truncated { get { return ChromInfo.IsTruncated; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? PeakRank { get { return ChromInfo.IsEmpty ? (int?)null : ChromInfo.Rank; } }
        public UserSet UserSetPeak { get { return ChromInfo.UserSet; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int OptStep { get { return ChromInfo.OptimizationStep; } }
        [InvariantDisplayName("TransitionReplicateNote")]
        public string Note
        {
            get { return ChromInfo.Annotations.Note; }
            set
            {
                ChangeChromInfo(EditDescription.SetColumn("TransitionReplicateNote", value), // Not L10N
                    ChromInfo.ChangeAnnotations(ChromInfo.Annotations.ChangeNote(value)));
            }
        }

        public override void SetAnnotation(AnnotationDef annotationDef, object value)
        {
            ChangeChromInfo(EditDescription.SetAnnotation(annotationDef, value), 
                ChromInfo.ChangeAnnotations(ChromInfo.Annotations.ChangeAnnotation(annotationDef, value)));
        }

        public override object GetAnnotation(AnnotationDef annotationDef)
        {
            return ChromInfo.Annotations.GetAnnotation(annotationDef);
        }

        [HideWhen(AncestorOfType = typeof(SkylineDocument))]
        public PrecursorResult PrecursorResult 
        {
            get
            {
                return new PrecursorResult(Transition.Precursor, GetResultFile());
            }
        }
        public override string ToString()
        {
            return string.Format("{0:0}", ChromInfo.Area); // Not L10N
        }
    }
}
