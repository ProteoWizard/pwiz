/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public class AreaCVGraphData : Immutable
    {
        private readonly AreaCVGraphSettings _graphSettings;

        public AreaCVGraphData(SrmDocument document, AreaCVGraphSettings graphSettings)
        {
            _graphSettings = graphSettings;

            if (document == null || !document.Settings.HasResults)
            {
                IsValid = false;
                return;
            } 

            var annotations = AnnotationHelper.GetPossibleAnnotations(document.Settings, graphSettings.Group, AnnotationDef.AnnotationTarget.replicate);
            if (!annotations.Any() && AreaGraphController.GroupByGroup == null)
                annotations = new string[] { null };

            var data = new List<InternalData>();
            double? qvalueCutoff = null;
            if (AreaGraphController.ShouldUseQValues(document))
                qvalueCutoff = _graphSettings.QValueCutoff;
            var hasHeavyMods = document.Settings.PeptideSettings.Modifications.HasHeavyModifications;
            var hasGlobalStandards = document.Settings.HasGlobalStandardArea;

            var replicates = document.MeasuredResults.Chromatograms.Count;
            var areas = new List<AreaInfo>(replicates);

            MedianInfo medianInfo = null;

            if (graphSettings.NormalizationMethod == AreaCVNormalizationMethod.medians)
                medianInfo = CalculateMedianAreas(document);

            foreach (var transitionGroupDocNode in document.MoleculeTransitionGroups)
            {
                if (graphSettings.PointsType == PointsTypePeakArea.decoys != transitionGroupDocNode.IsDecoy)
                    continue;

                foreach (var a in annotations)
                {
                    areas.Clear();

                    if (a != _graphSettings.Annotation && (_graphSettings.Group == null || _graphSettings.Annotation != null))
                        continue;

                    foreach (var i in AnnotationHelper.GetReplicateIndices(document.Settings, _graphSettings.Group, a))
                    {
                        var peakArea = transitionGroupDocNode.GetPeakArea(i, qvalueCutoff);
                        if (!peakArea.HasValue)
                            continue;

                        double area = peakArea.Value;
                        var normalizedArea = area;
                        if (graphSettings.NormalizationMethod == AreaCVNormalizationMethod.medians)
                            normalizedArea /= medianInfo.Medians[i] / medianInfo.MedianMedian;
                        else if (graphSettings.NormalizationMethod == AreaCVNormalizationMethod.global_standards && hasGlobalStandards)
                            normalizedArea = NormalizeToGlobalStandard(document, transitionGroupDocNode, i, area);
                        else if (graphSettings.NormalizationMethod == AreaCVNormalizationMethod.ratio && hasHeavyMods && graphSettings.RatioIndex >= 0)
                        {
                            var ci = transitionGroupDocNode.Results[i].FirstOrDefault(c => c.OptimizationStep == 0);
                            if (ci != null)
                            {
                                var ratioValue = ci.GetRatio(_graphSettings.RatioIndex);
                                if (ratioValue != null)
                                    normalizedArea /= ratioValue.Ratio;
                            }
                        }

                        areas.Add(new AreaInfo(area, normalizedArea));
                    }

                    if (qvalueCutoff.HasValue && areas.Count < graphSettings.MinimumDetections)
                        continue;

                    AddToInternalData(data, areas);
                }
            }

            Data = ImmutableList<CVData>.ValueOf(data.GroupBy(i => i)
                .Select(g => new CVData(g.Key.CVBucketed, g.Key.Area, g.Count()))
                .OrderBy(d => d.CV));

            CalculateStats();

            if(IsValid)
                MedianCV = new Statistics(data.Select(d => d.CV)).Median();
        }

        private void AddToInternalData(ICollection<InternalData> data, List<AreaInfo> areas)
        {
            var normalizedStatistics = new Statistics(areas.Select(t => t.NormalizedArea));
            var normalizedMean = normalizedStatistics.Mean();
            var normalizedStdDev = normalizedStatistics.StdDev();

            var unnormalizedStatistics = new Statistics(areas.Select(t => t.Area));
            var unnomarlizedMean = unnormalizedStatistics.Mean();

            // If the mean is 0 or NaN or the standard deviaiton is NaN the cv would also be NaN
            if (normalizedMean == 0.0 || double.IsNaN(normalizedMean) || double.IsNaN(normalizedStdDev))
                return;

            // Round cvs so that the smallest difference between two cv's is BinWidth
            var cv = normalizedStdDev / normalizedMean;
            var cvBucketed = Math.Floor(cv / _graphSettings.BinWidth) * _graphSettings.BinWidth;
            var log10Mean = _graphSettings.GraphType == GraphTypePeakArea.histogram2d ? Math.Floor(Math.Log10(unnomarlizedMean) / 0.05) * 0.05 : 0.0;
            data.Add(new InternalData { Area = log10Mean, CV = cv , CVBucketed = cvBucketed});
        }

        private static double NormalizeToGlobalStandard(SrmDocument document, TransitionGroupDocNode transitionGroupDocNode, int replicateIndex, double area)
        {
            var chromSet = document.MeasuredResults.Chromatograms[replicateIndex];
            var groupChromSet = transitionGroupDocNode.Results[replicateIndex];
            var fileInfoFirst = chromSet.GetFileInfo(groupChromSet.First(c => c.OptimizationStep == 0).FileId);
            var globalStandard = document.Settings.CalcGlobalStandardArea(replicateIndex, fileInfoFirst);
            if (globalStandard != 0.0)
                area /= globalStandard;
            return area;
        }

        private void CalculateStats()
        {
            MedianCV = 0.0;
            MinMeanArea = double.MaxValue;
            MaxMeanArea = 0.0;
            MaxFrequency = 0;
            MaxCV = 0.0;
            Total = 0;

            IsValid = Data.Any();
            if (IsValid)
            {
                var belowCvCutoffCount = 0;

                // Calculate max frequency, total number of cv's and number of cv's below the cutoff
                MinCV = Data.First().CV;
                MaxCV = Data.Last().CV;

                foreach (var d in Data)
                {
                    MinMeanArea = Math.Min(MinMeanArea, d.MeanArea);
                    MaxMeanArea = Math.Max(MaxMeanArea, d.MeanArea);
                    MaxFrequency = Math.Max(MaxFrequency, d.Frequency);

                    Total += d.Frequency;
                    if (d.CV < _graphSettings.CVCutoff)
                        belowCvCutoffCount += d.Frequency;
                }

                MeanCV = Data.Sum(d => d.CV * d.Frequency) / Total;

                belowCVCutoff = (double)belowCvCutoffCount / Total;
            }
        }

        private class AreaInfo
        {
            public AreaInfo(double area, double normalizedArea)
            {
                Area = area;
                NormalizedArea = normalizedArea;
            }

            public double Area { get; private set; }
            public double NormalizedArea { get; private set; }
        }

        private class MedianInfo
        {
            public MedianInfo(List<double> medians, double medianMedian)
            {
                Medians = medians;
                MedianMedian = medianMedian;
            }

            public IList<double> Medians { get; private set; }
            public double MedianMedian { get; private set; }
        }
        
        private MedianInfo CalculateMedianAreas(SrmDocument document)
        {
            double? qvalueCutoff = null;
            if (AreaGraphController.ShouldUseQValues(document))
                qvalueCutoff = _graphSettings.QValueCutoff;
            var replicates = document.MeasuredResults.Chromatograms.Count;
            var allAreas = new List<List<double?>>(document.MoleculeTransitionGroupCount);

            foreach (var transitionGroupDocNode in document.MoleculeTransitionGroups)
            {
                if ((_graphSettings.PointsType == PointsTypePeakArea.decoys) != transitionGroupDocNode.IsDecoy)
                    continue;

                var detections = 0;
                var areas = new List<double?>(replicates);

                for (var i = 0; i < replicates; ++i)
                {
                    double? area = transitionGroupDocNode.GetPeakArea(i, qvalueCutoff);
                    if (area.HasValue)
                        ++detections;
                    areas.Add(area);
                }

                if (qvalueCutoff.HasValue && detections < _graphSettings.MinimumDetections)
                    continue;

                allAreas.Add(areas);
            }

            var medians = new List<double>(replicates);
            for (var i = 0; i < replicates; ++i)
                medians.Add(new Statistics(GetReplicateAreas(allAreas, i)).Median());

            return new MedianInfo(medians, new Statistics(medians).Median());
        }

        private IEnumerable<double> GetReplicateAreas(List<List<double?>> allAreas, int replicateIndex)
        {
            foreach (var areas in allAreas)
            {
                var area = areas[replicateIndex];
                if (area.HasValue)
                    yield return area.Value;
            }
        }

        public IList<CVData> Data { get; private set; }

        public bool IsValid { get; private set; }
        public double MinMeanArea { get; private set; } // Smallest mean area
        public double MaxMeanArea { get; private set; } // Largest mean area
        public int Total { get; private set; } // Total number of CV's
        public double MaxCV { get; private set; } // Highest CV
        public double MinCV { get; private set; } // Smallest CV
        public int MaxFrequency { get; private set; } // Highest count of CV's
        public double MedianCV { get; private set; } // Median CV
        public double MeanCV { get; private set; } // Mean CV
        public double belowCVCutoff { get; private set; } // Fraction/Percentage of CV's below cutoff


        public class CVData : Immutable
        {
            public CVData(double cv, double meanArea, int frequency)
            {
                CV = cv;
                MeanArea = meanArea;
                Frequency = frequency;
            }

            public double CV { get; private set; }
            public double MeanArea { get; private set; }
            public int Frequency { get; private set; }
        }

        private class InternalData
        {
            public double CV;
            public double CVBucketed;
            public double Area;

            #region object overrides

            protected bool Equals(InternalData other)
            {
                return CVBucketed.Equals(other.CVBucketed) && Area.Equals(other.Area);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((InternalData) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (CVBucketed.GetHashCode() * 397) ^ Area.GetHashCode();
                }
            }

            #endregion
        }

        public class AreaCVGraphDataCache
        {
            private readonly AreaCVGraphSettings _settings;
            private readonly List<AreaCVGraphData> _cachedData;
            private Action<CancellationToken> _action;
            private CancellationTokenSource _cts;
            private IAsyncResult _result;

            public AreaCVGraphDataCache(AreaCVGraphSettings settings)
            {
                _settings = settings;
                _cachedData = new List<AreaCVGraphData>();
            }

            public bool IsCaching { get; private set; }

            public bool Add(AreaCVGraphData data)
            {
                if (!IsValidFor(data._graphSettings))
                    return false;

                lock (_cachedData)
                {
                    var old = data.IsValid
                        ? Get(data._graphSettings.Group,
                              data._graphSettings.Annotation,
                              data._graphSettings.MinimumDetections,
                              data._graphSettings.NormalizationMethod,
                              data._graphSettings.RatioIndex)
                        : null;
                    if (old != null)
                        old.Data = data.Data;
                    else
                        _cachedData.Add(data);
                }

                return true;
            }

            public AreaCVGraphData Get(string group, string annotation, int minimumDetections, AreaCVNormalizationMethod normalizationMethod, int ratioIndex)
            {
                lock (_cachedData)
                {
                    // Linear search, but very short list
                    return _cachedData.FirstOrDefault(d => d._graphSettings.Group == group &&
                                                            d._graphSettings.Annotation == annotation &&
                                                            d._graphSettings.MinimumDetections == minimumDetections &&
                                                            d._graphSettings.NormalizationMethod == normalizationMethod &&
                                                            d._graphSettings.RatioIndex == ratioIndex);
                }
            }

            public bool IsValidFor(AreaCVGraphSettings settings)
            {
                return AreaCVGraphSettings.CacheEqual(settings, _settings);
            }

            public void CacheRemaining(SrmDocument document)
            {
                if (IsCaching)
                    return;

                _action = token =>
                {
                    var factor = AreaGraphController.GetAreaCVFactorToDecimal();

                    ParallelEx.ForEach(GetPropertyVariants(document), properties =>
                    {
                        if (token.IsCancellationRequested)
                            return;

                        CreateIfNotExists(document, properties, factor);
                    });

                    IsCaching = false;
                };

                _cts = new CancellationTokenSource();
                IsCaching = true;
                _result = _action.BeginInvoke(_cts.Token, null, null);
            }

            private static int GetMinDetectionsForAnnotation(SrmDocument document, string annotationValue)
            {
                return document.Settings.PeptideSettings.Integration.PeakScoringModel.IsTrained && !double.IsNaN(Settings.Default.AreaCVQValueCutoff)
                    ? AnnotationHelper.GetReplicateIndices(document.Settings, AreaGraphController.GroupByGroup, annotationValue).Length
                    : 2;
            }

            private void CreateIfNotExists(SrmDocument document, GraphDataProperties properties, double factor)
            {
                if (Get(AreaGraphController.GroupByGroup,
                    properties.Annotation,
                    properties.Detections,
                    properties.NormalizationMethod,
                    properties.RatioIndex) == null)
                {
                    Add(new AreaCVGraphData(document,
                        new AreaCVGraphSettings(AreaGraphController.GraphType,
                            properties.NormalizationMethod,
                            properties.RatioIndex,
                            AreaGraphController.GroupByGroup,
                            properties.Annotation,
                            AreaGraphController.PointsType,
                            Settings.Default.AreaCVQValueCutoff,
                            Settings.Default.AreaCVCVCutoff / factor,
                            properties.Detections,
                            Settings.Default.AreaCVHistogramBinWidth / factor)));
                }
            }

            private IEnumerable<GraphDataProperties> GetPropertyVariants(SrmDocument document)
            {
                var annotationsArray = AnnotationHelper.GetPossibleAnnotations(document.Settings,
                    AreaGraphController.GroupByGroup, AnnotationDef.AnnotationTarget.replicate);

                // Add an entry for All
                var annotations = annotationsArray.Concat(new string[] { null }).ToList();

                var normalizationMethods = new List<AreaCVNormalizationMethod> { AreaCVNormalizationMethod.none, AreaCVNormalizationMethod.medians, AreaCVNormalizationMethod.ratio };
                if (document.Settings.HasGlobalStandardArea)
                    normalizationMethods.Add(AreaCVNormalizationMethod.global_standards);

                // First cache for current normalization method
                if (normalizationMethods.Remove(AreaGraphController.NormalizationMethod))
                    normalizationMethods.Insert(0, AreaGraphController.NormalizationMethod);

                // First cache the histograms for the current annotation
                if (annotations.Remove(AreaGraphController.GroupByAnnotation))
                    annotations.Insert(0, AreaGraphController.GroupByAnnotation);

                foreach (var n in normalizationMethods)
                {
                    bool isRatio = n == AreaCVNormalizationMethod.ratio;
                    // There can be RatioInternalStandardTypes even though HasHeavyModifications is false
                    if (isRatio && !document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
                        continue;

                    var ratioIndices = isRatio
                        ? Enumerable.Range(0, document.Settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count).ToList()
                        : new List<int> { -1 };

                    if (AreaGraphController.AreaCVRatioIndex != -1)
                    {
                        if (ratioIndices.Remove(AreaGraphController.AreaCVRatioIndex))
                            ratioIndices.Insert(0, AreaGraphController.AreaCVRatioIndex);
                    }

                    foreach (var r in ratioIndices)
                    {
                        foreach (var a in annotations)
                        {
                            var minDetections = GetMinDetectionsForAnnotation(document, a);

                            for (var i = 2; i <= minDetections; ++i)
                            {
                                yield return new GraphDataProperties(n, r, a, i);
                            }
                        }
                    }
                }
            }

            private struct GraphDataProperties
            {
                public GraphDataProperties(AreaCVNormalizationMethod normalizationMethod, int ratioIndex, string annotation, int detections) : this()
                {
                    NormalizationMethod = normalizationMethod;
                    RatioIndex = ratioIndex;
                    Annotation = annotation;
                    Detections = detections;
                }

                public AreaCVNormalizationMethod NormalizationMethod { get; private set; }
                public int RatioIndex { get; private set; }
                public string Annotation { get; private set; }
                public int Detections { get; private set; }
            }

            public void Cancel(bool waitOnSeparateThread = false)
            {
                if (IsCaching && !_cts.IsCancellationRequested)
                {
                    Action cancelAction = () =>
                    {
                        _cts.Cancel();
                        _action.EndInvoke(_result);
                        Clear();
                        IsCaching = false;
                    };

                    if (waitOnSeparateThread)
                        cancelAction.BeginInvoke(cancelAction.EndInvoke, null);
                    else
                        cancelAction.Invoke();
                }
                else
                {
                    Clear();
                }
            }

            public void Clear()
            {
                lock (_cachedData)
                {
                    _cachedData.Clear();
                }
            }

            #region Functional test support
            public int DataCount { get { return _cachedData.Count; } }
            #endregion
        }

        public class AreaCVGraphSettings : Immutable
        {
            public AreaCVGraphSettings(bool convertToDecimal = true)
            {
                var factor = !Settings.Default.AreaCVShowDecimals && convertToDecimal ? 0.01 : 1.0;
                GraphType = AreaGraphController.GraphType;
                NormalizationMethod = AreaGraphController.NormalizationMethod;
                RatioIndex = AreaGraphController.AreaCVRatioIndex;
                Group = AreaGraphController.GroupByGroup != null ? string.Copy(AreaGraphController.GroupByGroup) : null;
                Annotation = AreaGraphController.GroupByAnnotation != null ? string.Copy(AreaGraphController.GroupByAnnotation) : null;
                PointsType = AreaGraphController.PointsType;
                QValueCutoff = Settings.Default.AreaCVQValueCutoff;
                CVCutoff = Settings.Default.AreaCVCVCutoff * factor;
                MinimumDetections = AreaGraphController.MinimumDetections;
                BinWidth = Settings.Default.AreaCVHistogramBinWidth * factor;
            }

            public AreaCVGraphSettings(GraphTypePeakArea graphType, AreaCVNormalizationMethod normalizationMethod, int ratioIndex, string group, string annotation, PointsTypePeakArea pointsType, double qValueCutoff,
                double cvCutoff, int minimumDetections, double binwidth)
            {
                GraphType = graphType;
                NormalizationMethod = normalizationMethod;
                RatioIndex = ratioIndex;
                Group = group;
                Annotation = annotation;
                PointsType = pointsType;
                QValueCutoff = qValueCutoff;
                CVCutoff = cvCutoff;
                MinimumDetections = minimumDetections;
                BinWidth = binwidth;
            }

            public static bool CacheEqual(AreaCVGraphSettings a, AreaCVGraphSettings b)
            {
                return a.GraphType == b.GraphType && a.Group == b.Group &&
                        a.PointsType == b.PointsType && (a.QValueCutoff == b.QValueCutoff || double.IsNaN(a.QValueCutoff) && double.IsNaN(b.QValueCutoff)) &&
                        a.CVCutoff == b.CVCutoff && a.BinWidth == b.BinWidth;
            }

            public GraphTypePeakArea GraphType { get; private set; }
            public AreaCVNormalizationMethod NormalizationMethod { get; private set; }
            public int RatioIndex { get; private set; }
            public string Group { get; private set; }
            public string Annotation { get; private set; }
            public PointsTypePeakArea PointsType { get; private set; }
            public double QValueCutoff { get; private set; }
            public double CVCutoff { get; private set; }
            public int MinimumDetections { get; private set; }
            public double BinWidth { get; private set; }
        }
    }
}