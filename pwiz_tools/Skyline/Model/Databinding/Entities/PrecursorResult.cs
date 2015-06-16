/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.precursor_result)]
    public class PrecursorResult : Result
    {
        private readonly CachedValue<TransitionGroupChromInfo> _chromInfo;
        public PrecursorResult(Precursor precursor, ResultFile file) : base(precursor, file)
        {
            _chromInfo = CachedValue.Create(DataSchema, ()=>GetResultFile().FindChromInfo(precursor.DocNode.Results));
        }

        [HideWhen(AncestorOfType = typeof(Precursor))]
        public Precursor Precursor { get { return SkylineDocNode as Precursor; } }
        [Browsable(false)]
        public TransitionGroupChromInfo ChromInfo { get { return _chromInfo.Value; } }
        public void ChangeChromInfo(EditDescription editDescription, TransitionGroupChromInfo newChromInfo)
        {
            var newDocNode = Precursor.DocNode.ChangeResults(GetResultFile().ChangeChromInfo(Precursor.DocNode.Results, newChromInfo));
            Precursor.ChangeDocNode(editDescription, newDocNode);
        }
        [Format(Formats.PEAK_FOUND_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double PrecursorPeakFoundRatio { get { return ChromInfo.PeakCountRatio; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? BestRetentionTime { get { return ChromInfo.RetentionTime; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? MaxFwhm { get { return ChromInfo.Fwhm; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? MinStartTime { get { return ChromInfo.StartRetentionTime; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? MaxEndTime { get { return ChromInfo.EndRetentionTime; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalArea { get { return ChromInfo.Area; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalAreaMs1 { get { return ChromInfo.AreaMs1; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalAreaFragment { get { return ChromInfo.AreaFragment; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalBackground { get { return ChromInfo.BackgroundArea; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalBackgroundMs1 { get { return ChromInfo.BackgroundAreaMs1; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalBackgroundFragment { get { return ChromInfo.BackgroundAreaFragment; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalAreaRatio { get { return ChromInfo.Ratio; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? RatioDotProduct { get { return RatioValue.GetDotProduct(ChromInfo.Ratios.FirstOrDefault()); } }
        [Format(Formats.PEAK_AREA_NORMALIZED, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalAreaNormalized { get { return TotalArea / GetResultFile().GetTotalArea(Precursor.IsotopeLabelType); } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? MaxHeight {get { return ChromInfo.Height; }}
        [Format(Formats.MASS_ERROR, NullValue = TextUtil.EXCEL_NA)]
        public double? AverageMassErrorPPM { get { return ChromInfo.MassError; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? CountTruncated { get { return ChromInfo.Truncated; } }
        public PeakIdentification Identified { get { return ChromInfo.Identified; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryDotProduct { get { return ChromInfo.LibraryDotProduct; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? IsotopeDotProduct { get { return ChromInfo.IsotopeDotProduct; } }
        public UserSet UserSetTotal { get { return ChromInfo.UserSet; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int OptStep { get { return ChromInfo.OptimizationStep; } }
        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? OptCollisionEnergy
        {
            get
            {
                var ceRegression = GetResultFile().Replicate.ChromatogramSet.OptimizationFunction as CollisionEnergyRegression;
                if (null == ceRegression)
                {
                    return null;
                }
                return ceRegression.GetCollisionEnergy(Precursor.Charge, Precursor.GetRegressionMz(), ChromInfo.OptimizationStep);
            }
        }
        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? OptDeclusteringPotential 
        { 
            get 
            {
                var dpRegression = GetResultFile().Replicate.ChromatogramSet.OptimizationFunction as DeclusteringPotentialRegression;
                if (null == dpRegression)
                {
                    return null;
                }
                return dpRegression.GetDeclustringPotential(Precursor.GetRegressionMz(), ChromInfo.OptimizationStep);
            }
        }
        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? OptCompensationVoltage
        {
            get
            {
                var optimizationFunction = GetResultFile().Replicate.ChromatogramSet.OptimizationFunction;
                var covRegression = optimizationFunction as CompensationVoltageParameters;
                if (null == covRegression)
                {
                    return null;
                }
                return SrmDocument.GetOptimizedCompensationVoltage(Precursor.Peptide.DocNode, Precursor.DocNode);
            }
        }
        [InvariantDisplayName("PrecursorReplicateNote")]
        public string Note 
        { 
            get { return ChromInfo.Annotations.Note; } 
            set
            {
                ChangeChromInfo(
                    EditDescription.SetColumn("PrecursorReplicateNote", value), // Not L10N
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

        public override string ToString()
        {
            return string.Format("{0:0}", ChromInfo.Area); // Not L10N
        }

        private PeptideResult _peptideResult;
        [HideWhen(AncestorOfType = typeof(Precursor))]
        public PeptideResult PeptideResult 
        {
            get { return _peptideResult = _peptideResult ?? new PeptideResult(Precursor.Peptide, GetResultFile()); }
        }
    }
}
