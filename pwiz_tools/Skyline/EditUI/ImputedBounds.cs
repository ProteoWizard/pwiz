/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Imputation;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.EditUI
{
    public class ImputedBounds
    {
        public static readonly Producer<Parameter, ImputedBounds> PRODUCER = new Producer();

        private Dictionary<Tuple<ReplicateFileId, IdentityPath>, PeakBounds> _boundsDictionary;

        public PeakBounds GetImputedBounds(ReplicateFileId replicateFileId, IdentityPath peptideIdentityPath)
        {
            _boundsDictionary.TryGetValue(Tuple.Create(replicateFileId, peptideIdentityPath), out var bounds);
            return bounds;
        }

        private ImputedBounds(Dictionary<Tuple<ReplicateFileId, IdentityPath>, PeakBounds> dictionary)
        {
            _boundsDictionary = dictionary;
        }

        protected bool Equals(ImputedBounds other)
        {
            return CollectionUtil.EqualsDeep(_boundsDictionary, other._boundsDictionary);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ImputedBounds)obj);
        }

        public override int GetHashCode()
        {
            return CollectionUtil.GetHashCodeDeep(_boundsDictionary);
        }

        public static ImputedBounds MakeImputedBounds(SrmDocument document, IEnumerable<MoleculePeaks> moleculePeaksList,
            AlignmentData alignmentData)
        {
            var dictionary = new Dictionary<Tuple<ReplicateFileId, IdentityPath>, PeakBounds>();
            foreach (var moleculePeaks in moleculePeaksList)
            {
                if (moleculePeaks.BestPeak == null)
                {
                    continue;
                }
                foreach (var chromatogramSet in document.MeasuredResults.Chromatograms)
                {
                    foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                    {
                        var replicateFileId = new ReplicateFileId(chromatogramSet.Id, chromFileInfo.FileId);
                        if (!Equals(replicateFileId, moleculePeaks.BestPeak.ReplicateFileInfo.ReplicateFileId))
                        {
                            var alignmentFunction = alignmentData.Alignments?.GetAlignment(replicateFileId) ?? AlignmentFunction.IDENTITY;
                            dictionary.Add(Tuple.Create(replicateFileId, moleculePeaks.PeptideIdentityPath), moleculePeaks.ExemplaryPeakBounds.ReverseAlignPreservingWidth(alignmentFunction).ToPeakBounds());
                        }
                    }
                }

            }
            return new ImputedBounds(dictionary);
        }

        public class Parameter
        {
            public Parameter(SrmDocument document, IEnumerable<IdentityPath> peptideIdentityPaths)
            {
                Document = document;
                PeptideIdentityPaths = ImmutableList.ValueOf(peptideIdentityPaths);
            }

            public SrmDocument Document { get; }
            public ImmutableList<IdentityPath> PeptideIdentityPaths
            {
                get;
            }

            protected bool Equals(Parameter other)
            {
                return ReferenceEquals(Document, other.Document) && Equals(PeptideIdentityPaths, other.PeptideIdentityPaths);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((Parameter)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(Document) * 397) ^ PeptideIdentityPaths.GetHashCode();
                }
            }
        }

        private class Producer : Producer<Parameter, ImputedBounds>
        {
            public override ImputedBounds ProduceResult(ProductionMonitor productionMonitor, Parameter parameter, IDictionary<WorkOrder, object> inputs)
            {
                var peakImputationRows = inputs.Values.OfType<PeakImputationRows>().FirstOrDefault();
                if (peakImputationRows == null)
                {
                    return null;
                }

                return MakeImputedBounds(parameter.Document, peakImputationRows.MoleculePeaks,
                    peakImputationRows.AlignmentData);
            }

            public override IEnumerable<WorkOrder> GetInputs(Parameter parameter)
            {
                if (parameter.PeptideIdentityPaths.Count > 0)
                {
                    var peakImputationParameters = new PeakImputationRows.Parameters(parameter.Document).ChangePeptideIdentityPaths(parameter.PeptideIdentityPaths);
                    yield return PeakImputationRows.PRODUCER.MakeWorkOrder(peakImputationParameters);
                }
            }
        }
    }
}
