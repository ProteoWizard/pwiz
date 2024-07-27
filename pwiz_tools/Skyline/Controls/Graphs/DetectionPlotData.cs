/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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

using pwiz.Skyline.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Util;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentlySynchronizedField

namespace pwiz.Skyline.Controls.Graphs
{
    public class DetectionPlotData
    {
        private Dictionary<DetectionsGraphController.TargetType, DataSet> _data = new Dictionary<DetectionsGraphController.TargetType, DataSet>();

        public SrmDocument Document { get; private set; }
        public float QValueCutoff { get; private set; }
        public bool IsValid { get; private set; }
        public int ReplicateCount { get; private set; }

        public bool TryGetTargetData(DetectionsGraphController.TargetType target, out DataSet dataSet) // For test support
        {
            return _data.TryGetValue(target, out dataSet);
        }

        public DataSet GetTargetData(DetectionsGraphController.TargetType target)
        {
            return _data[target];

        }

        public List<string> ReplicateNames { get; private set; }

        public static DetectionPlotData INVALID = new DetectionPlotData(null, 0.001f);
        public const int REPORTING_STEP = 3;


        public DetectionPlotData(SrmDocument document, float qValueCutoff)
        {
            Document = document;
            QValueCutoff = qValueCutoff;
        }
        public string Init(CancellationToken cancellationToken = default(CancellationToken), 
                [CanBeNull] Action<int> progressReport = null)
        {
            if (Document == null || !Document.Settings.HasResults)
                return GraphsResources.DetectionPlotData_NoDataLoaded_Label;

            if (QValueCutoff == 0 || QValueCutoff == 1)
                return GraphsResources.DetectionPlotData_InvalidQValue_Label;


            if (Document.MoleculeTransitionGroupCount == 0 || Document.PeptideCount == 0 ||
                Document.MeasuredResults.Chromatograms.Count == 0)
                return GraphsResources.DetectionPlotData_NoResults_Label;

            var precursorData = new List<QData>(Document.MoleculeTransitionGroupCount);
            var peptideData = new List<QData>(Document.PeptideCount);
            ReplicateCount = Document.MeasuredResults.Chromatograms.Count;


            ReplicateNames = (from chromatogram in Document.MeasuredResults.Chromatograms
                select chromatogram.Name).ToList();

            var thisPeptideData = new List<List<float>>();
            var peptideCount = 0;
            var currentProgress = 0;
            var reportingStep = Document.PeptideCount / (90/REPORTING_STEP);

            foreach (var peptide in Document.Peptides)
            {
                thisPeptideData.Clear();
                //iterate over peptide's precursors
                foreach (var precursor in peptide.TransitionGroups)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return GraphsResources.DetectionPlotPane_EmptyPlotCanceled_Label;

                    if (precursor.IsDecoy) continue;
                    var qs = new List<float>(ReplicateCount);
                    //get q-values for precursor replicates
                    foreach (var i in Enumerable.Range(0, ReplicateCount))
                    {
                        var chromInfo = precursor.GetSafeChromInfo(i).FirstOrDefault(c => c.OptimizationStep == 0);
                        if (chromInfo != null && chromInfo.QValue.HasValue)
                            qs.Add(chromInfo.QValue.Value);
                        else
                            qs.Add(float.NaN);
                    }

                    precursorData.Add(new QData(precursor.Id, qs));
                    thisPeptideData.Add(qs);
                }

                if (thisPeptideData.Count > 0)
                {
                    peptideData.Add(new QData(peptide.Id, 
                        Enumerable.Range(0, ReplicateCount).Select(
                            i =>
                            {
                                var min = new Statistics(thisPeptideData.Select(lst => (double) lst[i])).Min();
                                return (float) min;
                            }).ToList()
                    ));
                }

                if(peptideCount++ == reportingStep * currentProgress)
                    progressReport?.Invoke(REPORTING_STEP * currentProgress++);
            }

            if(precursorData.All(p => p.IsEmpty))
                return GraphsResources.DetectionPlotData_NoQValuesInDocument_Label;

            _data[DetectionsGraphController.TargetType.PRECURSOR] = new DataSet(precursorData, ReplicateCount, QValueCutoff);
            _data[DetectionsGraphController.TargetType.PEPTIDE] = new DataSet(peptideData, ReplicateCount, QValueCutoff);

            IsValid = true;
            return string.Empty;
        }

        private bool IsValidFor(SrmDocument document, float qValue)
        {
            return document != null && Document != null && IsValid &&
                   ReferenceEquals(document, Document) &&
                   Equals(qValue, QValueCutoff);
        }

        public class DataSet
        {
            public ImmutableList<int> TargetsCount { get; private set; }
            public ImmutableList<int> TargetsCumulative { get; private set; }
            public ImmutableList<int> TargetsAll { get; private set; }
            public ImmutableList<float> QMedians { get; private set; }
            public ImmutableList<int> Histogram { get; private set; }


            public double MaxCount
            {
                get { return new Statistics(TargetsCumulative.Select(i => (double)i)).Max(); }
            }

