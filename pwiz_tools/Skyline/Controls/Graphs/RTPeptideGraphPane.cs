/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ZedGraph;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.Graphs
{
// ReSharper disable InconsistentNaming
    public enum RTPeptideValue { All, Retention, FWHM, FWB }
// ReSharper restore InconsistentNaming

    internal class RTPeptideGraphPane : SummaryPeptideGraphPane
    {
        public static RTPeptideValue RTValue
        {
            get
            {
                return Helpers.ParseEnum(Settings.Default.RTPeptideValue, RTPeptideValue.All);
            }
        }

        public RTPeptideGraphPane(GraphSummary graphSummary)
            : base(graphSummary, PaneKey.DEFAULT)
        {
        }

        protected override GraphData CreateGraphData(SrmDocument document, PeptideGroupDocNode selectedProtein, TransitionGroupDocNode selectedGroup, DisplayTypeChrom displayType)
        {
            int? result = null;
            if (RTLinearRegressionGraphPane.ShowReplicate == ReplicateDisplay.single)
                result = GraphSummary.ResultsIndex;

            return new RTGraphData(document, selectedGroup, selectedProtein, result, displayType, GraphSummary.StateProvider.GetRetentionTimeTransformOperation());
        }

        protected override void UpdateAxes()
        {
            string text;
            var transformOperation = GraphSummary.StateProvider.GetRetentionTimeTransformOperation();
            if (null != transformOperation)
            {
                text = transformOperation.GetAxisTitle(RTValue);
            }
            else
            {
                if (RTValue != RTPeptideValue.All)
                    text = TextUtil.SpaceSeparate(RTValue.ToString(), Resources.RTPeptideGraphPane_UpdateAxes_Time);
                else
                    text = Resources.RTPeptideGraphPane_UpdateAxes_Retention_Time;
            }
            YAxis.Title.Text = text;
            UpdateAxes(false);
        }

        internal class RTGraphData : GraphData
        {
            public static PointPair RTPointPairMissing(int xValue)
            {
                return HiLowMiddleErrorBarItem.MakePointPair(xValue,
                    PointPairBase.Missing, PointPairBase.Missing, PointPairBase.Missing, 0);
            }

            public RTGraphData(SrmDocument document,
                               TransitionGroupDocNode selectedGroup,
                               PeptideGroupDocNode selectedProtein,
                               int? result,
                               DisplayTypeChrom displayType,
                               GraphValues.IRetentionTimeTransformOp retentionTimeTransformOp
                )
                : base(document, selectedGroup, selectedProtein, result, displayType, retentionTimeTransformOp, PaneKey.DEFAULT)
            {
            }

            public override double MaxValueSetting { get { return Settings.Default.PeakTimeMax; } }
            public override double MinValueSetting { get { return Settings.Default.PeakTimeMin; } }
            public override double MaxCVSetting { get { return Settings.Default.PeakTimeMaxCv; } }

            protected override PointPair CreatePointPairMissing(int iGroup)
            {
                if (RTValue != RTPeptideValue.All)
                    return base.CreatePointPairMissing(iGroup);
                return RTPointPairMissing(iGroup);
            }

            protected override PointPair CreatePointPair(int iGroup, TransitionGroupDocNode nodeGroup, ref double maxY, ref double minY, int? resultIndex)
            {
                if (RTValue != RTPeptideValue.All)
                    return base.CreatePointPair(iGroup, nodeGroup, ref maxY, ref minY, resultIndex);

                if (!nodeGroup.HasResults)
                    return RTPointPairMissing(iGroup);

                var listTimes = new List<double>();
                var listStarts = new List<double>();
                var listEnds = new List<double>();
                var listFwhms = new List<double>();
                foreach (var chromInfo in nodeGroup.GetChromInfos(resultIndex))
                {
                    if (chromInfo.OptimizationStep == 0)
                    {
                        var retentionTimeValues = ScaleRetentionTimeValues(chromInfo.FileId, RetentionTimeValues.GetValues(chromInfo));
                        if (!retentionTimeValues.HasValue)
                        {
                            continue;
                        }
                        listTimes.Add(retentionTimeValues.Value.RetentionTime);
                        listStarts.Add(retentionTimeValues.Value.StartRetentionTime);
                        listEnds.Add(retentionTimeValues.Value.EndRetentionTime);
                        if (retentionTimeValues.Value.Fwhm.HasValue)
                        {
                            listFwhms.Add(retentionTimeValues.Value.Fwhm.Value);
                        }
                    }
                }

                return CreatePointPair(iGroup, listTimes, listStarts, listEnds, listFwhms, ref maxY, ref minY);
            }

            protected override double? GetValue(TransitionGroupChromInfo chromInfo)
            {
                var retentionTimeValues = RetentionTimeValues.GetValues(chromInfo);
                if (!retentionTimeValues.HasValue)
                {
                    return null;
                }
                retentionTimeValues = ScaleRetentionTimeValues(chromInfo.FileId, retentionTimeValues.Value);
                if (!retentionTimeValues.HasValue)
                {
                    return null;
                }
                switch (RTValue)
                {
                    case RTPeptideValue.Retention:
                        return retentionTimeValues.Value.RetentionTime;
                    case RTPeptideValue.FWHM:
                        return retentionTimeValues.Value.Fwhm;
                    case RTPeptideValue.FWB:
                        return retentionTimeValues.Value.Fwb;
                }
                return null;
            }

            protected override PointPair CreatePointPair(int iGroup, TransitionDocNode nodeTran, ref double maxY, ref double minY, int? resultIndex)
            {
                if (RTValue != RTPeptideValue.All)
                    return base.CreatePointPair(iGroup, nodeTran, ref maxY, ref minY, resultIndex);

                if (!nodeTran.HasResults)
                    return RTPointPairMissing(iGroup);

                var listTimes = new List<double>();
                var listStarts = new List<double>();
                var listEnds = new List<double>();
                var listFwhms = new List<double>();
                foreach (var chromInfo in nodeTran.GetChromInfos(resultIndex))
                {
                    if (chromInfo.OptimizationStep == 0 && !chromInfo.IsEmpty)
                    {
                        var retentionTimeValues = ScaleRetentionTimeValues(chromInfo.FileId, RetentionTimeValues.GetValues(chromInfo));
                        if (retentionTimeValues.HasValue)
                        {
                            listTimes.Add(retentionTimeValues.Value.RetentionTime);
                            listStarts.Add(retentionTimeValues.Value.StartRetentionTime);
                            listEnds.Add(retentionTimeValues.Value.EndRetentionTime);
                            listFwhms.Add(retentionTimeValues.Value.Fwhm ?? 0);
                        }
                    }
                }

                return CreatePointPair(iGroup, listTimes, listStarts, listEnds, listFwhms, ref maxY, ref minY);
            }

            protected override double GetValue(TransitionChromInfo chromInfo)
            {
                var retentionTimeValues = ScaleRetentionTimeValues(chromInfo.FileId, RetentionTimeValues.GetValues(chromInfo));
                if (!retentionTimeValues.HasValue)
                {
                    return 0;
                }
                switch (RTValue)
                {
                    case RTPeptideValue.Retention:
                        return retentionTimeValues.Value.RetentionTime;
                    case RTPeptideValue.FWHM:
                        return retentionTimeValues.Value.Fwhm ?? 0;
                    case RTPeptideValue.FWB:
                        return retentionTimeValues.Value.Fwb;
                }
                return 0;
            }

            private static PointPair CreatePointPair(int iGroup,
                                                     ICollection<double> listTimes,
                                                     IEnumerable<double> listStarts,
                                                     IEnumerable<double> listEnds,
                                                     IEnumerable<double> listFwhms,
                                                     ref double maxY,
                                                     ref double minY)
            {
                if (listTimes.Count == 0)
                    return RTPointPairMissing(iGroup);

                var statTimes = new Statistics(listTimes);
                var statStarts = new Statistics(listStarts);
                var statEnds = new Statistics(listEnds);
                var statFwhms = new Statistics(listFwhms);

                double endY = statEnds.Mean();
                double startY = statStarts.Mean();

                var pointPair = HiLowMiddleErrorBarItem.MakePointPair(iGroup,
                    endY, startY, statTimes.Mean(), statFwhms.Mean());

                maxY = Math.Max(maxY, endY);
                minY = Math.Min(minY, startY);

                return pointPair;
            }
        }
    }
}