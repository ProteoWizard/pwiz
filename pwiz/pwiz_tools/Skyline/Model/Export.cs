/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
// ReSharper disable InconsistentNaming
    public enum ExportStrategy { Single, Protein, Buckets }
    public enum ExportMethodType { Standard, Scheduled }
    public enum ExportFileType { List, Method }
    public static class ExportInstrumentType
    {
        public const string ABI = "ABI";
        public const string Agilent = "Agilent";
        public const string Thermo = "Thermo";
        public const string Thermo_TSQ = "Thermo TSQ";
        public const string Thermo_LTQ = "Thermo LTQ";
        public const string Waters = "Waters";
        public const string Waters_Xevo = "Waters Xevo";
        public const string Waters_Quattro_Premier = "Waters Quattro Premier";
    }
    public static class ExportOptimize
    {
        public const string NONE = "None";
        public const string CE = "Collision Energy";
        public const string DP = "Declustering Potential";
    }

// ReSharper restore InconsistentNaming
    public abstract class MassListExporter
    {
        public const string MEMORY_KEY_ROOT = "memory";

        public static bool IsSingleWindowInstrumentType(string type)
        {
            return Equals(type, ExportInstrumentType.ABI) ||
                   Equals(type, ExportInstrumentType.Waters) ||
                   Equals(type, ExportInstrumentType.Waters_Xevo) ||
                   Equals(type, ExportInstrumentType.Waters_Quattro_Premier);
        }

        protected MassListExporter(SrmDocument document, DocNode node)
        {
            Document = document;
            DocNode = node;
            CultureInfo = CultureInfo.InvariantCulture;
        }        

        public SrmDocument Document { get; private set; }
        public DocNode DocNode { get; private set; }

        public ExportStrategy Strategy { get; set; }
        public ExportMethodType MethodType { get; set; }
        public int? MaxTransitions { get; set; }
        public int MinTransitions { get; set; }
        public bool IgnoreProteins { get; set; }

        public string OptimizeType { get; set; }
        public double OptimizeStepSize { get; set; }
        public int OptimizeStepCount { get; set; }

        // CONSIDER: Should transition lists ever be exported with local culture
        //           CSV format?  This would allow them to be opened directly into
        //           Excel on the same system, but multiple vendors do not support
        //           international settings on their instrument control computers,
        //           which means the resulting CSVs probably wouldn't import correctly
        //           into methods.
        private CultureInfo _cultureInfo;
        public CultureInfo CultureInfo
        {
            get { return _cultureInfo; }
            set
            {
                _cultureInfo = value;
                FieldSeparator = TextUtil.GetCsvSeparator(_cultureInfo);
            }
        }
        public char FieldSeparator { get; private set; }

        public Dictionary<string, StringBuilder> MemoryOutput { get; private set; }

        /// <summary>
        /// Export to a transition list to a file or to memory.
        /// </summary>
        /// <param name="fileName">A file on disk to export to, or null to export to memory.</param>
        public void Export(string fileName)
        {
            bool single = (Strategy == ExportStrategy.Single);
            using (var fileIterator = new FileIterator(fileName, single, WriteHeaders))
            {
                MemoryOutput = fileIterator.MemoryOutput;

                fileIterator.Init();

                if (MethodType == ExportMethodType.Scheduled && Strategy == ExportStrategy.Buckets)
                    ExportScheduledBuckets(fileIterator);
                else
                    ExportNormal(fileIterator, single);
                fileIterator.Commit();
            }            
        }

        private void ExportNormal(FileIterator fileIterator, bool single)
        {
            foreach (PeptideGroupDocNode seq in Document.PeptideGroups)
            {
                // Skip peptide groups with no transitions
                if (seq.TransitionCount == 0)
                    continue;
                if (DocNode is PeptideGroupDocNode && !ReferenceEquals(seq, DocNode))
                    continue;

                if (Strategy == ExportStrategy.Protein)
                {
                    fileIterator.Suffix = FileEscape(seq.Name);
                    fileIterator.NextFile();
                }
                else if (!single && (!fileIterator.HasFile ||
                    (!IgnoreProteins && ExceedsMax(fileIterator.TransitionCount + CalcTransitionCount(seq.TransitionCount)))))
                {
                    fileIterator.NextFile();
                }

                foreach (PeptideDocNode peptide in seq.Children)
                {
                    if (DocNode is PeptideDocNode && !ReferenceEquals(peptide, DocNode))
                        continue;

                    // Make sure we can write out all the transitions for this peptide.
                    // Never split transitions from a single peptide across multiple injections,
                    // since this would mess up coelution and quantitation.
                    if (!single && fileIterator.TransitionCount > 0 &&
                            ExceedsMax(fileIterator.TransitionCount + CalcTransitionCount(peptide.TransitionCount)))
                    {
                        fileIterator.NextFile();
                    }

                    foreach (TransitionGroupDocNode group in peptide.Children)
                    {
                        // Skip percursors with too few transitions.
                        int groupTransitions = group.Children.Count;
                        if (groupTransitions < MinTransitions)
                            continue;

                        if (DocNode is TransitionGroupDocNode && !ReferenceEquals(group, DocNode))
                            continue;

                        foreach (TransitionDocNode transition in group.Children)
                        {
                            if (OptimizeType == null)
                                fileIterator.WriteTransition(this, seq, peptide, group, transition, 0);
                            else
                            {
                                // -step through step
                                for (int i = -OptimizeStepCount; i <= OptimizeStepCount; i++)
                                    fileIterator.WriteTransition(this, seq, peptide, group, transition, i);
                            }
                        }
                    }
                }
            }
        }

        private int CalcTransitionCount(int transitionNodes)
        {
            if (OptimizeType == null)
                return transitionNodes;
            return transitionNodes*(OptimizeStepCount*2 + 1);
        }

        private void ExportScheduledBuckets(FileIterator fileIterator)
        {
            bool singleWindow = IsSingleWindowInstrumentType(InstrumentType);

            var predict = Document.Settings.PeptideSettings.Prediction;
            int? maxInstrumentTrans = Document.Settings.TransitionSettings.Instrument.MaxTransitions;
            var listSchedules = new List<PeptideSchedule>();
            var listUnscheduled = new List<PeptideSchedule>();
            foreach (PeptideGroupDocNode nodePepGroup in Document.PeptideGroups)
            {
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    var peptideSchedule = new PeptideSchedule(nodePep, maxInstrumentTrans);
                    foreach (TransitionGroupDocNode nodeTranGroup in nodePep.Children)
                    {
                        double timeWindow;
                        double? retentionTime = predict.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                            singleWindow, out timeWindow);
                        if (retentionTime.HasValue)
                            peptideSchedule.Add(new PrecursorSchedule(nodePepGroup, nodePep, nodeTranGroup, retentionTime.Value, timeWindow, OptimizeStepCount));
                        else
                            peptideSchedule.Add(new PrecursorSchedule(nodePepGroup, nodePep, nodeTranGroup, 0, 0, OptimizeStepCount));
                    }
                    if (peptideSchedule.CanSchedule)
                        listSchedules.Add(peptideSchedule);
                    else
                        listUnscheduled.Add(peptideSchedule);
                }
            }

            int totalScheduled = 0;
            var listScheduleBuckets = new List<PeptideScheduleBucket>();
            while (!PeptideSchedule.IsListScheduled(listSchedules))
            {
                var listScheduleNext = new PeptideScheduleBucket();
                foreach (var schedule in listSchedules)
                    schedule.Schedule(listScheduleNext, MaxTransitions.Value);
                listScheduleBuckets.Add(listScheduleNext);
                if (listScheduleNext.TransitionCount == 0)
                {
                    var sb = new StringBuilder();
                    foreach (var peptideSchedule in listSchedules)
                    {
                        if (peptideSchedule.TransitionCount > MaxTransitions.Value)
                        {
                            sb.AppendLine(string.Format("{0} - {1} transitions",
                                peptideSchedule.Peptide.Peptide,
                                peptideSchedule.TransitionCount));
                        }
                    }
                    if (OptimizeStepCount == 0)
                        throw new IOException(string.Format("Failed to schedule the following peptides with the current settings:\n\n{0}\n\nCheck max concurrent transitions count.", sb));
                    else
                        throw new IOException(string.Format("Failed to schedule the following peptides with the current settings:\n\n{0}\nCheck max concurrent transitions count and optimization step count.", sb));
                }
                totalScheduled += listScheduleNext.TransitionCount;
            }

            int countScheduleGroups = listScheduleBuckets.Count;
            if (countScheduleGroups > 1)
            {
                // Balance the scheduling buckets to counteract the tendancy for each
                // successive bucket to have fewer transitions than the previous.
                // CONSIDER: O(n^2) but number of groups should never get that large
                int balanceCount = totalScheduled / countScheduleGroups;

                for (int i = 0; i < countScheduleGroups; i++)
                {
                    var bucketUnder = listScheduleBuckets[i];
                    if (bucketUnder.TransitionCount >= balanceCount)
                        continue;

                    // It should not be possible to borrow from scheduling lists
                    // after the current list, since the reason they are there is
                    // that they had too much overlap to be included in any of the
                    // preceding buckets.
                    for (int j = 0; j < i; j++)
                    {
                        var bucketOver = listScheduleBuckets[j];
                        if (bucketOver.TransitionCount <= balanceCount)
                            continue;
                        BorrowTransitions(bucketUnder, bucketOver, balanceCount);
                        // If the under bucket ever goes over balance, then quit.
                        if (bucketUnder.Count > balanceCount)
                            break;
                    }
                }
            }

            foreach (var listScheduleNext in listScheduleBuckets)
                WriteScheduledList(fileIterator, listScheduleNext);
            WriteScheduledList(fileIterator, listUnscheduled);
        }

        private void BorrowTransitions(PeptideScheduleBucket bucketUnder, PeptideScheduleBucket bucketOver, int balanceCount)
        {
            foreach (var schedule in bucketOver.ToArray().RandomOrder())
            {
                int newOverCount = bucketOver.TransitionCount - schedule.TransitionCount;
                int newUnderCount = bucketUnder.TransitionCount + schedule.TransitionCount;
                // If borrowing would not change the balance
                if ((newOverCount > balanceCount && balanceCount > newUnderCount) ||
                        // Or the transfer gets us closer to being balanced
                        Math.Abs(newOverCount - balanceCount) + Math.Abs(newUnderCount - balanceCount) <
                        Math.Abs(bucketOver.Count - balanceCount) + Math.Abs(bucketUnder.Count - balanceCount))
                {
                    // Make sure this doesn't exceed the maximum concurrent transition limit.
                    if (schedule.CanAddToBucket(bucketUnder, MaxTransitions.Value))
                    {
                        bucketOver.Remove(schedule);
                        bucketUnder.Add(schedule);
                    }
                }

                // If the over bucket goes below the balance, then quit.
                if (bucketOver.TransitionCount < balanceCount)
                    break;
            }
        }

        private void WriteScheduledList(FileIterator fileIterator,
            ICollection<PeptideSchedule> listSchedules)
        {
            if (listSchedules.Count == 0)
                return;

            fileIterator.NextFile();
            foreach (var schedule in PeptideSchedule.GetPrecursorSchedules(listSchedules))
            {
                var nodePepGroup = schedule.PeptideGroup;
                var nodePep = schedule.Peptide;
                var nodeGroup = schedule.TransitionGroup;

                // Skip percursors with too few transitions.
                int groupTransitions = nodeGroup.Children.Count;
                if (groupTransitions < MinTransitions)
                    continue;

                foreach (TransitionDocNode transition in nodeGroup.Children)
                {
                    if (OptimizeType == null)
                        fileIterator.WriteTransition(this, nodePepGroup, nodePep, nodeGroup, transition, 0);
                    else
                    {
                        // -step through step
                        for (int i = -OptimizeStepCount; i <= OptimizeStepCount; i++)
                            fileIterator.WriteTransition(this, nodePepGroup, nodePep, nodeGroup, transition, i);
                    }
                }
            }
        }

        private sealed class PeptideScheduleBucket : Collection<PeptideSchedule>
        {
            public int TransitionCount { get; private set; }

            protected override void ClearItems()
            {
                TransitionCount = 0;
                base.ClearItems();
            }

            protected override void InsertItem(int index, PeptideSchedule item)
            {
                TransitionCount += item.TransitionCount;
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                TransitionCount -= this[index].TransitionCount;
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, PeptideSchedule item)
            {
                TransitionCount += item.TransitionCount - this[index].TransitionCount;
                base.SetItem(index, item);
            }
        }

        private sealed class PeptideSchedule
        {
            private readonly List<PrecursorSchedule> _precursorSchedules = new List<PrecursorSchedule>();

            public PeptideSchedule(PeptideDocNode nodePep, int? maxInstrumentTrans)
            {
                Peptide = nodePep;
                MaxInstrumentTrans = maxInstrumentTrans;
            }

            public PeptideDocNode Peptide { get; private set; }

            private bool IsScheduled { get; set; }

            public bool CanSchedule
            {
                get { return !_precursorSchedules.Contains(s => s.EndTime == 0); }
            }

            public int TransitionCount { get; private set; }

            private int? MaxInstrumentTrans { get; set; }

            public void Add(PrecursorSchedule schedule)
            {
                TransitionCount += schedule.TransitionCount;
                _precursorSchedules.Add(schedule);
            }

            public bool CanAddToBucket(PeptideScheduleBucket schedules, int maxTransitions)
            {
                int transitionCount = TransitionCount;
                if (MaxInstrumentTrans.HasValue && schedules.TransitionCount + transitionCount > MaxInstrumentTrans)
                    return false;

                return GetOverlapCount(schedules) + transitionCount <= maxTransitions;
            }

            /// <summary>
            /// Attempts to add this <see cref="PrecursorSchedule"/> to a scheduling list
            /// without exceeding the maximum current transitions allowed.
            /// </summary>
            /// <param name="schedules">Scheduling list</param>
            /// <param name="maxTransitions">Maximum number of concurrent transitions allowed</param>
            public void Schedule(PeptideScheduleBucket schedules, int maxTransitions)
            {
                if (!IsScheduled && CanAddToBucket(schedules, maxTransitions))
                {
                    schedules.Add(this);
                    IsScheduled = true;
                }
            }

            private int GetOverlapCount(IEnumerable<PeptideSchedule> peptideSchedules)
            {
                // While this may be less completely correct the less the precursors in a
                // peptide overlap, wildly different precursor peaks are not all that interesting.
                int maxOverlap = 0;
                foreach (var precursorSchedule in _precursorSchedules)
                {
                    maxOverlap = Math.Max(maxOverlap,
                        precursorSchedule.GetOverlapCount(GetPrecursorSchedules(peptideSchedules)));
                }
                return maxOverlap;
            }

            public static IEnumerable<PrecursorSchedule> GetPrecursorSchedules(IEnumerable<PeptideSchedule> peptideSchedules)
            {
                foreach (var schedule in peptideSchedules)
                {
                    foreach (var precursorSchedule in schedule._precursorSchedules)
                        yield return precursorSchedule;
                }
            }

            /// <summary>
            /// Returns true, if all elements in the given scheduling list have been scheduled.
            /// </summary>
            public static bool IsListScheduled(IEnumerable<PeptideSchedule> schedules)
            {
                return !schedules.Contains(s => !s.IsScheduled);
            }
        }

        private sealed class PrecursorSchedule : PrecursorScheduleBase
        {
            public PrecursorSchedule(PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep,
                    TransitionGroupDocNode nodeTranGroup, double retentionTime, double timeWindow,
                    int optimizeStepCount)
                : base(nodeTranGroup, retentionTime, timeWindow, optimizeStepCount)
            {
                PeptideGroup = nodePepGroup;
                Peptide = nodePep;
            }

            public PeptideGroupDocNode PeptideGroup { get; private set; }
            public PeptideDocNode Peptide { get; private set; }
        }

        protected abstract string InstrumentType { get; }

        public virtual bool HasHeaders { get { return false; } }

        protected virtual void WriteHeaders(TextWriter writer) { /* No headers by default */ }

        protected abstract void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionDocNode nodeTran,
                                                int step);

        protected double GetProductMz(double productMz, int step)
        {
            return productMz + ChromatogramInfo.OPTIMIZE_SHIFT_SIZE*step;
        }

        protected double GetCollisionEnergy(PeptideDocNode nodePep,
                                            TransitionGroupDocNode nodeGroup,
                                            TransitionDocNode nodeTran,
                                            int step)
        {
            var prediction = Document.Settings.TransitionSettings.Prediction;
            var methodType = prediction.OptimizedMethodType;
            var regression = prediction.CollisionEnergy;

            // If exporting optimization methods, or optimization data should be ignored,
            // use the regression setting to calculate CE
            if (OptimizeType != null || methodType == OptimizedMethodType.None)
            {
                if (!Equals(OptimizeType, ExportOptimize.CE))
                    step = 0;
                return GetCollisionEnergy(Document, nodePep, nodeGroup, regression, step);
            }

            return OptimizationStep<CollisionEnergyRegression>.FindOptimizedValue(Document,
                nodePep, nodeGroup, nodeTran, methodType, regression, GetCollisionEnergy);
        }

        protected static double GetCollisionEnergy(SrmDocument document, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, CollisionEnergyRegression regression, int step)
        {
            int charge = nodeGroup.TransitionGroup.PrecursorCharge;
            double mz = document.Settings.GetRegressionMz(nodePep, nodeGroup);
            return regression.GetCollisionEnergy(charge, mz) + regression.StepSize * step;
        }

        protected double GetDeclusteringPotential(PeptideDocNode nodePep,
                                                  TransitionGroupDocNode nodeGroup,
                                                  TransitionDocNode nodeTran,
                                                  int step)
        {
            var prediction = Document.Settings.TransitionSettings.Prediction;
            var methodType = prediction.OptimizedMethodType;
            var regression = prediction.DeclusteringPotential;

            // If exporting optimization methods, or optimization data should be ignored,
            // use the regression setting to calculate CE
            if (OptimizeType != null || prediction.OptimizedMethodType == OptimizedMethodType.None)
            {
                if (!Equals(OptimizeType, ExportOptimize.DP))
                    step = 0;
                return GetDeclusteringPotential(Document, nodePep, nodeGroup, regression, step);
            }

            return OptimizationStep<DeclusteringPotentialRegression>.FindOptimizedValue(Document,
                nodePep, nodeGroup, nodeTran, methodType, regression, GetDeclusteringPotential);
        }

        private static double GetDeclusteringPotential(SrmDocument document, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, DeclusteringPotentialRegression regression, int step)
        {
            if (regression == null)
                return 0;
            double mz = document.Settings.GetRegressionMz(nodePep, nodeGroup);
            return regression.GetDeclustringPotential(mz) + regression.StepSize * step;
        }

        private sealed class OptimizationStep<T>
            where T : OptimizableRegression
        {
            private OptimizationStep(T regression, int step)
            {
                Regression = regression;
                Step = step;
            }

            private T Regression { get; set; }
            private int Step { get; set; }
            private double TotalArea { get; set; }

            private void AddArea(double area)
            {
                TotalArea += area;
            }

            public delegate double GetRegressionValue(SrmDocument document, PeptideDocNode nodePep,
                                                      TransitionGroupDocNode nodeGroup, T regression, int step);

            public static double FindOptimizedValue(SrmDocument document,
                                                 PeptideDocNode nodePep,
                                                 TransitionGroupDocNode nodeGroup,
                                                 TransitionDocNode nodeTran,
                                                 OptimizedMethodType methodType,
                                                 T regressionDocument,
                                                 GetRegressionValue getRegressionValue)
            {
                // Collect peak area for 
                var dictOptTotals = new Dictionary<T, Dictionary<int, OptimizationStep<T>>>();
                if (document.Settings.HasResults)
                {
                    var chromatograms = document.Settings.MeasuredResults.Chromatograms;
                    for (int i = 0; i < chromatograms.Count; i++)
                    {
                        var chromSet = chromatograms[i];
                        var regression = chromSet.OptimizationFunction as T;
                        if (regression == null)
                            continue;

                        Dictionary<int, OptimizationStep<T>> stepAreas;
                        if (!dictOptTotals.TryGetValue(regression, out stepAreas))
                            dictOptTotals.Add(regression, stepAreas = new Dictionary<int, OptimizationStep<T>>());

                        if (methodType == OptimizedMethodType.Precursor)
                        {
                            TransitionGroupDocNode[] listGroups = FindCandidateGroups(nodePep, nodeGroup);
                            foreach (var nodeGroupCandidate in listGroups)
                                AddOptimizationStepAreas(nodeGroupCandidate, i, regression, stepAreas);
                        }
                        else if (methodType == OptimizedMethodType.Transition)
                        {
                            TransitionDocNode[] listTransitions = FindCandidateTransitions(nodePep, nodeGroup, nodeTran);
                            foreach (var nodeTranCandidate in listTransitions)
                                AddOptimizationStepAreas(nodeTranCandidate, i, regression, stepAreas);
                        }
                    }
                }
                // If no candidate values were found, use the document regressor.
                if (dictOptTotals.Count == 0)
                    return getRegressionValue(document, nodePep, nodeGroup, regressionDocument, 0);
                // Get the CE value with the maximum total peak area
                double maxArea = 0;
                double bestValue = 0;
                foreach (var optTotals in dictOptTotals.Values)
                {
                    foreach (var optStep in optTotals.Values)
                    {
                        if (maxArea < optStep.TotalArea)
                        {
                            maxArea = optStep.TotalArea;
                            bestValue = getRegressionValue(document, nodePep, nodeGroup, optStep.Regression, optStep.Step);
                        }
                    }
                }
                // Use value for candidate with the largest area
                return bestValue;
            }

// ReSharper disable SuggestBaseTypeForParameter
            private static TransitionGroupDocNode[] FindCandidateGroups(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
// ReSharper restore SuggestBaseTypeForParameter
            {
                if (nodePep.Children.Count == 1)
                    return new[] { nodeGroup };
                // Add all precursors with the same charge as the one passed in
                var listCandidates = new List<TransitionGroupDocNode> { nodeGroup };
                foreach (TransitionGroupDocNode nodeGroupCandidate in nodePep.Children)
                {
                    if (nodeGroup.TransitionGroup.PrecursorCharge == nodeGroupCandidate.TransitionGroup.PrecursorCharge &&
                            !ReferenceEquals(nodeGroup, nodeGroupCandidate))
                        listCandidates.Add(nodeGroupCandidate);
                }
                return listCandidates.ToArray();
            }

            private static TransitionDocNode[] FindCandidateTransitions(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
            {
                var candidateGroups = FindCandidateGroups(nodePep, nodeGroup);
                if (candidateGroups.Length < 2)
                    return new[] { nodeTran };
                Debug.Assert(ReferenceEquals(nodeGroup, candidateGroups[0]));
                var listCandidates = new List<TransitionDocNode> { nodeTran };
                var transition = nodeTran.Transition;
                for (int i = 1; i < candidateGroups.Length; i++)
                {
                    foreach (TransitionDocNode nodeTranCandidate in candidateGroups[i].Children)
                    {
                        var transitionCandidate = nodeTranCandidate.Transition;
                        if (transition.Charge == transitionCandidate.Charge &&
                            transition.Ordinal == transitionCandidate.Ordinal &&
                            transition.IonType == transitionCandidate.IonType)
                        {
                            listCandidates.Add(nodeTranCandidate);
                            break;
                        }
                    }
                }
                return listCandidates.ToArray();
            }

            private static void AddOptimizationStepAreas(TransitionGroupDocNode nodeGroup, int iResult, T regression,
                IDictionary<int, OptimizationStep<T>> optTotals)
            {
                var results = (nodeGroup.HasResults ? nodeGroup.Results[iResult] : null);
                if (results == null)
                    return;
                foreach (var chromInfo in results)
                {
                    if (!chromInfo.Area.HasValue)
                        continue;
                    int step = chromInfo.OptimizationStep;
                    OptimizationStep<T> optStep;
                    if (!optTotals.TryGetValue(step, out optStep))
                        optTotals.Add(step, optStep = new OptimizationStep<T>(regression, step));
                    optStep.AddArea(chromInfo.Area.Value);
                }
            }

            private static void AddOptimizationStepAreas(TransitionDocNode nodeTran, int iResult, T regression,
                IDictionary<int, OptimizationStep<T>> optTotals)
            {
                var results = (nodeTran.HasResults ? nodeTran.Results[iResult] : null);
                if (results == null)
                    return;
                foreach (var chromInfo in results)
                {
                    if (chromInfo.Area == 0)
                        continue;
                    int step = chromInfo.OptimizationStep;
                    OptimizationStep<T> optStep;
                    if (!optTotals.TryGetValue(step, out optStep))
                        optTotals.Add(step, optStep = new OptimizationStep<T>(regression, step));
                    optStep.AddArea(chromInfo.Area);
                }
            }
        }

        private bool ExceedsMax(int count)
        {
            return (MaxTransitions != null && count > 0 && count > MaxTransitions);
        }

        private static string FileEscape(IEnumerable<char> namePart)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in namePart)
            {
                if ("/\\:*?\"<>|".IndexOf(c) == -1)
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }

        private sealed class FileIterator : IDisposable
        {
            private FileSaver _saver;
            private TextWriter _writer;
            private readonly bool _single;
            private readonly Action<TextWriter> _writeHeaders;

            public FileIterator(string fileName, bool single, Action<TextWriter> writeHeaders)
            {
                FileName = fileName;
                _single = single;
                _writeHeaders = writeHeaders;
                if (fileName == null)
                {
                    BaseName = MEMORY_KEY_ROOT;
                    MemoryOutput = new Dictionary<string, StringBuilder>();
                }
                else
                {
                    BaseName = Path.Combine(Path.GetDirectoryName(fileName),
                        Path.GetFileNameWithoutExtension(fileName));
                }
            }

// ReSharper disable MemberCanBePrivate.Local
            public string FileName { get; private set; }
            public string BaseName { get; set; }
            public string Suffix { get; set; }
            public int FileCount { get; set; }
            public int TransitionCount { get; set; }
// ReSharper restore MemberCanBePrivate.Local

            public Dictionary<string, StringBuilder> MemoryOutput { get; private set; }

            public bool HasFile { get { return _writer != null; } }

            public void Init()
            {
                if (_single)
                {
                    if (FileName != null)
                    {
                        _saver = new FileSaver(FileName);
                        if (!_saver.CanSave(false))
                            throw new IOException(string.Format("Cannot save to {0}.", FileName));

                        _writer = new StreamWriter(_saver.SafeName);
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        MemoryOutput[BaseName] = sb;
                        _writer = new StringWriter(sb);
                    }
                    _writeHeaders(_writer);
                }                
            }

            public void Commit()
            {
                // Never commit an empty file to disk
                if (TransitionCount == 0)
                    Dispose();
                else
                {
                    if (_writer != null)
                        _writer.Close();
                    _writer = null;
                    if (_saver != null)
                        _saver.Commit();
                    _saver = null;                    
                }
            }

            public void Dispose()
            {
                try
                {
                    if (_writer != null)
                        _writer.Close();
                    _writer = null;
                    if (_saver != null)
                        _saver.Dispose();
                    _saver = null;
                }
                catch (IOException)
                {
                }
            }

            public void NextFile()
            {
                Commit();

                TransitionCount = 0;
                FileCount++;

                string baseName;
                // Make sure file names sort into the order in which they were
                // written.  This will help the results load in tree order.
                if (Suffix == null)
                    baseName = string.Format("{0}_{1:0000}", BaseName, FileCount);
                else
                    baseName = string.Format("{0}_{1:0000}_{2}", BaseName, FileCount, Suffix);

                if (MemoryOutput == null)
                {
                    _saver = new FileSaver(baseName + ".csv");
                    _writer = new StreamWriter(_saver.SafeName);
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    MemoryOutput[baseName] = sb;
                    _writer = new StringWriter(sb);
                }
                _writeHeaders(_writer);
            }

            public void WriteTransition(MassListExporter exporter,
                                        PeptideGroupDocNode seq,
                                        PeptideDocNode peptide,
                                        TransitionGroupDocNode group,
                                        TransitionDocNode transition,
                                        int step)
            {
                if (!HasFile)
                    throw new IOException("Unexpected failure writing transitions.");

                exporter.WriteTransition(_writer, seq, peptide, group, transition, step);

                TransitionCount++;
            }
        }
    }

    internal class PrecursorScheduleBase
    {
        public PrecursorScheduleBase(TransitionGroupDocNode nodeGroup, double retentionTime,
            double timeWindow, int optimizeStepCount)
        {
            TransitionGroup = nodeGroup;
            StartTime = retentionTime - (timeWindow / 2);
            EndTime = StartTime + timeWindow;
            OptimizeStepCount = optimizeStepCount;
        }

        public TransitionGroupDocNode TransitionGroup { get; private set; }
        public int TransitionCount { get { return TransitionGroup.TransitionCount * (OptimizeStepCount*2 + 1); } }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int OptimizeStepCount { get; set; }

        public bool ContainsTime(double time)
        {
            return StartTime <= time && time <= EndTime;
        }

        public int GetOverlapCount<T>(IEnumerable<T> schedules)
            where T : PrecursorScheduleBase
        {
            // Check for maximum overlap count at start and end times of this
            // schedule window, and any other start or end time that falls within
            // this schedule window.
            List<double> times = new List<double> {StartTime, EndTime};
            foreach (var schedule in schedules)
            {
                if (ContainsTime(schedule.StartTime))
                    times.Add(schedule.StartTime);
                if (ContainsTime(schedule.EndTime))
                    times.Add(schedule.EndTime);
            }

            int overlapMax = 0;
            foreach (double time in times)
                overlapMax = Math.Max(overlapMax, GetOverlapCount(schedules, time));
            return overlapMax;            
        }

        /// <summary>
        /// Returns the number of transitions in a list of schedules that contain a given time.
        /// </summary>
        public static int GetOverlapCount<T>(IEnumerable<T> schedules, double time)
            where T : PrecursorScheduleBase
        {
            int overlapping = 0;
            foreach (var schedule in schedules)
            {
                if (schedule.ContainsTime(time))
                    overlapping += schedule.TransitionCount;
            }
            return overlapping;                        
        }
    }

    public class ThermoMassListExporter : MassListExporter
    {
        public ThermoMassListExporter(SrmDocument document)
            : base(document, null)
        {
        }

        public bool AddEnergyRamp { get; set; }
        public double? RunLength { get; set; }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.Thermo; }
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            if (MethodType == ExportMethodType.Scheduled)
            {
                if (AddEnergyRamp)
                {
                    writer.Write('1');  // Energy Ramp
                    writer.Write(FieldSeparator);                                        
                }

                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    false, out windowRT);
                predictedRT = RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT);
                if (predictedRT.HasValue)
                {
                    writer.Write(Math.Max(0, predictedRT.Value - windowRT / 2).ToString(CultureInfo));    // No negative retention times
                    writer.Write(FieldSeparator);
                    writer.Write((predictedRT.Value + windowRT / 2).ToString(CultureInfo));
                    writer.Write(FieldSeparator);
                    writer.Write('1');  // Polarity
                    writer.Write(FieldSeparator);                    
                }
                else
                {
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                    writer.Write('1');  // Polarity
                    writer.Write(FieldSeparator);
                }
            }
            else if (RunLength.HasValue)
            {
                writer.Write('0');    // No negative retention times
                writer.Write(FieldSeparator);
                writer.Write(RunLength);
                writer.Write(FieldSeparator);
                writer.Write('1');  // Polarity
                writer.Write(FieldSeparator);                                    
            }
            writer.Write(nodePep.Peptide.Sequence);
            writer.Write(FieldSeparator);
            writer.WriteDsvField(nodePepGroup.Name, FieldSeparator);
            writer.Write(FieldSeparator);
            writer.Write(nodeTran.Transition.FragmentIonName);
            writer.Write(FieldSeparator);
            if (nodeTran.HasLibInfo)
                writer.Write(nodeTran.LibInfo.Rank);
            if (Document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                writer.Write(FieldSeparator);
                writer.WriteDsvField(nodeTranGroup.TransitionGroup.LabelType.ToString(), FieldSeparator);
            }

            writer.WriteLine();
        }
    }

    public class ThermoMethodExporter : ThermoMassListExporter
    {
        public const string EXE_BUILD_TSQ_METHOD = @"Method\Thermo\BuildTSQEZMethod";

        public ThermoMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            // First export transition lists to map in memory
            Export(null);

            MethodExporter.ExportMethod(EXE_BUILD_TSQ_METHOD, new List<string>(),
                fileName, templateName, MemoryOutput, progressMonitor);
        }
    }

    public class ThermoLtqMethodExporter : ThermoMassListExporter
    {
        public const string EXE_BUILD_LTQ_METHOD = @"Method\Thermo\BuildLTQMethod";

        public ThermoLtqMethodExporter(SrmDocument document)
            : base(document)
        {
            // Export scheduling fields, but no actual scheduling
            // is yet possible on the LTQ. (requires dealing with
            // segments)
            RunLength = 0;
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            // First export transition lists to map in memory
            Export(null);

            MethodExporter.ExportMethod(EXE_BUILD_LTQ_METHOD, new List<string>(),
                fileName, templateName, MemoryOutput, progressMonitor);
        }
    }

    public class AbiMassListExporter : MassListExporter
    {
        public AbiMassListExporter(SrmDocument document)
            : this(document, null)
        {
        }

        public AbiMassListExporter(SrmDocument document, DocNode node)
            : base(document, node)
        {
        }

        public double DwellTime { get; set; }

        private bool HasResults { get { return Document.Settings.HasResults; } }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.ABI; }
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            if (MethodType == ExportMethodType.Standard)
                writer.Write(Math.Round(DwellTime, 2));
            else
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    HasResults, out windowRT);
                if (predictedRT.HasValue)
                    writer.Write(RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT).Value.ToString(CultureInfo));
            }
            writer.Write(FieldSeparator);

            // Write special ID for AB software
            string extPeptideId = string.Format("{0}.{1}.{2}{3}.{4}",
                                                nodePepGroup.Name,
                                                nodePep.Peptide.Sequence,
                                                nodeTran.HasLibInfo ? nodeTran.LibInfo.Rank.ToString() : "",
                                                nodeTran.Transition.FragmentIonName,
                                                nodeTranGroup.TransitionGroup.LabelType);
            writer.WriteDsvField(extPeptideId, FieldSeparator);
            writer.Write(FieldSeparator);

            writer.Write(Math.Round(GetDeclusteringPotential(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.WriteLine();
        }
    }

    public class AgilentMassListExporter : MassListExporter
    {
        public AgilentMassListExporter(SrmDocument document)
            : this(document, null)
        {
        }

        public AgilentMassListExporter(SrmDocument document, DocNode node)
            : base(document, node)
        {
            Fragmentor = 130;
        }

        public double DwellTime { get; set; }
        public double Fragmentor { get; set; }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.Agilent; }
        }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write("Compound Name");
            writer.Write(FieldSeparator);
            writer.Write("ISTD?");
            writer.Write(FieldSeparator);
            writer.Write("Precursor Ion");
            writer.Write(FieldSeparator);
            writer.Write("MS1 Res");
            writer.Write(FieldSeparator);
            writer.Write("Product Ion");
            writer.Write(FieldSeparator);
            writer.Write("MS2 Res");
            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write("Dwell");                
            }
            writer.Write(FieldSeparator);
            writer.Write("Fragmentor");
            writer.Write(FieldSeparator);
            writer.Write("Collision Energy");
            if (MethodType != ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write("Ret Time (min)");
                writer.Write(FieldSeparator);
                writer.Write("Delta Ret Time");                
            }
            writer.Write(FieldSeparator);
            writer.Write("Protein");
            writer.Write(FieldSeparator);
            writer.Write("Ion Name");
            if (Document.Settings.PeptideSettings.Libraries.HasLibraries)
            {
                writer.Write(FieldSeparator);
                writer.Write("Library Rank");
            }
            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.Write(nodePep.Peptide.Sequence);
            writer.Write(FieldSeparator);
            writer.Write(nodeTranGroup.TransitionGroup.LabelType.IsLight ? "FALSE" : "TRUE");
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write("Unit");
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write("Unit");
            writer.Write(FieldSeparator);

            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(Math.Round(DwellTime, 2).ToString(CultureInfo));
                writer.Write(FieldSeparator);                
            }

            writer.Write(Fragmentor.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.Write(FieldSeparator);

            if (MethodType != ExportMethodType.Standard)
            {
                // Scheduling information
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    false, out windowRT);
                if (predictedRT.HasValue)
                {
                    writer.Write(RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT).Value.ToString(CultureInfo));
                    writer.Write(FieldSeparator);
                    writer.Write(Math.Round(windowRT, 1).ToString(CultureInfo));
                    writer.Write(FieldSeparator);
                }
                else
                {
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                }
            }

            // Extra information not used by instrument
            writer.Write(nodePepGroup.Name);
            writer.Write(FieldSeparator);
            writer.Write(nodeTran.Transition.FragmentIonName);
            writer.Write(FieldSeparator);
            if (nodeTran.HasLibInfo)
                writer.Write(nodeTran.LibInfo.Rank);
            writer.WriteLine();
        }
    }

    public class WatersMassListExporter : MassListExporter
    {
        public WatersMassListExporter(SrmDocument document)
            : this(document, null)
        {
        }

        public WatersMassListExporter(SrmDocument document, DocNode node)
            : base(document, node)
        {
            ConeVoltage = 35;
        }

//        public double DwellTime { get; set; }
        public double ConeVoltage { get; set; }
        public double RunLength { get; set; }

        protected double RTWindow { get; private set; }

        private bool HasResults { get { return Document.Settings.HasResults; } }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.Waters; }
        }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write("protein.name");
            writer.Write(FieldSeparator);
            writer.Write("peptide.seq");    // modified sequence to support 1:1 requirement with precursor m/z
            writer.Write(FieldSeparator);
            writer.Write("precursor.mz");
            writer.Write(FieldSeparator);
            writer.Write("precursor.retT");
            writer.Write(FieldSeparator);
            writer.Write("product.m_z");
            writer.Write(FieldSeparator);
            writer.Write("collision_energy");
            writer.Write(FieldSeparator);
            writer.Write("cone_voltage");
            // Informational columns
            writer.Write(FieldSeparator);
            writer.Write("peptide_unmod.seq");
            writer.Write(FieldSeparator);
            writer.Write("ion_name");
            writer.Write(FieldSeparator);
            writer.Write("library_rank");
            if (Document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                writer.Write(FieldSeparator);
                writer.Write("label_type");                
            }
            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.WriteDsvField(nodePepGroup.Name.Replace(' ', '_'), FieldSeparator);  // Quanpedia can't handle spaces
            writer.Write(FieldSeparator);
            // Write special ID to ensure 1-to-1 relationship between this ID and precursor m/z
            writer.Write(Document.Settings.GetModifiedSequence(nodePep.Peptide.Sequence,
                nodeTranGroup.TransitionGroup.LabelType, nodePep.ExplicitMods));
            writer.Write('.');
            writer.Write(nodeTranGroup.TransitionGroup.PrecursorCharge);
            if (step != 0)
            {
                writer.Write('.');                
                writer.Write(step);
            }
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);

            if (MethodType == ExportMethodType.Standard)
            {
                RTWindow = RunLength;   // Store for later use
                writer.Write((RunLength / 2).ToString(CultureInfo));
            }
            else
            {
                // Scheduling information
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    HasResults, out windowRT);
                if (predictedRT.HasValue)
                {
                    RTWindow = windowRT;    // Store for later use
                    writer.Write(RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT).Value.ToString(CultureInfo));
                }
            }

            writer.Write(FieldSeparator);

            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);

            // Waters only excepts integers for CE and CV
            writer.Write((int)Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step)));
            writer.Write(FieldSeparator);
            writer.Write((int)Math.Round(ConeVoltage));
            writer.Write(FieldSeparator);

            // Extra information not used by instrument
            writer.Write(nodePep.Peptide.Sequence);
            writer.Write(FieldSeparator);
            writer.Write(nodeTran.Transition.FragmentIonName);
            writer.Write(FieldSeparator);
            if (nodeTran.HasLibInfo)
                writer.Write(nodeTran.LibInfo.Rank);
            else
                writer.Write(-1);   // Because VerifyE can't deal with an empty field
            if (Document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                writer.Write(FieldSeparator);
                writer.WriteDsvField(nodeTranGroup.TransitionGroup.LabelType.ToString(), FieldSeparator);
            }
            writer.WriteLine();
        }
    }

    public class WatersMethodExporter : WatersMassListExporter
    {
        public const string EXE_BUILD_WATERS_METHOD = @"Method\Waters\BuildWatersMethod";

        public WatersMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        public string MethodInstrumentType { get; set; }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            EnsureLibraries();

            // First export transition lists to map in memory
            Export(null);

            var argv = new List<string>();
            if (Equals(MethodInstrumentType, ExportInstrumentType.Waters_Quattro_Premier))
                argv.Add("-q");
            argv.Add("-w");
            argv.Add(RTWindow.ToString());
            MethodExporter.ExportMethod(EXE_BUILD_WATERS_METHOD,
                argv, fileName, templateName, MemoryOutput, progressMonitor);
        }

        private const string PRIMARY_DEPENDENCY_LIBRARY = "QuantifyClassLibrary.dll";

        private static readonly string[] DEPENDENCY_LIBRARIES = {
                                                                    PRIMARY_DEPENDENCY_LIBRARY,
                                                                    "CompoundDatabaseClassLibrary.dll",
                                                                    "MassSpectrometerLibrary.dll",
                                                                    "MSMethodClassLibrary.dll",
                                                                    "ResourceClassLibrary.dll",
                                                                    "SQLControl.dll",
                                                                    "System.Data.SQLite.dll",
                                                                    "UtilityClassLibrary.dll",
                                                                    "WizardData.dll"
                                                                };
        private static void EnsureLibraries()
        {
            string skylinePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(skylinePath))
                throw new IOException("Waters method creation software may not be installed correctly.");
            string buildSubdir = Path.GetDirectoryName(EXE_BUILD_WATERS_METHOD);
            string exeDir = Path.Combine(Path.GetDirectoryName(skylinePath), buildSubdir);
            string libraryPath = Path.Combine(exeDir, PRIMARY_DEPENDENCY_LIBRARY);
            if (File.Exists(libraryPath))
                return;

            string dacServerPath = AdvApi.GetPathFromProgId("DACScanStats.DACScanStats");
            if (dacServerPath == null)
                throw new IOException("Failed to find a valid MassLynx installation.");

            string massLynxDir = Path.GetDirectoryName(dacServerPath);
            foreach (var library in DEPENDENCY_LIBRARIES)
            {
                string srcFile = Path.Combine(massLynxDir, library);
                if (!File.Exists(srcFile))
                    throw new IOException(string.Format("MassLynx may not be installed correctly.  The library {0} could not be found.", library));
                string destFile = Path.Combine(exeDir, library);
                File.Copy(srcFile, destFile, true);
            }
        }
    }

    internal class AdvApi
    {
        private AdvApi()
        {            
        }
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int RegOpenKeyEx(
          UIntPtr hKey,
          string subKey,
          int ulOptions,
          int samDesired,
          out UIntPtr hkResult);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW", SetLastError = true)]
        public static extern int RegQueryValueEx(
            UIntPtr hKey,
            string lpValueName,
            int lpReserved,
            out uint lpType,
            StringBuilder lpData,
            ref uint lpcbData);
        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern int RegCloseKey(
            UIntPtr hKey);

// ReSharper disable InconsistentNaming
        public static UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);
        public static UIntPtr HKEY_CURRENT_USER = new UIntPtr(0x80000001u);

        public const int KEY_READ = 0x20019;  

        public const int REG_SZ = 1;
