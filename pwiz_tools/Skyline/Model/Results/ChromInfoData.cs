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
        protected ChromInfoData(MeasuredResults measuredResults, int replicateIndex, ChromFileInfo chromFileInfo, ChromInfo chromInfo, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode)
        {
            MeasuredResults = measuredResults;
            ReplicateIndex = replicateIndex;
            ChromFileInfo = chromFileInfo;
            ChromInfo = chromInfo;
            PeptideDocNode = peptideDocNode;
            TransitionGroupDocNode = transitionGroupDocNode;
        }

        public MeasuredResults MeasuredResults { get; private set; }
        public int ReplicateIndex { get; private set; }
        public ChromFileInfo ChromFileInfo { get; private set; }
        public ChromInfo ChromInfo { get; private set; }
        public ChromatogramSet ChromatogramSet { get { return MeasuredResults.Chromatograms[ReplicateIndex]; } }
        public abstract int OptimizationStep { get; }
        public abstract RetentionTimeValues GetRetentionTimes();

        public PeptideDocNode PeptideDocNode { get; private set; }
        public TransitionGroupDocNode TransitionGroupDocNode { get; private set; }
    }

    public class TransitionChromInfoData : ChromInfoData
    {
        private TransitionChromInfoData(MeasuredResults measuredResults, int replicateIndex, ChromFileInfo chromFileInfo, TransitionChromInfo transitionChromInfo, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode, TransitionDocNode transitionDocNode)
            : base(measuredResults, replicateIndex, chromFileInfo, transitionChromInfo, peptideDocNode, transitionGroupDocNode)
        {
            TransitionDocNode = transitionDocNode;
        }

        public override int OptimizationStep
        {
            get { return ChromInfo != null ? ChromInfo.OptimizationStep : 0; }
        }

        public new TransitionChromInfo ChromInfo { get { return (TransitionChromInfo) base.ChromInfo; } }

        public static IList<ICollection<TransitionChromInfoData>> GetTransitionChromInfoDatas(MeasuredResults measuredResults, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode, TransitionDocNode transitionDocNode)
        {
            var transitionResults = transitionDocNode.Results;
            var list = new List<ICollection<TransitionChromInfoData>>();
            if (null == transitionResults)
            {
                return list;
            }
           Assume.IsTrue(transitionResults.Count == measuredResults.Chromatograms.Count,
                string.Format(@"Unexpected mismatch between transition results {0} and chromatogram sets {1}", transitionResults.Count, measuredResults.Chromatograms.Count)); // CONSIDER: localize?
            for (int replicateIndex = 0; replicateIndex < transitionResults.Count; replicateIndex++)
            {
                var datas = new List<TransitionChromInfoData>();
                var chromatograms = measuredResults.Chromatograms[replicateIndex];
                var transitionChromInfos = transitionResults[replicateIndex];
                if (transitionChromInfos.IsEmpty)
                    datas.Add(new TransitionChromInfoData(measuredResults, replicateIndex, null, null, peptideDocNode, transitionGroupDocNode, transitionDocNode));
                else
                {
                    foreach (var transitionChromInfo in transitionChromInfos)
                    {
                        var chromFileInfo = chromatograms.GetFileInfo(transitionChromInfo.FileId);
                        datas.Add(new TransitionChromInfoData(measuredResults, replicateIndex, chromFileInfo, transitionChromInfo, peptideDocNode, transitionGroupDocNode, transitionDocNode));
                    }
                }
                list.Add(datas);
            }
            return list;
        }

        public override RetentionTimeValues GetRetentionTimes()
        {
            return RetentionTimeValues.FromTransitionChromInfo(ChromInfo);
        }

        public TransitionDocNode TransitionDocNode { get; private set; }
    }

    public class TransitionGroupChromInfoData : ChromInfoData
    {
        private TransitionGroupChromInfoData(MeasuredResults measuredResults, int replicateIndex, ChromFileInfo chromFileInfo, TransitionGroupChromInfo transitionGroupChromInfo, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode) 
            : base(measuredResults, replicateIndex, chromFileInfo, transitionGroupChromInfo, peptideDocNode, transitionGroupDocNode)
        {
        }

        public new TransitionGroupChromInfo ChromInfo { get { return (TransitionGroupChromInfo) base.ChromInfo; } }

        public override int OptimizationStep
        {
            get { return ChromInfo != null ? ChromInfo.OptimizationStep : 0; }
        }

        public static IList<ICollection<TransitionGroupChromInfoData>> GetTransitionGroupChromInfoDatas(MeasuredResults measuredResults, PeptideDocNode peptideDocNode, TransitionGroupDocNode transitionGroupDocNode)
        {
            var list = new List<ICollection<TransitionGroupChromInfoData>>();
            var transitionGroupResults = transitionGroupDocNode.Results;
            if (null == transitionGroupResults)
            {
                return list;
            }
            Assume.IsTrue(transitionGroupResults.Count == measuredResults.Chromatograms.Count,
                string.Format(@"Unexpected mismatch between precursor results {0} and chromatogram sets {1}", transitionGroupResults.Count, measuredResults.Chromatograms.Count)); // CONSIDER: localize? Will users see this?
            for (int replicateIndex = 0; replicateIndex < transitionGroupResults.Count; replicateIndex++)
            {
                var datas = new List<TransitionGroupChromInfoData>();
                var chromatograms = measuredResults.Chromatograms[replicateIndex];
                var transitionGroupChromInfos = transitionGroupResults[replicateIndex];
                if (transitionGroupChromInfos.IsEmpty)
                    datas.Add(new TransitionGroupChromInfoData(measuredResults, replicateIndex, null, null, peptideDocNode, transitionGroupDocNode));
                else
                {
                    foreach (var transitionGroupChromInfo in transitionGroupChromInfos)
                    {
                        var chromFileInfo = chromatograms.GetFileInfo(transitionGroupChromInfo.FileId);
                        datas.Add(new TransitionGroupChromInfoData(measuredResults, replicateIndex, chromFileInfo, transitionGroupChromInfo, peptideDocNode, transitionGroupDocNode));
                    }
                }
                list.Add(datas);
            }
            return list;
        }

        public override RetentionTimeValues GetRetentionTimes()
        {
            return RetentionTimeValues.FromTransitionGroupChromInfo(ChromInfo);
        }
    }

    public class PeptideChromInfoData : ChromInfoData
    {
        private PeptideChromInfoData(MeasuredResults measuredResults, int replicateIndex, ChromFileInfo chromFileInfo, PeptideChromInfo peptideChromInfo, PeptideDocNode peptideDocNode)
            : base(measuredResults, replicateIndex, chromFileInfo, peptideChromInfo, peptideDocNode, null)
        {
        }

        public override int OptimizationStep
        {
            get { return 0; }
        }

        public override RetentionTimeValues GetRetentionTimes()
        {
            return null;
        }

        public static IList<ICollection<PeptideChromInfoData>> GetPeptideChromInfoDatas(MeasuredResults measuredResults, PeptideDocNode peptideDocNode)
        {
            var list = new List<ICollection<PeptideChromInfoData>>();
            var peptideResults = peptideDocNode.Results;
            if (null == peptideResults)
            {
                return list;
            }
            Assume.IsTrue(peptideResults.Count == measuredResults.Chromatograms.Count,
                string.Format(@"Unexpected mismatch between precursor results {0} and chromatogram sets {1}", peptideResults.Count, measuredResults.Chromatograms.Count)); // CONSIDER: localize? Will users see this?
            for (int replicateIndex = 0; replicateIndex < peptideResults.Count; replicateIndex++)
            {
                var datas = new List<PeptideChromInfoData>();
                var chromatograms = measuredResults.Chromatograms[replicateIndex];
                var peptideChromInfos = peptideResults[replicateIndex];
                if (peptideChromInfos.IsEmpty)
                    datas.Add(new PeptideChromInfoData(measuredResults, replicateIndex, null, null, peptideDocNode));
                else
                {
                    foreach (var peptideChromInfo in peptideChromInfos)
                    {
                        var chromFileInfo = chromatograms.GetFileInfo(peptideChromInfo.FileId);
                        datas.Add(new PeptideChromInfoData(measuredResults, replicateIndex, chromFileInfo, peptideChromInfo, peptideDocNode));
                    }
                }
                list.Add(datas);
            }
            return list;
        }

    }
}
