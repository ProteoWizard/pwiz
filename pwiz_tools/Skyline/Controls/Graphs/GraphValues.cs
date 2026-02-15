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

using System.Collections.Generic;
using System.ComponentModel;
using pwiz.CommonMsData;
using ZedGraph;
using pwiz.Skyline.Model.DocSettings;
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
            return string.Format(GraphsResources.GraphValues_Log_AxisTitle, title);
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
                if (statValues.Length == 0)
                    return MeanErrorBarItem.MakePointPair(xValue, PointPairBase.Missing, PointPairBase.Missing);
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
                        title = string.Format(GraphsResources.AggregateOp_AxisTitleCv, title);
                    }
                    else
                    {
                        title = string.Format(GraphsResources.AggregateOp_AxisTitleCvPercent, title);
                    }
                }
                return title;
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
            bool TryGetRegressionFunction(MsDataFileUri filePath, out AlignmentFunction regressionFunction);

        }

        public class RetentionTimeAlignmentTransformOp : IRetentionTimeTransformOp
        {
            public RetentionTimeAlignmentTransformOp(SrmSettings settings)
            {
                SrmSettings = settings;
                settings.TryGetAlignmentTarget(out var target);
                AlignmentTarget = target;
            }

            public RetentionTimeAlignmentTransformOp(SrmSettings settings, AlignmentTarget alignmentTarget)
            {
                SrmSettings = settings;
                AlignmentTarget = alignmentTarget;
            }

            public static IRetentionTimeTransformOp FromSettings(SrmSettings settings)
            {
                AlignmentTarget.TryGetAlignmentTarget(settings, out var target);
                return target == null ? null : new RetentionTimeAlignmentTransformOp(settings, target);
            }

            public SrmSettings SrmSettings { get; }

            public AlignmentTarget AlignmentTarget { get; }

            public string GetAxisTitle(RTPeptideValue rtPeptideValue)
            {
                if (AlignmentTarget == null)
                {
                    return rtPeptideValue.ToLocalizedString();
                }

                return AlignmentTarget.GetAxisTitle(rtPeptideValue);
            }

            public bool TryGetRegressionFunction(MsDataFileUri filePath, out AlignmentFunction regressionFunction)
            {
                regressionFunction = SrmSettings.DocumentRetentionTimes.GetRunToRunAlignmentFunction(SrmSettings.PeptideSettings.Libraries, filePath,
                    false);
                return regressionFunction != null;
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
                    title = RtPeptideValue.ToLocalizedString();
                }
                title = AggregateOp.AnnotateTitle(title);
                return title;
            }
        }
    }
}
