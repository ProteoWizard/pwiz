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
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{

// ReSharper disable InconsistentNaming
    public enum ExportStrategy { Single, Protein, Buckets }
    public enum ExportMethodType { Standard, Scheduled }
    public enum ExportSchedulingAlgorithm { Average, Trends, Single }
    public enum ExportFileType { List, Method }
    public static class ExportInstrumentType
    {
        public const string ABI = "AB SCIEX";
        public const string ABI_QTRAP = "AB SCIEX QTRAP";
        public const string ABI_TOF = "AB SCIEX TOF";
        public const string Agilent = "Agilent";
        public const string Agilent6400 = "Agilent 6400 Series";
        public const string Thermo = "Thermo";
        public const string Thermo_TSQ = "Thermo TSQ";
        public const string Thermo_LTQ = "Thermo LTQ";
        public const string Waters = "Waters";
        public const string Waters_Xevo = "Waters Xevo";
        public const string Waters_Quattro_Premier = "Waters Quattro Premier";

        public const string EXT_AB_SCIEX = ".dam";
        public const string EXT_AGILENT = ".m";
        public const string EXT_THERMO = ".meth";
        public const string EXT_WATERS = ".exp";

        public static readonly string[] METHOD_TYPES =
            {
                ABI_QTRAP,
                ABI_TOF,
                Agilent6400,
                Thermo_TSQ,
                Thermo_LTQ,
                Waters_Xevo,
                Waters_Quattro_Premier,
            };

        public static readonly string[] TRANSITION_LIST_TYPES =
            {
                ABI,
                Agilent,
                Thermo,
                Waters
            };

        private readonly static Dictionary<string, string> MethodExtensions;

        static ExportInstrumentType()
        {
            MethodExtensions = new Dictionary<string, string>
                                   {
                                       {ABI_QTRAP, EXT_AB_SCIEX},
                                       {ABI_TOF, EXT_AB_SCIEX},
                                       {Agilent6400, EXT_AGILENT},
                                       {Thermo_TSQ, EXT_THERMO},
                                       {Thermo_LTQ, EXT_THERMO},
                                       {Waters_Xevo, EXT_WATERS},
                                       {Waters_Quattro_Premier, EXT_WATERS}
                                   };
        }

        /// <summary>
        /// Returns the method file extension associated with the given instrument.
        /// If the given instrument is not in METHOD_TYPES, the string returned
        /// will be null.
        /// </summary>
        public static string MethodExtension(string instrument)
        {
            string ext;
            return MethodExtensions.TryGetValue(instrument, out ext) ? ext : null;
        }

        public static bool IsFullScanInstrumentType(string type)
        {
            return Equals(type, Thermo_LTQ) ||
                   Equals(type, ABI_TOF);
        }

        public static bool IsPrecursorOnlyInstrumentType(string type)
        {
            return Equals(type, Thermo_LTQ) ||
                   Equals(type, ABI_TOF);
        }

        public static bool CanScheduleInstrumentType(string type, SrmDocument doc)
        {
            return !(Equals(type, Thermo_LTQ) || Equals(type, ABI_TOF))|| IsInclusionListMethod(doc);
        }

        public static bool IsInclusionListMethod(SrmDocument doc)
        {
            return (doc.Settings.TransitionSettings.FullScan.IsEnabledMs &&
                !doc.Settings.TransitionSettings.FullScan.IsEnabledMsMs);
        }

        public static bool CanSchedule(string instrumentType, SrmDocument doc)
        {
            return CanScheduleInstrumentType(instrumentType, doc) &&
                   doc.Settings.PeptideSettings.Prediction.CanSchedule(doc,
                                                                       IsSingleWindowInstrumentType(instrumentType));
        }

        public static bool IsSingleWindowInstrumentType(string type)
        {
            return Equals(type, ABI) ||
                   Equals(type, ABI_QTRAP) ||
                   Equals(type, Waters) ||
                   Equals(type, Waters_Xevo) ||
                   Equals(type, Waters_Quattro_Premier);
        }
    }

    public abstract class ExportProperties
    {
        public virtual ExportStrategy ExportStrategy { get; set; }
        public virtual bool IgnoreProteins { get; set; }
        public virtual int? MaxTransitions { get; set; }
        public virtual ExportMethodType MethodType { get; set; }
        public virtual string OptimizeType { get; set; }
        public virtual double OptimizeStepSize { get; set; }
        public virtual int OptimizeStepCount { get; set; }
        public virtual int SchedulingReplicateNum { get; set; }
        public virtual ExportSchedulingAlgorithm SchedulingAlgorithm { get; set; }

        public virtual int DwellTime { get; set; }
        public virtual bool AddEnergyRamp { get; set; }
        public virtual bool AddTriggerReference { get; set; }
        public virtual double RunLength { get; set; }
        public virtual bool FullScans { get; set; }

        public virtual bool Ms1Scan { get; set; }
        public virtual bool InclusionList { get; set; }
        public virtual string MsAnalyzer { get; set; }
        public virtual string MsMsAnalyzer { get; set; }

        public TExp InitExporter<TExp>(TExp exporter)
            where TExp : MassListExporter
        {
            exporter.Strategy = ExportStrategy;
            exporter.IgnoreProteins = IgnoreProteins;
            exporter.InclusionList = InclusionList;
            exporter.MaxTransitions = MaxTransitions;
            exporter.MethodType = MethodType;
            exporter.Ms1Scan = Ms1Scan;
            exporter.MsAnalyzer = MsAnalyzer;
            exporter.MsMsAnalyzer = MsMsAnalyzer;
            exporter.OptimizeType = OptimizeType;
            exporter.OptimizeStepSize = OptimizeStepSize;
            exporter.OptimizeStepCount = OptimizeStepCount;
            exporter.SchedulingReplicateIndex = SchedulingReplicateNum;
            exporter.SchedulingAlgorithm = SchedulingAlgorithm;
            return exporter;
        }

        public MassListExporter ExportFile(string instrumentType, ExportFileType type, string path, SrmDocument doc, string template)
        {
            switch (instrumentType)
            {
                case ExportInstrumentType.ABI:
                case ExportInstrumentType.ABI_QTRAP:
                    if (type == ExportFileType.List)
                        return ExportAbiCsv(doc, path);
                    else
                        return ExportAbiQtrapMethod(doc, path, template);
                case ExportInstrumentType.ABI_TOF:
                    OptimizeType = null;
                    return ExportAbiTofMethod(doc, path, template);
                case ExportInstrumentType.Agilent:
                case ExportInstrumentType.Agilent6400:
                    if (type == ExportFileType.List)
                        return ExportAgilentCsv(doc, path);
                    else
                        return ExportAgilentMethod(doc, path, template);
                case ExportInstrumentType.Thermo:
                case ExportInstrumentType.Thermo_TSQ:
                    if (type == ExportFileType.List)
                        return ExportThermoCsv(doc, path);
                    else
                        return ExportThermoMethod(doc, path, template);
                case ExportInstrumentType.Thermo_LTQ:
                    OptimizeType = null;
                    return ExportThermoLtqMethod(doc, path, template);
                case ExportInstrumentType.Waters:
                case ExportInstrumentType.Waters_Xevo:
                    if (type == ExportFileType.List)
                        return ExportWatersCsv(doc, path);
                    else
                        return ExportWatersMethod(doc, path, template);
                case ExportInstrumentType.Waters_Quattro_Premier:
                    return ExportWatersQMethod(doc, path, template);
                default:
                    throw new InvalidOperationException(string.Format("Unrecognized instrument type {0}.", instrumentType));
            }
        }

        public MassListExporter ExportAbiCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new AbiMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            exporter.Export(fileName);

            return exporter;
        }

        public MassListExporter ExportAbiQtrapMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new AbiQtrapMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public MassListExporter ExportAbiTofMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new AbiTofMethodExporter(document));

            exporter.FullScans = true;
            
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public MassListExporter ExportAgilentCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new AgilentMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            exporter.Export(fileName);

            return exporter;
        }

        public MassListExporter ExportAgilentMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new AgilentMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public MassListExporter ExportThermoCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new ThermoMassListExporter(document));
            exporter.AddEnergyRamp = AddEnergyRamp;
            exporter.AddTriggerReference = AddTriggerReference;
            exporter.Export(fileName);

            return exporter;
        }

        public MassListExporter ExportThermoMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public MassListExporter ExportThermoLtqMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoLtqMethodExporter(document));
            exporter.FullScans = FullScans;
            exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public MassListExporter ExportWatersCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new WatersMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.Export(fileName);

            return exporter;
        }

        public MassListExporter ExportWatersMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new WatersMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public MassListExporter ExportWatersQMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new WatersMethodExporter(document)
            {
                MethodInstrumentType = ExportInstrumentType.Waters_Quattro_Premier
            });
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public abstract void PerformLongExport(Action<IProgressMonitor> performExport);
    }

    public static class ExportOptimize
    {
        public const string NONE = "None";
        public const string CE = "Collision Energy";
        public const string DP = "Declustering Potential";

        public static string[] OptimizeTypes = {NONE, CE, DP};
    }

