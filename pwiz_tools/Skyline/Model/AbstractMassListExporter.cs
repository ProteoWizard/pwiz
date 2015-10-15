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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public abstract class AbstractMassListExporter
    {
        public const int PRIMARY_COUNT_MIN = 1;
        public const int PRIMARY_COUNT_MAX = 10;
        public const int PRIMARY_COUNT_DEFAULT = 2;

        public const int DWELL_TIME_MIN = 1;
        public const int DWELL_TIME_MAX = 1000;
        public const int DWELL_TIME_DEFAULT = 20;

        public const int RUN_LENGTH_MIN = 5;
        public const int RUN_LENGTH_MAX = 500;
        public const int RUN_LENGTH_DEFAULT = 60;

        public const int MAX_TRANS_PER_INJ_DEFAULT = 130;
        public const int MAX_TRANS_PER_INJ_MIN = 2;

        public const string MEMORY_KEY_ROOT = "memory"; // Not L10N

        protected AbstractMassListExporter(SrmDocument document, DocNode node)
        {
            Document = document;
            DocNode = node;
            CultureInfo = CultureInfo.InvariantCulture;
        }

        protected RequiredPeptideSet RequiredPeptides { get; private set; }
        public SrmDocument Document { get; private set; }
        public DocNode DocNode { get; private set; }

        public ExportStrategy Strategy { get; set; }
        public ExportMethodType MethodType { get; set; }
        public bool IsPrecursorLimited { get; set; }
        public bool FullScans { get; set; }
        public bool IsolationList { get; set; }
        public int? MaxTransitions { get; set; }
        public int MinTransitions { get; set; }
        public int PrimaryTransitionCount { get; set; }
        public bool IgnoreProteins { get; set; }

        public string OptimizeType { get; set; }
        public double OptimizeStepSize { get; set; }
        public int OptimizeStepCount { get; set; }

        public int? SchedulingReplicateIndex { get; set; }
        public ExportSchedulingAlgorithm SchedulingAlgorithm { get; set; }

        public bool Ms1Scan { get; set; }
        public bool InclusionList { get; set; }
        public string MsAnalyzer { get; set; }
        public string MsMsAnalyzer { get; set; }

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
                FieldSeparatorReplacement = "_";  // For use in formats where quoting the value does not suffice, as reportedly in xcalibur  // Not L10N
            }
        }
        public char FieldSeparator { get; private set; }
        public string FieldSeparatorReplacement { get; private set; }

        public Dictionary<string, StringBuilder> MemoryOutput { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        protected bool InitExport(string fileName, IProgressMonitor progressMonitor)
        {
            if (progressMonitor != null && progressMonitor.IsCanceled)
                return false;

            // First export transition lists to map in memory
            Export(null);

            // If filename is null, then no more work needs to be done.
            if (fileName == null)
            {
                if (progressMonitor != null)
                    progressMonitor.UpdateProgress(new ProgressStatus(string.Empty).Complete());
                return false;
            }

            return true;
        }

        /// <summary>
        /// Export to a transition list to a file or to memory.
        /// </summary>
        /// <param name="fileName">A file on disk to export to, or null to export to memory.</param>
        public void Export(string fileName)
        {
            bool single = (Strategy == ExportStrategy.Single);
            RequiredPeptides = GetRequiredPeptides(single);
            if (MaxTransitions.HasValue && RequiredPeptides.TransitionCount > MaxTransitions)
            {
                throw new IOException(string.Format(Resources.AbstractMassListExporter_Export_The_number_of_required_transitions__0__exceeds_the_maximum__1__,
                                                    RequiredPeptides.TransitionCount, MaxTransitions));
            }

            using (var fileIterator = new FileIterator(fileName, single, IsPrecursorLimited, WriteHeaders))
            {
                MemoryOutput = fileIterator.MemoryOutput;

                fileIterator.Init();

                if (MethodType != ExportMethodType.Standard && Strategy == ExportStrategy.Buckets)
                    ExportScheduledBuckets(fileIterator);
                else
                    ExportNormal(fileIterator, single);
                fileIterator.Commit();
            }
        }

        private void NextFile(FileIterator fileIterator)
        {
            if (fileIterator.HasFile)
                fileIterator.WriteRequiredTransitions(this, RequiredPeptides);
            fileIterator.NextFile();
        }

        public string GetCompound(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        {
            return nodePep.Peptide.IsCustomIon ? nodeGroup.CustomIon.InvariantName : Document.Settings.GetModifiedSequence(nodePep);
        }

        private RequiredPeptideSet GetRequiredPeptides(bool single)
        {
            return single
                       ? new RequiredPeptideSet()
                       : new RequiredPeptideSet(Document, IsPrecursorLimited);
        }

        protected sealed class RequiredPeptideSet
        {
            private readonly RequiredPeptide[] _peptides;
            private readonly HashSet<int> _setPepIndexes;

            public RequiredPeptideSet()
            {
                _setPepIndexes = new HashSet<int>();
                _peptides = new RequiredPeptide[0];
            }

            public RequiredPeptideSet(SrmDocument document, bool isPrecursorLimited)
            {
                _peptides = (from nodePepGroup in document.PeptideGroups
                             from nodePep in nodePepGroup.Peptides
                             where nodePep.GlobalStandardType != null
                             select new RequiredPeptide(nodePepGroup, nodePep))
                    .ToArray();
                _setPepIndexes = new HashSet<int>(_peptides.Select(pep => pep.PeptideNode.Peptide.GlobalIndex));
                TransitionCount = _peptides.Sum(pep => isPrecursorLimited
                                                           ? pep.PeptideNode.TransitionGroupCount
                                                           : pep.PeptideNode.TransitionCount);
            }

            public int TransitionCount { get; private set; }

            public IEnumerable<RequiredPeptide> Peptides { get { return _peptides; } }

            public bool IsRequired(PeptideDocNode nodePep)
            {
                return _setPepIndexes.Contains(nodePep.Id.GlobalIndex);
            }
        }

        protected struct RequiredPeptide
        {
            public RequiredPeptide(PeptideGroupDocNode peptideGroup, PeptideDocNode peptide)
                : this()
            {
                PeptideGroupNode = peptideGroup;
                PeptideNode = peptide;
            }

            public PeptideGroupDocNode PeptideGroupNode { get; private set; }
            public PeptideDocNode PeptideNode { get; private set; }
        }

        private void ExportNormal(FileIterator fileIterator, bool single)
        {
            foreach (PeptideGroupDocNode seq in Document.MoleculeGroups)
            {
                // Skip peptide groups with no transitions
                if (seq.TransitionCount == 0)
                    continue;
                if (DocNode is PeptideGroupDocNode && !ReferenceEquals(seq, DocNode))
                    continue;

                if (Strategy == ExportStrategy.Protein)
                {
                    fileIterator.Suffix = FileEscape(seq.Name);
                    NextFile(fileIterator);
                }
                else if (!single && (!fileIterator.HasFile ||
                    (!IgnoreProteins && ExceedsMax(fileIterator.TransitionCount + CalcTransitionCount(seq)))))
                {
                    NextFile(fileIterator);
                }

                foreach (PeptideDocNode peptide in seq.Children)
                {
                    if (DocNode is PeptideDocNode && !ReferenceEquals(peptide, DocNode))
                        continue;
                    // Required peptides will be written by the NextFile method
                    if (RequiredPeptides.IsRequired(peptide))
                        continue;

                    // Make sure we can write out all the transitions for this peptide.
                    // Never split transitions from a single peptide across multiple injections,
                    // since this would mess up coelution and quantitation.
                    if (!single && fileIterator.TransitionCount > 0 &&
                            ExceedsMax(fileIterator.TransitionCount + CalcTransitionCount(peptide)))
                    {
                        NextFile(fileIterator);
                    }

                    foreach (TransitionGroupDocNode group in peptide.Children)
                    {
                        // Skip precursors with too few transitions.
                        int groupTransitions = group.Children.Count;
                        if (groupTransitions < MinTransitions)
                            continue;

                        if (DocNode is TransitionGroupDocNode && !ReferenceEquals(group, DocNode))
                            continue;

                        if (IsolationList)
                        {
                            fileIterator.WriteTransition(this, seq, peptide, group, null, null, 0);
                        }
                        else
                        {
                            var groupPrimary = PrimaryTransitionCount > 0
                                                       ? peptide.GetPrimaryResultsGroup(group)
                                                       : null;

                            WriteTransitions(fileIterator, seq, peptide, group, groupPrimary);
                        }
                    }
                }
            }
            // Add the required transitions to the last file
            fileIterator.WriteRequiredTransitions(this, RequiredPeptides);
        }

        private int CalcTransitionCount(PeptideGroupDocNode nodePepGroup)
        {
            return CalcTransitionCount(IsPrecursorLimited ? nodePepGroup.TransitionGroupCount : nodePepGroup.TransitionCount);
        }

        private int CalcTransitionCount(PeptideDocNode nodePep)
        {
            return CalcTransitionCount(IsPrecursorLimited ? nodePep.TransitionGroupCount : nodePep.TransitionCount);
        }

        private int CalcTransitionCount(int transitionNodes)
        {
            if (OptimizeType == null)
                return transitionNodes;
            return transitionNodes * (OptimizeStepCount * 2 + 1);
        }

        private void ExportScheduledBuckets(FileIterator fileIterator)
        {
            if (!MaxTransitions.HasValue)
                throw new InvalidOperationException(Resources.AbstractMassListExporter_ExportScheduledBuckets_Maximum_transitions_per_file_required);

            bool singleWindow = ExportInstrumentType.IsSingleWindowInstrumentType(InstrumentType);

            var predict = Document.Settings.PeptideSettings.Prediction;
            int? maxInstrumentTrans = null;
            if (!IsPrecursorLimited)
                maxInstrumentTrans = Document.Settings.TransitionSettings.Instrument.MaxTransitions;
            var listSchedules = new List<PeptideSchedule>();
            var listRequired = new List<PeptideSchedule>();
            var listUnscheduled = new List<PeptideSchedule>();
            foreach (PeptideGroupDocNode nodePepGroup in Document.MoleculeGroups)
            {
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    var peptideSchedule = new PeptideSchedule(nodePep, maxInstrumentTrans);
                    foreach (TransitionGroupDocNode nodeTranGroup in nodePep.Children)
                    {
                        double timeWindow;
                        double retentionTime = predict.PredictRetentionTime(Document, nodePep, nodeTranGroup, SchedulingReplicateIndex,
                            SchedulingAlgorithm, singleWindow, out timeWindow) ?? 0;
                        var nodeTranGroupPrimary = PrimaryTransitionCount > 0
                                                   ? nodePep.GetPrimaryResultsGroup(nodeTranGroup)
                                                   : null;

                        var ps = new PrecursorSchedule(nodePepGroup,
                                                       nodePep,
                                                       nodeTranGroup,
                                                       nodeTranGroupPrimary,
                                                       retentionTime,
                                                       timeWindow,
                                                       IsPrecursorLimited,
                                                       SchedulingReplicateIndex,
                                                       PrimaryTransitionCount,
                                                       OptimizeStepCount);
                        peptideSchedule.Add(ps);
                    }
                    if (RequiredPeptides.IsRequired(nodePep))
                    {
                        if (!peptideSchedule.CanSchedule)
                        {
                            throw new IOException(string.Format(Resources.AbstractMassListExporter_ExportScheduledBuckets_The_required_peptide__0__cannot_be_scheduled,
                                                                Document.Settings.GetDisplayName(nodePep)));
                        }
                        listRequired.Add(peptideSchedule);
                    }
                    else if (peptideSchedule.CanSchedule)
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
                // First add all required transitions
                foreach (var schedule in listRequired)
                    schedule.Schedule(listScheduleNext, MaxTransitions.Value);
                // Then try to add from the scheduling list
                foreach (var schedule in listSchedules)
                    schedule.Schedule(listScheduleNext, MaxTransitions.Value);
                // Throw an error if nothing beyond the required transitions could be added
                if (listScheduleNext.TransitionCount == RequiredPeptides.TransitionCount)
                {
                    string itemName = IsPrecursorLimited
                                          ? Resources.AbstractMassListExporter_ExportScheduledBuckets_precursors
                                          : Resources.AbstractMassListExporter_ExportScheduledBuckets_transitions;
                    var sb = new StringBuilder();
                    foreach (var peptideSchedule in listSchedules)
                    {
                        if (peptideSchedule.TransitionCount > MaxTransitions.Value)
                        {
                            sb.AppendLine(string.Format("{0} - {1} {2}", // Not L10N
                                peptideSchedule.Peptide.Peptide,
                                peptideSchedule.TransitionCount,
                                itemName));
                        }
                    }

                    var message = new StringBuilder(Resources.AbstractMassListExporter_ExportScheduledBuckets_Failed_to_schedule_the_following_peptides_with_the_current_settings);
                    message.AppendLine().AppendLine().AppendLine(sb.ToString());
                    if (OptimizeStepCount == 0)
                    {
                        message.AppendLine().AppendLine(string.Format(Resources.AbstractMassListExporter_ExportScheduledBuckets_Check_max_concurrent__0__count, itemName));
                    }
                    else
                    {
                        message.Append(string.Format(Resources.AbstractMassListExporter_ExportScheduledBuckets_Check_max_concurrent__0__count_and_optimization_step_count, itemName));
                    }
                    throw new IOException(message.ToString());
                }
                listScheduleBuckets.Add(listScheduleNext);
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
            if (!MaxTransitions.HasValue)
                throw new InvalidOperationException(Resources.AbstractMassListExporter_ExportScheduledBuckets_Maximum_transitions_per_file_required);

            foreach (var schedule in bucketOver.ToArray().RandomOrder())
            {
                // Required peptides may not be removed
                if (RequiredPeptides.IsRequired(schedule.Peptide))
                    continue;

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
                var nodeGroupPrimary = schedule.TransitionGroupPrimary;
                // Write required peptides at the end, like unscheduled methods
                if (RequiredPeptides.IsRequired(nodePep))
                    continue;

                // Skip percursors with too few transitions.
                int groupTransitions = nodeGroup.Children.Count;
                if (groupTransitions < MinTransitions)
                    continue;

                WriteTransitions(fileIterator, nodePepGroup, nodePep, nodeGroup, nodeGroupPrimary);
            }
            fileIterator.WriteRequiredTransitions(this, RequiredPeptides);
        }

        private void WriteTransitions(FileIterator fileIterator, PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionGroupDocNode nodeGroupPrimary)
        {
            // Allow derived classes a chance to reorder the transitions.  Currently only used by AB SCIEX.
            var reorderedTransitions = GetTransitionsInBestOrder(nodeGroup, nodeGroupPrimary);
            foreach (TransitionDocNode nodeTran in reorderedTransitions)
            {
                if (OptimizeType == null)
                {
                    fileIterator.WriteTransition(this, nodePepGroup, nodePep, nodeGroup, nodeGroupPrimary, nodeTran, 0);
                }
                else if (!SkipTransition(nodePepGroup, nodePep, nodeGroup, nodeGroupPrimary, nodeTran))
                {
                    // -step through step
                    bool transitionWritten = false;
                    for (int i = -OptimizeStepCount; i <= OptimizeStepCount; i++)
                    {
                        // But avoid writing zero or negative CE values, which will just mess up actuisition
                        // Always write the highest CE if no other transition has been written, even if it is
                        // negative, since not doing so makes it a lot harder to understand why a particular
                        // transition did not get written at all.
                        if (Equals(OptimizeType, ExportOptimize.CE) && GetCollisionEnergy(nodePep, nodeGroup, nodeTran, i) <= 0)
                        {
                            if (transitionWritten || i < OptimizeStepCount)
                                continue;
                        }
                        fileIterator.WriteTransition(this, nodePepGroup, nodePep, nodeGroup, nodeGroupPrimary, nodeTran, i);
                        transitionWritten = true;
                    }
                }
            }
        }

        protected virtual bool SkipTransition(PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, TransitionGroupDocNode nodeGroupPrimary, TransitionDocNode nodeTran)
        {
            return false;
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

            private int GetOverlapCount(IList<PeptideSchedule> peptideSchedules)
            {
                // While this may be less completely correct the less the precursors in a
                // peptide overlap, wildly different precursor peaks are not all that interesting.
                int maxOverlap = 0;
                foreach (var precursorSchedule in _precursorSchedules)
                {
                    maxOverlap = Math.Max(maxOverlap,
                        precursorSchedule.GetOverlapCount(GetPrecursorSchedules(peptideSchedules).ToArray()));
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
            public PrecursorSchedule(PeptideGroupDocNode nodePepGroup,
                                     PeptideDocNode nodePep,
                                     TransitionGroupDocNode nodeTranGroup,
                                     TransitionGroupDocNode nodePrimaryGroup,
                                     double retentionTime,
                                     double timeWindow,
                                     bool isPrecursorLimited,
                                     int? replicateIndex,
                                     int primaryTransitionCount,
                                     int optimizeStepCount)
                : base(nodeTranGroup, nodePrimaryGroup, retentionTime, timeWindow, isPrecursorLimited,
                       replicateIndex, primaryTransitionCount, optimizeStepCount)
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
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step);

        protected double GetProductMz(double productMz, int step)
        {
            return productMz + ChromatogramInfo.OPTIMIZE_SHIFT_SIZE * step;
        }

        protected double GetCollisionEnergy(PeptideDocNode nodePep,
                                            TransitionGroupDocNode nodeGroup,
                                            TransitionDocNode nodeTran,
                                            int step)
        {
            // If explicit CE, then no optimizing. Just return zero CE for anything but central transition
            var explicitCE = nodeGroup.ExplicitValues.CollisionEnergy;
            if (explicitCE.HasValue)
            {
                return step == 0 ? explicitCE.Value : 0;  // No optimizing of explicit values
            }

            if (OptimizeType == null)
            {
                double? optimizedCE = Document.GetOptimizedCollisionEnergy(nodePep, nodeGroup, nodeTran);
                if (optimizedCE.HasValue)
                    return optimizedCE.Value;
            }

            // If exporting optimization methods, or optimization data should be ignored,
            // use the regression setting to calculate CE
            if (!Equals(OptimizeType, ExportOptimize.CE))
                step = 0;

            return Document.GetCollisionEnergy(nodePep, nodeGroup, step);
        }

        protected double GetDeclusteringPotential(PeptideDocNode nodePep,
                                                  TransitionGroupDocNode nodeGroup,
                                                  TransitionDocNode nodeTran,
                                                  int step)
        {
            double? explicitDP = nodeGroup.ExplicitValues.DeclusteringPotential;
            if (explicitDP.HasValue)
            {
                return step == 0 ? explicitDP.Value : 0;  // No optimizing of explicit values
            }

            var prediction = Document.Settings.TransitionSettings.Prediction;

            // If exporting optimization methods, or optimization data should be ignored,
            // use the regression setting to calculate CE
            if (OptimizeType != null || prediction.OptimizedMethodType == OptimizedMethodType.None)
            {
                if (!Equals(OptimizeType, ExportOptimize.DP))
                    step = 0;
                return Document.GetDeclusteringPotential(nodePep, nodeGroup, step);
            }

            return Document.GetOptimizedDeclusteringPotential(nodePep, nodeGroup, nodeTran);
        }

        protected double GetCompensationVoltage(PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeGroup,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            double? explicitCV = nodeGroup.ExplicitValues.CompensationVoltage;
            if (explicitCV.HasValue)
            {
                return step == 0 ? explicitCV.Value : 0;  // No optimizing of explicit values
            }
            var prediction = Document.Settings.TransitionSettings.Prediction;
            return ExportOptimize.CompensationVoltageTuneTypes.Contains(OptimizeType) || prediction.OptimizedMethodType == OptimizedMethodType.None
                ? Document.GetCompensationVoltage(nodePep, nodeGroup, step, CompensationVoltageParameters.GetTuneLevel(OptimizeType))
                : Document.GetOptimizedCompensationVoltage(nodePep, nodeGroup);
        }

        protected int? GetRank(TransitionGroupDocNode nodeGroup,
                               TransitionGroupDocNode nodeGroupPrimary,
                               TransitionDocNode nodeTran)
        {
            return nodeGroup.GetRank(nodeGroupPrimary, nodeTran, SchedulingReplicateIndex);
        }

        protected virtual bool IsPrimary(TransitionGroupDocNode nodeGroup,
                                TransitionGroupDocNode nodeGroupPrimary,
                                TransitionDocNode nodeTran)
        {
            int? rank = GetRank(nodeGroup, nodeGroupPrimary, nodeTran);
            return (rank.HasValue && rank.Value <= PrimaryTransitionCount);
        }

        protected virtual IEnumerable<DocNode> GetTransitionsInBestOrder(TransitionGroupDocNode nodeGroup, TransitionGroupDocNode nodeGroupPrimary)
        {
            // most instruments do not care about the order of transitions, just return them in the same order
            return nodeGroup.Children;
        }

        private bool ExceedsMax(int count)
        {
            // Leave room for the required peptides
            count += RequiredPeptides.TransitionCount;

            return (MaxTransitions != null && count > 0 && count > MaxTransitions);
        }

        private static string FileEscape(IEnumerable<char> namePart)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in namePart)
            {
                if ("/\\:*?\"<>|".IndexOf(c) == -1) // Not L10N
                    sb.Append(c);
                else
                    sb.Append('_'); // Not L10N
            }
            return sb.ToString();
        }

        private sealed class FileIterator : IDisposable
        {
            private FileSaver _saver;
            private TextWriter _writer;
            private readonly bool _single;
            private readonly bool _isPrecursorLimited;
            private readonly Action<TextWriter> _writeHeaders;

            private TransitionGroupDocNode _nodeGroupLast;

            public FileIterator(string fileName, bool single, bool isPrecursorLimited, Action<TextWriter> writeHeaders)
            {
                FileName = fileName;
                _single = single;
                _isPrecursorLimited = isPrecursorLimited;
                _writeHeaders = writeHeaders;
                if (fileName == null)
                {
                    BaseName = MEMORY_KEY_ROOT;
                    MemoryOutput = new Dictionary<string, StringBuilder>();
                }
                else
                {
                    BaseName = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty,
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
                        if (!_saver.CanSave())
                            throw new IOException(string.Format(Resources.FileIterator_Init_Cannot_save_to__0__, FileName));

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
                    baseName = string.Format("{0}_{1:0000}", BaseName, FileCount); // Not L10N
                else
                    baseName = string.Format("{0}_{1:0000}_{2}", BaseName, FileCount, Suffix); // Not L10N

                if (MemoryOutput == null)
                {
                    _saver = new FileSaver(baseName + ".csv"); // Not L10N
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

            public void WriteTransition(AbstractMassListExporter exporter,
                                        PeptideGroupDocNode seq,
                                        PeptideDocNode peptide,
                                        TransitionGroupDocNode group,
                                        TransitionGroupDocNode groupPrimary,
                                        TransitionDocNode transition,
                                        int step)
            {
                if (!HasFile)
                    throw new IOException(Resources.FileIterator_WriteTransition_Unexpected_failure_writing_transitions);

                exporter.WriteTransition(_writer, seq, peptide, group, groupPrimary, transition, step);

                // If not full-scan, count transtions
                if (!_isPrecursorLimited)
                    TransitionCount++;
                // Otherwise, count precursors
                else if (!ReferenceEquals(_nodeGroupLast, group))
                {
                    TransitionCount++;
                    _nodeGroupLast = group;
                }
            }

            public void WriteRequiredTransitions(AbstractMassListExporter exporter, RequiredPeptideSet requiredPeptides)
            {
                foreach (var requiredPeptide in requiredPeptides.Peptides)
                {
                    var seq = requiredPeptide.PeptideGroupNode;
                    var peptide = requiredPeptide.PeptideNode;

                    foreach (var group in peptide.TransitionGroups)
                    {
                        foreach (var transition in group.Transitions)
                        {
                            WriteTransition(exporter, seq, peptide, group, null, transition, 0);
                        }
                    }
                }
            }
        }
    }

    internal class PrecursorScheduleBase
    {
        public PrecursorScheduleBase(TransitionGroupDocNode nodeGroup,
                                     TransitionGroupDocNode nodeGroupPrimary,
                                     double retentionTime,
                                     double timeWindow,
                                     bool isPrecursorLimited,
                                     int? replicateIndex,
                                     int primaryTransitionCount,
                                     int optimizeStepCount)
        {
            TransitionGroup = nodeGroup;
            TransitionGroupPrimary = nodeGroupPrimary;
            StartTime = retentionTime - (timeWindow / 2);
            EndTime = StartTime + timeWindow;
            IsPrecursorLimited = isPrecursorLimited;
            ReplicateIndex = replicateIndex;
            PrimaryTransitionCount = primaryTransitionCount;
            OptimizeStepCount = optimizeStepCount;

            if (IsPrecursorLimited)
                TransitionCount = 1;
            else if (PrimaryTransitionCount == 0)
                TransitionCount = TransitionGroup.TransitionCount;
            else
            {
                // Figure out how many transitions actually meet the rank
                // restriction for primary transitions.  Secondary transitions
                // are not considered for scheduling.
                TransitionCount = TransitionGroup.Transitions.Where(IsPrimary).Count();
            }
            TransitionCount *= (OptimizeStepCount * 2 + 1);
        }

        public TransitionGroupDocNode TransitionGroup { get; private set; }
        public TransitionGroupDocNode TransitionGroupPrimary { get; private set; }
        public int TransitionCount { get; private set; }
        public double StartTime { get; private set; }
        public double EndTime { get; private set; }
        public bool IsPrecursorLimited { get; private set; }
        public int? ReplicateIndex { get; private set; }
        public int PrimaryTransitionCount { get; private set; }
        public int OptimizeStepCount { get; private set; }

        public bool ContainsTime(double time)
        {
            return StartTime <= time && time <= EndTime;
        }

        public bool IsPrimary(TransitionDocNode nodeTran)
        {
            int? rank = TransitionGroup.GetRank(TransitionGroupPrimary, nodeTran, ReplicateIndex);
            return rank.HasValue && rank <= PrimaryTransitionCount;
        }

        public int GetOverlapCount<TBase>(IList<TBase> schedules)
            where TBase : PrecursorScheduleBase
        {
            // Check for maximum overlap count at start and end times of this
            // schedule window, and any other start or end time that falls within
            // this schedule window.
            List<double> times = new List<double> { StartTime, EndTime };
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
        public static int GetOverlapCount<TBase>(IEnumerable<TBase> schedules, double time)
            where TBase : PrecursorScheduleBase
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
}
