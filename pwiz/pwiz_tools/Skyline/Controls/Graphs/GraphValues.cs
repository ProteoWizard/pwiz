/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ZedGraph;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Graphs
{
    /// <summary>
    /// Classes and methods for transforming values that are displayed in graphs based on the 
    /// current settings.
    /// </summary>
    public static class GraphValues
    {
        /// <summary>
        /// Prepend the string "Log " to an axis title.
        /// </summary>
        [Localizable(true)]
        public static string AnnotateLogAxisTitle(string title)
        {
            return string.Format(Resources.GraphValues_Log_AxisTitle, title);
        }
        
        [Localizable(true)]
        public static string ToLocalizedString(RTPeptideValue rtPeptideValue)
        {
            switch (rtPeptideValue)
            {
                case RTPeptideValue.All:
                case RTPeptideValue.Retention:
                    return Resources.RtGraphValue_Retention_Time;
                case RTPeptideValue.FWB:
                    return Resources.RtGraphValue_FWB_Time;
                case RTPeptideValue.FWHM:
                    return Resources.RtGraphValue_FWHM_Time;
            }
            throw new ArgumentException(rtPeptideValue.ToString());
        }

        /// <summary>
        /// Operations for combining values from multiple replicates that are displayed 
        /// at a single point on a graph.
        /// </summary>
        public class AggregateOp
        {
            public static readonly AggregateOp MEAN = new AggregateOp(false, false);
            public static readonly AggregateOp CV = new AggregateOp(true, false);
            public static readonly AggregateOp CV_DECIMAL = new AggregateOp(true, true);

            public static AggregateOp FromCurrentSettings()
            {
                if (!Settings.Default.ShowPeptideCV)
                {
                    return MEAN;
                }
                return Settings.Default.PeakDecimalCv ? CV_DECIMAL : CV;
            }

            private AggregateOp(bool cv, bool cvDecimal)
            {
                Cv = cv;
                CvDecimal = cvDecimal;
            }

            public bool Cv { get; private set; }
            public bool CvDecimal { get; private set; }

            public PointPair MakeBarValue(double xValue, IEnumerable<double> values)
            {
                var statValues = new Statistics(values);
                if (Cv)
                {
                    var cv = statValues.StdDev()/statValues.Mean();
                    return MeanErrorBarItem.MakePointPair(xValue, CvDecimal ? cv : cv*100, 0);
                }
                return MeanErrorBarItem.MakePointPair(xValue, statValues.Mean(), statValues.StdDev());
            }

            [Localizable(true)]
            public string AnnotateTitle(string title)
            {
                if (Cv)
                {
                    if (CvDecimal)
                    {
                        title = string.Format(Resources.AggregateOp_AxisTitleCv, title);
                    }
                    else
                    {
                        title = string.Format(Resources.AggregateOp_AxisTitleCvPercent, title);
                    }
                }
                return title;
            }
        }

        /// <summary>
        /// Ways of combining replicates together on a graph into a single point.
        /// Replicates may be combined based on the value of an annotation on the replicate.
        /// </summary>
        public class ReplicateGroupOp
        {
            private ReplicateGroupOp(AnnotationDef groupByAnnotationDef, AggregateOp aggregateOp)
            {
                GroupByAnnotation = groupByAnnotationDef;
                AggregateOp = aggregateOp;
            }

            public AnnotationDef GroupByAnnotation { get; private set; }
            public AggregateOp AggregateOp { get; private set; }
            [Localizable(true)]
            public string ReplicateAxisTitle
            {
                get
                {
                    return GroupByAnnotation == null ? Resources.ReplicateGroupOp_ReplicateAxisTitle : GroupByAnnotation.Name;   
                }
            }

            /// <summary>
            /// Returns the ReplicateGroupOp based on the current value of Settings.Default.GroupByReplicateAnnotation,
            /// and the current Settings.Default.ShowPeptideCV.  Note that if the ReplicateGroupOp is not grouping on
            /// an annotation, the AggregateOp will always be set to MEAN.
            /// </summary>
            public static ReplicateGroupOp FromCurrentSettings(SrmSettings settings)
            {
                return FromCurrentSettings(settings, AggregateOp.FromCurrentSettings());
            }

            /// <summary>
            /// Returns the ReplicateGroupOp based on the current value of Settings.Default.GroupByReplicateAnnotation,
            /// and the specified AggregateOp.  Note that if the ReplicateGroupOp is not grouping on an annotation,
            /// the AggregateOp will be override with the value MEAN.
            /// </summary>
            public static ReplicateGroupOp FromCurrentSettings(SrmSettings settings, AggregateOp aggregateOp)
            {
                AnnotationDef groupByAnnotationDef = null;
                string annotationName = Settings.Default.GroupByReplicateAnnotation;
                if (null != annotationName)
                {
                    groupByAnnotationDef =
                        settings.DataSettings.AnnotationDefs.FirstOrDefault(
                            annotationDef => annotationName == annotationDef.Name);
                }
                if (null == groupByAnnotationDef)
                {
                    aggregateOp = AggregateOp.MEAN;
                }
                return new ReplicateGroupOp(groupByAnnotationDef, aggregateOp);
            }
        }

        /// <summary>
        /// A scaling of a retention time
        /// </summary>
        public interface IRetentionTimeTransformOp
        {
            /// <summary>
            /// Returns the localized text to display on a graph's axis for the transformed value
            /// </summary>
            string GetAxisTitle(RTPeptideValue rtPeptideValue);
            /// <summary>
            /// Try to get the regression function to use for the specified file.
            /// If the file is not supposed to be transformed (for instance, if this
            /// transform is a retention time alignment to that particular file), then
            /// regressionFunction will be set to null, and this method will return true.
            /// If the regressionFunction cannot be found for some other reason, then this method
            /// returns false.
            /// If successful, then this method returns true, and the regressionFunction is set 
            /// appropriately.
            /// </summary>
            bool TryGetRegressionFunction(ChromFileInfoId chromFileInfoId, out IRegressionFunction regressionFunction);
        }

        public class RegressionUnconversion : IRetentionTimeTransformOp
        {
            private readonly RetentionTimeRegression _retentionTimeRegression;
            public RegressionUnconversion(RetentionTimeRegression retentionTimeRegression)
            {
                _retentionTimeRegression = retentionTimeRegression;
            }

            public string GetAxisTitle(RTPeptideValue rtPeptideValue)
            {
                string calculatorName = _retentionTimeRegression.Calculator.Name;
                if (rtPeptideValue == RTPeptideValue.Retention || rtPeptideValue == RTPeptideValue.All)
                {
                    return string.Format(Resources.RegressionUnconversion_CalculatorScoreFormat, calculatorName);
                }
                return string.Format(Resources.RegressionUnconversion_CalculatorScoreValueFormat, calculatorName, ToLocalizedString(rtPeptideValue));
            }

            public bool TryGetRegressionFunction(ChromFileInfoId chromFileInfoId, out IRegressionFunction regressionFunction)
            {
                regressionFunction = _retentionTimeRegression.GetUnconversion(chromFileInfoId);
                return regressionFunction != null;
            }
        }
        
        /// <summary>
        /// Holds information about how to align retention times before displaying them in a graph.
        /// </summary>
        public class AlignToFileOp : IRetentionTimeTransformOp
        {
            public static AlignToFileOp GetAlignmentToFile(ChromFileInfoId chromFileInfoId, SrmSettings settings)
            {
                if (!settings.HasResults)
                {
                    return null;
                }
                var chromSetInfos = GetChromSetInfos(settings.MeasuredResults);
                Tuple<ChromatogramSet, ChromFileInfo> chromSetInfo;
                if (!chromSetInfos.TryGetValue(chromFileInfoId, out chromSetInfo))
                {
                    return null;
                }
                var fileRetentionTimeAlignments = settings.DocumentRetentionTimes.FileAlignments.Find(chromSetInfo.Item2);
                if (null == fileRetentionTimeAlignments)
                {
                    return null;
                }
                return new AlignToFileOp(chromSetInfo.Item1, chromSetInfo.Item2, fileRetentionTimeAlignments, chromSetInfos);
            }

            private static IDictionary<ChromFileInfoId, Tuple<ChromatogramSet, ChromFileInfo>> GetChromSetInfos(
                MeasuredResults measuredResults)
            {
                var dict =
                    new Dictionary<ChromFileInfoId, Tuple<ChromatogramSet, ChromFileInfo>>(
                        new ChromFileIdEqualityComparer());
                foreach (var chromatogramSet in measuredResults.Chromatograms)
                {
                    foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                    {
                        dict.Add(chromFileInfo.FileId, new Tuple<ChromatogramSet, ChromFileInfo>(chromatogramSet, chromFileInfo));
                    }
                }
                return dict;
            }

            private readonly IDictionary<ChromFileInfoId, Tuple<ChromatogramSet, ChromFileInfo>> _chromSetInfos;
            private AlignToFileOp(ChromatogramSet chromatogramSet, ChromFileInfo chromFileInfo, 
                FileRetentionTimeAlignments fileRetentionTimeAlignments, IDictionary<ChromFileInfoId, Tuple<ChromatogramSet, ChromFileInfo>> chromSetInfos)
            {
                ChromatogramSet = chromatogramSet;
                ChromFileInfo = chromFileInfo;
                FileRetentionTimeAlignments = fileRetentionTimeAlignments;
                _chromSetInfos = chromSetInfos;
            }

            public ChromatogramSet ChromatogramSet { get; private set; }
            public ChromFileInfo ChromFileInfo { get; private set; }
            public FileRetentionTimeAlignments FileRetentionTimeAlignments { get; private set; }
            public bool TryGetRegressionFunction(ChromFileInfoId chromFileInfoId, out IRegressionFunction regressionFunction)
            {
                if (ReferenceEquals(chromFileInfoId, ChromFileInfo.Id))
                {
                    regressionFunction = null;
                    return true;
                }
                Tuple<ChromatogramSet, ChromFileInfo> chromSetInfo;
                if (_chromSetInfos.TryGetValue(chromFileInfoId, out chromSetInfo))
                {
                    var retentionTimeAlignment =
                        FileRetentionTimeAlignments.RetentionTimeAlignments.Find(chromSetInfo.Item2);
                    if (null != retentionTimeAlignment)
                    {
                        regressionFunction = retentionTimeAlignment.RegressionLine;
                        return true;
                    }
                }
                regressionFunction = null;
                return false;
            }

            /// <summary>
            /// If retention time alignment is being performed, append "aligned to {ReplicateName}" to the title.
            /// </summary>
            [Localizable(true)]
            public string GetAxisTitle(RTPeptideValue rtPeptideValue)
            {
                return string.Format(Resources.RtAlignment_AxisTitleAlignedTo, ToLocalizedString(rtPeptideValue), ChromatogramSet.Name);
            }

            private class ChromFileIdEqualityComparer : IEqualityComparer<ChromFileInfoId>
            {
                public bool Equals(ChromFileInfoId x, ChromFileInfoId y)
                {
                    return ReferenceEquals(x, y);
                }

                public int GetHashCode(ChromFileInfoId obj)
                {
                    return RuntimeHelpers.GetHashCode(obj);
                }
            }
        }

        public class RetentionTimeTransform
        {
            public RetentionTimeTransform(RTPeptideValue rtPeptideValue, IRetentionTimeTransformOp rtAlignment, AggregateOp aggregateOp)
            {
                RtPeptideValue = rtPeptideValue;
                RtTransformOp = rtAlignment;
                AggregateOp = aggregateOp;
            }

            public RTPeptideValue RtPeptideValue { get; private set; }
            public IRetentionTimeTransformOp RtTransformOp { get; private set; }
            public AggregateOp AggregateOp { get; private set; }

            [Localizable(true)]
            public string GetAxisTitle()
            {
                string title;
                if (null != RtTransformOp)
                {
                    title = RtTransformOp.GetAxisTitle(RtPeptideValue);
                }
                else
                {
                    title = ToLocalizedString(RtPeptideValue);
                }
                title = AggregateOp.AnnotateTitle(title);
                return title;
            }
        }
    }
}
