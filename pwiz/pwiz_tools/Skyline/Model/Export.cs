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
using System.Collections;
using System.Collections.Generic;
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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Shimadzu.LabSolutions.MethodConverter;

// ReSharper disable NonLocalizedString
namespace pwiz.Skyline.Model
{
// ReSharper disable InconsistentNaming
    public enum ExportStrategy { Single, Protein, Buckets }
    public static class ExportStrategyExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.ExportStrategyExtension_LOCALIZED_VALUES_Single,
                    Resources.ExportStrategyExtension_LOCALIZED_VALUES_Protein,
                    Resources.ExportStrategyExtension_LOCALIZED_VALUES_Buckets
                };
            }
        }
        public static string GetLocalizedString(this ExportStrategy val)
        {
            return LOCALIZED_VALUES[(int)val];
        }

        public static ExportStrategy GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<ExportStrategy>(enumValue, LOCALIZED_VALUES);
        }

        public static ExportStrategy GetEnum(string enumValue, ExportStrategy defaultValue)
        {
            return Helpers.EnumFromLocalizedString(enumValue, LOCALIZED_VALUES, defaultValue);
        }  
    }

    public enum ExportMethodType { Standard, Scheduled, Triggered }
    public static class ExportMethodTypeExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.ExportMethodTypeExtension_LOCALIZED_VALUES_Standard,
                    Resources.ExportMethodTypeExtension_LOCALIZED_VALUES_Scheduled,
                    Resources.ExportMethodTypeExtension_LOCALIZED_VALUES_Triggered
                };
            }
        }
        public static string GetLocalizedString(this ExportMethodType val)
        {
            return LOCALIZED_VALUES[(int) val];
        }

        public static ExportMethodType GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<ExportMethodType>(enumValue, LOCALIZED_VALUES);
        }  
    }

    public enum ExportSchedulingAlgorithm { Average, Trends, Single }
    public static class ExportSchedulingAlgorithmExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.ExportSchedulingAlgorithmExtension_LOCALIZED_VALUES_Average,
                    Resources.ExportSchedulingAlgorithmExtension_LOCALIZED_VALUES_Trends,
                    Resources.ExportSchedulingAlgorithmExtension_LOCALIZED_VALUES_Single
                };
            }
        }
        public static string GetLocalizedString(this ExportSchedulingAlgorithm val)
        {
            return LOCALIZED_VALUES[(int)val];
        }

        public static ExportSchedulingAlgorithm GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<ExportSchedulingAlgorithm>(enumValue, LOCALIZED_VALUES);
        }  
    }

    public enum ExportFileType { List, Method, IsolationList }
    public static class ExportFileTypeExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get
            {
                return new[]
                {
                    Resources.ExportFileTypeExtension_LOCALIZED_VALUES_List,
                    Resources.ExportFileTypeExtension_LOCALIZED_VALUES_Method,
                    Resources.ExportFileTypeExtension_LOCALIZED_VALUES_IsolationList
                };
            }
        }
        public static string GetLocalizedString(this ExportFileType val)
        {
            return LOCALIZED_VALUES[(int)val];
        }

        public static ExportFileType GetEnum(string enumValue)
        {
            return Helpers.EnumFromLocalizedString<ExportFileType>(enumValue, LOCALIZED_VALUES);
        }  
    }