// ReSharper restore InconsistentNaming
    public abstract class MassListExporter
    {
        public const int DWELL_TIME_MIN = 1;
        public const int DWELL_TIME_MAX = 1000;
        public const int DWELL_TIME_DEFAULT = 20;

        public const int RUN_LENGTH_MIN = 5;
        public const int RUN_LENGTH_MAX = 500;
        public const int RUN_LENGTH_DEFAULT = 60;

        public const int MAX_TRANS_PER_INJ_DEFAULT = 130;
        public const int MAX_TRANS_PER_INJ_MIN = 2;

        public const string MEMORY_KEY_ROOT = "memory";

        protected MassListExporter(SrmDocument document, DocNode node)
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
        public int? MaxTransitions { get; set; }
        public int MinTransitions { get; set; }
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
            }
        }
        public char FieldSeparator { get; private set; }

        public Dictionary<string, StringBuilder> MemoryOutput { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        protected bool InitExport(string fileName, IProgressMonitor progressMonitor)
        {
            if (progressMonitor.IsCanceled)
                return false;

            // First export transition lists to map in memory
            Export(null);

            // If filename is null, then no more work needs to be done.
            if (fileName == null)
            {
                progressMonitor.UpdateProgress(new ProgressStatus("").Complete());
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
                throw new IOException(string.Format("The number of required transitions {0} exceeds the maximum {1}", RequiredPeptides.TransitionCount, MaxTransitions));

            using (var fileIterator = new FileIterator(fileName, single, IsPrecursorLimited, WriteHeaders))
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

        private void NextFile(FileIterator fileIterator)
        {
            if (fileIterator.HasFile)
                fileIterator.WriteRequiredTransitions(this, RequiredPeptides);
            fileIterator.NextFile();
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
                var settings = document.Settings;
                if (settings.PeptideSettings.Prediction.RetentionTime == null)
                    _peptides = new RequiredPeptide[0];
                else
                {
                    var setRegression = document.GetRetentionTimeStandards();
                    _peptides = (from nodePepGroup in document.PeptideGroups
                                 from nodePep in nodePepGroup.Peptides
                                 where setRegression.Contains(settings.GetModifiedSequence(nodePep))
                                 select new RequiredPeptide(nodePepGroup, nodePep))
                        .ToArray();
                }
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
            return transitionNodes*(OptimizeStepCount*2 + 1);
        }

        private void ExportScheduledBuckets(FileIterator fileIterator)
        {
            if (!MaxTransitions.HasValue)
                throw new InvalidOperationException("Maximum transitions per file required");

            bool singleWindow = ExportInstrumentType.IsSingleWindowInstrumentType(InstrumentType);

            var predict = Document.Settings.PeptideSettings.Prediction;
            int? maxInstrumentTrans = null;
            if (!IsPrecursorLimited)
                maxInstrumentTrans = Document.Settings.TransitionSettings.Instrument.MaxTransitions;
            var listSchedules = new List<PeptideSchedule>();
            var listRequired = new List<PeptideSchedule>();
            var listUnscheduled = new List<PeptideSchedule>();
            foreach (PeptideGroupDocNode nodePepGroup in Document.PeptideGroups)
            {
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    var peptideSchedule = new PeptideSchedule(nodePep, maxInstrumentTrans);
                    foreach (TransitionGroupDocNode nodeTranGroup in nodePep.Children)
                    {
                        double timeWindow;
                        double? retentionTime = predict.PredictRetentionTime(Document, nodePep, nodeTranGroup, SchedulingReplicateIndex,
                            SchedulingAlgorithm, singleWindow, out timeWindow);
                        if (retentionTime.HasValue)
                        {
                            peptideSchedule.Add(new PrecursorSchedule(nodePepGroup, nodePep, nodeTranGroup,
                                retentionTime.Value, timeWindow, IsPrecursorLimited, OptimizeStepCount));
                        }
                        else
                        {
                            peptideSchedule.Add(new PrecursorSchedule(nodePepGroup, nodePep, nodeTranGroup,
                                0, 0, IsPrecursorLimited, OptimizeStepCount));
                        }
                    }
                    if (RequiredPeptides.IsRequired(nodePep))
                    {
                        if (!peptideSchedule.CanSchedule)
                            throw new IOException(string.Format("The required peptide {0} cannot be scheduled", Document.Settings.GetModifiedSequence(nodePep)));
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
                    string itemName = IsPrecursorLimited ? "precursors" : "transitions";
                    var sb = new StringBuilder();
                    foreach (var peptideSchedule in listSchedules)
                    {
                        if (peptideSchedule.TransitionCount > MaxTransitions.Value)
                        {
                            sb.AppendLine(string.Format("{0} - {1} {2}",
                                peptideSchedule.Peptide.Peptide,
                                peptideSchedule.TransitionCount,
                                itemName));
                        }
                    }
                    if (OptimizeStepCount == 0)
                        throw new IOException(string.Format("Failed to schedule the following peptides with the current settings:\n\n{0}\n\nCheck max concurrent {1} count.", sb, itemName));
                    else
                        throw new IOException(string.Format("Failed to schedule the following peptides with the current settings:\n\n{0}\nCheck max concurrent {1} count and optimization step count.", sb, itemName));
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
                throw new InvalidOperationException("Maximum transitions per file required");

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
                // Write required peptides at the end, like unscheduled methods
                if (RequiredPeptides.IsRequired(nodePep))
                    continue;

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
            fileIterator.WriteRequiredTransitions(this, RequiredPeptides);
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
            public PrecursorSchedule(PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep,
                    TransitionGroupDocNode nodeTranGroup, double retentionTime, double timeWindow,
                    bool isPrecursorLimited, int optimizeStepCount)
                : base(nodeTranGroup, retentionTime, timeWindow, isPrecursorLimited, optimizeStepCount)
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

        private sealed class OptimizationStep<TReg>
            where TReg : OptimizableRegression
        {
            private OptimizationStep(TReg regression, int step)
            {
                Regression = regression;
                Step = step;
            }

            private TReg Regression { get; set; }
            private int Step { get; set; }
            private double TotalArea { get; set; }

            private void AddArea(double area)
            {
                TotalArea += area;
            }

            public delegate double GetRegressionValue(SrmDocument document, PeptideDocNode nodePep,
                                                      TransitionGroupDocNode nodeGroup, TReg regression, int step);

            public static double FindOptimizedValue(SrmDocument document,
                                                 PeptideDocNode nodePep,
                                                 TransitionGroupDocNode nodeGroup,
                                                 TransitionDocNode nodeTran,
                                                 OptimizedMethodType methodType,
                                                 TReg regressionDocument,
                                                 GetRegressionValue getRegressionValue)
            {
                // Collect peak area for 
                var dictOptTotals = new Dictionary<TReg, Dictionary<int, OptimizationStep<TReg>>>();
                if (document.Settings.HasResults)
                {
                    var chromatograms = document.Settings.MeasuredResults.Chromatograms;
                    for (int i = 0; i < chromatograms.Count; i++)
                    {
                        var chromSet = chromatograms[i];
                        var regression = chromSet.OptimizationFunction as TReg;
                        if (regression == null)
                            continue;

                        Dictionary<int, OptimizationStep<TReg>> stepAreas;
                        if (!dictOptTotals.TryGetValue(regression, out stepAreas))
                            dictOptTotals.Add(regression, stepAreas = new Dictionary<int, OptimizationStep<TReg>>());

                        if (methodType == OptimizedMethodType.Precursor)
                        {
                            TransitionGroupDocNode[] listGroups = FindCandidateGroups(nodePep, nodeGroup);
                            foreach (var nodeGroupCandidate in listGroups)
                                AddOptimizationStepAreas(nodeGroupCandidate, i, regression, stepAreas);
                        }
                        else if (methodType == OptimizedMethodType.Transition)
                        {
                            IEnumerable<TransitionDocNode> listTransitions = FindCandidateTransitions(nodePep, nodeGroup, nodeTran);
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

            private static IEnumerable<TransitionDocNode> FindCandidateTransitions(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
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

            private static void AddOptimizationStepAreas(TransitionGroupDocNode nodeGroup, int iResult, TReg regression,
                IDictionary<int, OptimizationStep<TReg>> optTotals)
            {
                var results = (nodeGroup.HasResults ? nodeGroup.Results[iResult] : null);
                if (results == null)
                    return;
                foreach (var chromInfo in results)
                {
                    if (!chromInfo.Area.HasValue)
                        continue;
                    int step = chromInfo.OptimizationStep;
                    OptimizationStep<TReg> optStep;
                    if (!optTotals.TryGetValue(step, out optStep))
                        optTotals.Add(step, optStep = new OptimizationStep<TReg>(regression, step));
                    optStep.AddArea(chromInfo.Area.Value);
                }
            }

            private static void AddOptimizationStepAreas(TransitionDocNode nodeTran, int iResult, TReg regression,
                IDictionary<int, OptimizationStep<TReg>> optTotals)
            {
                var results = (nodeTran.HasResults ? nodeTran.Results[iResult] : null);
                if (results == null)
                    return;
                foreach (var chromInfo in results)
                {
                    if (chromInfo.Area == 0)
                        continue;
                    int step = chromInfo.OptimizationStep;
                    OptimizationStep<TReg> optStep;
                    if (!optTotals.TryGetValue(step, out optStep))
                        optTotals.Add(step, optStep = new OptimizationStep<TReg>(regression, step));
                    optStep.AddArea(chromInfo.Area);
                }
            }
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
                    BaseName = Path.Combine(Path.GetDirectoryName(fileName) ?? "",
                        Path.GetFileNameWithoutExtension(fileName) ?? "");
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

            public void WriteRequiredTransitions(MassListExporter exporter, RequiredPeptideSet requiredPeptides)
            {
                foreach (var requiredPeptide in requiredPeptides.Peptides)
                {
                    var seq = requiredPeptide.PeptideGroupNode;
                    var peptide = requiredPeptide.PeptideNode;

                    foreach (var group in peptide.TransitionGroups)
                    {
                        foreach (var transition in group.Transitions)
                        {
                            WriteTransition(exporter, seq, peptide, group, transition, 0);
                        }
                    }
                }
            }
        }
    }

    internal class PrecursorScheduleBase
    {
        public PrecursorScheduleBase(TransitionGroupDocNode nodeGroup, double retentionTime,
            double timeWindow, bool isPrecursorLimited, int optimizeStepCount)
        {
            TransitionGroup = nodeGroup;
            StartTime = retentionTime - (timeWindow / 2);
            EndTime = StartTime + timeWindow;
            IsPrecursorLimited = isPrecursorLimited;
            OptimizeStepCount = optimizeStepCount;
        }

        public TransitionGroupDocNode TransitionGroup { get; private set; }
        public int TransitionCount
        {
            get
            {
                int count = IsPrecursorLimited ? 1 : TransitionGroup.TransitionCount;
                return count*(OptimizeStepCount*2 + 1);
            }
        }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public bool IsPrecursorLimited { get; set; }
        public int OptimizeStepCount { get; set; }

        public bool ContainsTime(double time)
        {
            return StartTime <= time && time <= EndTime;
        }

        public int GetOverlapCount<TBase>(IList<TBase> schedules)
            where TBase : PrecursorScheduleBase
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

    public class ThermoMassListExporter : MassListExporter
    {
        private bool _addTriggerReference;
        private HashSet<string> _setRTStandards;

        public ThermoMassListExporter(SrmDocument document)
            : base(document, null)
        {
            _setRTStandards = new HashSet<string>();
        }

        public bool AddEnergyRamp { get; set; }
        
        public bool AddTriggerReference
        {
            get { return _addTriggerReference; }
            set
            {
                _addTriggerReference = value;
                if (_addTriggerReference)
                    _setRTStandards = Document.GetRetentionTimeStandards();
            }
        }

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
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);
                predictedRT = RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT);
                // Start Time and Stop Time
                if (predictedRT.HasValue)
                {
                    writer.Write(Math.Max(0, predictedRT.Value - windowRT / 2).ToString(CultureInfo));    // No negative retention times
                    writer.Write(FieldSeparator);
                    writer.Write((predictedRT.Value + windowRT / 2).ToString(CultureInfo));
                    writer.Write(FieldSeparator);
                }
                else
                {
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                }
                writer.Write('1');  // Polarity
                writer.Write(FieldSeparator);                    

                if (AddTriggerReference)
                {
                    if (_setRTStandards.Contains(Document.Settings.GetModifiedSequence(nodePep)))
                    {
                        writer.Write("1000");  // Trigger
                        writer.Write(FieldSeparator);
                        writer.Write("1");  // Reference
                        writer.Write(FieldSeparator);
                    }
                    else
                    {
                        writer.Write("1.0E+10");  // Trigger
                        writer.Write(FieldSeparator);
                        writer.Write("0");  // Reference
                        writer.Write(FieldSeparator);
                    }
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
            if (!InitExport(fileName, progressMonitor))
                return;

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
            // The LTQ is always precursor limited even when exporting pseudo-SRM
            IsPrecursorLimited = true;
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            var argv = new List<string>();
            if (FullScans)
                argv.Add("-f");
            if(MsAnalyzer != null)
                argv.Add(String.Format("-a {0}", MsAnalyzer));
            if(MsMsAnalyzer != null)
                argv.Add(String.Format("-b {0}", MsMsAnalyzer));
            if(InclusionList)
                argv.Add("-i");
            if(Ms1Scan)
                argv.Add("-1");
            MethodExporter.ExportMethod(EXE_BUILD_LTQ_METHOD, argv,
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
        protected double? RTWindow { get; private set; }

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
                    SchedulingReplicateIndex, SchedulingAlgorithm, HasResults, out windowRT);
                if (predictedRT.HasValue)
                {
                    RTWindow = windowRT; // Store for later use
                    writer.Write((RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT) ?? 0).ToString(CultureInfo));
                }
            }
            writer.Write(FieldSeparator);

            // Write special ID for AB software
            string extPeptideId = string.Format("{0}.{1}.{2}{3}.{4}",
                                                nodePepGroup.Name,
                                                nodePep.Peptide.Sequence,
                                                nodeTran.HasLibInfo ? nodeTran.LibInfo.Rank.ToString(CultureInfo.InvariantCulture) : "",
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

    public abstract class AbiMethodExporter : AbiMassListExporter
    {

        private const string ANALYST_NAME = "Analyst";
        private const string ANALYST_EXE = ANALYST_NAME + ".exe";

        protected AbiMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        protected abstract string GetRegQueryKey();

        protected abstract string GetExeName();

        protected abstract string GetAnalystVersions();

        protected abstract List<string> getArgs();

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (fileName != null)
                EnsureAnalyst(progressMonitor);

            if (!InitExport(fileName, progressMonitor))
                return;

            MethodExporter.ExportMethod(GetExeName(),
                getArgs(), fileName, templateName, MemoryOutput, progressMonitor);
        }

        private void EnsureAnalyst(IProgressMonitor progressMonitor)
        {
            string analystPath = AdvApi.GetPathFromProgId("Analyst.MassSpecMethod.1");
            string analystDir = (analystPath != null ? Path.GetDirectoryName(analystPath) : null);

            if (analystDir != null)
            {
                string ver = AdvApi.RegQueryKeyValue(AdvApi.HKEY_LOCAL_MACHINE, GetRegQueryKey(), "Version");
                
                if (string.IsNullOrEmpty(ver) || !GetAnalystVersions().Contains(ver))
                    analystDir = null;
            }
            if (analystDir == null)
            {
                throw new IOException(String.Format("Failed to find a valid Analyst {0} installation.", GetAnalystVersions()));
            }


            var procAnalyst = AnalystProcess ?? Process.Start(Path.Combine(analystDir, ANALYST_EXE));
            // Wait for main window to be present.
            ProgressStatus status = null;
            while (!progressMonitor.IsCanceled &&
                    !Equals(ANALYST_NAME, procAnalyst != null ? procAnalyst.MainWindowTitle : ""))
            {
                if (status == null)
                {
                    status = new ProgressStatus("Waiting for Analyst to start...").ChangePercentComplete(-1);
                    progressMonitor.UpdateProgress(status);
                }
                Thread.Sleep(500);
                procAnalyst = AnalystProcess;
            }
            if (status != null)
            {
                // Wait an extra 1.5 seconds, if the Analyst window was not already present
                // to make sure it is really completely started.
                Thread.Sleep(1500);
                progressMonitor.UpdateProgress(status.ChangeMessage("Working..."));
            }    
        }

        private static Process AnalystProcess
        {
            get
            {
                var processList = Process.GetProcesses();
                int indexAnalyst = processList.IndexOf(proc => Equals(ANALYST_EXE, GetModuleName(proc)));
                return (indexAnalyst != -1 ? processList[indexAnalyst] : null);
            }
        }

        private static string GetModuleName(Process proc)
        {
            try
            {
                return proc.MainModule.ModuleName;
            }
            catch
            {
                return null;
            }
        }
        
    }
    public class AbiQtrapMethodExporter : AbiMethodExporter
    {
        
        public AbiQtrapMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        protected override string GetRegQueryKey()
        {
            return @"SOFTWARE\PE SCIEX\Products\Analyst3Q";
        }

        protected override string GetExeName()
        {
            return @"Method\AbSciex\TQ\BuildQTRAPMethod";
        }

        protected override string GetAnalystVersions()
        {
            return "1.5.1 or 1.5.2";
        }

        protected override List<string> getArgs()
        {
            var argv = new List<string>();
            if (RTWindow.HasValue)
            {
                argv.Add("-w");
                argv.Add(RTWindow.Value.ToString(CultureInfo.InvariantCulture));
            }
            return argv;
        }
    }

    public class AbiTofMethodExporter : AbiMethodExporter
    {

        public AbiTofMethodExporter(SrmDocument document)
            : base(document)
        {
            IsPrecursorLimited = true;
        }

        protected override string GetRegQueryKey()
        {
            return @"SOFTWARE\PE SCIEX\Products\AnalystQS";
        }

        protected override string GetExeName()
        {
            return @"Method\AbSciex\TOF\BuildAnalystFullScanMethod";
        }

        protected override string GetAnalystVersions()
        {
            return "TF1.5 or TF1.5.1 or 2.0";
        }

        protected override List<string> getArgs()
        {
            /*
            *  These are the command-line options specific to ABI TOF method builders
               "   -1               Do an MS1 scan each cycle" +
               "   -i               Generate method for Information Dependent Acquisition (IDA)" +
               "   -r               Add retention time information to inclusion list (requires -i)\n" +
            */
            var argv = new List<string>();
            if (Ms1Scan)
                argv.Add("-1");
            if (InclusionList)
                argv.Add("-i");
            if (MethodType == ExportMethodType.Scheduled)
                argv.Add("-r");

            return argv;
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
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);

                if (predictedRT.HasValue)
                {
                    writer.Write((RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT) ?? 0).ToString(CultureInfo));
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

    public class AgilentMethodExporter : AgilentMassListExporter
    {
        public const string EXE_BUILD_AGILENT_METHOD = @"Method\Agilent\BuildAgilentMethod";

        public AgilentMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            MethodExporter.ExportMethod(EXE_BUILD_AGILENT_METHOD,
                new List<string>(), fileName, templateName, MemoryOutput, progressMonitor);
        }

        public static bool IsAgilentMethodPath(string methodPath)
        {
            return methodPath.EndsWith(".m") && File.Exists(Path.Combine(methodPath, "qqqacqmeth.xsd"));
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
                    SchedulingReplicateIndex, SchedulingAlgorithm, HasResults, out windowRT);
                if (predictedRT.HasValue)
                {
                    RTWindow = windowRT;    // Store for later use
                    writer.Write((RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT) ?? 0).ToString(CultureInfo));
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
            if(fileName != null)
                EnsureLibraries();

            if (!InitExport(fileName, progressMonitor))
                return;

            var argv = new List<string>();
            if (Equals(MethodInstrumentType, ExportInstrumentType.Waters_Quattro_Premier))
                argv.Add("-q");
            argv.Add("-w");
            argv.Add(RTWindow.ToString(CultureInfo.InvariantCulture));
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
            string buildSubdir = Path.GetDirectoryName(EXE_BUILD_WATERS_METHOD) ?? "";
            string exeDir = Path.Combine(Path.GetDirectoryName(skylinePath) ?? "", buildSubdir);
            string dacServerPath = AdvApi.GetPathFromProgId("DACScanStats.DACScanStats");
            if (dacServerPath == null)
            {
                // If all the necessary libraries exist, then continue even if MassLynx is gone.
                foreach (var libraryName in DEPENDENCY_LIBRARIES)
                {
                    if (!File.Exists(Path.Combine(exeDir, libraryName)))
                        throw new IOException("Failed to find a valid MassLynx installation.");
                }
                return;
            }

            string massLynxDir = Path.GetDirectoryName(dacServerPath) ?? "";
            foreach (var library in DEPENDENCY_LIBRARIES)
            {
                string srcFile = Path.Combine(massLynxDir, library);
                if (!File.Exists(srcFile))
                    throw new IOException(string.Format("MassLynx may not be installed correctly.  The library {0} could not be found.", library));
                // If destination file does not exist or has a different modification time from
                // the source, then copy the source file from the MassLynx installation.
                string destFile = Path.Combine(exeDir, library);
                if (!File.Exists(destFile) || !Equals(File.GetLastWriteTime(destFile), File.GetLastWriteTime(srcFile)))
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
            return RegQueryKeyValue(hKey, path, "");
        }

        public static string RegQueryKeyValue(UIntPtr hKey, string path, string valueName)
        {
            UIntPtr hKeyQuery;
            if (RegOpenKeyEx(hKey, path, 0, KEY_READ, out hKeyQuery) != 0)
                return null;

            uint size = 1024;
            StringBuilder sb = new StringBuilder(1024);

            try
            {
                uint type;
                if (RegQueryValueEx(hKeyQuery, valueName, 0, out type, sb, ref size) != 0)
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

        public const int MAX_TRANS_PER_INJ_MIN_TLTQ = 10;

        public static void ExportMethod(string exeName,
                                        List<string> argv,
                                        string fileName,
                                        string templateName,
                                        Dictionary<string, StringBuilder> dictTranLists,
                                        IProgressMonitor progressMonitor)
        {
            string baseName = Path.Combine(Path.GetDirectoryName(fileName) ?? "",
                                           Path.GetFileNameWithoutExtension(fileName) ?? "");
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
                    WorkingDirectory = dirWork ?? "",
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