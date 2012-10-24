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
        /// Holds information about how to align retention times before displaying them in a graph.
        /// </summary>
        public struct RtAlignment
        {
            public static readonly RtAlignment NONE = new RtAlignment();

            public RtAlignment(ChromatogramSet chromatogramSet, ChromFileInfo chromFileInfo, 
                FileRetentionTimeAlignments fileRetentionTimeAlignments) : this()
            {
                ChromatogramSet = chromatogramSet;
                ChromFileInfo = chromFileInfo;
                FileRetentionTimeAlignments = fileRetentionTimeAlignments;
            }

            public ChromatogramSet ChromatogramSet { get; private set; }
            public ChromFileInfo ChromFileInfo { get; private set; }
            public FileRetentionTimeAlignments FileRetentionTimeAlignments { get; private set; }
            public bool IsValid 
            {
                get { return null == ChromatogramSet || null != FileRetentionTimeAlignments; }
            }

            public bool TryGetRetentionTimeAlignment(ChromInfoData chromInfoData,
                                                     out RetentionTimeAlignment retentionTimeAlignment)
            {
                if (null == FileRetentionTimeAlignments || Equals(chromInfoData.ChromFileInfo, ChromFileInfo))
                {
                    retentionTimeAlignment = null;
                    return true;
                }
                retentionTimeAlignment =
                    FileRetentionTimeAlignments.RetentionTimeAlignments.Find(chromInfoData.ChromFileInfo);
                return null != retentionTimeAlignment;
            }

            /// <summary>
            /// If retention time alignment is being performed, append "aligned to {ReplicateName}" to the title.
            /// </summary>
            [Localizable(true)]
            public string AnnotateTitle(string title)
            {
                if (ChromatogramSet != null)
                {
                    title = string.Format(Resources.RtAlignment_AxisTitleAlignedTo, title, ChromatogramSet.Name);
                }
                return title;
            }
        }

        public class RetentionTimeTransform
        {
            public RetentionTimeTransform(RTPeptideValue rtPeptideValue, RtAlignment rtAlignment, AggregateOp aggregateOp)
            {
                RtPeptideValue = rtPeptideValue;
                RtAlignment = rtAlignment;
                AggregateOp = aggregateOp;
            }

            public RTPeptideValue RtPeptideValue { get; private set; }
            public RtAlignment RtAlignment { get; private set; }
            public AggregateOp AggregateOp { get; private set; }

            [Localizable(true)]
            public string GetAxisTitle()
            {
                string title = ToLocalizedString(RtPeptideValue);
                title = RtAlignment.AnnotateTitle(title);
                title = AggregateOp.AnnotateTitle(title);
                return title;
            }
        }
    }
}