// ReSharper restore InconsistentNaming
    public static class ExportInstrumentType
    {
        // ReSharper disable NonLocalizedString
        public const string ABI = "AB SCIEX";
        public const string ABI_QTRAP = "AB SCIEX QTRAP";
        public const string ABI_TOF = "AB SCIEX TOF";
        public const string AGILENT = "Agilent";
        public const string AGILENT_TOF = "Agilent TOF";
        public const string AGILENT6400 = "Agilent 6400 Series";
        public const string BRUKER = "Bruker";
        public const string BRUKER_TOF = "Bruker TOF";
        public const string SHIMADZU = "Shimadzu";
        public const string THERMO = "Thermo";
        public const string THERMO_TSQ = "Thermo TSQ";
        public const string THERMO_QUANTIVA = "Thermo Quantiva";
        public const string THERMO_LTQ = "Thermo LTQ";
        public const string THERMO_Q_EXACTIVE = "Thermo Q Exactive";
        public const string WATERS = "Waters";
        public const string WATERS_XEVO = "Waters Xevo";
        public const string WATERS_QUATTRO_PREMIER = "Waters Quattro Premier";

        public const string EXT_AB_SCIEX = ".dam";
        public const string EXT_AGILENT = ".m";
        public const string EXT_BRUKER = ".m";
        public const string EXT_THERMO = ".meth";
        public const string EXT_WATERS = ".exp";
        // ReSharper restore NonLocalizedString

        public static readonly string[] METHOD_TYPES =
            {
                ABI_QTRAP,
                ABI_TOF,
                AGILENT6400,
                BRUKER_TOF,
                THERMO_TSQ,
                THERMO_LTQ,
                WATERS_XEVO,
                WATERS_QUATTRO_PREMIER,
            };

        public static readonly string[] TRANSITION_LIST_TYPES =
            {
                ABI,
                AGILENT,
                BRUKER,
                SHIMADZU,
                THERMO,
                THERMO_QUANTIVA,
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
                                       {BRUKER_TOF, EXT_BRUKER},
                                       {THERMO_TSQ, EXT_THERMO},
                                       {THERMO_LTQ, EXT_THERMO},
                                       {WATERS_XEVO, EXT_WATERS},
                                       {WATERS_QUATTRO_PREMIER, EXT_WATERS}
                                   };
        }

        public static string TransitionListExtention(string instrument)
        {
            return (Equals(instrument, SHIMADZU)
                ? ShimadzuNativeMassListExporter.EXT_SHIMADZU_TRANSITION_LIST
                : TextUtil.EXT_CSV);
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
                   Equals(type, BRUKER_TOF) ||
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
                doc.CanSchedule(IsSingleWindowInstrumentType(instrumentType));
        }

        public static bool CanTriggerInstrumentType(string type)
        {
            return Equals(type, AGILENT) ||
                   Equals(type, AGILENT6400) ||
                   Equals(type, THERMO) ||
                   Equals(type, ABI_QTRAP) ||
                   Equals(type, ABI)
                // TODO: TSQ Method writing API does not yet support triggered methods
                // || Equals(type, THERMO_TSQ)
                   ;
        }

        public static bool CanTrigger(string instrumentType, SrmDocument document, int? replicateIndex)
        {
            return CanTriggerInstrumentType(instrumentType) &&
                document.CanTrigger(replicateIndex);
        }

        public static bool IsSingleWindowInstrumentType(string type)
        {
            return Equals(type, WATERS) ||
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
        public virtual int? SchedulingReplicateNum { get; set; }
        public virtual ExportSchedulingAlgorithm SchedulingAlgorithm { get; set; }

        public virtual int PrimaryTransitionCount { get; set; }
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
            exporter.PrimaryTransitionCount = PrimaryTransitionCount;
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
                        throw new InvalidOperationException(string.Format(Resources.ExportProperties_ExportFile_Unrecognized_instrument_type__0__, instrumentType));
                case ExportInstrumentType.BRUKER_TOF:
                    return ExportBrukerMethod(doc, path, template);
                case ExportInstrumentType.THERMO:
                case ExportInstrumentType.THERMO_TSQ:
                    if (type == ExportFileType.List)
                        return ExportThermoCsv(doc, path);
                    else
                        return ExportThermoMethod(doc, path, template);
                case ExportInstrumentType.THERMO_QUANTIVA:
                    return ExportThermoQuantivaCsv(doc, path);
                case ExportInstrumentType.SHIMADZU:
                    return ExportShimadzuCsv(doc, path);
                case ExportInstrumentType.BRUKER:
                    return ExportBrukerCsv(doc, path);
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
                    throw new InvalidOperationException(string.Format(Resources.ExportProperties_ExportFile_Unrecognized_instrument_type__0__, instrumentType));
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

        public AbstractMassListExporter ExportBrukerMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new BrukerMethodExporter(document));
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

        public AbstractMassListExporter ExportThermoQuantivaCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new ThermoQuantivaMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.Export(fileName);

            return exporter;
        }

        public AbstractMassListExporter ExportShimadzuCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new ShimadzuNativeMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            PerformLongExport(m => exporter.ExportNativeList(fileName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportBrukerCsv(SrmDocument document, string filename)
        {
            var exporter = InitExporter(new BrukerMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.DwellTime = DwellTime;
            exporter.Export(filename);

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
        public static string NONE { get { return Resources.ExportOptimize_NONE_None; }}
        public static string CE { get { return Resources.ExportOptimize_CE_Collision_Energy; }}
        public static string DP { get { return Resources.ExportOptimize_DP_Declustering_Potential; }}

        private static readonly string[] OPTIMIZE_TYPES = {NONE, CE, DP};

        public static string[] OptimizeTypes { get { return OPTIMIZE_TYPES; } }
    }

    public class ThermoMassListExporter : AbstractMassListExporter
    {
        private bool _addTriggerReference;
        protected HashSet<string> _setRTStandards;

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
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            if (MethodType != ExportMethodType.Standard)
            {
                if (AddEnergyRamp)
                {
                    writer.Write(1);  // Energy Ramp
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
                writer.Write(1);  // Polarity
                writer.Write(FieldSeparator);                    

                if (MethodType == ExportMethodType.Triggered)
                {
                    if (_setRTStandards.Contains(Document.Settings.GetModifiedSequence(nodePep)))
                    {
                        writer.Write(1000);  // Trigger
                        writer.Write(FieldSeparator);
                        writer.Write(2);     // Reference
                        writer.Write(FieldSeparator);
                    }
                    else if (IsPrimary(nodeTranGroup, nodeTranGroupPrimary, nodeTran))
                    {
                        writer.Write(1000);  // Trigger
                        writer.Write(FieldSeparator);
                        writer.Write(0);     // Primary
                        writer.Write(FieldSeparator);
                    }
                    else
                    {
                        writer.Write("1.0E+10");  // Trigger // Not L10N: Number
                        writer.Write(FieldSeparator);
                        writer.Write(1);          // Secondary
                        writer.Write(FieldSeparator);
                    }                    
                }
                else if (AddTriggerReference)
                {
                    if (_setRTStandards.Contains(Document.Settings.GetModifiedSequence(nodePep)))
                    {
                        writer.Write(1000);  // Trigger
                        writer.Write(FieldSeparator);
                        writer.Write(2);     // Reference
                        writer.Write(FieldSeparator);
                    }
                    else
                    {
                        writer.Write("1.0E+10");  // Trigger // Not L10N: Number
                        writer.Write(FieldSeparator);
                        writer.Write(0);     // Reference
                        writer.Write(FieldSeparator);
                    }
                }
            }
            else if (RunLength.HasValue)
            {
                writer.Write(0);    // No negative retention times
                writer.Write(FieldSeparator);
                writer.Write(RunLength);
                writer.Write(FieldSeparator);
                writer.Write(1);  // Polarity
                writer.Write(FieldSeparator);                                    
            }
            // Write modified sequence for the light peptide molecular structure
            writer.Write(Document.Settings.GetModifiedSequence(nodePep));
            writer.Write(FieldSeparator);
            writer.WriteDsvField(nodePepGroup.Name, FieldSeparator);
            writer.Write(FieldSeparator);
            writer.Write(nodeTran.Transition.GetFragmentIonName(CultureInfo.InvariantCulture));
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

    public class ThermoQuantivaMassListExporter : ThermoMassListExporter
    {
        // Hack to workaround Quantiva limitation
        protected readonly Dictionary<string, int> CompoundCounts = new Dictionary<string, int>();
        protected const int MAX_COMPOUND_NAME = 10;

        public ThermoQuantivaMassListExporter(SrmDocument document)
            : base(document)
        {
        }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.THERMO_QUANTIVA; }
        }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write("Compound"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Start Time (min)"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("End Time (min)"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Polarity"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Precursor (m/z)"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Product (m/z)"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Collision Energy (V)"); // Not L10N

            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            string compound = Document.Settings.GetModifiedSequence(nodePep);
            if (!CompoundCounts.ContainsKey(compound))
            {
                CompoundCounts[compound] = 0;
            }
            else
            {
                int compoundStep = (++CompoundCounts[compound]) / MAX_COMPOUND_NAME + 1;
                if (compoundStep > 1)
                    compound += compoundStep;
            }
            writer.Write(compound);
            writer.Write(FieldSeparator);
            
            // Retention time
            if (MethodType == ExportMethodType.Standard)
            {
                // Start Time and Stop Time
                writer.Write(0);
                writer.Write(FieldSeparator);
                writer.Write(RunLength);
                writer.Write(FieldSeparator);
            }
            else
            {
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
            }

            writer.Write("Positive"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));

            writer.WriteLine();
        }
    }

    public class ShimadzuMassListExporter : AbstractMassListExporter
    {
        public double? RunLength { get; set; }
        private readonly Dictionary<GroupStepKey, int> _peptidesSeen = new Dictionary<GroupStepKey, int>();

        private struct GroupStepKey
        {
            private readonly int _groupGlobalIndex;
            private readonly int _step;

            public GroupStepKey(int groupGlobalIndex, int step)
            {
                _groupGlobalIndex = groupGlobalIndex;
                _step = step;
            }

            #region object overrides

            public bool Equals(GroupStepKey other)
            {
                return _groupGlobalIndex == other._groupGlobalIndex && _step == other._step;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is GroupStepKey && Equals((GroupStepKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (_groupGlobalIndex*397) ^ _step;
                }
            }

            #endregion
        }

        public ShimadzuMassListExporter(SrmDocument document)
            : base(document, null)
        {
        }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.SHIMADZU; }
        }
        
        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write("Peptide"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("ID"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Type"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Precursor"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Product"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("RT"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("RT Window"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("CE"); // Not L10N

            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.Write(Document.Settings.GetModifiedSequence(nodePep) +
                "_" + nodeTranGroup.TransitionGroup.LabelType.ToString().Replace(' ', '_')); // Not L10N
            if (step != 0)
                writer.Write("_" + step); // Not L10N
            writer.Write(FieldSeparator);
            int id;
            var key = new GroupStepKey(nodeTranGroup.Id.GlobalIndex, step);
            if (!_peptidesSeen.TryGetValue(key, out id))
            {
                id = _peptidesSeen.Count + 1;
                _peptidesSeen.Add(key, id);
            }
            writer.Write(id);
            writer.Write(FieldSeparator);
            var istdTypes = Document.Settings.PeptideSettings.Modifications.InternalStandardTypes;
            writer.Write(istdTypes.Contains(nodeTranGroup.TransitionGroup.LabelType)
                             ? "ISTD" // Not L10N
                             : String.Empty); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            // Shimadzu cannot handle product mass shifting, so the product m/z step is always 0
            // The collision energy will be changed, however, and ProteoWizard has access to that
            // which allows us to sort identical precursor, product m/z pairs correctly
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), 0).ToString(CultureInfo));
            writer.Write(FieldSeparator);

            // Retention time and window
            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(RunLength / 2);
                writer.Write(FieldSeparator);
                writer.Write(RunLength);
                writer.Write(FieldSeparator);
            }
            else
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);
                predictedRT = RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT);
                if (predictedRT.HasValue)
                {
                    writer.Write(predictedRT);
                    writer.Write(FieldSeparator);
                    writer.Write(windowRT);
                    writer.Write(FieldSeparator);
                }
                else
                {
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                }
            }

            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.WriteLine();
        }
    }

    public class ShimadzuNativeMassListExporter : ShimadzuMassListExporter
    {
        public const string EXT_SHIMADZU_TRANSITION_LIST = ".txt";
        public const string EXE_BUILD_TSQ_METHOD = @"Method\Thermo\BuildTSQEZMethod"; // Not L10N

        public ShimadzuNativeMassListExporter(SrmDocument document)
            : base(document)
        {
        }

        public void ExportNativeList(string fileName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            string baseName = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty,
                                           Path.GetFileNameWithoutExtension(fileName) ?? string.Empty);
            string ext = Path.GetExtension(fileName);

            var methodConverter = new MassMethodConverter();
            foreach (KeyValuePair<string, StringBuilder> pair in MemoryOutput)
            {
                string suffix = pair.Key.Substring(MEMORY_KEY_ROOT.Length);
                suffix = Path.GetFileNameWithoutExtension(suffix);
                string methodName = baseName + suffix + ext;

                using (var fs = new FileSaver(methodName))
                {
                    string tranList = pair.Value.ToString();
                    var result = methodConverter.ConvertMethod(fs.SafeName, tranList);
                    switch (result)
                    {
                        case ConverterResult.CannotOpenOutputFile:
                            throw new IOException(string.Format("Failure attempting to save to the temporary file {0}", fs.SafeName));
                        case ConverterResult.MaxTransitionError:
                            throw new ArgumentException(string.Format("The transition count {0} exceeds the maximum allowed for this instrument type", tranList.Split('\n').Length));
                        case ConverterResult.InputCannotBeParsed:
                        case ConverterResult.InputIsEmpty:
                        case ConverterResult.InvalidParameter:
                            Assume.Fail(string.Format("Unexpected response {0} from Shimadzu method converter", result));   // Not L10N
                            break;
                    }
                    fs.Commit();
                }
            }
        }
    }

    public class BrukerMassListExporter : AbstractMassListExporter
    {
        public double? DwellTime { get; set; }
        protected readonly Dictionary<double, int> _retentionIndices = new Dictionary<double, int>();
        protected int _retentionIndex;

        public BrukerMassListExporter(SrmDocument document)
            : base(document, null)
        {
        }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.BRUKER; }
        }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write("Compound Name"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Retention Time"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Retention Time Window"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("CAS Number"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Retention Index"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Scan Type"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Polarity"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Scan Time (ms)"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Separation Method"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Source"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Regulation"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Classification"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Comment"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Transitions Count"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Q1 First Mass"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Q1 Last Mass"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Q1 Resolution"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Q3 First Mass"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Q3 Last Mass"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Q3 Resolution"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Collision Energy"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Dwell Time (ms)"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Is Quantifier"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Quantifier Ions"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Is Qualifier"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qualifier Count"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Mass 1"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Ratio 1"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Mass 2"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Ratio 2"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Mass 3"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Ratio 3"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Mass 4"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Ratio 4"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Mass 5"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Qual Ratio 5"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("GUID (Dont fill this Column)");

            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            // Compound Name
            writer.Write(Document.Settings.GetModifiedSequence(nodePep));
            for (int i = 0; i < nodeTranGroup.TransitionGroup.PrecursorCharge; ++i)
                writer.Write('+');
            writer.Write(FieldSeparator);
            // Retention Time
            double rtWindow;
            double? rt = Document.Settings.PeptideSettings.Prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                SchedulingReplicateIndex, SchedulingAlgorithm, Document.Settings.HasResults, out rtWindow);
            writer.Write(rt.HasValue ? rt.ToString() : "");
            writer.Write(FieldSeparator);
            // Retention Time Window
            writer.Write(rtWindow);
            writer.Write(FieldSeparator);
            // CAS Number
            writer.Write(FieldSeparator);
            // Retention Index
            double retentionIndexKey = rt.GetValueOrDefault();
            if (_retentionIndices.ContainsKey(retentionIndexKey))
            {
                writer.Write(_retentionIndices[retentionIndexKey]);
            }
            else
            {
                _retentionIndices[retentionIndexKey] = ++_retentionIndex;
                writer.Write(_retentionIndex);
            }
            writer.Write(FieldSeparator);
            // Scan Type
            writer.Write("MRM");
            writer.Write(FieldSeparator);
            // Polarity
            writer.Write("Positive");
            writer.Write(FieldSeparator);
            // Scan Time (ms)
            writer.Write("100");
            writer.Write(FieldSeparator);
            // Separation Method
            writer.Write("LCMS");
            writer.Write(FieldSeparator);
            // Source
            writer.Write(FieldSeparator);
            // Regulation
            writer.Write(FieldSeparator);
            // Classification
            writer.Write(FieldSeparator);
            // Comment
            writer.Write(FieldSeparator);
            // Transitions Count
            writer.Write(nodePepGroup.TransitionCount);
            writer.Write(FieldSeparator);
            // Q1 First Mass
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            // Q1 Last Mass
            writer.Write(FieldSeparator);
            // Q1 Resolution
            // TODO fill in value
            writer.Write(FieldSeparator);
            // Q3 First Mass
            double productMz = GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step);
            writer.Write(productMz.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            // Q3 Last Mass
            writer.Write(FieldSeparator);
            // Q3 Resolution
            // TODO fill in value
            writer.Write(FieldSeparator);
            // Collision Energy
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            // Dwell Time
            writer.Write(DwellTime.HasValue ? DwellTime.ToString() : "");
            writer.Write(FieldSeparator);
            // Is Quantifier
            writer.Write(1);
            writer.Write(FieldSeparator);
            // Quantifier Ions
            writer.Write(productMz.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            // Is Qualifier
            writer.Write(0);
            writer.Write(FieldSeparator);
            // Qualifier Count
            writer.Write(FieldSeparator);
            // Qual Mass 1
            writer.Write(FieldSeparator);
            // Qual Ratio 1
            writer.Write(FieldSeparator);
            // Qual Mass 2
            writer.Write(FieldSeparator);
            // Qual Ratio 2
            writer.Write(FieldSeparator);
            // Qual Mass 3
            writer.Write(FieldSeparator);
            // Qual Ratio 3
            writer.Write(FieldSeparator);
            // Qual Mass 4
            writer.Write(FieldSeparator);
            // Qual Ratio 4
            writer.Write(FieldSeparator);
            // Qual Mass 5
            writer.Write(FieldSeparator);
            // Qual Ratio 5
            writer.Write(FieldSeparator);
            // GUID ("Dont fill this Column")

            writer.WriteLine();
        }
    }

    public class ThermoMethodExporter : ThermoMassListExporter
    {
        public const string EXE_BUILD_TSQ_METHOD = @"Method\Thermo\BuildTSQEZMethod"; // Not L10N

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
        public const string EXE_BUILD_LTQ_METHOD = @"Method\Thermo\BuildLTQMethod"; // Not L10N

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
                argv.Add("-f"); // Not L10N
            if(MsAnalyzer != null)
                argv.Add(String.Format("-a {0}", MsAnalyzer)); // Not L10N
            if(MsMsAnalyzer != null)
                argv.Add(String.Format("-b {0}", MsMsAnalyzer)); // Not L10N
            if(InclusionList)
                argv.Add("-i"); // Not L10N
            if(Ms1Scan)
                argv.Add("-1"); // Not L10N
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

        private int OptimizeStepIndex { get; set; }

        private readonly Dictionary<string, int> _groupNamesToCharge = new Dictionary<string, int>();

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.ABI; }
        }

        protected override IEnumerable<DocNode> GetTransitionsInBestOrder(TransitionGroupDocNode nodeGroup, TransitionGroupDocNode nodeGroupPrimary)
        {
            if(MethodType != ExportMethodType.Triggered)
            {
                return nodeGroup.Children;
            }

            IComparer<TransitionOrdered> comparer = TransitionOrdered.TransitionComparerInstance;
            var sortedByPrimaryTransitions = new SortedDictionary<TransitionOrdered, TransitionDocNode>(comparer);
            foreach (TransitionDocNode transition in nodeGroup.Children)
            {
                int? calculatedRank = GetRank(nodeGroup, nodeGroupPrimary, transition);
                int? useableRank = (calculatedRank.HasValue && calculatedRank == 0) ? 10000 : calculatedRank; // red integrations appear to give a rank of 0, making them better rank than the best transition
                sortedByPrimaryTransitions.Add(new TransitionOrdered { Mz = transition.Mz, Rank = useableRank }, transition);
            }
            return sortedByPrimaryTransitions.Values;
        }

        private struct TransitionOrdered
        {
            public int? Rank { private get; set; }
            public double Mz { private get; set; }

            private sealed class TransitionComparer : IComparer<TransitionOrdered>
            {
                public int Compare(TransitionOrdered x, TransitionOrdered y)
                {
                    // null results should be treated has having the worst rank (highest Rank number)
                    if (x.Rank == null && y.Rank != null)
                        return 1;
                    if (x.Rank != null && y.Rank == null)
                        return -1;

                    int c = Comparer.Default.Compare(x.Rank, y.Rank);
                    if (c != 0)
                        return c;
                    c = Comparer.Default.Compare(x.Mz, y.Mz);
                    if (c != 0)
                        return c;

                    return 1;
                }
            }

            private static readonly IComparer<TransitionOrdered> PRECURSOR_MOD_SEQ_COMPARER_INSTANCE = new TransitionComparer();

            public static IComparer<TransitionOrdered> TransitionComparerInstance
            {
                get { return PRECURSOR_MOD_SEQ_COMPARER_INSTANCE; }
            }
        }

        protected override bool IsPrimary(TransitionGroupDocNode nodeGroup, TransitionGroupDocNode nodeGroupPrimary, TransitionDocNode nodeTran)
        {
            if (OptimizeType == null || OptimizeType == ExportOptimize.NONE)
            {
                IEnumerable<DocNode> transitionsInBestOrder = GetTransitionsInBestOrder(nodeGroup, nodeGroupPrimary);
                int i = transitionsInBestOrder.TakeWhile(node => !ReferenceEquals(node, nodeTran)).Count();
                return i < PrimaryTransitionCount;
            }
            else
            {
                return ((OptimizeStepIndex + OptimizeStepCount) < PrimaryTransitionCount);
            }
        }


        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            OptimizeStepIndex = step;
            string q1 = SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo);
            string q3 = GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo);

            double? predictedRT;
            string dwellOrRt;
            GetTransitionTimeValues(nodePep, nodeTranGroup, out predictedRT, out dwellOrRt);

            string extPeptideId;
            string extGroupId;
            GetPeptideAndGroupNames(nodePepGroup, nodePep, nodeTranGroup, nodeTran, step, out extPeptideId, out extGroupId);

            string dp = Math.Round(GetDeclusteringPotential(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo);
            string ce = Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo);

            string precursorWindow = string.Empty;
            string productWindow = string.Empty;
            if (FullScans)
            {
                precursorWindow = Document.Settings.TransitionSettings.FullScan.GetProductFilterWindow(nodeTranGroup.PrecursorMz).ToString(CultureInfo);
                productWindow = Document.Settings.TransitionSettings.FullScan.GetProductFilterWindow(nodeTran.Mz).ToString(CultureInfo);
            }          

            double maxRtDiff = 0;
            float? averagePeakArea = null;
            if (nodeTran.Results != null && predictedRT.HasValue)
            {
                GetValuesFromResults(nodeTran, predictedRT, out averagePeakArea, out maxRtDiff);
            }
            string averagePeakAreaText = averagePeakArea.HasValue ? averagePeakArea.Value.ToString(CultureInfo) : string.Empty;

            double? variableRtWindow;
            string variableRtWindowText;
            GetVariableRtWindow(maxRtDiff, out variableRtWindow, out variableRtWindowText);

            string primaryOrSecondary = string.Empty;
            if (MethodType == ExportMethodType.Triggered)
            {
                primaryOrSecondary = IsPrimary(nodeTranGroup, nodeTranGroupPrimary, nodeTran) ? "1" : "2"; // Not L10N
            }

            string oneLine = string.Format("{0},{1},{2},{3}", q1, q3, dwellOrRt, extPeptideId) + // Not L10N
                             GetOptionalColumns(dp,
                                                ce,
                                                precursorWindow,
                                                productWindow,
                                                extGroupId,
                                                averagePeakAreaText,
                                                variableRtWindowText,
                                                primaryOrSecondary);

            writer.Write(oneLine.Replace(',', FieldSeparator));
            writer.WriteLine();
        }

        protected virtual string GetOptionalColumns(string dp,
                                          string ce,
                                          string precursorWindow,
                                          string productWindow,
                                          string extGroupId,
                                          string averagePeakAreaText,
                                          string variableRtWindowText,
                                          string primaryOrSecondary)
        {
            if (MethodType == ExportMethodType.Triggered) // CSV for triggered
            {
                return string.Format(",{0},{1},{2},{3},{4},{5},{6}",    // Not L10N
                    extGroupId,
                    variableRtWindowText,
                    primaryOrSecondary,
                    "1000", // Not L10N
                    "1.0", // Not L10N
                    dp,
                    ce);
            }
            else // CSV
            {
                return string.Format(",{0},{1}",    // Not L10N
                    dp,
                    ce);
            }
        }

        private void GetVariableRtWindow(double maxRtDiff, out double? variableRtWindow, out string variableRtWindowText)
        {
            // increase window size if observed data goes close to window edge
            double maxWindowObservedInData = (maxRtDiff*2);
            double? measuredWindow = Document.Settings.PeptideSettings.Prediction.MeasuredRTWindow;
            double triggerFraction = Settings.Default.FractionOfRtWindowAtWhichVariableSizeIsTriggered;
            variableRtWindow = null;
            if (measuredWindow.HasValue && maxWindowObservedInData > triggerFraction*measuredWindow.Value)
            {
                variableRtWindow = maxWindowObservedInData +
                                   (Settings.Default.VariableRtWindowIncreaseFraction*measuredWindow);
            }
            variableRtWindowText = variableRtWindow.HasValue ? variableRtWindow.Value.ToString(CultureInfo) : string.Empty;
        }

        private void GetPeptideAndGroupNames(PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, TransitionDocNode nodeTran, int step, out string extPeptideId,
            out string extGroupId)
        {
            // Write special ID for AB software
            // Use light modified sequence for peptide molecular structure, with decimal points replaced by underscores
            // because AB uses periods as field separators
            string modifiedPepSequence = GetSequenceWithModsString(nodePep, Document.Settings); // Not L10N;

            int charge = nodeTranGroup.TransitionGroup.PrecursorCharge;
            if (OptimizeType == null)
            {
                extPeptideId = string.Format("{0}.{1}.{2}.{3}", // Not L10N
                    nodePepGroup.Name,
                    modifiedPepSequence,
                    GetTransitionName(charge, nodeTran),
                    nodeTranGroup.TransitionGroup.LabelType);
                extGroupId = string.Format("{0}.{1}.{2}", // Not L10N
                    nodePepGroup.Name,
                    modifiedPepSequence,
                    nodeTranGroup.TransitionGroup.LabelType);
            }
            else
            {
                extPeptideId = string.Format("{0}.{1}.{2}.CE_{3}.{4}", // Not L10N
                    nodePepGroup.Name,
                    modifiedPepSequence,
                    GetTransitionName(charge, nodeTran),
                    GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step).ToString("0.0", CultureInfo.InvariantCulture), // Not L10N
                    nodeTranGroup.TransitionGroup.LabelType);
                extGroupId = string.Format("{0}.{1}.{2}.{3}", // Not L10N
                    nodePepGroup.Name,
                    modifiedPepSequence,
                    GetTransitionName(charge, nodeTran),
                    nodeTranGroup.TransitionGroup.LabelType);
            }

            // remove commas to prevent addition of extra columns that will be misinterpretted in method builder exe 
            extPeptideId = extPeptideId.Replace(',', '_').Replace('/', '_').Replace(@"\", "_"); // Not L10N
            extGroupId = extGroupId.Replace(',', '_').Replace('/', '_').Replace(@"\", "_"); // Not L10N

            int existCharge;
            if (!_groupNamesToCharge.TryGetValue(extGroupId, out existCharge))
            {
                _groupNamesToCharge.Add(extGroupId, charge);
            }
            else if (existCharge != charge)
            {
                extGroupId = string.Format("{0} +{1}", extGroupId, charge); // Not L10N
            }
        }

        private void GetTransitionTimeValues(PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, out double? predictedRT, out string dwellOrRt)
        {
            predictedRT = null;
            if (MethodType == ExportMethodType.Standard)
                dwellOrRt = Math.Round(DwellTime, 2).ToString(CultureInfo);
            else
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, Document.Settings.HasResults, out windowRT);
                if (predictedRT.HasValue)
                {
                    RTWindow = windowRT; // Store for later use
                    dwellOrRt = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT) ?? 0).ToString(CultureInfo);
                }
                else
                {
                    dwellOrRt = 0.ToString(CultureInfo);
                }
            }
        }

        private void GetValuesFromResults(TransitionDocNode nodeTran, double? predictedRT, out float? averagePeakArea,
                                     out double maxRtDiff)
        {
            maxRtDiff = 0;
            averagePeakArea = null;
            if (!predictedRT.HasValue)
                return;

            float sumPeakArea = 0;
            int resultsUsedCount = 0;
            for (int resultIdx = 0; resultIdx < nodeTran.Results.Count; resultIdx++)
            {
                if (SchedulingReplicateIndex.HasValue && SchedulingReplicateIndex != resultIdx)
                    continue;

                var result = nodeTran.Results[resultIdx];
                if (result == null)
                    continue;

                foreach (TransitionChromInfo chromInfo in result)
                {
                    if (chromInfo.IsEmpty)
                        continue;

                    double rtDiff = predictedRT.Value - chromInfo.StartRetentionTime;
                    if (rtDiff > maxRtDiff)
                        maxRtDiff = rtDiff;

                    sumPeakArea += chromInfo.Area;
                    resultsUsedCount++;
                }
            }
            if (resultsUsedCount > 0)
                averagePeakArea = sumPeakArea/resultsUsedCount;
        }

        static internal string GetSequenceWithModsString(PeptideDocNode nodePep, SrmSettings settings)
        {
            var mods = new ExplicitMods(nodePep,
                                        settings.PeptideSettings.Modifications.StaticModifications
                                            .Where(mod => !mod.IsVariable).ToArray(),
                                        Settings.Default.StaticModList,
                                        new List<TypedModifications>(),
                                        null);

            
            if (nodePep.ExplicitMods != null)
            {
                var staticMods = new List<ExplicitMod>();
                if (mods.StaticModifications != null)
                {
                    foreach (var staticMod in mods.StaticModifications)
                        staticMods.Add(staticMod);
                }
                if (nodePep.ExplicitMods.StaticModifications != null)
                {
                    if (!nodePep.ExplicitMods.IsVariableStaticMods)
                    {
                        // Explicit modifications (not variable) override the settings
                        staticMods.Clear();
                    }
                    foreach (var explicitMod in nodePep.ExplicitMods.StaticModifications)
                        staticMods.Add(explicitMod);
                }
                mods = mods.ChangeStaticModifications(staticMods);
            }

            string sequenceWithMods = settings.GetModifiedSequence(nodePep.Peptide.Sequence,
                                                                            IsotopeLabelType.light,
                                                                            mods, SequenceModFormatType.three_letter_code,
                                                                            true);

            sequenceWithMods = sequenceWithMods.Replace('.', '_');

            return sequenceWithMods;
        }

        private static string GetTransitionName(int precursorCharge, TransitionDocNode transitionNode)
        {
            string ionName = transitionNode.GetFragmentIonName(CultureInfo.InvariantCulture);
            if (transitionNode.Transition.IsPrecursor())
            {
                return GetPrecursorTransitionName(precursorCharge, ionName, transitionNode.Transition.MassIndex);
            }
            else
            {
                return GetTransitionName(precursorCharge, ionName, transitionNode.Transition.Charge);
            }
        }

        public static string GetTransitionName(int precursorCharge, string fragmentIonName, int fragmentCharge)
        {
            return string.Format("+{0}{1}{2}", precursorCharge, // Not L10N
                                 fragmentIonName,
                                 fragmentCharge > 1
                                     ? string.Format("+{0}", fragmentCharge) // Not L10N
                                     : string.Empty); // Not L10N
        }

        public static string GetPrecursorTransitionName(int precursorCharge, string fragmentIonName, int isotopeIndex)
        {
            return string.Format("+{0}{1}{2}", precursorCharge, // Not L10N
                                 fragmentIonName,
                                 isotopeIndex > 0
                                     ? string.Format("[M+{0}]", isotopeIndex) // Not L10N
                                     : string.Empty); // Not L10N
        }
    }

    public abstract class AbiMethodExporter : AbiMassListExporter
    {
        private const string ANALYST_EXE = "Analyst.exe"; // Not L10N

        protected AbiMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        protected abstract string GetRegQueryKey();

        protected abstract string GetExeName();

        protected abstract List<string> GetArgs();

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (fileName != null)
                EnsureAnalyst(progressMonitor);

            if (!InitExport(fileName, progressMonitor))
                return;

            MethodExporter.ExportMethod(GetExeName(),
                GetArgs(), fileName, templateName, MemoryOutput, progressMonitor);
        }

        private void EnsureAnalyst(IProgressMonitor progressMonitor)
        {
            string analystPath = AdvApi.GetPathFromProgId("Analyst.MassSpecMethod.1"); // Not L10N
            string analystDir = (analystPath != null ? Path.GetDirectoryName(analystPath) : null);

            if (analystDir == null)
            {
                throw new IOException(Resources.AbiMethodExporter_EnsureAnalyst_Failed_to_find_a_valid_Analyst_installation);
            }


            var procAnalyst = AnalystProcess ?? Process.Start(Path.Combine(analystDir, ANALYST_EXE));
            // Wait for main window to be present.
            ProgressStatus status = null;
            while (!progressMonitor.IsCanceled && IsAnalystProcessMainWindowActive(procAnalyst) == false)
            {
                if (status == null)
                {
                    status = new ProgressStatus(Resources.AbiMethodExporter_EnsureAnalyst_Waiting_for_Analyst_to_start).ChangePercentComplete(-1);
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
                progressMonitor.UpdateProgress(status.ChangeMessage(Resources.AbiMethodExporter_EnsureAnalyst_Working));
            }    
        }

        private static bool IsAnalystProcessMainWindowActive(Process process)
        {
            if (process == null)
            {
                return false;
            }

            if (process.MainWindowTitle.StartsWith("Analyst") // Not L10N
                && process.MainWindowTitle.Contains("Registration") == false) // Not L10N
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

        protected override string GetOptionalColumns(string dp,
                                                     string ce,
                                                     string precursorWindow,
                                                     string productWindow,
                                                     string extGroupId,
                                                     string averagePeakAreaText,
                                                     string variableRtWindowText,
                                                     string primaryOrSecondary)
        {
            // Provide all columns for method export
            return string.Format(",{0},{1},{2},{3},{4},{5},{6},{7},{8}",    // Not L10N
                                 dp,
                                 ce,
                                 precursorWindow,
                                 productWindow,
                                 extGroupId,
                                 averagePeakAreaText,
                                 variableRtWindowText,
                                 string.Empty,  // Threshold for triggering secondary
                                 primaryOrSecondary);
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
            return @"SOFTWARE\PE SCIEX\Products\Analyst3Q"; // Not L10N
        }

        protected override string GetExeName()
        {
            return @"Method\AbSciex\TQ\BuildQTRAPMethod"; // Not L10N
        }

        protected override List<string> GetArgs()
        {
            var argv = new List<string>();
            if (RTWindow.HasValue)
            {
                argv.Add("-w"); // Not L10N
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
            return @"SOFTWARE\PE SCIEX\Products\AnalystQS"; // Not L10N
        }

        protected override string GetExeName()
        {
            return @"Method\AbSciex\TOF\BuildAnalystFullScanMethod"; // Not L10N
        }

        protected override List<string> GetArgs()
        {
            /*
            *  These are the command-line options specific to ABI TOF method builders
                  -1               Do an MS1 scan each cycle
                  -i               Generate method for Information Dependent Acquisition (IDA)
                  -r               Add retention time information to inclusion list (requires -i)
                  -w <RT window>   Retention time window in minutes for schedule (requires -r)
                  -mq              Create a MultiQuant text method (with same name as generated method but with .txt extension)
            */
            var argv = new List<string>();
            if (Ms1Scan)
                argv.Add("-1"); // Not L10N
            if (InclusionList)
                argv.Add("-i"); // Not L10N
            if (MethodType == ExportMethodType.Scheduled)
                argv.Add("-r"); // Not L10N
            if (RTWindow.HasValue)
            {
                argv.Add("-w"); // Not L10N
                argv.Add(RTWindow.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (ExportMultiQuant)
                argv.Add("-mq"); // Not L10N

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
            writer.Write("Compound Group"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Compound Name"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("ISTD?"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Precursor Ion"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("MS1 Res"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Product Ion"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("MS2 Res"); // Not L10N
            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write("Dwell"); // Not L10N                
            }
            else if (MethodType == ExportMethodType.Triggered)
            {
                writer.Write(FieldSeparator);
                writer.Write("Primary"); // Not L10N
                writer.Write(FieldSeparator);
                writer.Write("Trigger"); // Not L10N
            }
            writer.Write(FieldSeparator);
            writer.Write("Fragmentor"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Collision Energy"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Cell Accelerator Voltage"); // Not L10N
            if (MethodType != ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write("Ret Time (min)"); // Not L10N
                writer.Write(FieldSeparator);
                writer.Write("Delta Ret Time"); // Not L10N
            }
            writer.Write(FieldSeparator);
            writer.Write("Ion Name"); // Not L10N
            if (Document.Settings.PeptideSettings.Libraries.HasLibraries)
            {
                writer.Write(FieldSeparator);
                writer.Write("Library Rank"); // Not L10N
            }
            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.Write(nodePepGroup.Name);
            writer.Write(FieldSeparator);
            // Write modified sequence for the light peptide molecule
            string modifiedSequence = Document.Settings.GetModifiedSequence(nodePep);
            string compoundName = string.Format("{0}.{1}", modifiedSequence, nodeTranGroup.TransitionGroup.LabelType); // Not L10N
            writer.Write(compoundName);

            writer.Write(FieldSeparator);
            var istdTypes = Document.Settings.PeptideSettings.Modifications.InternalStandardTypes;
            writer.Write(istdTypes.Contains(nodeTranGroup.TransitionGroup.LabelType)    // ISTD?
                             ? "TRUE" // Not L10N
                             : "FALSE"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write("Unit");   // MS1 Res // Not L10N
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write("Unit");   // MS2 Res // Not L10N

            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write(Math.Round(DwellTime, 2).ToString(CultureInfo));
            }
            else if (MethodType == ExportMethodType.Triggered)
            {
                writer.Write(FieldSeparator);
                int? rank = GetRank(nodeTranGroup, nodeTranGroupPrimary, nodeTran);
                writer.Write(rank.HasValue && rank.Value <= PrimaryTransitionCount  // Primary
                    ? "TRUE"    // Not L10N
                    : "FALSE"); // Not L10N
                writer.Write(FieldSeparator);
                // Trigger must be rank 1 transition, of analyte type and minimum precursor charge
                bool trigger = false;
                if (IsTriggerType(nodePep, nodeTranGroup, istdTypes) && rank.HasValue && rank.Value == 1)
                {
                    int minCharge = nodePep.TransitionGroups.Select(g => g.TransitionGroup.PrecursorCharge).Min();
                    if (nodeTranGroup.TransitionGroup.PrecursorCharge == minCharge)
                        trigger = true;
                }
                writer.Write(trigger ? "TRUE" : "FALSE"); // Not L10N
            }

            writer.Write(FieldSeparator);
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
            writer.Write(nodeTran.Transition.GetFragmentIonName(CultureInfo.InvariantCulture));
            writer.Write(FieldSeparator);
            if (nodeTran.HasLibInfo)
                writer.Write(nodeTran.LibInfo.Rank);
            writer.WriteLine();
        }

        private static bool IsTriggerType(PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, IList<IsotopeLabelType> istdTypes)
        {
            // If there is a light precursor, then it is always the trigger
            if (nodeTranGroup.TransitionGroup.LabelType.IsLight)
                return true;
            // Get all precursors with the same charge state and at least 1 transition, including this one
            var arrayTranGroups = nodePep.TransitionGroups
                .Where(g => g.TransitionGroup.PrecursorCharge == nodeTranGroup.TransitionGroup.PrecursorCharge &&
                            g.TransitionCount > 0).ToArray();
            // If it is the only precursor of this charge state, then it must be the trigger
            if (arrayTranGroups.Length == 1)
                return true;
            // If there is no light precursor
            var firstGroup = arrayTranGroups.First();
            if (!firstGroup.TransitionGroup.LabelType.IsLight)
            {
                // See if there is a precursor not of an internal standard type, and use the first such precursor
                var analyteGroup = arrayTranGroups.FirstOrDefault(g => !istdTypes.Contains(g.TransitionGroup.LabelType));
                if (analyteGroup != null)
                    return ReferenceEquals(analyteGroup, nodeTranGroup);
            }
            // Otherwise, the first precursor in the list is the trigger.
            return ReferenceEquals(firstGroup, nodeTranGroup);
        }
    }

    public class AgilentMethodExporter : AgilentMassListExporter
    {
        public const string EXE_BUILD_AGILENT_METHOD = @"Method\Agilent\BuildAgilentMethod"; // Not L10N

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
            return methodPath.EndsWith(ExportInstrumentType.EXT_AGILENT) && File.Exists(Path.Combine(methodPath, "qqqacqmeth.xsd")); // Not L10N
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
            return "On,Prec. m/z,Delta m/z (ppm),Z,Prec. Type,Ret. Time (min),Delta Ret. Time (min),Iso. Width,Collision Energy".Replace(',', fieldSeparator); // Not L10N
        }

        public static string GetTargetedHeader(char fieldSeparator)
        {
            return "On,Prec. m/z,Z,Ret. Time (min),Delta Ret. Time (min),Iso. Width,Collision Energy,Acquisition Time (ms/spec)".Replace(',', fieldSeparator); // Not L10N
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            string precursorMz = SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo);
            string z = nodeTranGroup.TransitionGroup.PrecursorCharge.ToString(CultureInfo);
            string retentionTime = "0"; // Not L10N
            string deltaRetentionTime = string.Empty; // Not L10N
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
            string isolationWidth = string.Format(CultureInfo, "Narrow (~{0:0.0} m/z)", 1.3); // Not L10N
            string collisionEnergy = Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo);

            if (IsDda)
            {
                const string deltaMz = "20"; // Not L10N // TODO check: Delta m/z (ppm) 
                Write(writer, "True", precursorMz, deltaMz, z, // Not L10N
                      "Preferred", retentionTime, // Not L10N
                      deltaRetentionTime, isolationWidth, collisionEnergy);
            }
            else
            {
                string acquisitionTime = string.Empty;  // TODO check: nothing to write for: Acquisition Time (ms/spec)
                Write(writer, "True", precursorMz, z, retentionTime, // Not L10N
                      deltaRetentionTime, isolationWidth, collisionEnergy, acquisitionTime);
            }
        }
    }

    public class BrukerMethodExporter : AbstractMassListExporter
    {
        public const string EXE_BUILD_BRUKER_METHOD = @"Method\Bruker\BuildBrukerMethod"; // Not L10N

        public BrukerMethodExporter(SrmDocument document)
            : base(document, null)
        {
            IsPrecursorLimited = true;
            IsolationList = true;
        }
        
        protected override string InstrumentType
        {
            get { return ExportInstrumentType.BRUKER_TOF; }
        }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write("Ret Time (min)"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Tolerance"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Precursor Ion Min"); // Not L10N
            writer.Write(FieldSeparator);
            writer.Write("Precursor Ion Max"); // Not L10N

            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            var prediction = Document.Settings.PeptideSettings.Prediction;
            double windowRT;
            double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);

            if (predictedRT.HasValue)
            {
                writer.Write((RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT) ?? 0).ToString(CultureInfo));
                writer.Write(FieldSeparator);
                writer.Write(windowRT.ToString(CultureInfo));
                writer.Write(FieldSeparator);
            }
            else
            {
                writer.Write(FieldSeparator);
                writer.Write(FieldSeparator);
            }

            double precursorMz = SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz);
            writer.Write(precursorMz.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(precursorMz.ToString(CultureInfo));

            writer.WriteLine();
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            MethodExporter.ExportMethod(EXE_BUILD_BRUKER_METHOD,
                new List<string>(), fileName, templateName, MemoryOutput, progressMonitor);
        }

        public static bool IsBrukerMethodPath(string methodPath)
        {
            return methodPath.EndsWith(ExportInstrumentType.EXT_BRUKER) && File.Exists(Path.Combine(methodPath, "submethods.xml")); // Not L10N
        }
    }

    public class ThermoQExactiveIsolationListExporter : ThermoMassListExporter
    {
        public const double NARROW_NCE = 27.0;
        public const double WIDE_NCE = 30.0;

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
            return "Mass [m/z],Formula [M],Species,CS [z],Polarity,Start [min],End [min],NCE,Comment".Replace(',', fieldSeparator); // Not L10N
        }

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            string precursorMz = SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo);

            string start = string.Empty;
            string end = string.Empty;
            if (MethodType == ExportMethodType.Scheduled)
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);
                // Start Time and End Time
                if (predictedRT.HasValue)
                {
                    start = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT.Value - windowRT/2) ?? 0).ToString(CultureInfo);
                    end = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT.Value + windowRT/2) ?? 0).ToString(CultureInfo);
                }
            }

            string z = nodeTranGroup.TransitionGroup.PrecursorCharge.ToString(CultureInfo);
            // Note that this is normalized CE (not absolute)
            var fullScan = Document.Settings.TransitionSettings.FullScan;
            bool wideWindowDia = false;
            if (fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA && fullScan.IsolationScheme != null)
            {
                // Suggested by Thermo to use 27 for normal isolation ranges and 30 for wider windows
                var scheme = fullScan.IsolationScheme;
                if (!scheme.FromResults && !scheme.IsAllIons)
                {
                    wideWindowDia = scheme.PrespecifiedIsolationWindows.Average(
                        iw => iw.IsolationEnd - iw.IsolationStart) >= 5;
                }
            }
            string collisionEnergy = (wideWindowDia ? WIDE_NCE : NARROW_NCE).ToString(CultureInfo);
            string comment = string.Format("{0} ({1})", // Not L10N
                                           Document.Settings.GetModifiedSequence(nodePep),
                                           nodeTranGroup.TransitionGroup.LabelType);

            Write(writer, precursorMz, string.Empty, string.Empty, z, "Positive", start, end, collisionEnergy, comment); // Not L10N
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

        // ReSharper disable NonLocalizedString
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
        // ReSharper restore NonLocalizedString

        protected override void WriteTransition(TextWriter writer,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.WriteDsvField(nodePepGroup.Name.Replace(' ', '_'), FieldSeparator);  // Quanpedia can't handle spaces // Not L10N
            writer.Write(FieldSeparator);
            // Write special ID to ensure 1-to-1 relationship between this ID and precursor m/z
            // Better to use one ID per peptide molecular structure, as Waters has a 512 ID limit
            // and this allows for 512 peptide charge states and not just 512 precursors.
//            writer.Write(Document.Settings.GetModifiedSequence(nodePep.Peptide.Sequence,
//                nodeTranGroup.TransitionGroup.LabelType, nodePep.ExplicitMods));
            writer.Write(Document.Settings.GetModifiedSequence(nodePep));
            writer.Write('.'); // Not L10N
            writer.Write(nodeTranGroup.TransitionGroup.PrecursorCharge);
            if (step != 0)
            {
                writer.Write('.'); // Not L10N           
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
            writer.Write(nodeTran.Transition.GetFragmentIonName(CultureInfo.InvariantCulture));
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
        public const string EXE_BUILD_WATERS_METHOD = @"Method\Waters\BuildWatersMethod"; // Not L10N

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
                argv.Add("-q"); // Not L10N
            argv.Add("-w"); // Not L10N
            argv.Add(RTWindow.ToString(CultureInfo.InvariantCulture));
            MethodExporter.ExportMethod(EXE_BUILD_WATERS_METHOD,
                argv, fileName, templateName, MemoryOutput, progressMonitor);
        }

        private const string PRIMARY_DEPENDENCY_LIBRARY = "QuantifyClassLibrary.dll"; // Not L10N

        // ReSharper disable NonLocalizedString
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
        // ReSharper restore NonLocalizedString

        private static void EnsureLibraries()
        {
            string skylinePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(skylinePath))
                throw new IOException(Resources.WatersMethodExporter_EnsureLibraries_Waters_method_creation_software_may_not_be_installed_correctly);
            string buildSubdir = Path.GetDirectoryName(EXE_BUILD_WATERS_METHOD) ?? string.Empty;
            string exeDir = Path.Combine(Path.GetDirectoryName(skylinePath) ?? string.Empty, buildSubdir);
            string dacServerPath = AdvApi.GetPathFromProgId("DACScanStats.DACScanStats"); // Not L10N
            if (dacServerPath == null)
            {
                dacServerPath = AdvApi.RegQueryKeyValue(AdvApi.HKEY_LOCAL_MACHINE,
                                                        @"SOFTWARE\Wow6432Node\Micromass\MassLynx", "Root"); // Not L10N
                if (dacServerPath == null)
                {
                    // If all the necessary libraries exist, then continue even if MassLynx is gone.
                    foreach (var libraryName in DEPENDENCY_LIBRARIES)
                    {
                        if (!File.Exists(Path.Combine(exeDir, libraryName)))
                            throw new IOException(Resources.WatersMethodExporter_EnsureLibraries_Failed_to_find_a_valid_MassLynx_installation);
                    }
                    return;
                }

                dacServerPath = Path.Combine(dacServerPath, "bin"); // Not L10N
            }

            string massLynxDir = Path.GetDirectoryName(dacServerPath) ?? string.Empty;
            foreach (var library in DEPENDENCY_LIBRARIES)
            {
                string srcFile = Path.Combine(massLynxDir, library);
                if (!File.Exists(srcFile))
                {
                    throw new IOException(
                        string.Format(Resources.WatersMethodExporter_EnsureLibraries_MassLynx_may_not_be_installed_correctly_The_library__0__could_not_be_found,
                                      library));
                }
                // If destination file does not exist or has a different modification time from
                // the source, then copy the source file from the MassLynx installation.
                string destFile = Path.Combine(exeDir, library);
                if (!File.Exists(destFile) || !Equals(File.GetLastWriteTime(destFile), File.GetLastWriteTime(srcFile)))
                    File.Copy(srcFile, destFile, true);
            }
        }
    }

    internal static class AdvApi
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Auto)] // Not L10N
        public static extern int RegOpenKeyEx(
          UIntPtr hKey,
          string subKey,
          int ulOptions,
          int samDesired,
          out UIntPtr hkResult);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW", SetLastError = true)] // Not L10N
        public static extern int RegQueryValueEx(
            UIntPtr hKey,
            string lpValueName,
            int lpReserved,
            out uint lpType,
            StringBuilder lpData,
            ref uint lpcbData);
        [DllImport("advapi32.dll", SetLastError = true)] // Not L10N
        public static extern int RegCloseKey(
            UIntPtr hKey);

