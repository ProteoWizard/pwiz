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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [InvariantDisplayName(nameof(PrecursorResult))]
    [AnnotationTarget(AnnotationDef.AnnotationTarget.precursor_result)]
    public class PrecursorResult : Result
    {
        private readonly CachedValues _cachedValues = new CachedValues();
        public PrecursorResult(Precursor precursor, ResultFile file) : base(precursor, file)
        {
        }

        [HideWhen(AncestorOfType = typeof(Precursor))]
        public Precursor Precursor { get { return SkylineDocNode as Precursor; } }
        [Browsable(false)]
        public TransitionGroupChromInfo ChromInfo { get { return _cachedValues.GetValue(this); } }
        public void ChangeChromInfo(EditDescription editDescription, Func<TransitionGroupChromInfo, TransitionGroupChromInfo> newChromInfo)
        {
            Precursor.ChangeDocNode(editDescription, docNode => docNode.ChangeResults(GetResultFile()
                .ChangeChromInfo(docNode.Results, newChromInfo)));
        }
        [Format(Formats.PValue, NullValue = TextUtil.EXCEL_NA)]
        public double? DetectionQValue { get { return ChromInfo.QValue; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? DetectionZScore { get { return ChromInfo.ZScore; } }
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
        public double? TotalAreaRatio
        {
            get
            {
                var firstInternalStandard = DataSchema.NormalizedValueCalculator.RatioInternalStandardTypes.FirstOrDefault();
                if (firstInternalStandard != null)
                {
                    return GetNormalizedArea(new NormalizationMethod.RatioToLabel(firstInternalStandard));
                }
                return GetNormalizedArea(NormalizationMethod.GLOBAL_STANDARDS);
            }
        }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? RatioDotProduct {
            get
            {
                var firstInternalStandard = DataSchema.NormalizedValueCalculator.RatioInternalStandardTypes.FirstOrDefault();
                if (firstInternalStandard != null)
                {
                    return GetRatioValue(new NormalizationMethod.RatioToLabel(firstInternalStandard))?.DotProduct;
                }
                return null;
            }
        }
        [Format(Formats.PEAK_AREA_NORMALIZED, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalAreaNormalized { get { return TotalArea / GetResultFile().GetTotalArea(Precursor.IsotopeLabelType); } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? MaxHeight {get { return ChromInfo.Height; }}
        [Format(Formats.MASS_ERROR, NullValue = TextUtil.EXCEL_NA)]
        public double? AverageMassErrorPPM { get { return ChromInfo.MassError; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? CountTruncated { get { return ChromInfo.Truncated; } }
        [Format(Formats.Percent, NullValue = TextUtil.EXCEL_NA)]
        public double? ProportionTruncated { get { return CalculateProportionTruncated(); } }
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
                return ceRegression.GetCollisionEnergy(Precursor.DocNode.PrecursorAdduct, Precursor.GetRegressionMz(), ChromInfo.OptimizationStep);
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
                return SrmDocument.GetCompensationVoltage(Precursor.Peptide.DocNode, Precursor.DocNode, null, ChromInfo.OptimizationStep, covRegression.TuneLevel);
            }
        }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? CollisionalCrossSection { get { return ChromInfo.IonMobilityInfo.CollisionalCrossSection; } }

        [Obsolete("use IonMobilityMS1 instead")] 
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? DriftTimeMS1 { get { return ChromInfo.IonMobilityInfo.DriftTimeMS1; } }

        [Obsolete("use IonMobilityFragment instead")] 
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? DriftTimeFragment { get { return ChromInfo.IonMobilityInfo.DriftTimeFragment; } }

        [Obsolete("use IonMobilityWindow instead")]
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? DriftTimeWindow { get { return ChromInfo.IonMobilityInfo.DriftTimeWindow; } }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? IonMobilityMS1 { get { return ChromInfo.IonMobilityInfo.IonMobilityMS1; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? IonMobilityFragment { get { return ChromInfo.IonMobilityInfo.IonMobilityFragment; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? IonMobilityWindow { get { return ChromInfo.IonMobilityInfo.IonMobilityWindow; } }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public string IonMobilityUnits { get { return IonMobilityFilter.IonMobilityUnitsL10NString(ChromInfo.IonMobilityInfo.IonMobilityUnits); } }


        public LinkValue<PrecursorQuantificationResult> PrecursorQuantification
        {
            get
            {
                return new LinkValue<PrecursorQuantificationResult>(_cachedValues.GetValue1(this), (sender, args) =>
                {
                    SkylineWindow skylineWindow = DataSchema.SkylineWindow;
                    if (skylineWindow != null)
                    {
                        skylineWindow.ShowCalibrationForm();
                        skylineWindow.SelectedResultsIndex = GetResultFile().Replicate.ReplicateIndex;
                        skylineWindow.SelectedPath = Precursor.IdentityPath;
                        Settings.Default.CalibrationCurveOptions =
                            Settings.Default.CalibrationCurveOptions.ChangeSingleBatch(true);
                    }
                });
            }
        }

        [Expensive]
        [ChildDisplayName("{0}MS1")]
        [Format(Formats.PEAK_AREA)]
        public LcPeakIonMetrics LcPeakIonMetricsMS1
        {
            get
            {
                return _cachedValues.GetValue2(this)?.Item1;
            }
        }

        [Expensive]
        [ChildDisplayName("{0}Fragment")]
        [Format(Formats.PEAK_AREA)]
        public LcPeakIonMetrics LcPeakIonMetricsFragment
        {
            get
            {
                return _cachedValues.GetValue2(this)?.Item2;
            }
        }

        [ChildDisplayName("Original{0}")]
        [Format(Formats.RETENTION_TIME)]
        public ScoredPeakValue OriginalPeak {
            get
            {
                return ScoredPeakValue.FromScoredPeak(ChromInfo.OriginalPeak);
            }
        }

        [ChildDisplayName("Reintegrated{0}")]
        [Format(Formats.RETENTION_TIME)]
        public ScoredPeakValue ReintegratedPeak
        {
            get
            {
                return ScoredPeakValue.FromScoredPeak(ChromInfo.ReintegratedPeak);
            }
        }

        [InvariantDisplayName("PrecursorReplicateNote")]
        [Importable]
        public string Note 
        { 
            get { return ChromInfo.Annotations.Note; } 
            set
            {
                ChangeChromInfo(EditColumnDescription(nameof(Note), value),
                    chromInfo=>chromInfo.ChangeAnnotations(chromInfo.Annotations.ChangeNote(value)));
            }
        }

        public override void SetAnnotation(AnnotationDef annotationDef, object value)
        {
            // Ignore setting of the old q value and mProphet score annoations. They are
            // displayed for backward compatibility. Setting them manually never made and sense.
            if (Equals(annotationDef.Name, MProphetResultsHandler.AnnotationName) ||
                Equals(annotationDef.Name, MProphetResultsHandler.MAnnotationName))
                return;

            ChangeChromInfo(EditDescription.SetAnnotation(annotationDef, value), 
                chromInfo=>chromInfo.ChangeAnnotations(chromInfo.Annotations.ChangeAnnotation(annotationDef, value)));
        }

        public override object GetAnnotation(AnnotationDef annotationDef)
        {
            // Return q value and mProphet scores from their new locations
            if (Equals(annotationDef.Name, MProphetResultsHandler.AnnotationName))
                return DetectionQValue;
            else if (Equals(annotationDef.Name, MProphetResultsHandler.MAnnotationName))
                return DetectionZScore;

            return DataSchema.AnnotationCalculator.GetAnnotation(annotationDef, this, ChromInfo.Annotations);
        }

        public override string ToString()
        {
            return string.Format(@"{0:0}", ChromInfo.Area);
        }

        private PeptideResult _peptideResult;
        [HideWhen(AncestorOfType = typeof(Precursor))]
        public PeptideResult PeptideResult 
        {
            get { return _peptideResult = _peptideResult ?? new PeptideResult(Precursor.Peptide, GetResultFile()); }
        }

        [ChildDisplayName("Imputed{0}")]
        [Format(Formats.RETENTION_TIME)]
        public ImputedPeakBounds ImputedPeak
        {
            get
            {
                return ImputedPeakBounds.FromPeakBounds(DataSchema.PeakBoundaryImputer
                    .GetImputedPeakQuick(PeptideResult.Peptide.DocNode,
                        GetResultFile().Replicate.ChromatogramSet, GetResultFile().ChromFileInfo.FilePath)?.PeakBounds);
            }
        }

        [InvariantDisplayName("PrecursorResultLocator")]
        public string Locator { get { return GetLocator(); } }

        public override ElementRef GetElementRef()
        {
            return PrecursorResultRef.PROTOTYPE.ChangeChromInfo(GetResultFile().Replicate.ChromatogramSet, ChromInfo)
                .ChangeParent(Precursor.GetElementRef());
        }

        public override bool IsEmpty()
        {
            return !ChromInfo.RetentionTime.HasValue;
        }

        public double? GetNormalizedArea(NormalizationMethod normalizationMethod)
        {
            if (normalizationMethod == null)
            {
                return null;
            }

            return DataSchema.NormalizedValueCalculator.GetTransitionGroupValue(normalizationMethod,
                Precursor.Peptide.DocNode, Precursor.DocNode, GetResultFile().Replicate.ReplicateIndex, ChromInfo);
        }

        public RatioValue GetRatioValue(NormalizationMethod.RatioToLabel ratioToLabel)
        {
            return DataSchema.NormalizedValueCalculator.GetTransitionGroupRatioValue(ratioToLabel,
                Precursor.Peptide.DocNode, Precursor.DocNode, ChromInfo);
        }

        private class CachedValues 
            : CachedValues<PrecursorResult, TransitionGroupChromInfo, PrecursorQuantificationResult, Tuple<LcPeakIonMetrics, LcPeakIonMetrics>>
        {
            protected override SrmDocument GetDocument(PrecursorResult owner)
            {
                return owner.SrmDocument;
            }

            protected override TransitionGroupChromInfo CalculateValue(PrecursorResult owner)
            {
                return owner.GetResultFile().FindChromInfo(owner.Precursor.DocNode.Results);
            }

            protected override PrecursorQuantificationResult CalculateValue1(PrecursorResult owner)
            {
                var calibrationCurveFitter = owner.PeptideResult.GetCalibrationCurveFitter();
                return calibrationCurveFitter.GetPrecursorQuantificationResult(owner.GetResultFile().Replicate.ReplicateIndex,
                    owner.Precursor.DocNode);
            }

            protected override Tuple<LcPeakIonMetrics,LcPeakIonMetrics> CalculateValue2(PrecursorResult owner)
            {
                return owner.CalculatePeakIonMetrics();
            }
        }

        private Tuple<LcPeakIonMetrics, LcPeakIonMetrics> CalculatePeakIonMetrics()
        {
            if (IsEmpty())
            {
                return null;
            }

            var resultFileMetadata =
                SrmDocument.MeasuredResults?.GetResultFileMetaData(GetResultFile().ChromFileInfo.FilePath);
            if (resultFileMetadata == null)
            {
                return null;
            }
            var chromatogramGroup = new ChromatogramGroup(this);
            var timeIntensitiesGroup = chromatogramGroup.ReadTimeIntensitiesGroup();
            if (timeIntensitiesGroup == null)
            {
                return null;
            }
            float tolerance = (float) SrmDocument.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var chromatogramInfos = new List<ChromatogramInfo>();
            foreach (var transitionDocNode in Precursor.DocNode.Transitions)
            {
                var transitionChromInfo = GetResultFile().FindChromInfo(transitionDocNode.Results);
                if (transitionChromInfo == null || transitionChromInfo.IsEmpty)
                {
                    continue;
                }

                var chromatogramInfo =
                    chromatogramGroup.ChromatogramGroupInfo.GetTransitionInfo(transitionDocNode, tolerance,
                        TransformChrom.raw);
                if (chromatogramInfo == null)
                {
                    continue;
                }
                chromatogramInfos.Add(chromatogramInfo);
            }

            var chromatogramsBySource = chromatogramInfos.ToLookup(
                chromInfo => chromInfo.Source, chromInfo => timeIntensitiesGroup.TransitionTimeIntensities[chromInfo.TransitionIndex]);
            var ms1Chromatograms = chromatogramsBySource[ChromSource.sim].ToList();
            if (ms1Chromatograms.Count == 0)
            {
                ms1Chromatograms = chromatogramsBySource[ChromSource.ms1].ToList();
            }

            return Tuple.Create(GetIonMetrics(resultFileMetadata, ms1Chromatograms),
                GetIonMetrics(resultFileMetadata, chromatogramsBySource[ChromSource.fragment].ToList()));
        }

        private LcPeakIonMetrics GetIonMetrics(ResultFileMetaData resultFileMetadata, IList<TimeIntensities> chromatograms)
        {
            return GetPeakIonMetrics(resultFileMetadata, ChromInfo.StartRetentionTime.Value,
                ChromInfo.EndRetentionTime.Value, chromatograms);
        }

        public static LcPeakIonMetrics GetPeakIonMetrics(ResultFileMetaData resultFileMetadata, float startTime,
            float endTime, IList<TimeIntensities> chromatograms)
        {
            var scanIndexes = chromatograms.FirstOrDefault()?.ScanIds;
            if (scanIndexes == null)
            {
                return null;
            }
            var times = chromatograms[0].Times;
            if (!chromatograms.Skip(1).All(chromatogramInfo =>
                    Equals(chromatogramInfo.Times, times) && Equals(chromatogramInfo.ScanIds, scanIndexes)))
            {
                return null;
            }

            int iStart = CollectionUtil.BinarySearch(times, startTime);
            if (iStart < 0)
            {
                iStart = Math.Max(0, ~iStart - 1);
            }

            List<float> ticTimes = new List<float>();
            List<float> ticIntensities = new List<float>();
            double? apexSpectrumIonCount = null;
            double totalSpectrumIonCount = 0;
            double? apexAnalyteIonCount = null;
            double lcPeakIonCount = 0;
            double? apexIntensity = null;
            string apexSpectrumId = null;
            for (int i = iStart; i < times.Count; i++)
            {
                var scanIndex = scanIndexes[i];
                var time = times[i];
                ticTimes.Add(time);
                var spectrumMetadata = resultFileMetadata.SpectrumMetadatas[scanIndex];


                var totalIonCurrent = spectrumMetadata.TotalIonCurrent.GetValueOrDefault();
                ticIntensities.Add((float)totalIonCurrent);
                if (time > endTime)
                {
                    break;
                }

                if (time < startTime)
                {
                    continue;
                }

                double intensity = 0;
                foreach (var chromatogramInfo in chromatograms)
                {
                    intensity += chromatogramInfo.Intensities[i];
                }

                double? injectionTimeSeconds = spectrumMetadata.InjectionTime / 1000;
                double? ionCount = intensity * injectionTimeSeconds;
                double? spectrumIonCount = totalIonCurrent * injectionTimeSeconds;

                if (apexIntensity == null || intensity > apexIntensity.Value)
                {
                    apexAnalyteIonCount = ionCount;
                    apexSpectrumIonCount = spectrumIonCount;
                    apexIntensity = intensity;
                    apexSpectrumId = spectrumMetadata.Id;
                }
                lcPeakIonCount += ionCount.GetValueOrDefault();
                totalSpectrumIonCount += spectrumIonCount.GetValueOrDefault();
            }

            if (ticTimes.Count == 0)
            {
                return null;
            }


            var lcPeakIonMetrics = new LcPeakIonMetrics(apexSpectrumId);
            if (ticTimes.Count != 0)
            {
                var ticTimeIntensities = new TimeIntensities(ticTimes, ticIntensities);
                var chromPeak = ChromPeak.IntegrateWithoutBackground(ticTimeIntensities, startTime, endTime, 0, null);
                Assume.AreEqual(0f, chromPeak.BackgroundArea);
                lcPeakIonMetrics = lcPeakIonMetrics.ChangeTotalIonCurrentArea(chromPeak.Area);
            }

            if (apexSpectrumIonCount.HasValue)
            {
                lcPeakIonMetrics =
                    lcPeakIonMetrics.ChangeTotalIonCount(apexSpectrumIonCount.Value, totalSpectrumIonCount);
            }

            if (apexAnalyteIonCount.HasValue)
            {
                lcPeakIonMetrics = lcPeakIonMetrics.ChangeAnalyteIonCount(apexAnalyteIonCount.Value, lcPeakIonCount);
            }

            return lcPeakIonMetrics;
        }

        private double? CalculateProportionTruncated()
        {
            double totalArea = 0;
            double truncatedArea = 0;
            var srmSettings = DataSchema.Document.Settings;
            var resultFile = GetResultFile();
            foreach (var transition in Precursor.DocNode.Transitions)
            {
                if (!transition.IsQuantitative(srmSettings))
                {
                    continue;
                }

                foreach (var transitionChromInfo in transition.GetSafeChromInfo(
                             resultFile.Replicate.ReplicateIndex))
                {
                    if (transitionChromInfo.OptimizationStep != resultFile.OptimizationStep ||
                        !ReferenceEquals(transitionChromInfo.FileId, resultFile.ChromFileInfoId))
                    {
                        continue;
                    }

                    if (!transitionChromInfo.IsTruncated.HasValue || transitionChromInfo.Area <= 0)
                    {
                        continue;
                    }

                    totalArea += transitionChromInfo.Area;
                    if (transitionChromInfo.IsTruncated.Value)
                    {
                        truncatedArea += transitionChromInfo.Area;
                    }
                }
            }

            if (totalArea <= 0)
            {
                return null;
            }

            return truncatedArea / totalArea;
        }

        
        public class ImputedPeakBounds : IFormattable
        {
            public ImputedPeakBounds(double startTime, double endTime)
            {
                StartTime = startTime;
                EndTime = endTime;
            }

            [Format(Formats.RETENTION_TIME)]
            public double StartTime { get; }
            [Format(Formats.RETENTION_TIME)]
            public double EndTime { get; }

            public string ToString(string format, IFormatProvider formatProvider)
            {
                return string.Format(EntitiesResources.CandidatePeakGroup_ToString___0___1__,
                    StartTime.ToString(format, formatProvider), EndTime.ToString(format, formatProvider));
            }

            public static ImputedPeakBounds FromPeakBounds(PeakBounds peakBounds)
            {
                if (peakBounds == null)
                {
                    return null;
                }

                return new ImputedPeakBounds(peakBounds.StartTime, peakBounds.EndTime);
            }

            public override string ToString()
            {
                return ToString(null, CultureInfo.CurrentCulture);
            }
        }
    }
}
