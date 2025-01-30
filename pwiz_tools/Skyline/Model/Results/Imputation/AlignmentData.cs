/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Runtime.CompilerServices;
using MathNet.Numerics.Statistics;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class AlignmentData
    {
        public static readonly Producer<Parameters, AlignmentData> PRODUCER = new DataProducer();
        public AlignmentData(Parameters parameters, AlignmentResults alignmentResults, ChromatogramTimeRanges chromatogramTimeRanges)
        {
            Params = parameters;
            Alignments = alignmentResults;
            ChromatogramTimeRanges = chromatogramTimeRanges;
            MeanRtStdDev = GetMeanRtStandardDeviation(parameters.Document, alignmentResults);
        }

        public Parameters Params { get; }
        public AlignmentResults Alignments { get; }
        public ChromatogramTimeRanges ChromatogramTimeRanges { get; }
        public double? MeanRtStdDev { get; }

        public class Parameters : Immutable
        {
            public Parameters(SrmDocument document)
            {
                Document = document;
                var imputationSettings = document.Settings.PeptideSettings.Imputation;
                RtValueType = RtValueType.ForName(imputationSettings.RtCalcName);
                if (RtValueType != null)
                {
                    AlignmentType = AlignmentType.ForName(imputationSettings.RegressionMethodName) ?? AlignmentType.KDE;
                }
            }

            public SrmDocument Document { get; }

            public RtValueType RtValueType { get; private set; }

            public Parameters ChangeRtValueType(RtValueType value)
            {
                return ChangeProp(ImClone(this), im => im.RtValueType = value);
            }

            public AlignmentType AlignmentType { get; private set; }

            public Parameters ChangeAlignmentType(AlignmentType value)
            {
                return ChangeProp(ImClone(this), im => im.AlignmentType = value);
            }

            public AlignmentParameters GetAlignmentParameters()
            {
                if (RtValueType == null || AlignmentType == null)
                {
                    return null;
                }

                return new AlignmentParameters(Document, RtValueType, AlignmentType);
            }

            protected bool Equals(Parameters other)
            {
                return ReferenceEquals(Document, other.Document) &&
                       Equals(RtValueType, other.RtValueType) && 
                       Equals(AlignmentType, other.AlignmentType);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Parameters)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Document != null ? RuntimeHelpers.GetHashCode(Document) : 0;
                    hashCode = (hashCode * 397) ^ (AlignmentType != null ? AlignmentType.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }
        public static bool IsManualIntegrated(PeptideDocNode peptideDocNode, int replicateIndex,
            ChromFileInfoId fileId)
        {
            return EnumerateTransitionGroupChromInfos(peptideDocNode, replicateIndex, fileId)
                .Any(transitionGroupChromInfo => transitionGroupChromInfo.UserSet == UserSet.TRUE);
        }

        private static IEnumerable<TransitionGroupChromInfo> EnumerateTransitionGroupChromInfos(
            PeptideDocNode peptideDocNode, int replicateIndex,
            ChromFileInfoId fileId)
        {
            return peptideDocNode.TransitionGroups.SelectMany(tg => tg.GetSafeChromInfo(replicateIndex))
                .Where(tgci => ReferenceEquals(tgci.FileId, fileId));
        }
        private class DataProducer : Producer<Parameters, AlignmentData>
        {
            public override AlignmentData ProduceResult(ProductionMonitor productionMonitor, Parameters parameter, IDictionary<WorkOrder, object> inputs)
            {
                var consensusAlignment =
                    AlignmentParameters.ALIGNMENT_PRODUCER.GetResult(inputs, parameter.GetAlignmentParameters());
                var chromatogramTimeRanges = ChromatogramTimeRanges.PRODUCER.GetResult(inputs,
                    new ChromatogramTimeRanges.Parameter(parameter.Document.MeasuredResults, true));
                return new AlignmentData(parameter, consensusAlignment, chromatogramTimeRanges);
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
            {
                SrmDocument document = parameter.Document;
                if (document.MeasuredResults == null)
                {
                    yield break;
                }

                yield return ChromatogramTimeRanges.PRODUCER.MakeWorkOrder(
                    new ChromatogramTimeRanges.Parameter(document.MeasuredResults, true));
                if (parameter.GetAlignmentParameters() != null)
                {
                    yield return parameter.GetAlignmentParameters().MakeWorkOrder();
                }
            }
        }

        public static FormattablePeakBounds GetRawPeakBounds(PeptideDocNode peptideDocNode, int replicateIndex,
            ChromFileInfoId chromFileInfoId)
        {
            var peptideChromInfo = peptideDocNode.GetSafeChromInfo(replicateIndex)
                .FirstOrDefault(chromInfo => ReferenceEquals(chromInfo.FileId, chromFileInfoId));
            if (peptideChromInfo?.RetentionTime == null)
            {
                return null;
            }

            double apexTime = peptideChromInfo.RetentionTime.Value;
            double startTime = apexTime;
            double endTime = apexTime;
            foreach (var transitionGroup in peptideDocNode.TransitionGroups)
            {
                foreach (var chromInfo in transitionGroup.GetSafeChromInfo(replicateIndex))
                {
                    if (!ReferenceEquals(chromFileInfoId, chromInfo.FileId))
                    {
                        continue;
                    }

                    if (chromInfo.StartRetentionTime.HasValue)
                    {
                        startTime = Math.Min(startTime, chromInfo.StartRetentionTime.Value);
                    }

                    if (chromInfo.EndRetentionTime.HasValue)
                    {
                        endTime = Math.Max(endTime, chromInfo.EndRetentionTime.Value);
                    }
                }
            }

            return new FormattablePeakBounds(startTime, endTime);
        }


        public static double? GetMeanRtStandardDeviation(SrmDocument document, AlignmentResults alignmentResults)
        {
            var standardDeviations = new List<double>();
            foreach (var molecule in document.Molecules)
            {
                if (!molecule.HasResults)
                {
                    continue;
                }

                var times = new List<double>();
                for (int i = 0; i < molecule.Results.Count; i++)
                {
                    foreach (var fileGroup in molecule.GetSafeChromInfo(i)
                                 .GroupBy(peptideChromInfo => ReferenceValue.Of(peptideChromInfo.FileId)))
                    {
                        AlignmentFunction alignmentFunction = AlignmentFunction.IDENTITY;
                        if (alignmentResults != null)
                        {
                            alignmentFunction = alignmentResults.GetAlignment(
                                new ReplicateFileId(document.MeasuredResults.Chromatograms[i].Id,
                                    fileGroup.Key));
                            if (alignmentFunction == null)
                            {
                                continue;
                            }
                        }
                        var fileTimes = fileGroup.Select(peptideChromInfo => 
                                (double?)peptideChromInfo.RetentionTime)
                            .OfType<double>().Select(alignmentFunction.GetY).ToList();
                        if (fileTimes.Count > 0)
                        {
                            times.Add(fileTimes.Average());
                        }
                    }
                }

                if (times.Count > 1)
                {
                    var standardDeviation = times.StandardDeviation();
                    if (!double.IsNaN(standardDeviation))
                    {
                        standardDeviations.Add(standardDeviation);
                    }
                }
            }

            if (standardDeviations.Count == 0)
            {
                return null;
            }

            return standardDeviations.Average();
        }

        public static double? GetAveragePeakWidthCV(SrmDocument document)
        {
            var cvs= new List<double>();
            foreach (var molecule in document.Molecules)
            {
                if (!molecule.HasResults)
                {
                    continue;
                }

                var widths = new List<double>();
                for (int i = 0; i < molecule.Results.Count; i++)
                {
                    foreach (var group in molecule.TransitionGroups.SelectMany(tg => tg.GetSafeChromInfo(i))
                                 .GroupBy(chromInfo => ReferenceValue.Of(chromInfo.FileId)))
                    {
                        var fileWidths = group
                            .Select(chromInfo => (double?)chromInfo.EndRetentionTime - chromInfo.StartRetentionTime)
                            .OfType<double>().ToList();
                        if (fileWidths.Count > 0)
                        {
                            widths.Add(fileWidths.Average());
                        }
                    }
                }

                if (widths.Count > 1)
                {
                    var cv = widths.Variance() / widths.Mean();

                    if (!double.IsNaN(cv))
                    {
                        cvs.Add(cv);
                    }
                }
            }

            if (cvs.Count == 0)
            {
                return null;
            }

            return cvs.Average();
        }
    }
}
