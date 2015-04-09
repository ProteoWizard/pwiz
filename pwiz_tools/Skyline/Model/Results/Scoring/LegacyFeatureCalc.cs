/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class LegacyLogUnforcedAreaCalc : SummaryPeakFeatureCalculator
    {
        public LegacyLogUnforcedAreaCalc() : base("Log co-eluting area") { }  // Not L10N

        public override string Name
        {
            get { return Resources.LegacyLogUnforcedAreaCalc_LegacyLogUnforcedAreaCalc_Legacy_log_unforced_area; }
        }

        /// <summary>
        /// Standard peaks are assigned ^1.2 the value of analyte peaks, since standards
        /// are intended to be spiked in at a constant concentration, while analyte peaks
        /// are expected to vary, and may even be missing altogether.
        /// </summary>
        public const double STANDARD_MULTIPLIER = 1.2;

        public static float Score(double area, double areaStandard)
        {
            area += Math.Pow(areaStandard, STANDARD_MULTIPLIER);
            return (float) Math.Max(0, Math.Log10(area));
        }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            return Score(SummedArea(summaryPeakData, false), SummedArea(summaryPeakData, true));
        }

        public override bool IsReversedScore { get { return false; } }

        private double SummedArea(IPeptidePeakData<ISummaryPeakData> summaryPeakData, bool isStandard)
        {
            // Slightly verbose implementation, because simpler implementation using
            // Linq showed up in a profiler.
            double sumArea = 0;
            foreach (var groupPd in summaryPeakData.TransitionGroupPeakData)
            {
                if (groupPd.IsStandard != isStandard)
                    continue;
                foreach (var pd in groupPd.TransitionPeakData)
                {
                    if (!pd.PeakData.IsForcedIntegration)
                        sumArea += pd.PeakData.Area;
                }
            }
            return sumArea;
        }
    }

    public abstract class LegacyCountScoreCalc : SummaryPeakFeatureCalculator
    {
        protected LegacyCountScoreCalc(string headerName) : base(headerName) {}

        protected abstract IList<ITransitionGroupPeakData<ISummaryPeakData>> GetIncludedGroups(IPeptidePeakData<ISummaryPeakData> summaryPeakData);

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            float max = float.MinValue;
            foreach (var peakData in GetIncludedGroups(summaryPeakData))
            {
                max = Math.Max(max, (float) CalcCountScore(peakData));
            }
            return max != float.MinValue ? max : float.NaN;
        }

        private double CalcCountScore(ITransitionGroupPeakData<ISummaryPeakData> transitionGroupPeakData)
        {
            int count = 0, unforced = 0;
            foreach (var peakData in GetIonTypes(transitionGroupPeakData))
            {
                count++;
                if (!peakData.PeakData.IsForcedIntegration)
                    unforced++;
            }
            return GetPeakCountScore(unforced, count);
        }

        public static double GetPeakCountScore(double peakCount, double totalCount)
        {
            return totalCount > 4
                       ? 4.0 * peakCount / totalCount
                       : peakCount;
        }

        protected abstract IList<ITransitionPeakData<TData>> GetIonTypes<TData>(ITransitionGroupPeakData<TData> tranGroupPeakDatas);

        public override bool IsReversedScore { get { return false; } }
    }

    public class LegacyUnforcedCountScoreCalc : LegacyCountScoreCalc
    {
        public LegacyUnforcedCountScoreCalc() : base("Co-elution count") { }  // Not L10N

        public override string Name
        {
            get { return Resources.LegacyUnforcedCountScoreCalc_LegacyUnforcedCountScoreCalc_Legacy_unforced_count; }
        }

        protected override IList<ITransitionGroupPeakData<ISummaryPeakData>> GetIncludedGroups(IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            return MQuestHelpers.GetAnalyteGroups(summaryPeakData);
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(ITransitionGroupPeakData<TData> tranGroupPeakData)
        {
            return MQuestHelpers.GetDefaultIonTypes(new []{tranGroupPeakData});
        }
    }

    public class LegacyUnforcedCountScoreStandardCalc : LegacyCountScoreCalc
    {
        public LegacyUnforcedCountScoreStandardCalc() : base("Reference co-elution count") { }  // Not L10N

        public override string Name
        {
            get { return Resources.LegacyUnforcedCountScoreStandardCalc_LegacyUnforcedCountScoreStandardCalc_Legacy_unforced_count_standard; }
        }

        protected override IList<ITransitionGroupPeakData<ISummaryPeakData>> GetIncludedGroups(IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            return MQuestHelpers.GetStandardGroups(summaryPeakData);
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(ITransitionGroupPeakData<TData> tranPeakDatas)
        {
            return MQuestHelpers.GetDefaultIonTypes(new []{tranPeakDatas});
        }
    }

    public class LegacyUnforcedCountScoreDefaultCalc : LegacyCountScoreCalc
    {
        public LegacyUnforcedCountScoreDefaultCalc() : base("Default co-elution count") { }  // Not L10N

        public override string Name
        {
            get { return Resources.LegacyUnforcedCountScoreDefaultCalc_Name_Default_co_elution_count; }
        }

        protected override IList<ITransitionGroupPeakData<ISummaryPeakData>> GetIncludedGroups(IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            return MQuestHelpers.GetBestAvailableGroups(summaryPeakData);
        }

        protected override IList<ITransitionPeakData<TData>> GetIonTypes<TData>(ITransitionGroupPeakData<TData> tranPeakDatas)
        {
            return MQuestHelpers.GetDefaultIonTypes(new []{tranPeakDatas});
        }
    }

    public class LegacyIdentifiedCountCalc : SummaryPeakFeatureCalculator
    {
        public LegacyIdentifiedCountCalc() : base("Identified count") { }  // Not L10N

        public override string Name
        {
            get { return Resources.LegacyIdentifiedCountCalc_LegacyIdentifiedCountCalc_Legacy_identified_count; }
        }

        protected override float Calculate(PeakScoringContext context, IPeptidePeakData<ISummaryPeakData> summaryPeakData)
        {
            foreach (var groupPeakData in summaryPeakData.TransitionGroupPeakData)
            {
                foreach (var peakData in groupPeakData.TransitionPeakData)
                {
                    if (peakData.PeakData.Identified != PeakIdentification.FALSE)
                        return 1;
                }                
            }
            return 0;
        }

        public override bool IsReversedScore { get { return false; } }
    }
}
