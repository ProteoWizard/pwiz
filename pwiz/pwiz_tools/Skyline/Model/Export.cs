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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
// ReSharper disable InconsistentNaming
    public enum ExportStrategy { Single, Protein, Buckets }
    public enum ExportMethodType { Standard, Scheduled }
    public enum ExportSchedulingAlgorithm { Average, Trends, Single }
    public enum ExportFileType { List, Method, IsolationList }
// ReSharper restore InconsistentNaming
    public static class ExportInstrumentType
    {
        public const string ABI = "AB SCIEX";
        public const string ABI_QTRAP = "AB SCIEX QTRAP";
        public const string ABI_TOF = "AB SCIEX TOF";
        public const string AGILENT = "Agilent";
        public const string AGILENT_TOF = "Agilent TOF";
        public const string AGILENT6400 = "Agilent 6400 Series";
        public const string THERMO = "Thermo";
        public const string THERMO_TSQ = "Thermo TSQ";
        public const string THERMO_LTQ = "Thermo LTQ";
        public const string THERMO_Q_EXACTIVE = "Thermo Q Exactive";
        public const string WATERS = "Waters";
        public const string WATERS_XEVO = "Waters Xevo";
        public const string WATERS_QUATTRO_PREMIER = "Waters Quattro Premier";

        public const string EXT_AB_SCIEX = ".dam";
        public const string EXT_AGILENT = ".m";
        public const string EXT_THERMO = ".meth";
        public const string EXT_WATERS = ".exp";

        public static readonly string[] METHOD_TYPES =
            {
                ABI_QTRAP,
                ABI_TOF,
                AGILENT6400,
                THERMO_TSQ,
                THERMO_LTQ,
                WATERS_XEVO,
                WATERS_QUATTRO_PREMIER,
            };

        public static readonly string[] TRANSITION_LIST_TYPES =
            {
                ABI,
                AGILENT,
                THERMO,
                WATERS
            };

        public static readonly string[] ISOLATION_LIST_TYPES =
            {
                AGILENT_TOF,
                THERMO_Q_EXACTIVE
            };

        private readonly static Dictionary<string, string> METHOD_EXTENSIONS;

        static ExportInstrumentType()
        {
            METHOD_EXTENSIONS = new Dictionary<string, string>
                                   {
                                       {ABI_QTRAP, EXT_AB_SCIEX},
                                       {ABI_TOF, EXT_AB_SCIEX},
                                       {AGILENT6400, EXT_AGILENT},
                                       {THERMO_TSQ, EXT_THERMO},
                                       {THERMO_LTQ, EXT_THERMO},
                                       {WATERS_XEVO, EXT_WATERS},
                                       {WATERS_QUATTRO_PREMIER, EXT_WATERS}
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
            return METHOD_EXTENSIONS.TryGetValue(instrument, out ext) ? ext : null;
        }

        public static bool IsFullScanInstrumentType(string type)
        {
            return Equals(type, THERMO_LTQ) ||
                   Equals(type, THERMO_Q_EXACTIVE) ||
                   Equals(type, AGILENT_TOF) ||
                   Equals(type, ABI_TOF);
        }

        public static bool IsPrecursorOnlyInstrumentType(string type)
        {
            return Equals(type, THERMO_LTQ) ||
                   Equals(type, ABI_TOF);
        }

        public static bool CanScheduleInstrumentType(string type, SrmDocument doc)
        {
            return !(Equals(type, THERMO_LTQ)) || IsInclusionListMethod(doc);
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
                                                                       IsSingleWindowInstrumentType(instrumentType)
                                                                           ? PeptidePrediction.SchedulingStrategy.single_window
                                                                           : PeptidePrediction.SchedulingStrategy.all_variable_window);
        }

        public static bool IsSingleWindowInstrumentType(string type)
        {
            return Equals(type, ABI) ||
                   Equals(type, ABI_QTRAP) ||
                   Equals(type, WATERS) ||
                   Equals(type, WATERS_XEVO) ||
                   Equals(type, WATERS_QUATTRO_PREMIER);
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

        public virtual bool ExportMultiQuant { get; set; }

        public virtual int MultiplexIsolationListCalculationTime { get; set; }
        public virtual bool DebugCycles { get; set; }

        public TExp InitExporter<TExp>(TExp exporter)
            where TExp : AbstractMassListExporter
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

        public AbstractMassListExporter ExportFile(string instrumentType, ExportFileType type, string path, SrmDocument doc, string template)
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
                case ExportInstrumentType.AGILENT:
                case ExportInstrumentType.AGILENT6400:
                    if (type == ExportFileType.List)
                        return ExportAgilentCsv(doc, path);
                    else
                        return ExportAgilentMethod(doc, path, template);
                case ExportInstrumentType.AGILENT_TOF:
                    if (type == ExportFileType.IsolationList)
                        return ExportAgilentIsolationList(doc, path, template);
                    else
                        throw new InvalidOperationException(string.Format("Unrecognized instrument type {0}.", instrumentType));
                case ExportInstrumentType.THERMO:
                case ExportInstrumentType.THERMO_TSQ:
                    if (type == ExportFileType.List)
                        return ExportThermoCsv(doc, path);
                    else
                        return ExportThermoMethod(doc, path, template);
                case ExportInstrumentType.THERMO_LTQ:
                    OptimizeType = null;
                    return ExportThermoLtqMethod(doc, path, template);
                case ExportInstrumentType.THERMO_Q_EXACTIVE:
                    if (doc.Settings.TransitionSettings.FullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA)
                    {
                        ExportThermoQExactiveDiaList(
                            doc.Settings.TransitionSettings.FullScan.IsolationScheme,
                            doc.Settings.TransitionSettings.Instrument.MaxInclusions,
                            path,
                            MultiplexIsolationListCalculationTime,
                            DebugCycles);
                        return null;
                    }
                    return ExportThermoQExactiveIsolationList(doc, path, template);
                case ExportInstrumentType.WATERS:
                case ExportInstrumentType.WATERS_XEVO:
                    if (type == ExportFileType.List)
                        return ExportWatersCsv(doc, path);
                    else
                        return ExportWatersMethod(doc, path, template);
                case ExportInstrumentType.WATERS_QUATTRO_PREMIER:
                    return ExportWatersQMethod(doc, path, template);
                default:
                    throw new InvalidOperationException(string.Format("Unrecognized instrument type {0}.", instrumentType));
            }
        }

        public AbstractMassListExporter ExportAbiCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new AbiMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            exporter.Export(fileName);

            return exporter;
        }

        public AbstractMassListExporter ExportAbiQtrapMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new AbiQtrapMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportAbiTofMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new AbiTofMethodExporter(document));

            exporter.FullScans = true;
            exporter.ExportMultiQuant = ExportMultiQuant;
            
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportAgilentCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new AgilentMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            exporter.Export(fileName);

            return exporter;
        }

        public AbstractMassListExporter ExportAgilentMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new AgilentMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportAgilentIsolationList(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new AgilentIsolationListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportThermoCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new ThermoMassListExporter(document));
            exporter.AddEnergyRamp = AddEnergyRamp;
            exporter.AddTriggerReference = AddTriggerReference;
            exporter.Export(fileName);

            return exporter;
        }

        public AbstractMassListExporter ExportThermoMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportThermoLtqMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoLtqMethodExporter(document));
            exporter.FullScans = FullScans;
            exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportThermoQExactiveIsolationList(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoQExactiveIsolationListExporter(document));
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public void ExportThermoQExactiveDiaList(IsolationScheme isolationScheme, int? maxInclusions, string fileName,
            int calculationTime, bool debugCycles)
        {
            var exporter = new ThermoQExactiveDiaExporter(isolationScheme, maxInclusions)
                {
                    CalculationTime = calculationTime,
                    DebugCycles = debugCycles
                };

            PerformLongExport(m => exporter.ExportIsolationList(fileName, m));
        }

        public AbstractMassListExporter ExportWatersCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new WatersMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.Export(fileName);

            return exporter;
        }

        public AbstractMassListExporter ExportWatersMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new WatersMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportWatersQMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new WatersMethodExporter(document)
            {
                MethodInstrumentType = ExportInstrumentType.WATERS_QUATTRO_PREMIER
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

        private static readonly string[] OPTIMIZE_TYPES = {NONE, CE, DP};

        public static string[] OptimizeTypes { get { return OPTIMIZE_TYPES; } }
    }

    public class ThermoMassListExporter : AbstractMassListExporter
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
            get { return ExportInstrumentType.THERMO; }
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

    public class ThermoQExactiveDiaExporter : AbstractDiaExporter
    {
        public ThermoQExactiveDiaExporter(IsolationScheme isolationScheme, int? maxInclusions)
            : base(isolationScheme, maxInclusions)
        {
        }

        public void ExportIsolationList(string fileName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            Export(fileName, progressMonitor);
        }
    }

    public class AbiMassListExporter : AbstractMassListExporter
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
            string extPeptideId = string.Format("{0}.{1}.{2}.{3}",
                                                nodePepGroup.Name,
                                                nodePep.Peptide.Sequence,
                                                GetTransitionName(nodeTranGroup.TransitionGroup.PrecursorCharge,
                                                                     nodeTran.Transition),
                                                nodeTranGroup.TransitionGroup.LabelType);
            writer.WriteDsvField(extPeptideId, FieldSeparator);
            writer.Write(FieldSeparator);

            writer.Write(Math.Round(GetDeclusteringPotential(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));           
            if (FullScans)
            {
                writer.Write(FieldSeparator);
                writer.Write(
                    Document.Settings.TransitionSettings.FullScan.GetProductFilterWindow(nodeTranGroup.PrecursorMz));
                writer.Write(FieldSeparator);
                writer.Write(Document.Settings.TransitionSettings.FullScan.GetProductFilterWindow(nodeTran.Mz));
            }
            writer.WriteLine();
        }

        private static string GetTransitionName(int precursorCharge, Transition transition)
        {
            return GetTransitionName(precursorCharge, transition.FragmentIonName, transition.Charge);
        }

        public static string GetTransitionName(int precursorCharge, string fragmentIonName, int fragmentCharge)
        {
            return string.Format("+{0}{1}{2}", precursorCharge,
                                 fragmentIonName,
                                 fragmentCharge > 1
                                     ? string.Format("+{0}", fragmentCharge)
                                     : "");
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

            if (analystDir == null)
            {
                throw new IOException("Failed to find a valid Analyst installation");
            }


            var procAnalyst = AnalystProcess ?? Process.Start(Path.Combine(analystDir, ANALYST_EXE));
            // Wait for main window to be present.
            ProgressStatus status = null;
            while (!progressMonitor.IsCanceled && IsAnalystProcessMainWindowActive(procAnalyst) == false)
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

        private static bool IsAnalystProcessMainWindowActive(Process process)
        {
            if (process == null)
            {
                return false;
            }

            if (process.MainWindowTitle.StartsWith(ANALYST_NAME)
                && process.MainWindowTitle.Contains("Registration") == false)
            {
                return true;
            }
            else
            {
                return false;
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
        public bool ExportMultiQuant { get; set; }

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
            if (ExportMultiQuant)
                argv.Add("-mq");

            return argv;
        }
    }

    public class AgilentMassListExporter : AbstractMassListExporter
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
            get { return ExportInstrumentType.AGILENT; }
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
            writer.Write(FieldSeparator);
            writer.Write("Cell Accelerator Voltage");
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
            writer.Write("Unit");   // MS1 Res
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write("Unit");   // MS2 Res
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
            writer.Write(4);    // Cell Accelerator Voltage
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

    public class AgilentIsolationListExporter : AgilentMassListExporter
    {
        public AgilentIsolationListExporter(SrmDocument document)
            : base(document)
        {
            IsolationList = true;
        }

        private bool IsDda { get { return !Document.Settings.TransitionSettings.FullScan.IsEnabledMsMs; } }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;
            Export(fileName);
        }

        // Write values separated by the field separator, and a line separator at the end.
        private void Write(TextWriter writer, params string[] vals)
        {
            writer.WriteLine(string.Join(FieldSeparator.ToString(CultureInfo.InvariantCulture), vals));
        }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.WriteLine(IsDda ? GetDdaHeader(FieldSeparator) : GetTargetedHeader(FieldSeparator));
        }

        public static string GetDdaHeader(char fieldSeparator)
        {
            const string ddaHeader =
                "On,Prec. m/z,Delta m/z (ppm),Z,Prec. Type,Ret. Time (min),Delta Ret. Time (min),Iso. Width,Collision Energy";
            return ddaHeader.Replace(',', fieldSeparator);
        }

        public static string GetTargetedHeader(char fieldSeparator)
        {
            const string targetedHeader =
                "On,Prec. m/z,Z,Ret. Time (min),Delta Ret. Time (min),Iso. Width,Collision Energy,Acquisition Time (ms/spec)";
            return targetedHeader.Replace(',', fieldSeparator);
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            string precursorMz = SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo);
            string z = nodeTranGroup.TransitionGroup.PrecursorCharge.ToString(CultureInfo);
            string retentionTime = "0";
            string deltaRetentionTime = "";
            if (MethodType == ExportMethodType.Scheduled)
            {
                // Scheduling information
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);
                if (predictedRT.HasValue)
                {
                    retentionTime = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT) ?? 0).ToString(CultureInfo);  // Ret. Time (min)
                    deltaRetentionTime = Math.Round(windowRT, 1).ToString(CultureInfo); // Delta Ret. Time (min)
                }
            }
            string isolationWidth = string.Format(CultureInfo, "Narrow (~{0:0.0} m/z)", 1.3);
            string collisionEnergy = Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo);

            if (IsDda)
            {
                const string deltaMz = "20"; // TODO check: Delta m/z (ppm)
                const string precursorType = "Preferred";
                Write(writer, "True", precursorMz, deltaMz, z, precursorType, retentionTime, deltaRetentionTime, isolationWidth, collisionEnergy);
            }
            else
            {
                const string acquisitionTime = "";  // TODO check: nothing to write for: Acquisition Time (ms/spec)
                Write(writer, "True", precursorMz, z, retentionTime, deltaRetentionTime, isolationWidth, collisionEnergy, acquisitionTime);
            }
        }
    }

    public class ThermoQExactiveIsolationListExporter : ThermoMassListExporter
    {
        public ThermoQExactiveIsolationListExporter(SrmDocument document)
            : base(document)
        {
            IsolationList = true;
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;
            Export(fileName);
        }

        // Write values separated by the field separator, and a line separator at the end.
        private void Write(TextWriter writer, params string[] vals)
        {
            writer.WriteLine(string.Join(FieldSeparator.ToString(CultureInfo.InvariantCulture), vals));
        }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.WriteLine(GetHeader(FieldSeparator));
        }

        public static string GetHeader(char fieldSeparator)
        {
            const string header =
                "Mass [m/z],Polarity,Start [min],End [min],nCE,CS [z],Comment";
            return header.Replace(',', fieldSeparator);
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            string precursorMz = SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo);
            const string polarity = "Positive";

            string start = "";
            string end = "";
            if (MethodType == ExportMethodType.Scheduled)
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);
                // Start Time and End Time
                if (predictedRT.HasValue)
                {
                    start = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT) ?? 0).ToString(CultureInfo);
                    end = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT.Value + windowRT/2) ?? 0).ToString(CultureInfo);
                }
            }

            string z = nodeTranGroup.TransitionGroup.PrecursorCharge.ToString(CultureInfo);
            string collisionEnergy = Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo);

            string comment = string.Format("{0} ({1})",
                Document.Settings.GetModifiedSequence(nodePep.Peptide.Sequence, nodeTranGroup.TransitionGroup.LabelType, nodePep.ExplicitMods),
                nodeTranGroup.TransitionGroup.LabelType);

            Write(writer, precursorMz, polarity, start, end, collisionEnergy, z, comment);
        }
    }

    public class WatersMassListExporter : AbstractMassListExporter
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
            get { return ExportInstrumentType.WATERS; }
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
            if (Equals(MethodInstrumentType, ExportInstrumentType.WATERS_QUATTRO_PREMIER))
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
                    string suffix = pair.Key.Substring(AbstractMassListExporter.MEMORY_KEY_ROOT.Length);
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