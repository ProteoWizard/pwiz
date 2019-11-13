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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class AreaCVGraphData : Immutable
    {
        private readonly AreaCVGraphSettings _graphSettings;
        private readonly AreaCVRefinementData _refinementData;

        public AreaCVGraphData(SrmDocument document, AreaCVGraphSettings graphSettings, CancellationToken? token = null)
        {
            _graphSettings = graphSettings;

            if (document == null || !document.Settings.HasResults)
            {
                IsValid = false;
                return;
            }

            try
            {
                _refinementData = new AreaCVRefinementData(document, graphSettings, token);
            }
            catch (Exception)
            {
                IsValid = false;
                return;
            }

            CalculateStats();

            if(IsValid)
                MedianCV = _refinementData.CalcMedianCV();
        }

        public static readonly AreaCVGraphData INVALID = new AreaCVGraphData(null,
            new AreaCVGraphSettings((GraphTypeSummary) ~0, (AreaCVNormalizationMethod) ~0, -1, string.Empty,
                string.Empty, (PointsTypePeakArea) ~0, double.NaN, double.NaN, -1, double.NaN, (AreaCVMsLevel) ~0,
                (AreaCVTransitions) ~0, -1));

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

                BelowCVCutoff = (double)belowCvCutoffCount / Total;
            }
        }
        
        public IList<CVData> Data
        {
            get { return _refinementData.Data; }
        }

        public bool IsValid { get; private set; }
        public double MinMeanArea { get; private set; } // Smallest mean area
        public double MaxMeanArea { get; private set; } // Largest mean area
        public int Total { get; private set; } // Total number of CV's
        public double MaxCV { get; private set; } // Highest CV
        public double MinCV { get; private set; } // Smallest CV
        public int MaxFrequency { get; private set; } // Highest count of CV's
        public double MedianCV { get; private set; } // Median CV
        public double MeanCV { get; private set; } // Mean CV
        public double BelowCVCutoff { get; private set; } // Fraction/Percentage of CV's below cutoff

        public class AreaCVGraphSettings : AreaCVRefinementSettings
        {
            public AreaCVGraphSettings(GraphTypeSummary graphType, bool convertToDecimal = true)
            {
                var factor = !Settings.Default.AreaCVShowDecimals && convertToDecimal ? 0.01 : 1.0;
                GraphType = graphType;
                MsLevel = AreaGraphController.AreaCVMsLevel;
                Transitions = AreaGraphController.AreaCVTransitions;
                CountTransitions = AreaGraphController.AreaCVTransitionsCount;
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

            public AreaCVGraphSettings(GraphTypeSummary graphType, AreaCVNormalizationMethod normalizationMethod, int ratioIndex, string group, string annotation, PointsTypePeakArea pointsType, double qValueCutoff,
                double cvCutoff, int minimumDetections, double binwidth, AreaCVMsLevel msLevel, AreaCVTransitions transitions, int countTransitions)
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
                MsLevel = msLevel;
                Transitions = transitions;
                CountTransitions = countTransitions;
            }

            public static bool CacheEqual(AreaCVGraphSettings a, AreaCVGraphSettings b)
            {
                return a.GraphType == b.GraphType && a.Group == b.Group &&
                       a.PointsType == b.PointsType && (a.QValueCutoff == b.QValueCutoff || double.IsNaN(a.QValueCutoff) && double.IsNaN(b.QValueCutoff)) &&
                       a.CVCutoff == b.CVCutoff && a.BinWidth == b.BinWidth && a.MsLevel == b.MsLevel && a.Transitions == b.Transitions && a.CountTransitions == b.CountTransitions;
            }

            public override void AddToInternalData(ICollection<InternalData> data, List<AreaInfo> areas,
                PeptideGroupDocNode peptideGroup, PeptideDocNode peptide, TransitionGroupDocNode tranGroup,
                string annotation)
            {
                var normalizedStatistics = new Statistics(areas.Select(a => a.NormalizedArea));
                var normalizedMean = normalizedStatistics.Mean();
                var normalizedStdDev = normalizedStatistics.StdDev();

                var unnormalizedStatistics = new Statistics(areas.Select(a => a.Area));
                var unnormalizedMean = unnormalizedStatistics.Mean();

                // If the mean is 0 or NaN or the standard deviaiton is NaN the cv would also be NaN
                if (normalizedMean == 0.0 || double.IsNaN(normalizedMean) || double.IsNaN(normalizedStdDev))
                    return;

                // Round cvs so that the smallest difference between two cv's is BinWidth
                var cv = normalizedStdDev / normalizedMean;
                var cvBucketed = Math.Floor(cv / BinWidth) * BinWidth;
                var log10Mean = GraphType == GraphTypeSummary.histogram2d
                    ? Math.Floor(Math.Log10(unnormalizedMean) / 0.05) * 0.05
                    : 0.0;
                data.Add(new InternalData()
                {
                    Peptide = peptide,
                    PeptideGroup = peptideGroup,
                    TransitionGroup = tranGroup,
                    Annotation = annotation,
                    CV = cv,
                    CVBucketed = cvBucketed,
                    Area = log10Mean
                });
            }

            public GraphTypeSummary GraphType { get; private set; }
            public double BinWidth { get; private set; }
        }
    }

    #region Functional test support

    public class AreaCVGraphDataStatistics
    {
        public AreaCVGraphDataStatistics(int dataCount, int items, double minMeanArea, double maxMeanArea, int total, double maxCv, double minCv, int maxFrequency, double medianCv, double meanCv, double belowCvCutoff)
        {
            DataCount = dataCount;
            Items = items;
            MinMeanArea = minMeanArea;
            MaxMeanArea = maxMeanArea;
            Total = total;
            MaxCV = maxCv;
            MinCV = minCv;
            MaxFrequency = maxFrequency;
            MedianCV = medianCv;
            MeanCV = meanCv;
            BelowCVCutoff = belowCvCutoff;
        }

        public AreaCVGraphDataStatistics(AreaCVGraphData data, int items)
        {
            DataCount = data.Data.Count;
            Items = items;
            MinMeanArea = data.MinMeanArea;
            MaxMeanArea = data.MaxMeanArea;
            Total = data.Total;
            MaxCV = data.MaxCV;
            MinCV = data.MinCV;
            MaxFrequency = data.MaxFrequency;
            MedianCV = data.MedianCV;
            MeanCV = data.MeanCV;
            BelowCVCutoff = data.BelowCVCutoff;
        }

        protected bool Equals(AreaCVGraphDataStatistics other)
        {
            return DataCount == other.DataCount &&
                   Items == other.Items &&
                   MinMeanArea.Equals(other.MinMeanArea) &&
                   MaxMeanArea.Equals(other.MaxMeanArea) &&
                   Total == other.Total &&
                   MaxCV.Equals(other.MaxCV) &&
                   MinCV.Equals(other.MinCV) &&
                   MaxFrequency == other.MaxFrequency &&
                   MedianCV.Equals(other.MedianCV) &&
                   MeanCV.Equals(other.MeanCV) &&
                   BelowCVCutoff.Equals(other.BelowCVCutoff);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AreaCVGraphDataStatistics)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = DataCount;
                hashCode = (hashCode * 397) ^ Items.GetHashCode();
                hashCode = (hashCode * 397) ^ MinMeanArea.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxMeanArea.GetHashCode();
                hashCode = (hashCode * 397) ^ Total;
                hashCode = (hashCode * 397) ^ MaxCV.GetHashCode();
                hashCode = (hashCode * 397) ^ MinCV.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxFrequency;
                hashCode = (hashCode * 397) ^ MedianCV.GetHashCode();
                hashCode = (hashCode * 397) ^ MeanCV.GetHashCode();
                hashCode = (hashCode * 397) ^ BelowCVCutoff.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format(@"{0}, {1}, {2:R}, {3:R}, {4}, {5:R}, {6:R}, {7}, {8:R}, {9:R}, {10:R}",
                DataCount, Items, MinMeanArea, MaxMeanArea, Total, MaxCV, MinCV, MaxFrequency, MedianCV, MeanCV, BelowCVCutoff);
        }

        public string ToCode()
        {
            return string.Format(@"new {0}({1}),", GetType().Name, ToString());
        }

        private int DataCount { get; set; }
        private int Items { get; set; }
        private double MinMeanArea { get; set; } // Smallest mean area
        private double MaxMeanArea { get; set; } // Largest mean area
        private int Total { get; set; } // Total number of CV's
        private double MaxCV { get; set; } // Highest CV
        private double MinCV { get; set; } // Smallest CV
        private int MaxFrequency { get; set; } // Highest count of CV's
        private double MedianCV { get; set; } // Median CV
        private double MeanCV { get; set; } // Mean CV
        private double BelowCVCutoff { get; set; } // Fraction/Percentage of CV's below cutoff
    }
    
    #endregion
}
