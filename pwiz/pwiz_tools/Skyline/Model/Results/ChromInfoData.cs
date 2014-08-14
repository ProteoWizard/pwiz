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
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Wrapper around a ChromInfo which knows which file it came from.
    /// </summary>
    public abstract class ChromInfoData
    {
        protected ChromInfoData(MeasuredResults measuredResults, int replicateIndex, ChromFileInfo chromFileInfo, ChromInfo chromInfo)
        {
            MeasuredResults = measuredResults;
            ReplicateIndex = replicateIndex;
            ChromFileInfo = chromFileInfo;
            ChromInfo = chromInfo;
        }

        public MeasuredResults MeasuredResults { get; private set; }
        public int ReplicateIndex { get; private set; }
        public ChromFileInfo ChromFileInfo { get; private set; }
        public ChromInfo ChromInfo { get; private set; }
        public ChromatogramSet ChromatogramSet { get { return MeasuredResults.Chromatograms[ReplicateIndex]; } }
        public abstract int OptimizationStep { get; }
        public abstract RetentionTimeValues? GetRetentionTimes();
    }

    public class TransitionChromInfoData : ChromInfoData
    {
        private TransitionChromInfoData(MeasuredResults measuredResults, int replicateIndex, ChromFileInfo chromFileInfo, TransitionChromInfo transitionChromInfo)
            : base(measuredResults, replicateIndex, chromFileInfo, transitionChromInfo)
        {
        }

        public override int OptimizationStep
        {
            get { return ChromInfo != null ? ChromInfo.OptimizationStep : 0; }
        }

        public new TransitionChromInfo ChromInfo { get { return (TransitionChromInfo) base.ChromInfo; } }

        public static IList<ICollection<TransitionChromInfoData>> GetTransitionChromInfoDatas(MeasuredResults measuredResults, Results<TransitionChromInfo> transitionResults)
        {
            var list = new List<ICollection<TransitionChromInfoData>>();
            if (null == transitionResults)
            {
                return list;
            }
           Assume.IsTrue(transitionResults.Count == measuredResults.Chromatograms.Count,
                string.Format("Unexpected mismatch between transition results {0} and chromatogram sets {1}", transitionResults.Count, measuredResults.Chromatograms.Count)); // Not L10N?
            for (int replicateIndex = 0; replicateIndex < transitionResults.Count; replicateIndex++)
            {
                var datas = new List<TransitionChromInfoData>();
                var chromatograms = measuredResults.Chromatograms[replicateIndex];
                var transitionChromInfos = transitionResults[replicateIndex];
                if (transitionChromInfos == null)
                    datas.Add(new TransitionChromInfoData(measuredResults, replicateIndex, null, null));
                else
                {
                    foreach (var transitionChromInfo in transitionChromInfos)
                    {
                        var chromFileInfo = chromatograms.GetFileInfo(transitionChromInfo.FileId);
                        datas.Add(new TransitionChromInfoData(measuredResults, replicateIndex, chromFileInfo, transitionChromInfo));
                    }
                }
                list.Add(datas);
            }
            return list;
        }

        public override RetentionTimeValues? GetRetentionTimes()
        {
            return RetentionTimeValues.GetValues(ChromInfo);
        }
    }

    public class TransitionGroupChromInfoData : ChromInfoData
    {
        private TransitionGroupChromInfoData(MeasuredResults measuredResults, int replicateIndex, ChromFileInfo chromFileInfo, TransitionGroupChromInfo transitionGroupChromInfo) 
            : base(measuredResults, replicateIndex, chromFileInfo, transitionGroupChromInfo)
        {
        }

        public new TransitionGroupChromInfo ChromInfo { get { return (TransitionGroupChromInfo) base.ChromInfo; } }

        public override int OptimizationStep
        {
            get { return ChromInfo != null ? ChromInfo.OptimizationStep : 0; }
        }

        public static IList<ICollection<TransitionGroupChromInfoData>> GetTransitionGroupChromInfoDatas(MeasuredResults measuredResults, Results<TransitionGroupChromInfo> transitionGroupResults)
        {
            var list = new List<ICollection<TransitionGroupChromInfoData>>();
            if (null == transitionGroupResults)
            {
                return list;
            }
            Assume.IsTrue(transitionGroupResults.Count == measuredResults.Chromatograms.Count,
                string.Format("Unexpected mismatch between precursor results {0} and chromatogram sets {1}", transitionGroupResults.Count, measuredResults.Chromatograms.Count)); // Not L10N? Will users see this?
            for (int replicateIndex = 0; replicateIndex < transitionGroupResults.Count; replicateIndex++)
            {
                var datas = new List<TransitionGroupChromInfoData>();
                var chromatograms = measuredResults.Chromatograms[replicateIndex];
                var transitionGroupChromInfos = transitionGroupResults[replicateIndex];
                if (transitionGroupChromInfos == null)
                    datas.Add(new TransitionGroupChromInfoData(measuredResults, replicateIndex, null, null));
                else
                {
                    foreach (var transitionGroupChromInfo in transitionGroupChromInfos)
                    {
                        var chromFileInfo = chromatograms.GetFileInfo(transitionGroupChromInfo.FileId);
                        datas.Add(new TransitionGroupChromInfoData(measuredResults, replicateIndex, chromFileInfo, transitionGroupChromInfo));
                    }
                }
                list.Add(datas);
            }
            return list;
        }

        public override RetentionTimeValues? GetRetentionTimes()
        {
            return RetentionTimeValues.GetValues(ChromInfo);
        }
    } 
}