            public DataSet(List<QData> data, int replicateCount, float qValueCutoff,
                CancellationToken cancellationToken = default(CancellationToken), [CanBeNull] Action<int> progressReport = null)
            {
                TargetsCount = ImmutableList<int>.ValueOf(Enumerable.Range(0, replicateCount)
                    .Select(i => data.Count(t => t.QValues[i] < qValueCutoff)));
                CancelOrReport(92, cancellationToken, progressReport);

                QMedians = ImmutableList<float>.ValueOf(Enumerable.Range(0, replicateCount)
                    .Select(i =>
                    {
                        var qStats = new Statistics(
                            data.FindAll(t => t.QValues[i] < qValueCutoff)
                                .Select(t => (double)t.QValues[i]));
                        return (float)qStats.Median();
                    }));
                CancelOrReport(94, cancellationToken, progressReport);

                TargetsCumulative = ImmutableList<int>.ValueOf(Enumerable.Range(0, replicateCount)
                    .Select(i => data.Count(t => t.MinQValues[i] < qValueCutoff)));
                CancelOrReport(96, cancellationToken, progressReport);
                TargetsAll = ImmutableList<int>.ValueOf(Enumerable.Range(0, replicateCount)
                    .Select(i => data.Count(t => t.MaxQValues[i] < qValueCutoff)));
                CancelOrReport(98, cancellationToken, progressReport);

                var histogramPairs = data.Select(t => t.QValues.Count(f => f < qValueCutoff)) //Count replicates for each target
                    .GroupBy(f => f, c => 1,
                        (f, c) => new
                            {replicateNum = f, histCount = c.Sum()}).ToLookup((tuple)=> tuple.replicateNum); //Group targets by the number of replicates

                Histogram = ImmutableList<int>.ValueOf(Enumerable.Range(1, replicateCount + 1)
                    .Select(n => histogramPairs.Contains(n) ? histogramPairs[n].First().histCount : 0));
                CancelOrReport(100, cancellationToken, progressReport);
            }

            private static void CancelOrReport(int percent,
                CancellationToken cancellationToken = default(CancellationToken), [CanBeNull] Action<int> progressReport = null)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException();
                
                progressReport?.Invoke(percent);
            }
            /// <summary>
            /// Returns count of targets detected in at least minRep replicates
            /// </summary>
            public int getCountForMinReplicates(int minRep)
            {
                if (minRep > Histogram.Count) return 0;
                return Enumerable.Range(Math.Max(minRep-1, 0), Histogram.Count - Math.Max(minRep - 1, 0)).Select(i => Histogram[i]).Sum();
            }
        }

        /// <summary>
        /// List of q-values across replicates for a single target (peptide or precursor)
        /// It is also equipped with lists of running mins and maxes for this target.
        /// </summary>
        public class QData
        {
            public QData(Identity target, IReadOnlyList<float> qValues)
            {
                Target = target;
                QValues = ImmutableList.ValueOf(qValues);

                // Calculate running mins and maxes while taking NaNs into account
                var mins = Enumerable.Repeat(float.NaN, qValues.Count).ToList();
                var maxes = Enumerable.Repeat(float.NaN, qValues.Count).ToList();
                IsEmpty = qValues.All(float.IsNaN);
                if (!IsEmpty)
                {
                    var runningNaN = true;
                    for (var i = 0; i < qValues.Count; i++)
                    {
                        //if this and all previous values are NaN
                        if (float.IsNaN(qValues[i]))
                        {
                            if (!runningNaN)
                            {
                                mins[i] = mins[i - 1];
                                maxes[i] = maxes[i - 1];
                            }
                        }
                        else
                        {
                            if (runningNaN)
                            {
                                mins[i] = maxes[i] = qValues[i];
                                runningNaN = false;
                            }
                            else
                            {
                                mins[i] = Math.Min(mins[i - 1], qValues[i]);
                                maxes[i] = Math.Max(maxes[i - 1], qValues[i]);
                            }
                        }
                    }
                }
                MinQValues = ImmutableList.ValueOf(mins);
                MaxQValues = ImmutableList.ValueOf(maxes);
            }
            public Identity Target { get; private set; }

            public ImmutableList<float> QValues { get; private set; }
            public ImmutableList<float> MinQValues { get; private set; }
            public ImmutableList<float> MaxQValues { get; private set; }
            public bool IsEmpty { get; private set; }

        }

        public static readonly Producer<WorkOrderParam, DetectionPlotData> PRODUCER =
            new Factory();
            
        private class Factory : Producer<WorkOrderParam, DetectionPlotData>
        {
            public override DetectionPlotData ProduceResult(ProductionMonitor productionMonitor, WorkOrderParam parameter, IDictionary<WorkOrder, object> dependencies)
            {
                var data = new DetectionPlotData(parameter.Document, parameter.QValueCutoff);
                string message = data.Init(productionMonitor.CancellationToken, productionMonitor.SetProgress);
                if (!data.IsValid)
                {
                    throw new Exception(message);
                }
                return data;
            }
        }

        public class WorkOrderParam
        {
            public WorkOrderParam(SrmDocument document, float qValueCutoff)
            {
                Document = document;
                QValueCutoff = qValueCutoff;
            }

            public SrmDocument Document { get; }
            public float QValueCutoff { get; }

            protected bool Equals(WorkOrderParam other)
            {
                return ReferenceEquals(Document, other.Document) && QValueCutoff.Equals(other.QValueCutoff);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((WorkOrderParam)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(Document) * 397) ^ QValueCutoff.GetHashCode();
                }
            }
        }

    }
}
