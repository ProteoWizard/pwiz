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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model.DocSettings;
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
                   Equals(type, ExportInstrumentType.Waters);
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

        // TODO: Persist transition lists with correct number format
        private CultureInfo _cultureInfo;
        public CultureInfo CultureInfo
        {
            get { return _cultureInfo; }
            set
            {
                _cultureInfo = value;
                FieldSeparator = Equals(",", _cultureInfo.NumberFormat.NumberDecimalSeparator) ?
                    ';' : ',';
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
            var listSchedules = new List<PrecursorSchedule>();
            var listUnscheduled = new List<PrecursorSchedule>();
            foreach (PeptideGroupDocNode nodePepGroup in Document.PeptideGroups)
            {
                foreach (PeptideDocNode nodePep in nodePepGroup.Children)
                {
                    foreach (TransitionGroupDocNode nodeTranGroup in nodePep.Children)
                    {
                        double timeWindow;
                        double? retentionTime = predict.PredictRetentionTime(nodeTranGroup, singleWindow, out timeWindow);
                        if (retentionTime.HasValue)
                            listSchedules.Add(new PrecursorSchedule(nodePepGroup, nodePep, nodeTranGroup, retentionTime.Value, timeWindow));
                        else
                            listUnscheduled.Add(new PrecursorSchedule(nodePepGroup, nodePep, nodeTranGroup, 0, 0));
                    }
                }
            }

            var listScheduleGroups = new List<List<PrecursorSchedule>>();
            while (!PrecursorSchedule.IsListScheduled(listSchedules))
            {
                var listScheduleNext = new List<PrecursorSchedule>();
                foreach (var schedule in listSchedules)
                    schedule.Schedule(listScheduleNext, MaxTransitions.Value);
                listScheduleGroups.Add(listScheduleNext);
            }

            // TODO: Balance groups

            foreach (var listScheduleNext in listScheduleGroups)
                WriteScheduledList(fileIterator, listScheduleNext);
            WriteScheduledList(fileIterator, listUnscheduled);
        }

        private void WriteScheduledList(FileIterator fileIterator,
            ICollection<PrecursorSchedule> listSchedules)
        {
            if (listSchedules.Count == 0)
                return;

            fileIterator.NextFile();
            foreach (var schedule in listSchedules)
            {
                var nodePepGroup = schedule.PeptideGroup;
                var nodePep = schedule.Peptide;
                var nodeGroup = schedule.TransitionGroup;

                // Skip percursors with too few transitions.
                int groupTransitions = nodeGroup.Children.Count;
                if (groupTransitions < MinTransitions)
                    continue;

                foreach (TransitionDocNode transition in nodeGroup.Children)
                    fileIterator.WriteTransition(this, nodePepGroup, nodePep, nodeGroup, transition, 0);
            }
        }

        private sealed class PrecursorSchedule : PrecursorScheduleBase
        {
            public PrecursorSchedule(PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep,
                    TransitionGroupDocNode nodeTranGroup, double retentionTime, double timeWindow)
                : base(nodeTranGroup, retentionTime, timeWindow)
            {
                PeptideGroup = nodePepGroup;
                Peptide = nodePep;
            }

            public PeptideGroupDocNode PeptideGroup { get; private set; }
            public PeptideDocNode Peptide { get; private set; }
            private bool IsScheduled { get; set; }

            public void Schedule(ICollection<PrecursorSchedule> schedules, int maxTransitions)
            {
                if (IsScheduled)
                    return;
                int overlapping = GetOverlapCount(schedules);
                if (overlapping + TransitionCount > maxTransitions)
                    return;
                schedules.Add(this);
                IsScheduled = true;
            }

            public static bool IsListScheduled(IList<PrecursorSchedule> schedules)
            {
                return schedules.IndexOf(s => !s.IsScheduled) == -1;
            }
        }

        protected abstract string InstrumentType { get; }

        protected virtual void WriteHeaders(TextWriter writer) { /* No headers by default */ }

        protected abstract void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionDocNode nodeTran,
                                                int step);

        protected double GetCollisionEnergy(int charge, double mz, int step)
        {
            var regressionCE = Document.Settings.TransitionSettings.Prediction.CollisionEnergy;
            double ce = regressionCE.GetCollisionEnergy(charge, mz);
            if (Equals(OptimizeType, ExportOptimize.CE))
                ce += OptimizeStepSize * step;
            return ce;
        }

        protected double GetDeclusteringPotential(double mz, int step)
        {
            var regressionDP = Document.Settings.TransitionSettings.Prediction.DeclusteringPotential;
            if (regressionDP == null)
                return 0;
            double dp = regressionDP.GetDeclustringPotential(mz);
            if (Equals(OptimizeType, ExportOptimize.DP))
                dp += OptimizeStepSize * step;
            return dp;
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
        public PrecursorScheduleBase(TransitionGroupDocNode nodeGroup, double retentionTime, double timeWindow)
        {
            TransitionGroup = nodeGroup;
            StartTime = retentionTime - (timeWindow / 2);
            EndTime = StartTime + timeWindow;
        }

        public TransitionGroupDocNode TransitionGroup { get; private set; }
        public int TransitionCount { get { return TransitionGroup.TransitionCount; } }
        public double StartTime { get; set; }
        public double EndTime { get; set; }

        public bool ContainsTime(double time)
        {
            return StartTime <= time && time <= EndTime;
        }

        public int GetOverlapCount<T>(ICollection<T> schedules)
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

        public static int GetOverlapCount<T>(ICollection<T> schedules, double time)
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
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz));
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTran.Mz) + 0.01*step);
            writer.Write(FieldSeparator);
            writer.Write(GetCollisionEnergy(nodeTranGroup.TransitionGroup.PrecursorCharge, nodeTranGroup.PrecursorMz, step));
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
                double? predictedRT = prediction.PredictRetentionTime(nodeTranGroup, false, out windowRT);
                predictedRT = RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT);
                if (predictedRT.HasValue)
                {
                    writer.Write(Math.Max(0, predictedRT.Value - windowRT / 2));    // No negative retention times
                    writer.Write(FieldSeparator);
                    writer.Write(predictedRT.Value + windowRT / 2);
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
            writer.Write(nodePepGroup.Name);
            writer.Write(FieldSeparator);
            writer.Write(nodeTran.Transition.FragmentIonName);
            writer.Write(FieldSeparator);
            if (nodeTran.HasLibInfo)
                writer.Write(nodeTran.LibInfo.Rank);
            if (Document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                writer.Write(FieldSeparator);
                if (nodeTranGroup.TransitionGroup.LabelType == IsotopeLabelType.light)
                    writer.Write("light");
                else
                    writer.Write("heavy");
            }

            writer.WriteLine();
        }
    }

    public class ThermoMassListToMethodConverter : ThermoMassListExporter
    {
        public ThermoMassListToMethodConverter(SrmDocument document)
            : base(document)
        {
        }

        protected void ExportConvertMethod(string exeName, string fileName, string templateName)
        {
            // First export transition lists to map in memory
            Export(null);

            string baseName = Path.Combine(Path.GetDirectoryName(fileName),
                                           Path.GetFileNameWithoutExtension(fileName));
            string ext = Path.GetExtension(fileName);

            var listFileSavers = new List<FileSaver>();
            try
            {
                StringBuilder stdinBuilder = new StringBuilder();
                foreach (KeyValuePair<string, StringBuilder> pair in MemoryOutput)
                {
                    if (stdinBuilder.Length > 0)
                        stdinBuilder.AppendLine();

                    string suffix = pair.Key.Substring(MEMORY_KEY_ROOT.Length);
                    suffix = Path.GetFileNameWithoutExtension(suffix);
                    string methodName = baseName + suffix + ext;

                    var fs = new FileSaver(methodName);
                    listFileSavers.Add(fs);

                    stdinBuilder.AppendLine(fs.SafeName);
                    stdinBuilder.Append(pair.Value.ToString());
                }

                List<string> argv = new List<string> { "-s", "-m", "\"" + templateName + "\"" };  // Read from stdin, multi-file format

                string dirWork = Path.GetDirectoryName(fileName);
                var psiBlibBuilder = new ProcessStartInfo(exeName)
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
                psiBlibBuilder.RunProcess(stdinBuilder.ToString());

                foreach (var fs in listFileSavers)
                    fs.Commit();
            }
            finally
            {
                foreach (var fs in listFileSavers)
                    fs.Dispose();
            }
        }        
    }

    public class ThermoMethodExporter : ThermoMassListToMethodConverter
    {
        public const string EXE_BUILD_TSQ_METHOD = "BuildTSQEZMethod";

        public ThermoMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        public void ExportMethod(string fileName, string templateName)
        {
            ExportConvertMethod(EXE_BUILD_TSQ_METHOD, fileName, templateName);
        }
    }

    public class ThermoLtqMethodExporter : ThermoMassListToMethodConverter
    {
        public const string EXE_BUILD_LTQ_METHOD = "BuildLTQMethod";

        public ThermoLtqMethodExporter(SrmDocument document)
            : base(document)
        {
            // Export scheduling fields, but no actual scheduling
            // is yet possible on the LTQ. (requires dealing with
            // segments)
            RunLength = 0;
        }

        public void ExportMethod(string fileName, string templateName)
        {
            ExportConvertMethod(EXE_BUILD_LTQ_METHOD, fileName, templateName);
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
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz));
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTran.Mz) + 0.01*step);
            writer.Write(FieldSeparator);
            if (MethodType == ExportMethodType.Standard)
                writer.Write(Math.Round(DwellTime, 2));
            else
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(nodeTranGroup, HasResults, out windowRT);
                if (predictedRT.HasValue)
                    writer.Write(RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT));
            }
            writer.Write(FieldSeparator);

            // Write special ID for ABI software
            writer.Write(nodePepGroup.Name);
            writer.Write('.');
            writer.Write(nodePep.Peptide.Sequence);
            writer.Write('.');
            if (nodeTran.HasLibInfo)
                writer.Write(nodeTran.LibInfo.Rank);
            writer.Write(nodeTran.Transition.FragmentIonName);
            writer.Write('.');
            writer.Write(nodeTranGroup.TransitionGroup.LabelType == IsotopeLabelType.light ? "light" : "heavy");
            writer.Write(FieldSeparator);

            writer.Write(GetDeclusteringPotential(nodeTranGroup.PrecursorMz, step));
            writer.Write(FieldSeparator);
            writer.Write(GetCollisionEnergy(nodeTranGroup.TransitionGroup.PrecursorCharge, nodeTranGroup.PrecursorMz, step));
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
            writer.Write(nodeTranGroup.TransitionGroup.LabelType == IsotopeLabelType.light ? "FALSE" : "TRUE");
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz));
            writer.Write(FieldSeparator);
            writer.Write("Unit");
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTran.Mz) + 0.01*step);
            writer.Write(FieldSeparator);
            writer.Write("Unit");
            writer.Write(FieldSeparator);

            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(Math.Round(DwellTime, 2));
                writer.Write(FieldSeparator);                
            }


            writer.Write(Fragmentor);
            writer.Write(FieldSeparator);
            writer.Write(GetCollisionEnergy(nodeTranGroup.TransitionGroup.PrecursorCharge, nodeTranGroup.PrecursorMz, step));
            writer.Write(FieldSeparator);

            if (MethodType != ExportMethodType.Standard)
            {
                // Scheduling information
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(nodeTranGroup, false, out windowRT);
                if (predictedRT.HasValue)
                {
                    writer.Write(RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT));
                    writer.Write(FieldSeparator);
                    writer.Write(Math.Round(windowRT, 1));
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

        private bool HasResults { get { return Document.Settings.HasResults; } }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.Waters; }
        }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write("Protein");
            writer.Write(FieldSeparator);
            writer.Write("Sequence");
            writer.Write(FieldSeparator);
            writer.Write("Precursor M/Z");
            writer.Write(FieldSeparator);
            writer.Write("Precursor Retention Time");
            writer.Write(FieldSeparator);
            writer.Write("Product M/Z");
            writer.Write(FieldSeparator);
            writer.Write("Collision Energy");
            writer.Write(FieldSeparator);
            writer.Write("Cone Voltage");
            // Informational columns
            writer.Write(FieldSeparator);
            writer.Write("Ion Name");
            writer.Write(FieldSeparator);
            writer.Write("Library Rank");
            if (Document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                writer.Write(FieldSeparator);
                writer.Write("Label Type");                
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
            writer.Write(nodePepGroup.Name);
            writer.Write(FieldSeparator);
            writer.Write(nodePep.Peptide.Sequence);
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz));
            writer.Write(FieldSeparator);

            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(RunLength / 2);
            }
            else
            {
                // Scheduling information
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(nodeTranGroup, HasResults, out windowRT);
                if (predictedRT.HasValue)
                    writer.Write(RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT));
            }

            writer.Write(FieldSeparator);

            writer.Write(SequenceMassCalc.PersistentMZ(nodeTran.Mz) + 0.01*step);
            writer.Write(FieldSeparator);

            writer.Write(GetCollisionEnergy(nodeTranGroup.TransitionGroup.PrecursorCharge, nodeTranGroup.PrecursorMz, step));
            writer.Write(FieldSeparator);

            writer.Write(ConeVoltage);
            writer.Write(FieldSeparator);

            // Extra information not used by instrument
            writer.Write(nodeTran.Transition.FragmentIonName);
            writer.Write(FieldSeparator);
            if (nodeTran.HasLibInfo)
                writer.Write(nodeTran.LibInfo.Rank);
            if (Document.Settings.PeptideSettings.Modifications.HasHeavyModifications)
            {
                writer.Write(FieldSeparator);
                if (nodeTranGroup.TransitionGroup.LabelType == IsotopeLabelType.light)
                    writer.Write("light");
                else
                    writer.Write("heavy");
            }
            writer.WriteLine();
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