// ReSharper restore InconsistentNaming

        public static string GetPathFromProgId(string progId)
        {
            String clsid = RegQueryKeyValue(HKEY_LOCAL_MACHINE, @"SOFTWARE\Classes\" + progId + @"\CLSID");
            if (clsid == null)
                return null;
            return RegQueryKeyValue(HKEY_LOCAL_MACHINE, @"SOFTWARE\Classes\CLSID\" + clsid + @"\InprocServer32");
        }

        public static string RegQueryKeyValue(UIntPtr hKey, string path)
        {
            UIntPtr hKeyQuery;
            if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, path, 0, KEY_READ, out hKeyQuery) != 0)
                return null;

            uint size = 1024;
            StringBuilder sb = new StringBuilder(1024);

            try
            {
                uint type;
                if (RegQueryValueEx(hKeyQuery, "", 0, out type, sb, ref size) != 0)
                    return null;
            }
            finally
            {
                RegCloseKey(hKeyQuery);
            }
            return sb.ToString();
        }
    }

    internal static class MethodExporter
    {
        public static void ExportMethod(string exeName,
                                        List<string> argv,
                                        string fileName,
                                        string templateName,
                                        Dictionary<string, StringBuilder> dictTranLists,
                                        IProgressMonitor progressMonitor)
        {
            string baseName = Path.Combine(Path.GetDirectoryName(fileName),
                                           Path.GetFileNameWithoutExtension(fileName));
            string ext = Path.GetExtension(fileName);

            var listFileSavers = new List<FileSaver>();
            try
            {
                string methodName = "";
                StringBuilder stdinBuilder = new StringBuilder();
                foreach (KeyValuePair<string, StringBuilder> pair in dictTranLists)
                {
                    string suffix = pair.Key.Substring(MassListExporter.MEMORY_KEY_ROOT.Length);
                    suffix = Path.GetFileNameWithoutExtension(suffix);
                    methodName = baseName + suffix + ext;

                    if (stdinBuilder.Length > 0)
                        stdinBuilder.AppendLine();

                    var fs = new FileSaver(methodName);
                    listFileSavers.Add(fs);

                    stdinBuilder.AppendLine(fs.SafeName);
                    stdinBuilder.AppendLine(fs.RealName);
                    stdinBuilder.Append(pair.Value.ToString());
                }

                argv.AddRange(new[] { "-s", "-m", "\"" + templateName + "\"" });  // Read from stdin, multi-file format

                string dirWork = Path.GetDirectoryName(fileName);
                var psiExporter = new ProcessStartInfo(exeName)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    // Common directory includes the directory separator
                    WorkingDirectory = dirWork,
                    Arguments = string.Join(" ", argv.ToArray()),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };

                ProgressStatus status;
                if (dictTranLists.Count == 1)
                    status = new ProgressStatus(string.Format("Exporting method {0}...", methodName));
                else
                {
                    status = new ProgressStatus("Exporting methods...");
                    status = status.ChangeSegments(0, dictTranLists.Count);
                }
                progressMonitor.UpdateProgress(status);

                psiExporter.RunProcess(stdinBuilder.ToString(), "MESSAGE: ", progressMonitor, ref status);

                if (!status.IsError && !status.IsCanceled)
                {
                    foreach (var fs in listFileSavers)
                        fs.Commit();
                }
            }
            finally
            {
                foreach (var fs in listFileSavers)
                    fs.Dispose();
            }
        }
    }

    [XmlRoot("method_template")]    
    public sealed class MethodTemplateFile : XmlNamedElement
    {
        public MethodTemplateFile(string name, string filePath)
            : base(name)
        {
            FilePath = filePath;
        }

        public string FilePath { get; private set; }

        #region IXmlSerializable helpers

        /// <summary>
        /// For serialization
        /// </summary>
        private MethodTemplateFile()
        {
        }

        private enum ATTR
        {
            file_path
        }

        public static MethodTemplateFile Deserialize(XmlReader reader)
        {
            MethodTemplateFile methodTemplate = new MethodTemplateFile();
            methodTemplate.ReadXml(reader);
            return methodTemplate;
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            FilePath = reader.GetAttribute(ATTR.file_path);
            reader.Read();  // Consume tag
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.file_path, FilePath);
        }

        #endregion
    }
}