// ReSharper disable InconsistentNaming
        public static UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);
        public static UIntPtr HKEY_CURRENT_USER = new UIntPtr(0x80000001u);

        public const int KEY_READ = 0x20019;
        public const int KEY_WOW64_32KEY = 0x0200;

        public const int REG_SZ = 1;
// ReSharper restore InconsistentNaming

        public static string GetPathFromProgId(string progId)
        {
            String clsid = RegQueryKeyValue(HKEY_LOCAL_MACHINE, @"SOFTWARE\Classes\" + progId + @"\CLSID"); // Not L10N
            if (clsid == null)
                return null;
            return RegQueryKeyValue(HKEY_LOCAL_MACHINE, @"SOFTWARE\Classes\CLSID\" + clsid + @"\InprocServer32"); // Not L10N
        }

        public static string RegQueryKeyValue(UIntPtr hKey, string path)
        {
            return RegQueryKeyValue(hKey, path, string.Empty);
        }

        public static string RegQueryKeyValue(UIntPtr hKey, string path, string valueName)
        {
            UIntPtr hKeyQuery;
            if (RegOpenKeyEx(hKey, path, 0, KEY_READ, out hKeyQuery) != 0)
            {
                if (RegOpenKeyEx(hKey, path, 0, KEY_READ | KEY_WOW64_32KEY, out hKeyQuery) != 0)
                    return null;
            }

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
            string baseName = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty,
                                           Path.GetFileNameWithoutExtension(fileName) ?? string.Empty);
            string ext = Path.GetExtension(fileName);

            var listFileSavers = new List<FileSaver>();
            try
            {
                string methodName = string.Empty;
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
                    stdinBuilder.Append(pair.Value);
                }

                argv.AddRange(new[] { "-s", "-m", "\"" + templateName + "\"" });  // Read from stdin, multi-file format // Not L10N

                string dirWork = Path.GetDirectoryName(fileName);
                var psiExporter = new ProcessStartInfo(exeName)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    // Common directory includes the directory separator
                    WorkingDirectory = dirWork ?? string.Empty,
                    Arguments = string.Join(" ", argv.ToArray()), // Not L10N
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                };

                ProgressStatus status;
                if (dictTranLists.Count == 1)
                    status = new ProgressStatus(string.Format(Resources.MethodExporter_ExportMethod_Exporting_method__0__, methodName));
                else
                {
                    status = new ProgressStatus(Resources.MethodExporter_ExportMethod_Exporting_methods);
                    status = status.ChangeSegments(0, dictTranLists.Count);
                }
                progressMonitor.UpdateProgress(status);

                psiExporter.RunProcess(stdinBuilder.ToString(), "MESSAGE: ", progressMonitor, ref status); // Not L10N

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

        private enum ATTR // Not L10N
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