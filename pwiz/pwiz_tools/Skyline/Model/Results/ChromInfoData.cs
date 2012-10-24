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
    }

    public class TransitionChromInfoData : ChromInfoData
    {
        private TransitionChromInfoData(MeasuredResults measuredResults, int replicateIndex, ChromFileInfo chromFileInfo, TransitionChromInfo transitionChromInfo)
            : base(measuredResults, replicateIndex, chromFileInfo, transitionChromInfo)
        {
        }

        public override int OptimizationStep
        {
            get { return ChromInfo.OptimizationStep; }
        }

        public new TransitionChromInfo ChromInfo { get { return (TransitionChromInfo) base.ChromInfo; } }

        public static IList<ICollection<TransitionChromInfoData>> GetTransitionChromInfoDatas(MeasuredResults measuredResults, Results<TransitionChromInfo> transitionResults)
        {
            var list = new List<ICollection<TransitionChromInfoData>>();
            if (null == transitionResults)
            {
                return list;
            }
            for (int replicateIndex = 0; replicateIndex < transitionResults.Count; replicateIndex++)
            {
                var chromFileInfo = measuredResults.GetChromFileInfo(transitionResults, replicateIndex);
                if (null == chromFileInfo)
                {
                    continue;
                }
                var datas = new List<TransitionChromInfoData>();
                foreach (var transitionChromInfo in transitionResults[replicateIndex])
                {
                    datas.Add(new TransitionChromInfoData(measuredResults, replicateIndex, chromFileInfo, transitionChromInfo));
                }
                list.Add(datas);
            }
            return list;
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
            get { return ChromInfo.OptimizationStep; }
        }

        public static IList<ICollection<TransitionGroupChromInfoData>> GetTransitionGroupChromInfoDatas(MeasuredResults measuredResults, Results<TransitionGroupChromInfo> transitionGroupResults)
        {
            var list = new List<ICollection<TransitionGroupChromInfoData>>();
            if (null == transitionGroupResults)
            {
                return list;
            }
            for (int replicateIndex = 0; replicateIndex < transitionGroupResults.Count; replicateIndex++)
            {
                var chromFileInfo = measuredResults.GetChromFileInfo(transitionGroupResults, replicateIndex);
                if (null == chromFileInfo)
                {
                    continue;
                }
                var datas = new List<TransitionGroupChromInfoData>();
                foreach (var transitionGroupChromInfo in transitionGroupResults[replicateIndex])
                {
                    datas.Add(new TransitionGroupChromInfoData(measuredResults, replicateIndex, chromFileInfo, transitionGroupChromInfo));
                }
                list.Add(datas);
            }
            return list;
        }
    } 
}
