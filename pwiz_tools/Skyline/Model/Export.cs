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
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;
using pwiz.CLI.Bruker.PrmScheduling;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using ZedGraph;
using Process = System.Diagnostics.Process;
using Thread = System.Threading.Thread;

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

    public enum ExportPolarity
    {
        all,      // Both, in same output
        positive, // Only positive
        negative, // Only negative
        separate  // Both, but in separate outputs
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
        public const string ABI = "SCIEX";
        public const string ABI_QTRAP = "SCIEX QTRAP";
        public const string ABI_TOF = "SCIEX QTOF";
        public const string AGILENT = "Agilent";
        public const string AGILENT_TOF = "Agilent QTOF";
        public const string AGILENT6400 = "Agilent 6400 Series";
        public const string BRUKER = "Bruker";
        public const string BRUKER_TOF = "Bruker QTOF";
        public const string BRUKER_TIMSTOF = "Bruker timsTOF";
        public const string SHIMADZU = "Shimadzu";
        public const string THERMO = "Thermo";
        public const string THERMO_TSQ = "Thermo TSQ";
        public const string THERMO_ENDURA = "Thermo Endura";
        public const string THERMO_QUANTIVA = "Thermo Quantiva";
        public const string THERMO_ALTIS = "Thermo Altis";
        public const string THERMO_FUSION = "Thermo Fusion";
        public const string THERMO_LTQ = "Thermo LTQ";
        public const string THERMO_Q_EXACTIVE = "Thermo Q Exactive";
        public const string THERMO_EXPLORIS = "Thermo Exploris";
        public const string THERMO_FUSION_LUMOS = "Thermo Fusion Lumos";
        public const string THERMO_ECLIPSE = "Thermo Eclipse";
        public const string WATERS = "Waters";
        public const string WATERS_XEVO_TQ = "Waters Xevo TQ";
        public const string WATERS_XEVO_QTOF = "Waters Xevo QTOF";
        public const string WATERS_SYNAPT_TRAP = "Waters Synapt (trap)";
        public const string WATERS_SYNAPT_TRANSFER = "Waters Synapt (transfer)";
        public const string WATERS_QUATTRO_PREMIER = "Waters Quattro Premier";

        public const string EXT_AB_SCIEX = ".dam";
        public const string EXT_AGILENT = ".m";
        public const string EXT_BRUKER = ".m";
        public const string EXT_BRUKER_TIMSTOF = ".prmsqlite";
        public const string EXT_SHIMADZU = ".lcm";
        public const string EXT_THERMO = ".meth";
        public const string EXT_WATERS = ".exp";

        public static readonly string[] METHOD_TYPES =
            {
                AGILENT6400,
                BRUKER_TOF,
                BRUKER_TIMSTOF,
                ABI_QTRAP,
                ABI_TOF,
                SHIMADZU,
                THERMO_TSQ,
                THERMO_LTQ,
                THERMO_QUANTIVA,
                THERMO_ALTIS,
                THERMO_EXPLORIS,
                // THERMO_ECLIPSE,
                THERMO_FUSION,
                // THERMO_FUSION_LUMOS,
                WATERS_XEVO_TQ,
                WATERS_QUATTRO_PREMIER,
            };

        public static readonly string[] TRANSITION_LIST_TYPES =
            {
                AGILENT,
                BRUKER,
                ABI,
                SHIMADZU,
                THERMO,
                THERMO_QUANTIVA,
                THERMO_ALTIS,
                WATERS
            };

        public static readonly string[] ISOLATION_LIST_TYPES =
            {
                AGILENT_TOF,
                ABI_TOF,
                BRUKER_TIMSTOF,
                THERMO_Q_EXACTIVE,
                THERMO_FUSION,
                WATERS_SYNAPT_TRAP,
                WATERS_SYNAPT_TRANSFER,
                WATERS_XEVO_QTOF,
            };

        private static readonly Dictionary<string, string> METHOD_EXTENSIONS;

        static ExportInstrumentType()
        {
            METHOD_EXTENSIONS = new Dictionary<string, string>
                                   {
                                       {ABI_QTRAP, EXT_AB_SCIEX},
                                       {ABI_TOF, EXT_AB_SCIEX},
                                       {AGILENT6400, EXT_AGILENT},
                                       {BRUKER_TOF, EXT_BRUKER},
                                       {BRUKER_TIMSTOF, EXT_BRUKER_TIMSTOF},
                                       {SHIMADZU, EXT_SHIMADZU},
                                       {THERMO_TSQ, EXT_THERMO},
                                       {THERMO_LTQ, EXT_THERMO},
                                       {THERMO_QUANTIVA, EXT_THERMO},
                                       {THERMO_ALTIS, EXT_THERMO},
                                       {THERMO_EXPLORIS, EXT_THERMO},
                                       {THERMO_ECLIPSE, EXT_THERMO},
                                       {THERMO_FUSION, EXT_THERMO},
                                       {THERMO_FUSION_LUMOS, EXT_THERMO},
                                       {WATERS_XEVO_TQ, EXT_WATERS},
                                       {WATERS_QUATTRO_PREMIER, EXT_WATERS}
                                   };
        }

        public static string TransitionListExtension(string instrument)
        {
            return Equals(instrument, SHIMADZU)
                ? ShimadzuMassListExporter.EXT_SHIMADZU_TRANSITION_LIST
                : TextUtil.EXT_CSV;
        }

        public static string IsolationListExtension(string instrument)
        {
            switch (instrument)
            {
                case ABI_TOF:
                    return AbiTofIsolationListExporter.EXT_ABI_TOF_ISOLATION_LIST;
                case WATERS_SYNAPT_TRAP:
                case WATERS_SYNAPT_TRANSFER:
                case WATERS_XEVO_QTOF:
                    return WatersIsolationListExporter.EXT_WATERS_ISOLATION_LIST;
                default:
                    return TextUtil.EXT_CSV;
            }
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
                   Equals(type, THERMO_FUSION) ||
                   Equals(type, AGILENT_TOF) ||
                   Equals(type, WATERS_SYNAPT_TRAP) ||
                   Equals(type, WATERS_SYNAPT_TRANSFER) ||
                   Equals(type, WATERS_XEVO_QTOF) ||
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
                   Equals(type, WATERS_XEVO_TQ) ||
                   Equals(type, WATERS_QUATTRO_PREMIER);
        }
    }

    public abstract class ExportProperties
    {
        public virtual ExportStrategy ExportStrategy { get; set; }
        public virtual bool SortByMz { get; set; }
        public virtual bool IgnoreProteins { get; set; }
        public virtual int? MaxTransitions { get; set; }
        public virtual ExportMethodType MethodType { get; set; }
        public virtual string OptimizeType { get; set; }
        public virtual double OptimizeStepSize { get; set; }
        public virtual int OptimizeStepCount { get; set; }
        public virtual int? SchedulingReplicateNum { get; set; }
        public virtual ExportSchedulingAlgorithm SchedulingAlgorithm { get; set; }
        public virtual ExportPolarity PolarityFilter { get; set; }

        public virtual int PrimaryTransitionCount { get; set; }
        public virtual int DwellTime { get; set; }
        public virtual bool UseSlens { get; set; }
        public virtual bool WriteCompensationVoltages { get; set; }
        public virtual bool AddEnergyRamp { get; set; }
        public virtual bool AddTriggerReference { get; set; }
        public virtual double RunLength { get; set; }
        public virtual bool FullScans { get; set; }
        public virtual bool Tune3 { get; set; }

        public virtual bool Ms1Scan { get; set; }
        public virtual bool InclusionList { get; set; }
        public virtual string MsAnalyzer { get; set; }
        public virtual string MsMsAnalyzer { get; set; }

        public virtual bool ExportMultiQuant { get; set; }
        public virtual bool ExportSureQuant { get; set; }

        public virtual double? IntensityThresholdPercent { get; set; }
        public virtual double? IntensityThresholdValue { get; set; }
        public virtual double? IntensityThresholdMin { get; set; }

        public virtual bool RetentionStartAndEnd { get; set; }

        public virtual int MultiplexIsolationListCalculationTime { get; set; }
        public virtual bool DebugCycles { get; set; }

        public virtual bool ExportEdcMass { get; set; }

        public virtual double Ms1RepetitionTime { get; set; }

        public TExp InitExporter<TExp>(TExp exporter)
            where TExp : AbstractMassListExporter
        {
            exporter.Strategy = ExportStrategy;
            exporter.SortByMz = SortByMz;
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
            exporter.PolarityFilter = PolarityFilter;
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
                    if (type == ExportFileType.IsolationList)
                        return ExportAbiTofIsolationList(doc, path, template);
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
                    if (doc.Settings.TransitionSettings.FullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA)
                    {
                        ExportBrukerDiaMethod(doc, path, template);
                        return null;
                    }
                    return ExportBrukerMethod(doc, path, template);
                case ExportInstrumentType.BRUKER_TIMSTOF:
                    if (type == ExportFileType.IsolationList)
                        return ExportBrukerTimsTofIsolationList(doc, path);
                    return ExportBrukerTimsTofMethod(doc, path, template);
                case ExportInstrumentType.THERMO:
                case ExportInstrumentType.THERMO_TSQ:
                    if (type == ExportFileType.List)
                        return ExportThermoCsv(doc, path);
                    else
                        return ExportThermoMethod(doc, path, template);
                case ExportInstrumentType.THERMO_QUANTIVA:
                case ExportInstrumentType.THERMO_ALTIS:
                    if (type == ExportFileType.List)
                        return ExportThermoQuantivaCsv(doc, path);
                    else
                        return ExportThermoQuantivaMethod(doc, path, template, instrumentType);
                case ExportInstrumentType.THERMO_FUSION:
                    if (type == ExportFileType.IsolationList)
                    {
                        if (doc.Settings.TransitionSettings.FullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA)
                        {
                            ExportThermoFusionDiaList(
                                doc.Settings.TransitionSettings.FullScan.IsolationScheme,
                                doc.Settings.TransitionSettings.Instrument.MaxInclusions,
                                path,
                                MultiplexIsolationListCalculationTime,
                                DebugCycles);
                            return null;
                        }
                        return ExportThermoFusionIsolationList(doc, path, template);
                    }
                    else
                        return ExportThermoSureQuantMethod(doc, path, template, instrumentType);
                case ExportInstrumentType.THERMO_ECLIPSE:
                case ExportInstrumentType.THERMO_EXPLORIS:
                case ExportInstrumentType.THERMO_FUSION_LUMOS:
                    return ExportThermoSureQuantMethod(doc, path, template, instrumentType);
                case ExportInstrumentType.SHIMADZU:
                    if (type == ExportFileType.List)
                        return ExportShimadzuCsv(doc, path);
                    else
                        return ExportShimadzuMethod(doc, path, template);
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
                case ExportInstrumentType.WATERS_SYNAPT_TRAP:
                case ExportInstrumentType.WATERS_SYNAPT_TRANSFER:
                case ExportInstrumentType.WATERS_XEVO_TQ:
                case ExportInstrumentType.WATERS_XEVO_QTOF:
                    if (type == ExportFileType.List)
                        return ExportWatersCsv(doc, path);
                    else if (type == ExportFileType.IsolationList)
                        return ExportWatersIsolationList(doc, path, template, instrumentType);
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

        public AbstractMassListExporter ExportAbiTofIsolationList(SrmDocument document, string fileName, string templateName)
        {
            var exporter = new AbiTofIsolationListExporter(document);
            exporter.ExportIsolationList(fileName);

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
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public void ExportBrukerDiaMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = new BrukerDiaExporter(document) {RunLength = RunLength};

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));
        }

        public AbstractMassListExporter ExportBrukerTimsTofIsolationList(SrmDocument document, string filename)
        {
            var exporter = InitExporter(new BrukerTimsTofIsolationListExporter(document));
            exporter.RunLength = RunLength;
            PerformLongExport(m => exporter.ExportMethod(filename, m));
            return exporter;
        }

        public AbstractMassListExporter ExportBrukerTimsTofMethod(SrmDocument document, string filename, string templateName)
        {
            var exporter = InitExporter(new BrukerTimsTofMethodExporter(document));
            exporter.RunLength = RunLength;
            exporter.Ms1RepetitionTime = Ms1RepetitionTime;
            PerformLongExport(m => exporter.ExportMethod(filename, templateName, m, out _, out _, false));
            return exporter;
        }

        public AbstractMassListExporter ExportThermoCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new ThermoMassListExporter(document));
            exporter.UseSlens = UseSlens;
            exporter.AddEnergyRamp = AddEnergyRamp;
            exporter.AddTriggerReference = AddTriggerReference;
            exporter.RetentionStartAndEnd = RetentionStartAndEnd;
            exporter.Export(fileName);

            return exporter;
        }

        public AbstractMassListExporter ExportThermoQuantivaCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new ThermoQuantivaMassListExporter(document));
            exporter.UseSlens = UseSlens;
            exporter.WriteFaimsCv = WriteCompensationVoltages;
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.RetentionStartAndEnd = RetentionStartAndEnd;
            exporter.Export(fileName);

            return exporter;
        }

        public AbstractMassListExporter ExportShimadzuCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new ShimadzuMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            PerformLongExport(m => exporter.ExportMethod(fileName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportShimadzuMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ShimadzuMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportBrukerCsv(SrmDocument document, string filename)
        {
            var exporter = InitExporter(new BrukerMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
            {
                // TODO: Needs both run length and dwell time
                exporter.DwellTime = DwellTime;
            }
            exporter.Export(filename);

            return exporter;
        }

        public AbstractMassListExporter ExportThermoMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoMethodExporter(document));
            exporter.UseSlens = UseSlens;
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportThermoLtqMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoLtqMethodExporter(document));
            exporter.UseSlens = UseSlens;
            exporter.FullScans = FullScans;
            exporter.RunLength = RunLength;

            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportThermoQuantivaMethod(SrmDocument document, string fileName, string templateName, string instrumentType)
        {
            var exporter = InitExporter(new ThermoQuantivaMethodExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.RetentionStartAndEnd = RetentionStartAndEnd;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, instrumentType, m));

            return exporter;
        }

        public AbstractMassListExporter ExportThermoFusionIsolationList(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoFusionMassListExporter(document));
            exporter.UseSlens = UseSlens;
            exporter.WriteFaimsCv = WriteCompensationVoltages;
            exporter.Tune3 = Tune3;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public void ExportThermoFusionDiaList(IsolationScheme isolationScheme, int? maxInclusions, string fileName,
            int calculationTime, bool debugCycles)
        {
            var exporter = new ThermoFusionDiaExporter(isolationScheme, maxInclusions)
            {
                CalculationTime = calculationTime,
                DebugCycles = debugCycles
            };
            PerformLongExport(m => exporter.ExportIsolationList(fileName, m));
        }

        public AbstractMassListExporter ExportThermoQExactiveIsolationList(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoQExactiveIsolationListExporter(document));
            exporter.UseSlens = UseSlens;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public void ExportThermoQExactiveDiaList(IsolationScheme isolationScheme, int? maxInclusions, string fileName,
            int calculationTime, bool debugCycles)
        {
            var exporter = new ThermoQExactiveDiaExporter(isolationScheme, maxInclusions)  // TODO bspratt does this need SLens?
                {
                    CalculationTime = calculationTime,
                    DebugCycles = debugCycles
                };

            PerformLongExport(m => exporter.ExportIsolationList(fileName, m));
        }

        public AbstractMassListExporter ExportThermoSureQuantMethod(SrmDocument document, string fileName,
            string templateName, string instrumentType)
        {
            var exporter = InitExporter(new ThermoSureQuantMethodExporter(document, instrumentType, ExportSureQuant));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.UseSlens = UseSlens;
            exporter.WriteFaimsCv = WriteCompensationVoltages;
            exporter.RetentionStartAndEnd = RetentionStartAndEnd;
            exporter.IntensityThresholdPercent = IntensityThresholdPercent;
            exporter.IntensityThresholdValue = IntensityThresholdValue;
            exporter.IntensityThresholdMin = IntensityThresholdMin;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportWatersCsv(SrmDocument document, string fileName)
        {
            var exporter = InitExporter(new WatersMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.Export(fileName);

            return exporter;
        }

        public AbstractMassListExporter ExportWatersIsolationList(SrmDocument document, string fileName,
            string templateName, string instrumentType)
        {
            var exporter = InitExporter(new WatersIsolationListExporter(document, instrumentType));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.ExportEdcMass = ExportEdcMass;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

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
        public static string COV { get { return Resources.ExportOptimize_COV_Compensation_Voltage; } }

        public static string[] OptimizeTypes { get { return new[] { NONE, CE, DP, COV }; } }

        public static string COV_ROUGH { get { return Resources.ExportOptimize_COV_ROUGH_Rough_Tune; } }
        public static string COV_MEDIUM { get { return Resources.ExportOptimize_COV_MEDIUM_Medium_Tune; } }
        public static string COV_FINE { get { return Resources.ExportOptimize_COV_FINE_Fine_Tune; } }

        public static string[] CompensationVoltageTuneTypes { get { return new[] { COV_ROUGH, COV_MEDIUM, COV_FINE }; } }
    }

    public class ThermoMassListExporter : AbstractMassListExporter
    {
        private bool _addTriggerReference;
        public const double DEFAULT_SLENS = 50; // per Rick Bagshaw
        protected HashSet<Target> _setRTStandards;

        public ThermoMassListExporter(SrmDocument document)
            : base(document, null)
        {
            _setRTStandards = new HashSet<Target>();
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

        public bool UseSlens { get; set; }

        public double? RunLength { get; set; }

        public bool RetentionStartAndEnd { get; set; }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.THERMO; }
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
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
                if (predictedRT.HasValue)
                {
                    if (RetentionStartAndEnd)
                    {
                        // Start Time and Stop Time
                        writer.Write(Math.Max(0, predictedRT.Value - windowRT/2).ToString(CultureInfo));
                        // No negative retention times
                        writer.Write(FieldSeparator);
                        writer.Write((predictedRT.Value + windowRT/2).ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                    }
                    else
                    {
                        writer.Write(predictedRT.Value.ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                        writer.Write(windowRT.ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                    }
                }
                else
                {
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                }

                if (UseSlens)
                {
                    writer.Write(ExplicitTransitionValues.Get(nodeTran).SLens ?? DEFAULT_SLENS);
                    writer.Write(FieldSeparator);
                }

                writer.Write((nodeTranGroup.TransitionGroup.PrecursorAdduct.AdductCharge > 0) ? 1 : 0);  // Polarity
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
                        writer.Write(@"1.0E+10");  // Trigger 
                        writer.Write(FieldSeparator);
                        writer.Write(1);          // Secondary
                        writer.Write(FieldSeparator);
                    }                    
                }
                else if (AddTriggerReference)
                {
                    if (nodePep.IsProteomic && _setRTStandards.Contains(Document.Settings.GetModifiedSequence(nodePep)))
                    {
                        writer.Write(1000);  // Trigger
                        writer.Write(FieldSeparator);
                        writer.Write(2);     // Reference
                        writer.Write(FieldSeparator);
                    }
                    else
                    {
                        writer.Write(@"1.0E+10");  // Trigger 
                        writer.Write(FieldSeparator);
                        writer.Write(0);     // Reference
                        writer.Write(FieldSeparator);
                    }
                }
            }
            else if (RunLength.HasValue)
            {
                if (RetentionStartAndEnd)
                {
                    writer.Write(0); // No negative retention times
                    writer.Write(FieldSeparator);
                    writer.Write(RunLength);
                    writer.Write(FieldSeparator);
                }
                else
                {
                    writer.Write(RunLength/2);
                    writer.Write(FieldSeparator);
                    writer.Write(RunLength);
                    writer.Write(FieldSeparator);
                }
                writer.Write((nodeTranGroup.TransitionGroup.PrecursorAdduct.AdductCharge > 0) ? 1 : 0);  // Polarity
                writer.Write(FieldSeparator);                                    
            }
            // Write modified sequence for the light peptide molecular structure
            writer.WriteDsvField(GetCompound(nodePep, nodeTranGroup), FieldSeparator, FieldSeparatorReplacement);
            writer.Write(FieldSeparator);
            writer.WriteDsvField(nodePepGroup.Name, FieldSeparator);
            writer.Write(FieldSeparator);
            writer.WriteDsvField(nodeTran.Transition.GetFragmentIonName(CultureInfo.InvariantCulture), FieldSeparator, FieldSeparatorReplacement);
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

        protected const string EXE_BUILD_METHOD = @"Method\Thermo\BuildThermoMethod";

        // ReSharper disable LocalizableElement
        private static readonly string[] DEPENDENCY_LIBRARIES = {
                                                                    "Thermo.TNG.MethodXMLFactory.dll",
                                                                    "Thermo.TNG.MethodXMLInterface.dll"
                                                                };
        // ReSharper restore LocalizableElement

        protected static void EnsureLibraries()
        {
            string skylinePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(skylinePath))
                throw new IOException(Resources.ThermoMassListExporter_EnsureLibraries_Thermo_method_creation_software_may_not_be_installed_correctly_);

            // ReSharper disable ConstantNullCoalescingCondition
            string buildSubdir = Path.GetDirectoryName(EXE_BUILD_METHOD) ?? string.Empty;
            string exeDir = Path.Combine(Path.GetDirectoryName(skylinePath) ?? string.Empty, buildSubdir);
            string instrumentSoftwarePath = GetSoftwarePath();                                                        
            if (instrumentSoftwarePath == null)
            {
                // If all the necessary libraries exist, then continue even if MassLynx is gone.
                foreach (var libraryName in DEPENDENCY_LIBRARIES)
                {
                    if (!File.Exists(Path.Combine(exeDir, libraryName)))
                        throw new IOException(Resources.ThermoMassListExporter_EnsureLibraries_Failed_to_find_a_valid_Thermo_instrument_installation_);
                }
                return;
            }

            // ReSharper restore ConstantNullCoalescingCondition
            foreach (var library in DEPENDENCY_LIBRARIES)
            {
                string srcFile = Path.Combine(instrumentSoftwarePath, library);
                if (!File.Exists(srcFile))
                {
                    throw new IOException(
                        string.Format(Resources.ThermoMassListExporter_EnsureLibraries_Thermo_instrument_software_may_not_be_installed_correctly__The_library__0__could_not_be_found_,
                                      srcFile));
                }
                // If destination file does not exist or has a different modification time from
                // the source, then copy the source file from the installation.
                string destFile = Path.Combine(exeDir, library);
                if (!File.Exists(destFile) || !Equals(File.GetLastWriteTime(destFile), File.GetLastWriteTime(srcFile)))
                    File.Copy(srcFile, destFile, true);
            }
        }

        private static string GetSoftwarePath()
        {
            try
            {
                // CONSIDER: Might be worth breaking this up to provide more helpful error messages
                using (var tngKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Thermo Instruments\TNG"))
                using (var machineKey = GetFirstSubKey(tngKey))
                using (var versionKey = GetFirstSubKey(machineKey))
                {
                    if (versionKey == null)
                        return null;
                    var valueObject = versionKey.GetValue(@"ProgramPath");
                    if (valueObject == null)
                        return null;
                    return valueObject.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        private static RegistryKey GetFirstSubKey(RegistryKey parentKey)
        {
            if (parentKey == null)
                return null;
            int keyCount = parentKey.SubKeyCount;
            if (keyCount < 1)
                return null;
            return parentKey.OpenSubKey(parentKey.GetSubKeyNames()[0]);
        }
    }

    public class ThermoQuantivaMassListExporter : ThermoMassListExporter
    {
        // Hack to workaround Quantiva limitation
        protected readonly Dictionary<string, int> _compoundCounts = new Dictionary<string, int>();
        protected const int MAX_COMPOUND_NAME = 10;

        protected bool USE_COMPOUND_COUNT_WORKAROUND { get { return true; } }

        public bool WriteFaimsCv { get; set; }

        public ThermoQuantivaMassListExporter(SrmDocument document)
            : base(document)
        {
        }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.THERMO_QUANTIVA; }    // and THERMO_ALTIS
        }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write(@"Compound");
            writer.Write(FieldSeparator);
            if (RetentionStartAndEnd)
            {
                writer.Write(@"Start Time (min)");
                writer.Write(FieldSeparator);
                writer.Write(@"End Time (min)");
                writer.Write(FieldSeparator);
            }
            else
            {
                writer.Write(@"Retention Time (min)");
                writer.Write(FieldSeparator);
                writer.Write(@"RT Window (min)");
                writer.Write(FieldSeparator);
            }
            writer.Write(@"Polarity");
            writer.Write(FieldSeparator);
            writer.Write(@"Precursor (m/z)");
            writer.Write(FieldSeparator);
            writer.Write(@"Product (m/z)");
            writer.Write(FieldSeparator);
            writer.Write(@"Collision Energy (V)");
            if (UseSlens)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"S-lens");
            }
            if (WriteFaimsCv)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"FAIMS CV (V)");
            }
            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            string compound = string.Format(@"{0}{1}({2}{3})",
                                            GetCompound(nodePep, nodeTranGroup),
                                            nodeTranGroup.TransitionGroup.LabelTypeText,
                                            nodeTranGroup.PrecursorCharge >= 0 ? '+' : '-',
                                            nodeTranGroup.PrecursorCharge);
            if (step != 0)
            {
                compound += '.' + step.ToString(CultureInfo);
            }

            if (USE_COMPOUND_COUNT_WORKAROUND)
            {
                if (!_compoundCounts.ContainsKey(compound))
                {
                    _compoundCounts[compound] = 0;
                }
                else
                {
                    int compoundStep = ++_compoundCounts[compound]/MAX_COMPOUND_NAME + 1;
                    if (compoundStep > 1)
                        compound += '.' + compoundStep.ToString(CultureInfo);
                }
            }
            writer.WriteDsvField(compound, FieldSeparator);
            writer.Write(FieldSeparator);
            
            // Retention time
            if (MethodType == ExportMethodType.Standard)
            {
                if (RetentionStartAndEnd)
                {
                    // Start Time and Stop Time
                    writer.Write(0);
                    writer.Write(FieldSeparator);
                    writer.Write(RunLength);
                    writer.Write(FieldSeparator);
                }
                else
                {
                    writer.Write(RunLength/2);
                    writer.Write(FieldSeparator);
                    writer.Write(RunLength);
                    writer.Write(FieldSeparator);
                }
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
                    if (RetentionStartAndEnd)
                    {
                        // Start Time and Stop Time
                        writer.Write(Math.Max(0, predictedRT.Value - windowRT/2).ToString(CultureInfo));
                        // No negative retention times
                        writer.Write(FieldSeparator);
                        writer.Write((predictedRT.Value + windowRT/2).ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                    }
                    else
                    {
                        writer.Write(predictedRT.Value.ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                        writer.Write(windowRT.ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                    }
                }
                else
                {
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                }
            }

            writer.Write(nodeTranGroup.PrecursorCharge > 0 ? @"Positive" : @"Negative");
            writer.Write(FieldSeparator);
            writer.Write((Math.Truncate(1000*nodeTranGroup.PrecursorMz)/1000).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            if (UseSlens)
            {
                writer.Write(FieldSeparator);
                writer.Write((ExplicitTransitionValues.Get(nodeTran).SLens ?? DEFAULT_SLENS).ToString(CultureInfo));
            }
            if (WriteFaimsCv)
            {
                writer.Write(FieldSeparator);
                writer.Write(GetCompensationVoltage(nodePep, nodeTranGroup, nodeTran, step));
            }
            writer.WriteLine();
        }
    }

    public class ShimadzuMassListExporter : AbstractMassListExporter
    {
        public const string EXT_SHIMADZU_TRANSITION_LIST = ".txt";
        public const string EXE_BUILD_SHIMADZU_METHOD = @"Method\Shimadzu\BuildShimadzuMethod";

        public double? RunLength { get; set; }
        private int LastFileNumber { get; set; }

        private EventInfo _eventInfo;

        private class EventInfo
        {
            private const int MAX_TRANSITIONS_PER_EVENT = 32;
            public int Id { get; set; }
            public int PeptideId { get; set; }
            private int TransitionsWritten { get; set; }

            public EventInfo()
            {
                Id = 1;
                PeptideId = -1;
                TransitionsWritten = 0;
            }

            public void Write(TextWriter writer, int peptideId)
            {
                if (peptideId != PeptideId)
                {
                    Next();
                    PeptideId = peptideId;
                }
                writer.Write(Id);
                TransitionsWritten++;
                if (TransitionsWritten >= MAX_TRANSITIONS_PER_EVENT)
                {
                    Next();
                }
            }

            private void Next()
            {
                if (TransitionsWritten > 0)
                    Id++;
                TransitionsWritten = 0;
            }
        }

        public ShimadzuMassListExporter(SrmDocument document)
            : base(document, null)
        {
            LastFileNumber = -1;
        }

        protected override string InstrumentType { get { return ExportInstrumentType.SHIMADZU; } }
        
        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write(@"Peptide");
            writer.Write(FieldSeparator);
            writer.Write(@"ID");
            writer.Write(FieldSeparator);
            writer.Write(@"Type");
            writer.Write(FieldSeparator);
            writer.Write(@"Precursor");
            writer.Write(FieldSeparator);
            writer.Write(@"Product");
            writer.Write(FieldSeparator);
            writer.Write(@"RT");
            writer.Write(FieldSeparator);
            writer.Write(@"RT Window");
            writer.Write(FieldSeparator);
            writer.Write(@"CE");
            writer.Write(FieldSeparator);
            writer.Write(@"Polarity");

            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            if (fileNumber != LastFileNumber)
            {
                // When generating multiple files, Shimadzu expects each one to start from ID 1,
                // so reset if this is a new file
                LastFileNumber = fileNumber;
                _eventInfo = new EventInfo();
            }
            var compound = GetCompound(nodePep, nodeTranGroup) + @"_" + nodeTranGroup.TransitionGroup.LabelType;
            if (step != 0)
                compound += (@"_" + step);
            writer.WriteDsvField(compound.Replace(' ', '_'), FieldSeparator);
            writer.Write(FieldSeparator);
            _eventInfo.Write(writer, nodePep.Id.GlobalIndex);
            writer.Write(FieldSeparator);
            var istdTypes = Document.Settings.PeptideSettings.Modifications.InternalStandardTypes;
            writer.Write(istdTypes.Contains(nodeTranGroup.TransitionGroup.LabelType)
                             ? @"ISTD"
                             : String.Empty);
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
                    writer.Write(predictedRT.Value.ToString(CultureInfo));
                    writer.Write(FieldSeparator);
                    writer.Write(windowRT.ToString(CultureInfo));
                    writer.Write(FieldSeparator);
                }
                else
                {
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                }
            }

            var ce = Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1);
            bool positiveIon = nodeTranGroup.PrecursorCharge >= 0;
            if (positiveIon)
                ce = -ce;
            writer.Write(ce.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(positiveIon ? 0 : 1);
            writer.WriteLine();
        }

        public void ExportMethod(string fileName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            var argv = new List<string>();
            MethodExporter.ExportMethod(EXE_BUILD_SHIMADZU_METHOD, argv, fileName, null, MemoryOutput, progressMonitor);
        }
    }

    public class ShimadzuMethodExporter : ShimadzuMassListExporter
    {
        public ShimadzuMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            var argv = new List<string>();
            MethodExporter.ExportMethod(EXE_BUILD_SHIMADZU_METHOD, argv, fileName, templateName, MemoryOutput, progressMonitor);
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
            writer.Write(@"Compound Name");
            writer.Write(FieldSeparator);
            writer.Write(@"Retention Time");
            writer.Write(FieldSeparator);
            writer.Write(@"Retention Time Window");
            writer.Write(FieldSeparator);
            writer.Write(@"CAS Number");
            writer.Write(FieldSeparator);
            writer.Write(@"Retention Index");
            writer.Write(FieldSeparator);
            writer.Write(@"Scan Type");
            writer.Write(FieldSeparator);
            writer.Write(@"Polarity");
            writer.Write(FieldSeparator);
            writer.Write(@"Scan Time (ms)");
            writer.Write(FieldSeparator);
            writer.Write(@"Separation Method");
            writer.Write(FieldSeparator);
            writer.Write(@"Source");
            writer.Write(FieldSeparator);
            writer.Write(@"Regulation");
            writer.Write(FieldSeparator);
            writer.Write(@"Classification");
            writer.Write(FieldSeparator);
            writer.Write(@"Comment");
            writer.Write(FieldSeparator);
            writer.Write(@"Transitions Count");
            writer.Write(FieldSeparator);
            writer.Write(@"Q1 First Mass");
            writer.Write(FieldSeparator);
            writer.Write(@"Q1 Last Mass");
            writer.Write(FieldSeparator);
            writer.Write(@"Q1 Resolution");
            writer.Write(FieldSeparator);
            writer.Write(@"Q3 First Mass");
            writer.Write(FieldSeparator);
            writer.Write(@"Q3 Last Mass");
            writer.Write(FieldSeparator);
            writer.Write(@"Q3 Resolution");
            writer.Write(FieldSeparator);
            writer.Write(@"Collision Energy");
            writer.Write(FieldSeparator);
            writer.Write(@"Dwell Time (ms)");
            writer.Write(FieldSeparator);
            writer.Write(@"Is Quantifier");
            writer.Write(FieldSeparator);
            writer.Write(@"Quantifier Ions");
            writer.Write(FieldSeparator);
            writer.Write(@"Is Qualifier");
            writer.Write(FieldSeparator);
            writer.Write(@"Qualifier Count");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Mass 1");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Ratio 1");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Mass 2");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Ratio 2");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Mass 3");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Ratio 3");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Mass 4");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Ratio 4");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Mass 5");
            writer.Write(FieldSeparator);
            writer.Write(@"Qual Ratio 5");
            writer.Write(FieldSeparator);
            writer.Write(@"GUID (Dont fill this Column)");

            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            // Compound Name
            var compound = GetCompound(nodePep, nodeTranGroup);
            compound += nodeTranGroup.PrecursorAdduct.AsFormulaOrSigns(); // Something like +++ or -- or [M+Na]
            writer.WriteDsvField(compound, FieldSeparator);
            writer.Write(FieldSeparator);
            // Retention Time
            double? rt = null;
            if (MethodType == ExportMethodType.Standard)
            {
                // TODO: Should have run length here
//                writer.Write(RunLength); // Store for later use
                writer.Write(FieldSeparator);
//                writer.Write((RunLength/2).ToString(CultureInfo));
                writer.Write(FieldSeparator);
            }
            else
            {
                // Scheduling information
                double rtWindow;
                rt = Document.Settings.PeptideSettings.Prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, Document.Settings.HasResults, out rtWindow);
                if (rt.HasValue)
                    writer.Write(rt);
                writer.Write(FieldSeparator);
                // Retention Time Window
                if (rt.HasValue)
                    writer.Write(rtWindow);
                writer.Write(FieldSeparator);
            }
            // CAS Number
            writer.Write(GetCAS(nodePep, nodeTranGroup));
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
            writer.Write(@"MRM");
            writer.Write(FieldSeparator);
            // Polarity
            if (nodeTranGroup.TransitionGroup.PrecursorCharge > 0)
                writer.Write(@"Positive");
            else
                writer.Write(@"Negative");
            writer.Write(FieldSeparator);
            // Scan Time (ms)
            writer.Write(@"100");
            writer.Write(FieldSeparator);
            // Separation Method
            writer.Write(@"LCMS");
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
            writer.Write(DwellTime.HasValue ? DwellTime.ToString() : string.Empty);
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
        public const string EXE_BUILD_TSQ_METHOD = @"Method\Thermo\BuildTSQEZMethod";

        public ThermoMethodExporter(SrmDocument document)
            : base(document)
        {
            RetentionStartAndEnd = true;    // Because the converter depends on this format
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            RetentionStartAndEnd = true;    // Because the converter depends on this format
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
                argv.Add(@"-f");
            if(MsAnalyzer != null)
                argv.Add(String.Format(@"-a {0}", MsAnalyzer));
            if(MsMsAnalyzer != null)
                argv.Add(String.Format(@"-b {0}", MsMsAnalyzer));
            if(InclusionList)
                argv.Add(@"-i");
            if(Ms1Scan)
                argv.Add(@"-1");
            MethodExporter.ExportMethod(EXE_BUILD_LTQ_METHOD, argv,
                fileName, templateName, MemoryOutput, progressMonitor);
        }
    }

    public class ThermoQuantivaMethodExporter : ThermoQuantivaMassListExporter
    {
        public ThermoQuantivaMethodExporter(SrmDocument document)
            : base(document)
        {
        }

        public void ExportMethod(string fileName, string templateName, string instrumentType, IProgressMonitor progressMonitor)
        {
            if (fileName != null)
                EnsureLibraries();

            if (!InitExport(fileName, progressMonitor))
                return;

            var argv = new List<string>();
            if (instrumentType.Equals(ExportInstrumentType.THERMO_ENDURA))
            {
                argv.Add(@"-e");
            }
            else if (instrumentType.Equals(ExportInstrumentType.THERMO_QUANTIVA))
            {
                argv.Add(@"-q");
            }
            else if (instrumentType.Equals(ExportInstrumentType.THERMO_ALTIS))
            {
                argv.Add(@"-a");
            }
            MethodExporter.ExportMethod(EXE_BUILD_METHOD, argv, fileName, templateName, MemoryOutput, progressMonitor);
        }
    }

    public class ThermoSureQuantMethodExporter : ThermoMassListExporter
    {
        public ThermoSureQuantMethodExporter(SrmDocument document, string instrumentType, bool surequant)
            : base(document)
        {
            if (!surequant)
            {
                IsPrecursorLimited = true;
                IsolationList = true;
            }
            _instrumentType = instrumentType;
            _surequant = surequant;
        }

        private readonly string _instrumentType;
        private readonly bool _surequant;

        public bool WriteFaimsCv { get; set; }

        private const double DEFAULT_INTENSITY_THRESHOLD_PERCENT = 1;

        public double? IntensityThresholdPercent { get; set; }
        public double? IntensityThresholdValue { get; set; }
        public double? IntensityThresholdMin { get; set; }

        protected override string InstrumentType => _instrumentType;

        public override bool HasHeaders => true;

        protected override void WriteHeaders(TextWriter writer)
        {
            if (_surequant)
            {
                writer.Write(@"SureQuant Info");
                writer.Write(FieldSeparator);
            }
            writer.Write(@"Compound");
            writer.Write(FieldSeparator);
            if (RetentionStartAndEnd)
            {
                writer.Write(@"t start (min)");
                writer.Write(FieldSeparator);
                writer.Write(@"t stop (min)");
                writer.Write(FieldSeparator);
            }
            else
            {
                writer.Write(@"Retention Time (min)");
                writer.Write(FieldSeparator);
                writer.Write(@"RT Window (min)");
                writer.Write(FieldSeparator);
            }
            writer.Write(@"Polarity");
            writer.Write(FieldSeparator);
            writer.Write(@"m/z");
            writer.Write(FieldSeparator);
            writer.Write(@"Product (m/z)");
            writer.Write(FieldSeparator);
            writer.Write(@"CID Collision Energy (%)");
            if (UseSlens)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"S-lens");
            }
            if (WriteFaimsCv)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"FAIMS CV (V)");
            }
            writer.Write(FieldSeparator);
            writer.Write(@"Intensity Threshold");
            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
            int fileNumber,
            PeptideGroupDocNode nodePepGroup,
            PeptideDocNode nodePep,
            TransitionGroupDocNode nodeTranGroup,
            TransitionGroupDocNode nodeTranGroupPrimary,
            TransitionDocNode nodeTran,
            int step)
        {
            if (_surequant)
            {
                // <precursor charge><H|L><target>;[*]<transition name>
                var surequantInfo = nodeTranGroup.PrecursorCharge.ToString(CultureInfo);
                surequantInfo += Equals(nodeTranGroup.LabelType, IsotopeLabelType.heavy) ? 'H' : 'L';
                surequantInfo += nodePep.Target;
                surequantInfo += ';';
                if (nodeTran.Transition.IsPrecursor())
                    surequantInfo += '*';
                surequantInfo += nodeTran.FragmentIonName;
                writer.WriteDsvField(surequantInfo, FieldSeparator);
                writer.Write(FieldSeparator);
            }

            var compound = string.Format(@"{0}{1}({2}{3})",
                GetCompound(nodePep, nodeTranGroup),
                nodeTranGroup.TransitionGroup.LabelTypeText,
                nodeTranGroup.PrecursorCharge >= 0 ? '+' : '-',
                nodeTranGroup.PrecursorCharge);
            if (step != 0)
            {
                compound += '.' + step.ToString(CultureInfo);
            }

            writer.WriteDsvField(compound, FieldSeparator);
            writer.Write(FieldSeparator);

            // Retention time
            if (MethodType == ExportMethodType.Standard)
            {
                if (RetentionStartAndEnd)
                {
                    // Start Time and Stop Time
                    writer.Write(0);
                    writer.Write(FieldSeparator);
                    writer.Write(RunLength);
                    writer.Write(FieldSeparator);
                }
                else
                {
                    writer.Write(RunLength / 2);
                    writer.Write(FieldSeparator);
                    writer.Write(RunLength);
                    writer.Write(FieldSeparator);
                }
            }
            else
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                var predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out var windowRT);
                predictedRT = RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT);
                if (predictedRT.HasValue)
                {
                    if (RetentionStartAndEnd)
                    {
                        // Start Time and Stop Time
                        writer.Write(Math.Max(0, predictedRT.Value - windowRT / 2).ToString(CultureInfo));
                        // No negative retention times
                        writer.Write(FieldSeparator);
                        writer.Write((predictedRT.Value + windowRT / 2).ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                    }
                    else
                    {
                        writer.Write(predictedRT.Value.ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                        writer.Write(windowRT.ToString(CultureInfo));
                        writer.Write(FieldSeparator);
                    }
                }
                else
                {
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                }
            }

            writer.Write(nodeTranGroup.PrecursorCharge > 0 ? @"Positive" : @"Negative");
            writer.Write(FieldSeparator);
            writer.Write((Math.Truncate(1000 * nodeTranGroup.PrecursorMz) / 1000).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(nodeTran != null ? GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo) : string.Empty);
            writer.Write(FieldSeparator);
            writer.Write(ThermoFusionMassListExporter.GetCE(Document).ToString(CultureInfo));

            if (UseSlens)
            {
                writer.Write(FieldSeparator);
                writer.Write((ExplicitTransitionValues.Get(nodeTran).SLens ?? DEFAULT_SLENS).ToString(CultureInfo));
            }
            if (WriteFaimsCv)
            {
                var cv = GetCompensationVoltage(nodePep, nodeTranGroup, nodeTran, step);
                writer.Write(FieldSeparator);
                writer.Write(cv.HasValue ? cv.Value.ToString(CultureInfo) : string.Empty);
            }
            var maxHeight = (double?) null;
            if (IntensityThresholdPercent.HasValue)
            {
                if (nodeTranGroup.HasResults)
                {
                    var heights = nodeTranGroup.Results.SelectMany(chromInfoList => chromInfoList.AsList())
                        .Select(ci => ci.Height).Where(h => h.HasValue).ToArray();
                    if (heights.Any())
                        maxHeight = heights.Max().Value * ((IntensityThresholdPercent ?? DEFAULT_INTENSITY_THRESHOLD_PERCENT) / 100);
                }
            }
            else if (IntensityThresholdValue.HasValue)
            {
                maxHeight = IntensityThresholdValue.Value;
            }
            if (IntensityThresholdMin.HasValue && maxHeight.GetValueOrDefault() < IntensityThresholdMin)
                maxHeight = IntensityThresholdMin;
            writer.Write(FieldSeparator);
            writer.Write(maxHeight);

            writer.WriteLine();
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (fileName != null)
                EnsureLibraries();

            if (!InitExport(fileName, progressMonitor))
                return;

            var argv = new List<string>();
            switch (_instrumentType)
            {
                case ExportInstrumentType.THERMO_EXPLORIS:
                    argv.Add(@"-p");
                    break;
                case ExportInstrumentType.THERMO_FUSION:
                    argv.Add(@"-f");
                    break;
                case ExportInstrumentType.THERMO_FUSION_LUMOS:
                    argv.Add(@"-l");
                    break;
                case ExportInstrumentType.THERMO_ECLIPSE:
                    argv.Add(@"-c");
                    break;
            }
            MethodExporter.ExportMethod(EXE_BUILD_METHOD, argv, fileName, templateName, MemoryOutput, progressMonitor);
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

    public class ThermoFusionDiaExporter : AbstractDiaExporter
    {
        private const char FIELD_SEPARATOR = ',';
        
        public ThermoFusionDiaExporter(IsolationScheme isolationScheme, int? maxInclusions)
            : base(isolationScheme, maxInclusions)
        {
        }

        public void ExportIsolationList(string fileName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            Export(fileName, progressMonitor);
        }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write(@"m/z");
            writer.Write(FIELD_SEPARATOR);
            writer.Write(@"z");
            writer.Write(FIELD_SEPARATOR);
            writer.Write(@"t start (min)");
            writer.Write(FIELD_SEPARATOR);
            writer.Write(@"t stop (min)");
            writer.Write(FIELD_SEPARATOR);
            writer.Write(@"Name");
            writer.Write(FIELD_SEPARATOR);
            writer.Write(@"Isolation Window (m/z)");
            writer.WriteLine();
        }

        protected override void WriteIsolationWindow(TextWriter writer, IsolationWindow isolationWindow)
        {
            // m/z
            writer.Write(SequenceMassCalc.PersistentMZ(isolationWindow.Target ?? isolationWindow.MethodCenter).ToString(CultureInfo.InvariantCulture));
            writer.Write(FIELD_SEPARATOR);
            // z
            writer.Write(FIELD_SEPARATOR);
            // t start (min)
            writer.Write(FIELD_SEPARATOR);
            // t stop (min)
            writer.Write(FIELD_SEPARATOR);
            // Name
            writer.Write(FIELD_SEPARATOR);
            // Isolation Window (m/z)
            writer.Write(SequenceMassCalc.PersistentMZ(isolationWindow.MethodEnd - isolationWindow.MethodStart).ToString(CultureInfo.InvariantCulture));
            writer.WriteLine();
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

        private readonly Dictionary<string, Adduct> _groupNamesToCharge = new Dictionary<string, Adduct>();

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.ABI; }
        }

        protected override IEnumerable<TransitionDocNode> GetTransitionsInBestOrder(TransitionGroupDocNode nodeGroup, TransitionGroupDocNode nodeGroupPrimary)
        {
            if(MethodType != ExportMethodType.Triggered)
            {
                return nodeGroup.Transitions;
            }

            IComparer<TransitionOrdered> comparer = TransitionOrdered.TransitionComparerInstance;
            // ReSharper disable once CollectionNeverQueried.Local
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

        // Helper function for emitting CE, Dxp and CoV values consistent with ionization mode, for Sciex
        private double AdjustParameterPolarity(TransitionDocNode nodeTran, double paramValue)
        {
            return nodeTran.Transition.Charge < 0 ? -paramValue : paramValue;
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
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

            double ceValue = GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step);
            if (ceValue < 10) // SCIEX does not allow CE below 10
            {
                if (OptimizeType == ExportOptimize.CE)
                    return;
                ceValue = 10;
            }
            ceValue = AdjustParameterPolarity(nodeTran, ceValue); // Sciex wants negative CE values for negative ion mode
            string ce = Math.Round(ceValue, 1).ToString(CultureInfo);
            double dpValue = GetDeclusteringPotential(nodePep, nodeTranGroup, nodeTran, step);
            // CONSIDER: Is there a minimum DP value?
            dpValue = AdjustParameterPolarity(nodeTran, dpValue); // Sciex wants negative DxP values for negative ion mode
            string dp = Math.Round(dpValue, 1).ToString(CultureInfo);

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
                primaryOrSecondary = IsPrimary(nodeTranGroup, nodeTranGroupPrimary, nodeTran) ? @"1" : @"2";
            }

            // TODO: Need better way to handle case where user give all CoV as explicit
            string compensationVoltage = null;
            if (Document.Settings.TransitionSettings.Prediction.CompensationVoltage != null)
            {
                var coV = GetCompensationVoltage(nodePep, nodeTranGroup, nodeTran, step).GetValueOrDefault();
                coV = AdjustParameterPolarity(nodeTran, coV); // Sciex wants negative CoV values for negative ion mode
                compensationVoltage =  string.Format(@",{0}", coV.ToString(@"0.00", CultureInfo));
            }

           string oneLine = string.Format(@"{0},{1},{2},{3}{4}{5}", q1, q3, dwellOrRt, extPeptideId,
                                           GetOptionalColumns(dp,
                                                              ce,
                                                              precursorWindow,
                                                              productWindow,
                                                              extGroupId,
                                                              averagePeakAreaText,
                                                              variableRtWindowText,
                                                              primaryOrSecondary),
                                           compensationVoltage);

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
                return string.Format(@",{0},{1},{2},{3},{4},{5},{6}",
                    extGroupId,
                    variableRtWindowText,
                    primaryOrSecondary,
                    @"1000",
                    @"1.0",
                    dp,
                    ce);
            }
            else // CSV
            {
                return string.Format(@",{0},{1}",
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
            string modifiedPepSequence = GetSequenceWithModsString(nodePep, Document.Settings);

            var charge = nodeTranGroup.TransitionGroup.PrecursorAdduct;
            var pepGroupName = nodePepGroup.Name.Replace('.', '_');
            extPeptideId = string.Format(@"{0}.{1}.{2}.{3}{4}",
                pepGroupName,
                modifiedPepSequence,
                GetTransitionName(charge, nodeTran),
                GetOptValueText(nodePepGroup, nodePep, nodeTranGroup, nodeTran, step),
                nodeTranGroup.TransitionGroup.LabelType);
            extGroupId = string.Format(@"{0}.{1}.{2}",
                pepGroupName,
                modifiedPepSequence,
                nodeTranGroup.TransitionGroup.LabelType);

            // remove commas to prevent addition of extra columns that will be misinterpretted in method builder exe 
            extPeptideId = extPeptideId.Replace(',', '_').Replace('/', '_').Replace(@"\", @"_");
            extGroupId = extGroupId.Replace(',', '_').Replace('/', '_').Replace(@"\", @"_");

            Adduct existCharge;
            if (!_groupNamesToCharge.TryGetValue(extGroupId, out existCharge))
            {
                _groupNamesToCharge.Add(extGroupId, charge);
            }
            else if (existCharge != charge)
            {
                extGroupId = string.Format(@"{0} {1}", extGroupId, charge.AsFormulaOrSignedInt());
            }
        }

        private string GetOptValueText(PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, TransitionDocNode nodeTran, int step)
        {
            if (OptimizeType != null)
            {
                string optPrefix = null;
                double optValue = 0;
                if (ExportOptimize.CE == OptimizeType)
                {
                    optPrefix = @"CE";
                    optValue = GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step);
                }
                else if (ExportOptimize.DP == OptimizeType)
                {
                    optPrefix = @"DP";
                    optValue = GetDeclusteringPotential(nodePep, nodeTranGroup, nodeTran, step);
                }
                else if (ExportOptimize.CompensationVoltageTuneTypes.Contains(OptimizeType))
                {
                    optPrefix = @"CoV";
                    optValue = GetCompensationVoltage(nodePep, nodeTranGroup, nodeTran, step).GetValueOrDefault();
                }
                else
                {
                    Assume.Fail(string.Format(@"Unexpected optimization type {0}", OptimizeType));
                }

                if (optPrefix != null)
                {
                    optValue = AdjustParameterPolarity(nodeTran, optValue);  // Sciex wants negative values for negative ion mode
                    return string.Format(@"{0}_{1}.", optPrefix, optValue.ToString(@"0.0", CultureInfo.InvariantCulture));
                }
            }
            return string.Empty;
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
                if (result.IsEmpty)
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

        internal static string GetSequenceWithModsString(PeptideDocNode nodePep, SrmSettings settings)
        {
            string result;
            if (nodePep.Peptide.IsCustomMolecule)
            {
                result = nodePep.CustomMolecule.DisplayName;
            }
            else
            {

                var staticExplicitMods = settings.PeptideSettings.Modifications.StaticModifications
                    .Where(mod => !mod.IsVariable).ToArray();
                var staticModsList = new StaticModList();
                staticModsList.AddRange(staticExplicitMods);
                var mods = new ExplicitMods(nodePep,
                                        staticExplicitMods,
                                        staticModsList, 
                                        new List<TypedModifications>(),
                                        null).ChangeCrosslinkStructure(nodePep.CrosslinkStructure);
            
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

                result = settings.GetModifiedSequence(nodePep.Peptide.Target,
                                                                            IsotopeLabelType.light,
                                                                            mods, SequenceModFormatType.three_letter_code,
                                                                            true).ToString();
            }

            return result.Replace('.', '_');
        }

        private static string GetTransitionName(Adduct precursorCharge, TransitionDocNode transitionNode)
        {
            string ionName = transitionNode.GetFragmentIonName(CultureInfo.InvariantCulture);
            if (transitionNode.Transition.IsPrecursor())
            {
                return GetPrecursorTransitionName(precursorCharge, ionName, transitionNode.Transition.MassIndex).Replace('.', '_');
            }
            else
            {
                return GetTransitionName(precursorCharge, ionName, transitionNode.Transition.Adduct).Replace('.', '_');
            }
        }

        public static string GetTransitionName(Adduct precursorCharge, string fragmentIonName, Adduct fragmentCharge)
        {
            return string.Format(@"{0}{1}{2}", precursorCharge.AsFormulaOrSignedInt(),
                                 fragmentIonName,
                                 fragmentCharge != Adduct.SINGLY_PROTONATED
                                     ? fragmentCharge.AsFormulaOrSignedInt()
                                     : string.Empty);
        }

        public static string GetPrecursorTransitionName(Adduct precursorCharge, string fragmentIonName, int isotopeIndex)
        {
            return string.Format(@"{0}{1}{2}", precursorCharge.AsFormulaOrSignedInt(),
                                 fragmentIonName,
                                 isotopeIndex > 0
                                     ? string.Format(@"[M+{0}]", isotopeIndex)
                                     : string.Empty);
        }
    }

    public abstract class AbiMethodExporter : AbiMassListExporter
    {
        private const string ANALYST_EXE = "Analyst.exe";

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
            string analystPath = AdvApi.GetPathFromProgId(@"Analyst.MassSpecMethod.1");
            string analystDir = null;
            if (analystPath != null)
                analystDir = Path.GetDirectoryName(analystPath);

            if (analystDir == null)
            {
                throw new IOException(Resources.AbiMethodExporter_EnsureAnalyst_Failed_to_find_a_valid_Analyst_installation);
            }


            var procAnalyst = AnalystProcess ?? Process.Start(Path.Combine(analystDir, ANALYST_EXE));
            // Wait for main window to be present.
            IProgressStatus status = null;
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

            if (process.MainWindowTitle.StartsWith(@"Analyst")
                && process.MainWindowTitle.Contains(@"Registration") == false)
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
                // ReSharper disable once PossibleNullReferenceException
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
            return string.Format(@",{0},{1},{2},{3},{4},{5},{6},{7},{8}",
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
            return @"SOFTWARE\PE SCIEX\Products\Analyst3Q";
        }

        protected override string GetExeName()
        {
            return @"Method\AbSciex\TQ\BuildQTRAPMethod";
        }

        protected override List<string> GetArgs()
        {
            var argv = new List<string>();
            if (RTWindow.HasValue)
            {
                argv.Add(@"-w");
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
                argv.Add(@"-1");
            if (InclusionList)
                argv.Add(@"-i");
            if (MethodType == ExportMethodType.Scheduled)
                argv.Add(@"-r");
            if (RTWindow.HasValue)
            {
                argv.Add(@"-w");
                argv.Add(RTWindow.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (ExportMultiQuant)
                argv.Add(@"-mq");

            return argv;
        }
    }

    public class AbiTofIsolationListExporter : AbstractMassListExporter
    {
        public const string EXT_ABI_TOF_ISOLATION_LIST = ".txt";

        public AbiTofIsolationListExporter(SrmDocument document)
            : base(document, null)
        {
            IsolationList = true;
        }

        public void ExportIsolationList(string fileName)
        {
            var fullScan = Document.Settings.TransitionSettings.FullScan;
            if (fullScan.AcquisitionMethod != FullScanAcquisitionMethod.DIA ||
                fullScan.IsolationScheme == null || fullScan.IsolationScheme.FromResults)
            {
                throw new IOException(
                    Resources.SkylineWindow_exportIsolationListMenuItem_Click_There_is_no_isolation_list_data_to_export);
            }

            using (var fileSaver = new FileSaver(fileName))
            {
                var writer = new StreamWriter(fileSaver.SafeName);
                foreach (var isolationWindow in fullScan.IsolationScheme.PrespecifiedIsolationWindows)
                {
                    if (isolationWindow.CERange.HasValue)
                        Write(writer, isolationWindow.MethodStart, isolationWindow.MethodEnd, isolationWindow.CERange.Value);
                    else
                        Write(writer, isolationWindow.MethodStart, isolationWindow.MethodEnd);
                }
                writer.Close();
                fileSaver.Commit();
            }
        }

        // Write values separated by the field separator, and a line separator at the end.
        private void Write(StreamWriter writer, params double[] vals)
        {
            // ReSharper disable LocalizableElement
            writer.WriteLine(string.Join("\t", vals.Select(s => s.ToString(CultureInfo.InvariantCulture))));
            // ReSharper restore LocalizableElement
        }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.ABI_TOF; }
        }

        /// <summary>
        /// Stubbed out override of abstract function not used in this class
        /// </summary>
        protected override void WriteTransition(TextWriter writer, int fileNumber, PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeTranGroup, TransitionGroupDocNode nodeTranGroupPrimary, TransitionDocNode nodeTran,
            int step)
        {
            throw new InvalidOperationException();  // Not expected to ever be called.
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
            writer.Write(@"Compound Group");
            writer.Write(FieldSeparator);
            writer.Write(@"Compound Name");
            writer.Write(FieldSeparator);
            writer.Write(@"ISTD?");
            writer.Write(FieldSeparator);
            writer.Write(@"Precursor Ion");
            writer.Write(FieldSeparator);
            writer.Write(@"MS1 Res");
            writer.Write(FieldSeparator);
            writer.Write(@"Product Ion");
            writer.Write(FieldSeparator);
            writer.Write(@"MS2 Res");
            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"Dwell");
            }
            else
            {
                writer.Write(FieldSeparator);
                writer.Write(@"Primary");
                if (MethodType == ExportMethodType.Triggered)
                {
                    writer.Write(FieldSeparator);
                    writer.Write(@"Trigger");
                }
                writer.Write(FieldSeparator);
                writer.Write(@"Threshold");
                writer.Write(FieldSeparator);
                writer.Write(@"Ret Time (min)");
                writer.Write(FieldSeparator);
                writer.Write(@"Delta Ret Time");
            }
            writer.Write(FieldSeparator);
            writer.Write(@"Fragmentor");
            writer.Write(FieldSeparator);
            writer.Write(@"Collision Energy");
            writer.Write(FieldSeparator);
            writer.Write(@"Cell Accelerator Voltage");
            writer.Write(FieldSeparator);
            writer.Write(@"Polarity");
            if (MethodType != ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"Trigger Entrance Delay (cycles)");
                writer.Write(FieldSeparator);
                writer.Write(@"Trigger Delay (cycles)");
                writer.Write(FieldSeparator);
                writer.Write(@"Trigger Window");
                writer.Write(FieldSeparator);
                writer.Write(@"IsLogicEnabled");
                writer.Write(FieldSeparator);
                writer.Write(@"Trigger Logic Flag");
                writer.Write(FieldSeparator);
                writer.Write(@"Trigger Ratio");
                writer.Write(FieldSeparator);
                writer.Write(@"Trigger Ratio Window");
                writer.Write(FieldSeparator);
                writer.Write(@"Ignore MRM");
            }
            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.WriteDsvField(nodePepGroup.Name, FieldSeparator, FieldSeparatorReplacement);
            writer.Write(FieldSeparator);
            // Write modified sequence for the light peptide molecule
            string compound = GetCompound(nodePep, nodeTranGroup);
            string compoundName = string.Format(@"{0}.{1}", compound, nodeTranGroup.TransitionGroup.LabelType);
            writer.WriteDsvField(compoundName, FieldSeparator, FieldSeparatorReplacement);

            writer.Write(FieldSeparator);
            var istdTypes = Document.Settings.PeptideSettings.Modifications.InternalStandardTypes;
            writer.Write(BoolToString(istdTypes.Contains(nodeTranGroup.TransitionGroup.LabelType))); // ISTD?
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(@"Unit");   // MS1 Res
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(@"Unit");   // MS2 Res
            writer.Write(FieldSeparator);

            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write(Math.Round(DwellTime, 2).ToString(CultureInfo));
            }
            else
            {
                int? rank = GetRank(nodeTranGroup, nodeTranGroupPrimary, nodeTran);
                writer.Write(BoolToString(rank.HasValue && rank.Value <= PrimaryTransitionCount)); // Primary
                if (MethodType == ExportMethodType.Triggered)
                {
                    writer.Write(FieldSeparator);
                    // Trigger must be rank 1 transition, of analyte type and minimum precursor charge
                    var trigger = false;
                    if (IsTriggerType(nodePep, nodeTranGroup, istdTypes) && rank.HasValue && rank.Value == 1)
                    {
                        int minCharge = nodePep.TransitionGroups.Select(g => Math.Abs(g.PrecursorCharge)).Min();
                        if (Math.Abs(nodeTranGroup.PrecursorCharge) == minCharge)
                            trigger = true;
                    }
                    writer.Write(BoolToString(trigger));
                }
                writer.Write(FieldSeparator);
                writer.Write(0.ToString(CultureInfo)); // Threshold
                writer.Write(FieldSeparator);

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
                }
                else
                {
                    writer.Write(FieldSeparator);
                }
            }

            writer.Write(FieldSeparator);
            writer.Write(Fragmentor.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(4);    // Cell Accelerator Voltage
            writer.Write(FieldSeparator);
            writer.Write(nodeTranGroup.PrecursorCharge > 0 ? @"Positive" : @"Negative"); // Polarity

            if (MethodType != ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write(0.ToString(CultureInfo)); // Trigger Entrance Delay
                writer.Write(FieldSeparator);
                writer.Write(0.ToString(CultureInfo)); // Trigger Delay
                writer.Write(FieldSeparator);
                writer.Write(0.ToString(CultureInfo)); // Trigger Window
                writer.Write(FieldSeparator);
                writer.Write(BoolToString(false)); // IsLogicEnabled
                writer.Write(FieldSeparator);
                writer.Write(@"AND"); // Trigger Logic Flag
                writer.Write(FieldSeparator);
                writer.Write(1.ToString(CultureInfo)); // Trigger Ratio
                writer.Write(FieldSeparator);
                writer.Write(1.ToString(CultureInfo)); // Trigger Ratio Window
                writer.Write(FieldSeparator);
                writer.Write(BoolToString(false)); // Ignore MRM
            }

            writer.WriteLine();
        }

        private static bool IsTriggerType(PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, IList<IsotopeLabelType> istdTypes)
        {
            // If there is a light precursor, then it is always the trigger
            if (nodeTranGroup.TransitionGroup.LabelType.IsLight)
                return true;
            // Get all precursors with the same charge state and at least 1 transition, including this one
            var arrayTranGroups = nodePep.TransitionGroups
                .Where(g => g.PrecursorAdduct.Equals(nodeTranGroup.PrecursorAdduct) &&
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

        private static string BoolToString(bool b)
        {
            return b ? @"TRUE" : @"FALSE";
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
            return methodPath.EndsWith(ExportInstrumentType.EXT_AGILENT) && File.Exists(Path.Combine(methodPath, @"qqqacqmeth.xsd"));
        }
    }

    public class AgilentIsolationListExporter : AgilentMassListExporter
    {
        public AgilentIsolationListExporter(SrmDocument document)
            : base(document)
        {
            IsPrecursorLimited = true;
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
            return @"On,Prec. m/z,Delta m/z (ppm),Z,Prec. Type,Ret. Time (min),Delta Ret. Time (min),Iso. Width,Collision Energy".Replace(',', fieldSeparator);
        }

        public static string GetTargetedHeader(char fieldSeparator)
        {
            return @"On,Prec. m/z,Z,Ret. Time (min),Delta Ret. Time (min),Iso. Width,Collision Energy,Acquisition Time (ms/spec)".Replace(',', fieldSeparator);
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            string precursorMz = SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo);
            string z = nodeTranGroup.TransitionGroup.PrecursorCharge.ToString(CultureInfo); // CONSIDER(bspratt): Is charge all that's interesting, or are we implying protonation
            string retentionTime = @"0";
            string deltaRetentionTime = string.Empty;
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
            string isolationWidth = string.Format(CultureInfo, @"Narrow (~{0:0.0} m/z)", 1.3);
            string collisionEnergy = Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step), 1).ToString(CultureInfo);

            if (IsDda)
            {
                const string deltaMz = "20"; // // TODO check: Delta m/z (ppm) 
                Write(writer, @"True", precursorMz, deltaMz, z,
                      @"Preferred", retentionTime,
                      deltaRetentionTime, isolationWidth, collisionEnergy);
            }
            else
            {
                string acquisitionTime = string.Empty;  // TODO check: nothing to write for: Acquisition Time (ms/spec)
                Write(writer, @"True", precursorMz, z, retentionTime,
                      deltaRetentionTime, isolationWidth, collisionEnergy, acquisitionTime);
            }
        }
    }

    public class BrukerMethodExporter : AbstractMassListExporter
    {
        public const string EXE_BUILD_BRUKER_METHOD = @"Method\Bruker\BuildBrukerMethod";

        public BrukerMethodExporter(SrmDocument document)
            : base(document, null)
        {
            IsPrecursorLimited = true;
            IsolationList = true;
        }

        public double RunLength { get; set; }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.BRUKER_TOF; }
        }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write(@"Ret Time (min)");
            writer.Write(FieldSeparator);
            writer.Write(@"Tolerance");
            writer.Write(FieldSeparator);
            writer.Write(@"Precursor Ion Min");
            writer.Write(FieldSeparator);
            writer.Write(@"Precursor Ion Max");

            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            if (MethodType == ExportMethodType.Standard)
            {
                writer.Write((RunLength / 2).ToString(CultureInfo));    // rt
                writer.Write(FieldSeparator);
                writer.Write(RunLength);    // tolerance
                writer.Write(FieldSeparator);
            }
            else
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
                    writer.Write(windowRT.ToString(CultureInfo));
                    writer.Write(FieldSeparator);
                }
                else
                {
                    // Will probably cause an error, but should not happen
                    writer.Write(FieldSeparator);
                    writer.Write(FieldSeparator);
                }
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
            return methodPath.EndsWith(ExportInstrumentType.EXT_BRUKER) && File.Exists(Path.Combine(methodPath, @"submethods.xml"));
        }
    }

    public class BrukerDiaExporter : AbstractDiaExporter
    {
        public BrukerDiaExporter(SrmDocument document)
            : base(document.Settings.TransitionSettings.FullScan.IsolationScheme, 
                   document.Settings.TransitionSettings.Instrument.MaxInclusions)
        {
            CultureInfo = CultureInfo.InvariantCulture;
            FieldSeparator = TextUtil.GetCsvSeparator(CultureInfo);
        }

        public double RunLength { get; set; }

        public CultureInfo CultureInfo { get; private set; }
        public char FieldSeparator { get; private set; }

        public override bool HasHeaders { get { return true; } }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write(@"Ret Time (min)");
            writer.Write(FieldSeparator);
            writer.Write(@"Tolerance");
            writer.Write(FieldSeparator);
            writer.Write(@"Precursor Ion Min");
            writer.Write(FieldSeparator);
            writer.Write(@"Precursor Ion Max");

            writer.WriteLine();
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            var memoryOutput = new Dictionary<string, StringBuilder>
            {
                {AbstractMassListExporter.MEMORY_KEY_ROOT, new StringBuilder(ExportString)}
            };
            MethodExporter.ExportMethod(BrukerMethodExporter.EXE_BUILD_BRUKER_METHOD,
                new List<string>(), fileName, templateName, memoryOutput, progressMonitor);
        }

        protected override void WriteIsolationWindow(TextWriter writer, IsolationWindow isolationWindow)
        {
            writer.Write((RunLength / 2).ToString(CultureInfo));    // rt
            writer.Write(FieldSeparator);
            writer.Write(RunLength); // tolerance
            writer.Write(FieldSeparator);

            double startMz = SequenceMassCalc.PersistentMZ(isolationWindow.IsolationStart);
            writer.Write(startMz.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            double endtMz = SequenceMassCalc.PersistentMZ(isolationWindow.IsolationEnd);
            writer.Write(endtMz.ToString(CultureInfo));

            writer.WriteLine();
        }
    }

    public class BrukerTimsTofIsolationListExporter : AbstractMassListExporter
    {
        protected readonly HashSet<LibKey> _missingIonMobility;
        protected double _oneOverK0UpperLimit = 1.2;
        private int _id;

        public double RunLength { get; set; }

        public LibKey[] MissingIonMobility => _missingIonMobility.OrderBy(k => k.ToString()).ToArray();

        public BrukerTimsTofIsolationListExporter(SrmDocument document) : base(document, null)
        {
            IsPrecursorLimited = true;
            IsolationList = true;
            _missingIonMobility = new HashSet<LibKey>();
            _id = 0;
        }

        protected override string InstrumentType => ExportInstrumentType.BRUKER_TIMSTOF;

        public override bool HasHeaders => true;

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write(@"Mass [m/z]");
            writer.Write(FieldSeparator);
            writer.Write(@"Charge");
            writer.Write(FieldSeparator);
            writer.Write(@"Isolation Width [m/z]");
            writer.Write(FieldSeparator);
            writer.Write(@"RT [s]");
            writer.Write(FieldSeparator);
            writer.Write(@"RT Range [s]");
            writer.Write(FieldSeparator);
            writer.Write(@"Start IM [1/K0]");
            writer.Write(FieldSeparator);
            writer.Write(@"End IM [1/K0]");
            writer.Write(FieldSeparator);
            writer.Write(@"CE [eV]");
            writer.Write(FieldSeparator);
            writer.Write(@"External ID");
            writer.Write(FieldSeparator);
            writer.Write(@"Description");
            writer.WriteLine();
        }

        protected override void WriteTransition(TextWriter writer,
            int fileNumber,
            PeptideGroupDocNode nodePepGroup,
            PeptideDocNode nodePep,
            TransitionGroupDocNode nodeTranGroup,
            TransitionGroupDocNode nodeTranGroupPrimary,
            TransitionDocNode nodeTran,
            int step)
        {
            var target = GetTarget(nodePep, nodeTranGroup, nodeTran, step);
            ++_id;

            writer.Write(target.isolation_mz.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(target.charge.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(target.isolation_width.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(target.time_in_seconds.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write((target.time_in_seconds_end - target.time_in_seconds_begin).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(target.one_over_k0_lower_limit.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(target.one_over_k0_upper_limit.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(target.collision_energy.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(_id.ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(_id.ToString(CultureInfo));
            writer.WriteLine();
        }

        protected InputTarget GetTarget(PeptideDocNode nodePep, TransitionGroupDocNode nodeTranGroup, TransitionDocNode nodeTran, int step)
        {
            var target = new InputTarget();

            var prediction = Document.Settings.PeptideSettings.Prediction;

            if (MethodType == ExportMethodType.Standard)
            {
                target.time_in_seconds_begin = 0;
                target.time_in_seconds_end = RunLength * 60;
            }
            else
            {
                var predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out var windowRT);
                target.time_in_seconds_begin = (predictedRT - windowRT / 2) * 60 ?? 0;
                target.time_in_seconds_end = (predictedRT + windowRT / 2) * 60 ?? 0;
            }
            target.time_in_seconds = (target.time_in_seconds_begin + target.time_in_seconds_end) / 2;

            target.isolation_mz = nodeTranGroup.PrecursorMz;
            target.monoisotopic_mz = nodeTranGroup.PrecursorMz;
            target.isolation_width = 3.0;

            double? ionMobility = null;
            var windowIM = 0.4;
            if (Document.Settings.TransitionSettings.IonMobilityFiltering != null)
            {
                var result = Document.Settings.GetIonMobilityFilter(nodePep, nodeTranGroup, nodeTran, null, null, _oneOverK0UpperLimit);
                if (result.HasIonMobilityValue)
                {
                    ionMobility = result.IonMobility.Mobility.Value;
                    windowIM = Document.Settings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator
                        .WidthAt(ionMobility.Value, _oneOverK0UpperLimit);
                }
            }
            if (!ionMobility.HasValue)
                _missingIonMobility.Add(nodeTranGroup.GetLibKey(Document.Settings, nodePep));
            target.one_over_k0_lower_limit = (ionMobility ?? 1.0) - windowIM / 2;
            target.one_over_k0_upper_limit = (ionMobility ?? 1.0) + windowIM / 2;
            target.one_over_k0 = (target.one_over_k0_lower_limit + target.one_over_k0_upper_limit) / 2;

            target.charge = nodeTranGroup.PrecursorCharge;
            target.collision_energy = (int)Math.Round(GetCollisionEnergy(nodePep, nodeTranGroup, nodeTran, step));

            return target;
        }

        public static LibKey[] GetMissingIonMobility(SrmDocument document, ExportProperties exportProperties)
        {
            var exporter = exportProperties.InitExporter(new BrukerTimsTofIsolationListExporter(document));
            exporter.RunLength = exportProperties.RunLength;
            exporter.InitExport(null, null);
            return exporter.MissingIonMobility;
        }

        public void ExportMethod(string fileName, IProgressMonitor progressMonitor)
        {
            _id = 0;
            if (!InitExport(fileName, progressMonitor))
                return;

            _id = 0;
            Export(fileName);
        }
    }

    public class BrukerTimsTofMethodExporter : BrukerTimsTofIsolationListExporter
    {
        // TODO: Move this code to BuildMethod
        private readonly List<Tuple<InputTarget, string>> _targets;
        private Metrics _schedulingMetrics;

        public double Ms1RepetitionTime { get; set; }

        public BrukerTimsTofMethodExporter(SrmDocument document) : base(document)
        {
            _targets = new List<Tuple<InputTarget, string>>();
            _schedulingMetrics = null;
        }

        public override bool HasHeaders => false;

        protected override void WriteTransition(TextWriter writer,
            int fileNumber,
            PeptideGroupDocNode nodePepGroup,
            PeptideDocNode nodePep,
            TransitionGroupDocNode nodeTranGroup,
            TransitionGroupDocNode nodeTranGroupPrimary,
            TransitionDocNode nodeTran,
            int step)
        {
            _targets.Add(Tuple.Create(GetTarget(nodePep, nodeTranGroup, nodeTran, step), nodePep.ModifiedSequenceDisplay));
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor, out TimeSegmentList timeSegments, out SchedulingEntryList schedulingEntries,
            bool getMetrics)
        {
            if (templateName == null)
                throw new IOException(Resources.BrukerTimsTofMethodExporter_ExportMethod_Template_is_required_for_method_export_);

            using (var s = new Scheduler(templateName))
            {
                var methodInfo = s.GetPrmMethodInfo();
                if (methodInfo.Any())
                {
                    _oneOverK0UpperLimit = methodInfo[0].one_over_k0_upper_limit;
                }
            }

            _missingIonMobility.Clear();
            InitExport(fileName, progressMonitor);

            if (!Equals(templateName, fileName) && !string.IsNullOrEmpty(fileName))
                File.Copy(templateName, fileName, true);

            using (var s = new Scheduler(fileName ?? templateName))
            {
                s.SetAdditionalMeasurementParameters(new AdditionalMeasurementParameters
                {
                    ms1_repetition_time = Ms1RepetitionTime,
                    default_pasef_collision_energies = _targets.All(t => t.Item1.collision_energy == 0)
                });
                for (var i = 0; i < _targets.Count; i++)
                {
                    var id = (i + 1).ToString();
                    var description = id;
                    s.AddInputTarget(_targets[i].Item1, id, description);
                }

                var progress = new ProgressStatus(Resources.BrukerTimsTofMethodExporter_ExportMethod_Getting_scheduling___);

                timeSegments = new TimeSegmentList();
                schedulingEntries = new SchedulingEntryList();

                bool ProgressCallback(double progressPercentage)
                {
                    // return true to cancel, false to continue
                    if (progressMonitor == null) return false;

                    if (progressMonitor.IsCanceled) return true;

                    progressMonitor.UpdateProgress(progress.ChangePercentComplete((int)Math.Round(progressPercentage)));
                    return false;
                }

                s.GetScheduling(timeSegments, schedulingEntries, ProgressCallback);
                if (timeSegments.Count == 0 || schedulingEntries.Count == 0)
                {
                    throw new Exception(Resources.BrukerTimsTofMethodExporter_ExportMethod_Scheduling_failure__no_targets__);
                }

                if (!string.IsNullOrEmpty(fileName) && (progressMonitor == null || !progressMonitor.IsCanceled))
                    s.WriteScheduling();

                if (getMetrics)
                    _schedulingMetrics = new Metrics(s, _targets);
            }
        }

        public static void GetScheduling(SrmDocument document, ExportProperties exportProperties, string templateName,
            IProgressMonitor progressMonitor, out IPointList pointList)
        {
            var exporter = exportProperties.InitExporter(new BrukerTimsTofMethodExporter(document));
            exporter.RunLength = exportProperties.RunLength;
            exporter.Ms1RepetitionTime = exportProperties.Ms1RepetitionTime;
            exporter.ExportMethod(null, templateName, progressMonitor, out var timeSegments, out var schedulingEntries, false);

            var timeSegmentCounts = new Dictionary<uint, HashSet<uint>>();
            foreach (var entry in schedulingEntries)
            {
                if (!timeSegmentCounts.ContainsKey(entry.time_segment_id))
                    timeSegmentCounts[entry.time_segment_id] = new HashSet<uint>();
                timeSegmentCounts[entry.time_segment_id].Add(entry.frame_id);
            }

            var points = new List<PointPair>();
            for (uint i = 0; i < timeSegments.Count; i++)
            {
                var count = 0;
                if (timeSegmentCounts.TryGetValue(i, out var countSet))
                    count = countSet.Count;
                points.Add(new PointPair(timeSegments[(int)i].time_in_seconds_begin / 60, count));
                points.Add(new PointPair(timeSegments[(int)i].time_in_seconds_end / 60, count));
            }

            if (timeSegments.Count > 0)
            {
                points.Insert(0, new PointPair(points.First().X, 0));
                points.Insert(0, new PointPair(points.First().X - 1, 0));

                const double pointLimit = 1e9;
                if (points.Last().X > pointLimit)
                {
                    var penultimate = points[points.Count - 2].X;
                    if (penultimate < pointLimit)
                    {
                        points[points.Count - 1].X = penultimate + 1;
                    }
                }

                points.Add(new PointPair(points.Last().X, 0));
            }

            pointList = new PointPairList(points);
        }

        public static Metrics GetSchedulingMetrics(SrmDocument document,
            ExportProperties exportProperties, string templateName, IProgressMonitor progressMonitor)
        {
            var exporter = exportProperties.InitExporter(new BrukerTimsTofMethodExporter(document));
            exporter.RunLength = exportProperties.RunLength;
            exporter.Ms1RepetitionTime = exportProperties.Ms1RepetitionTime;
            exporter.ExportMethod(null, templateName, progressMonitor, out _, out _, true);
            return exporter._schedulingMetrics;
        }

        public class Metrics
        {
            private readonly Dictionary<SchedulingMetrics, PointPairList> _metrics;
            public DataTable Table { get; }

            public static string ColTarget => Resources.Metrics_ColTarget_Target;
            public static string ColMeanSamplingTime = Resources.Metrics_ColMeanSamplingTime_Mean_sampling_time__seconds_;
            public static string ColMaxSamplingTime = Resources.Metrics_ColMaxSamplingTime_Max_sampling_time__seconds_;
            public static string ColMz = Resources.Metrics_ColMz_m_z;
            public static string Col1K0LowerLimit = Resources.Metrics_Col1K0LowerLimit__1_K0_lower_limit;
            public static string Col1K0UpperLimit = Resources.Metrics_Col1K0UpperLimit__1_K0_upper_limit;
            public static string ColRtBegin = Resources.Metrics_ColRtBegin_RT_begin;
            public static string ColRtEnd = Resources.Metrics_ColRtEnd_RT_end;

            public Metrics(Scheduler s, IList<Tuple<InputTarget, string>> targets)
            {
                var concurrentFrames = s.GetSchedulingMetrics(SchedulingMetrics.CONCURRENT_FRAMES);
                var maxSamplingTimes = s.GetSchedulingMetrics(SchedulingMetrics.MAX_SAMPLING_TIMES);
                var meanSamplingTimes = s.GetSchedulingMetrics(SchedulingMetrics.MEAN_SAMPLING_TIMES);
                var redundancyOfTargets = s.GetSchedulingMetrics(SchedulingMetrics.REDUNDANCY_OF_TARGETS);
                var targetsPerFrame = s.GetSchedulingMetrics(SchedulingMetrics.TARGETS_PER_FRAME);

                _metrics = new Dictionary<SchedulingMetrics, PointPairList>
                {
                    {SchedulingMetrics.CONCURRENT_FRAMES, new PointPairList(concurrentFrames.Select(pt => new PointPair(pt.x / 60, pt.y)).ToList())},
                    {SchedulingMetrics.MAX_SAMPLING_TIMES, new PointPairList(
                        maxSamplingTimes.OrderBy(pt => pt.y).Select((pt, i) => new PointPair(i + 1, pt.y)).ToList())},
                    {SchedulingMetrics.MEAN_SAMPLING_TIMES, new PointPairList(
                        meanSamplingTimes.OrderBy(pt => pt.y)
                            .Select((pt, i) => new PointPair(i + 1, pt.y)).ToList())},
                    {SchedulingMetrics.REDUNDANCY_OF_TARGETS, new PointPairList(redundancyOfTargets.Select(pt => new PointPair(pt.x / 60, pt.y)).ToList())},
                    {SchedulingMetrics.TARGETS_PER_FRAME, new PointPairList(targetsPerFrame.Select(pt => new PointPair(pt.x / 60, pt.y)).ToList())}
                };

                Table = new DataTable();
                Table.Columns.Add(ColTarget, typeof(string));
                Table.Columns.Add(ColMz, typeof(double));
                Table.Columns.Add(ColMeanSamplingTime, typeof(double));
                Table.Columns.Add(ColMaxSamplingTime, typeof(double));
                Table.Columns.Add(Col1K0LowerLimit, typeof(double));
                Table.Columns.Add(Col1K0UpperLimit, typeof(double));
                Table.Columns.Add(ColRtBegin, typeof(double));
                Table.Columns.Add(ColRtEnd, typeof(double));
                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i].Item1;
                    var targetName = targets[i].Item2;

                    var row = Table.NewRow();
                    row[ColTarget] = targetName + Transition.GetChargeIndicator(target.charge);
                    row[ColMz] = target.monoisotopic_mz;
                    row[ColMeanSamplingTime] = meanSamplingTimes[i].y;
                    row[ColMaxSamplingTime] = maxSamplingTimes[i].y;
                    row[Col1K0LowerLimit] = target.one_over_k0_lower_limit;
                    row[Col1K0UpperLimit] = target.one_over_k0_upper_limit;
                    row[ColRtBegin] = target.time_in_seconds_begin / 60;
                    row[ColRtEnd] = target.time_in_seconds_end / 60;
                    Table.Rows.Add(row);
                }
            }

            public PointPairList Get(SchedulingMetrics metricType) { return _metrics.ContainsKey(metricType) ? _metrics[metricType] : new PointPairList(); }
        }
    }

    public class ThermoQExactiveIsolationListExporter : ThermoMassListExporter
    {
        public const double NARROW_NCE = 27.0;
        public const double WIDE_NCE = 30.0;

        public ThermoQExactiveIsolationListExporter(SrmDocument document)
            : base(document)
        {
            IsPrecursorLimited = true;
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

        public string GetHeader(char fieldSeparator)
        {
            var hdr = @"Mass [m/z],Formula [M],Species,CS [z],Polarity,Start [min],End [min],NCE,";
            if (UseSlens)
                hdr += @"S-lens,";
            return (hdr+@"Comment").Replace(',', fieldSeparator);
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
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

            string z = Math.Abs(nodeTranGroup.TransitionGroup.PrecursorAdduct.AdductCharge).ToString(CultureInfo);
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
            string collisionEnergy = (wideWindowDia ? WIDE_NCE : NARROW_NCE).ToString(CultureInfo); // Normalized CE, not a real voltage
            string comment = string.Format(@"{0} ({1})",
                GetCompound(nodePep, nodeTranGroup),
                nodeTranGroup.TransitionGroup.LabelType).ToDsvField(FieldSeparator);

            var polarity = (nodeTranGroup.PrecursorCharge > 0) ? @"Positive" : @"Negative";
            if (UseSlens)
            {
                var slens = (ExplicitTransitionValues.Get(nodeTran).SLens ?? DEFAULT_SLENS).ToString(CultureInfo);  
                Write(writer, precursorMz, string.Empty, string.Empty, z, polarity, start, end, collisionEnergy, slens, comment);
            }
            else
            {
                Write(writer, precursorMz, string.Empty, string.Empty, z, polarity, start, end, collisionEnergy, comment);
            }
        }
    }

    public class ThermoFusionMassListExporter : ThermoMassListExporter
    {
        public const double NARROW_NCE = 27.0;
        public const double WIDE_NCE = 30.0;

        public bool Tune3 { get; set; }
        public bool Tune3Columns { get { return IsolationList && Tune3; } }

        public bool WriteFaimsCv { get; set; }

        public ThermoFusionMassListExporter(SrmDocument document)
            : base(document)
        {
            IsolationList = true;
            IsPrecursorLimited = true;
        }

        public string GetHeader(char fieldSeparator)
        {
            var hdr = !Tune3Columns
                ? @"m/z,z,t start (min),t end (min),CID Collision Energy (%)"
                : @"Compound,Formula,Adduct,m/z,z,Polarity,t start (min),t stop (min),CID Collision Energy (%)";
            if (UseSlens)
                hdr += @",S-lens";
            if (WriteFaimsCv)
                hdr += @",FAIMS CV (V)";
            return hdr.Replace(',', fieldSeparator);
        }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.WriteLine(GetHeader(FieldSeparator));
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            if (Tune3Columns)
            {
                writer.Write(@"{0} ({1})",
                    nodePep.Peptide.IsCustomMolecule ? nodeTranGroup.CustomMolecule.InvariantName : Document.Settings.GetModifiedSequence(nodePep).Sequence,
                    nodeTranGroup.TransitionGroup.LabelType);
                writer.Write(FieldSeparator);
                writer.Write(string.Empty);
                writer.Write(FieldSeparator);
                writer.Write(string.Empty);
                writer.Write(FieldSeparator);
            }

            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);

            var z = nodeTranGroup.TransitionGroup.PrecursorCharge;  // CONSIDER(bspratt): Is charge all that matters, or are we implying protonation?
            if (Tune3Columns)
            {
                writer.Write(Math.Abs(z).ToString(CultureInfo));
                writer.Write(FieldSeparator);
                writer.Write(nodeTranGroup.PrecursorCharge > 0 ? @"Positive" : @"Negative");
            }
            else
            {
                writer.Write(z.ToString(CultureInfo));
            }
            writer.Write(FieldSeparator);

            var start = string.Empty;
            var end = string.Empty;
            if (MethodType == ExportMethodType.Scheduled)
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);
                // Start Time and End Time
                if (predictedRT.HasValue)
                {
                    start = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT.Value - windowRT / 2) ?? 0).ToString(CultureInfo);
                    end = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT.Value + windowRT / 2) ?? 0).ToString(CultureInfo);
                }
            }
            writer.Write(start);
            writer.Write(FieldSeparator);
            writer.Write(end);
            writer.Write(FieldSeparator);
            writer.Write(GetCE(Document).ToString(CultureInfo));

            if (UseSlens)
            {
                writer.Write(FieldSeparator);
                writer.Write((ExplicitTransitionValues.Get(nodeTran).SLens ?? DEFAULT_SLENS).ToString(CultureInfo));
            }
            if (WriteFaimsCv)
            {
                var cv = GetCompensationVoltage(nodePep, nodeTranGroup, nodeTran, step);
                writer.Write(FieldSeparator);
                writer.Write(cv.HasValue ? cv.Value.ToString(CultureInfo) : string.Empty);
            }
            writer.WriteLine();
        }

        public static double GetCE(SrmDocument doc)
        {
            // Note that this is normalized CE (not absolute)
            var fullScan = doc.Settings.TransitionSettings.FullScan;
            var wideWindowDia = false;
            if (fullScan.AcquisitionMethod == FullScanAcquisitionMethod.DIA && fullScan.IsolationScheme != null)
            {
                // Suggested by Thermo to use 27 for normal isolation ranges and 30 for wider windows
                var scheme = fullScan.IsolationScheme;
                if (!scheme.FromResults && !scheme.IsAllIons)
                {
                    wideWindowDia = scheme.PrespecifiedIsolationWindows.Average(iw => iw.IsolationEnd - iw.IsolationStart) >= 5;
                }
            }
            return wideWindowDia ? WIDE_NCE : NARROW_NCE;
        }

        public virtual void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;
            Export(fileName);
        }
    }

    public class WatersMassListExporter : AbstractMassListExporter
    {
        // Hack to workaround limitation of 32 transitions per function
        protected readonly Dictionary<Tuple<string, int>, int> _compoundCounts = new Dictionary<Tuple<string, int>, int>();
        protected const int MAX_COMPOUND_NAME = 32;

        protected bool USE_COMPOUND_COUNT_WORKAROUND { get { return true; } }

        public WatersMassListExporter(SrmDocument document)
            : this(document, null)
        {
        }

        public WatersMassListExporter(SrmDocument document, DocNode node)
            : base(document, node)
        {
            ConeVoltage = 35; // Default value, may be overridden by ExplicitValues in TransitionGroup
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

        // ReSharper disable LocalizableElement
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
        // ReSharper restore LocalizableElement

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.WriteDsvField(nodePepGroup.Name.Replace(' ', '_'), FieldSeparator);  // Quanpedia can't handle spaces
            writer.Write(FieldSeparator);
            // Write special ID to ensure 1-to-1 relationship between this ID and precursor m/z
            // Better to use one ID per peptide molecular structure, as Waters has a 512 ID limit
            // and this allows for 512 peptide charge states and not just 512 precursors.
//            writer.Write(Document.Settings.GetModifiedSequence(nodePep.Peptide.Sequence,
//                nodeTranGroup.TransitionGroup.LabelType, nodePep.ExplicitMods));

            var compound = GetCompound(nodePep, nodeTranGroup);
            compound += '.';
            compound += nodeTranGroup.PrecursorAdduct.AsFormulaOrInt();

            if (USE_COMPOUND_COUNT_WORKAROUND)
            {
                var key = Tuple.Create(compound, step);
                if (!_compoundCounts.ContainsKey(key))
                {
                    _compoundCounts[key] = 0;
                }
                else
                {
                    int compoundStep = ++_compoundCounts[key] / MAX_COMPOUND_NAME + 1;
                    if (compoundStep > 1)
                        compound += '.' + compoundStep.ToString(CultureInfo);
                }
            }

            if (step != 0)
            {
                compound += '.';
                compound += step.ToString(CultureInfo);
            }

            writer.WriteDsvField(compound, FieldSeparator, FieldSeparatorReplacement);

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
            writer.Write((int)Math.Round(ExplicitTransitionValues.Get(nodeTran).ConeVoltage ?? ConeVoltage));
            writer.Write(FieldSeparator);

            // Extra information not used by instrument
            writer.WriteDsvField(nodePep.RawUnmodifiedTextId, FieldSeparator, FieldSeparatorReplacement);
            writer.Write(FieldSeparator);
            writer.WriteDsvField(nodeTran.Transition.GetFragmentIonName(CultureInfo.InvariantCulture), FieldSeparator, FieldSeparatorReplacement);
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

    public class WatersIsolationListExporter : AbstractMassListExporter
    {
        public const string EXT_WATERS_ISOLATION_LIST = ".mrm";

        public const string RETENTION_TIME_FORMAT = "0.0";
        public const string MASS_FORMAT = "0.0000";
        public const string CE_FORMAT = "0.0";
        
        public bool ExportEdcMass { get; set; }

        private readonly string _instrumentType;
        public double RunLength { get; set; }
        public double ConeVoltage { get; set; }
        public double SlopeStart { get; set; }
        public double SlopeEnd { get; set; }
        public double InterceptTrapStart { get; set; }
        public double InterceptTrapEnd { get; set; }
        public double InterceptTransferStart { get; set; }
        public double InterceptTransferEnd { get; set; }
        public double TrapRegionTransferCE { get; set; }
        public double TransferRegionTrapCE { get; set; }

        public WatersIsolationListExporter(SrmDocument document, string instrumentType)
            : base(document, null)
        {
            _instrumentType = instrumentType;
            IsolationList = true;
            IsPrecursorLimited = true;

            /* From Waters:
             * 
             * For Synapt trap region (mode 1) methods, please use the following formulae and values:
             * Trap CE start = 0.0286*m/z + 4.5714
             * Trap CE end = 0.0352*m/z + 7.3956
             * Transfer CE start = 2
             * Transfer CE end = 2
             * 
             * For Synapt transfer region (mode 1) methods, please use the following formulae and values:
             * Trap CE start = 4
             * Trap CE end = 4
             * Transfer CE start = 0.0286*m/z + 9.5714
             * Transfer CE end = 0.0352*m/z + 12.3956
             * 
             * For Xevo QTof, please use the following formulae:
             * Trap CE start = 0.0286*m/z + 4.5714
             * Trap CE end = 0.0352*m/z + 7.3956
             */
            ConeVoltage = 30;  // N.B. this doesn't match our previously used default of 35
            SlopeStart = 0.0286;
            SlopeEnd = 0.0352;
            InterceptTrapStart = 4.5714;
            InterceptTrapEnd = 7.3956;
            InterceptTransferStart = 9.5714;
            InterceptTransferEnd = 12.3956;
            TrapRegionTransferCE = 2;
            TransferRegionTrapCE = 4;
        }

        protected override string InstrumentType
        {
            get { return _instrumentType; }
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;
            Export(fileName);
        }

        public override bool HasHeaders { get { return true; } }

        public static string GetHeader(char fieldSeparator)
        {
            return
                @";Function channel,Retention Time Start,Retention Time End,Set Mass,Mass Fragments 1,2,3,4,5,6,Trap CE Start,Trap CE End," +
                @"Transfer CE Start,Transfer CE End,CV,EDCMass,DT Start,DT End,compound name".Replace(',', fieldSeparator); 
        }

        protected override void WriteHeaders(TextWriter writer)
        {
            writer.Write(GetHeader(FieldSeparator));
        }

        protected override void WriteTransition(TextWriter writer,
                                                int fileNumber,
                                                PeptideGroupDocNode nodePepGroup,
                                                PeptideDocNode nodePep,
                                                TransitionGroupDocNode nodeTranGroup,
                                                TransitionGroupDocNode nodeTranGroupPrimary,
                                                TransitionDocNode nodeTran,
                                                int step)
        {
            writer.WriteLine(); // MassLynx doesn't like blank line at end of file

            var transitions = GetTransitionsInBestOrder(nodeTranGroup, nodeTranGroupPrimary).ToArray();

            // Function channel
            writer.Write(0);
            writer.Write(FieldSeparator);
            // Retention Time Start, Retention Time End
            double rtStart = 0;
            double rtEnd = 0;
            if (MethodType != ExportMethodType.Standard)
            {
                var prediction = Document.Settings.PeptideSettings.Prediction;
                double windowRT;
                double? predictedRT = prediction.PredictRetentionTime(Document, nodePep, nodeTranGroup,
                    SchedulingReplicateIndex, SchedulingAlgorithm, false, out windowRT);
                if (predictedRT.HasValue)
                {
                    rtStart = predictedRT.Value - windowRT/2;
                    rtEnd = predictedRT.Value + windowRT/2;
                }
            }
            else
            {
                rtStart = 0;
                rtEnd = RunLength;
            }
            writer.Write(rtStart.ToString(RETENTION_TIME_FORMAT, CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(rtEnd.ToString(RETENTION_TIME_FORMAT, CultureInfo));
            writer.Write(FieldSeparator);
            // Set Mass
            writer.Write(nodeTranGroup.PrecursorMz.ToString(MASS_FORMAT, CultureInfo));
            writer.Write(FieldSeparator);
            // Mass Fragments 1-6
            for (int i = 0; i < 6; i++)
            {
                var mz = i < transitions.Length
                    ? GetProductMz(SequenceMassCalc.PersistentMZ(transitions[i].Mz), step)
                    : 0;
                writer.Write(mz.ToString(MASS_FORMAT, CultureInfo));
                writer.Write(FieldSeparator);
            }
            // Trap CE Start, Trap CE End, Transfer CE Start, Transfer CE End
            double trapStart, trapEnd;
            double? transferStart, transferEnd;
            GetCEValues(nodeTranGroup.PrecursorMz, out trapStart, out trapEnd, out transferStart, out transferEnd);
            writer.Write(trapStart.ToString(CE_FORMAT, CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(trapEnd.ToString(CE_FORMAT, CultureInfo));
            writer.Write(FieldSeparator);
            if (transferStart.HasValue && transferEnd.HasValue)
            {
                writer.Write(transferStart.Value.ToString(CE_FORMAT, CultureInfo));
                writer.Write(FieldSeparator);
                writer.Write(transferEnd.Value.ToString(CE_FORMAT, CultureInfo));
                writer.Write(FieldSeparator);
            }
            // CV
            writer.Write(ExplicitTransitionValues.Get(nodeTran).ConeVoltage ?? ConeVoltage);
            writer.Write(FieldSeparator);
            // EDCMass
            var edcMass = ExportEdcMass && transitions.Any()
                ? GetProductMz(SequenceMassCalc.PersistentMZ(transitions.First().Mz), step)
                : 0;
            writer.Write(edcMass.ToString(MASS_FORMAT, CultureInfo));
            writer.Write(FieldSeparator);
            // DT Start, DT End
            writer.Write(0);
            writer.Write(FieldSeparator);
            writer.Write(199);
            writer.Write(FieldSeparator);
            // compound name
            writer.WriteDsvField(TextUtil.SpaceSeparate(nodePepGroup.Name, nodePep.ModifiedSequenceDisplay), FieldSeparator);
        }

        protected void GetCEValues(double mz, out double trapStart, out double trapEnd, out double? transferStart, out double? transferEnd)
        {
            switch (_instrumentType)
            {
                case ExportInstrumentType.WATERS_SYNAPT_TRAP:
                    trapStart = SlopeStart*mz + InterceptTrapStart;
                    trapEnd = SlopeEnd*mz + InterceptTrapEnd;
                    transferStart = transferEnd = TrapRegionTransferCE;
                    break;
                case ExportInstrumentType.WATERS_SYNAPT_TRANSFER:
                    trapStart = trapEnd = TransferRegionTrapCE;
                    transferStart = SlopeStart*mz + InterceptTransferStart;
                    transferEnd = SlopeEnd*mz + InterceptTransferEnd;
                    break;
                default:
                    trapStart = SlopeStart*mz + InterceptTrapStart;
                    trapEnd = SlopeEnd*mz + InterceptTrapEnd;
                    transferStart = transferEnd = null; // Ignored for Xevo
                    break;
            }
        }

        protected override IEnumerable<TransitionDocNode> GetTransitionsInBestOrder(TransitionGroupDocNode nodeGroup, TransitionGroupDocNode nodeGroupPrimary)
        {
            IComparer<TransitionOrdered> comparer = TransitionOrdered.TransitionComparerInstance;
            // ReSharper disable once CollectionNeverQueried.Local
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
                argv.Add(@"-q");
            argv.Add(@"-w"); 
            argv.Add(RTWindow.ToString(CultureInfo.InvariantCulture));
            MethodExporter.ExportMethod(EXE_BUILD_WATERS_METHOD,
                argv, fileName, templateName, MemoryOutput, progressMonitor);
        }

        private const string PRIMARY_DEPENDENCY_LIBRARY = "QuantifyClassLibrary.dll";

        // ReSharper disable LocalizableElement
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
        // ReSharper restore LocalizableElement

        private static void EnsureLibraries()
        {
            string skylinePath = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(skylinePath))
                throw new IOException(Resources.WatersMethodExporter_EnsureLibraries_Waters_method_creation_software_may_not_be_installed_correctly);

            // ReSharper disable ConstantNullCoalescingCondition
            string buildSubdir = Path.GetDirectoryName(EXE_BUILD_WATERS_METHOD) ?? string.Empty;
            string exeDir = Path.Combine(Path.GetDirectoryName(skylinePath) ?? string.Empty, buildSubdir);
            string dacServerPath = AdvApi.GetPathFromProgId(@"DACScanStats.DACScanStats"); 
            if (dacServerPath == null)
            {
                dacServerPath = AdvApi.RegQueryKeyValue(AdvApi.HKEY_LOCAL_MACHINE,
                                                        @"SOFTWARE\Wow6432Node\Micromass\MassLynx", @"Root"); 
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

                dacServerPath = Path.Combine(dacServerPath, @"bin");
            }

            string massLynxDir = Path.GetDirectoryName(dacServerPath) ?? string.Empty;
            // ReSharper restore ConstantNullCoalescingCondition
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
        [DllImport(@"advapi32.dll", CharSet = CharSet.Auto)]
        public static extern int RegOpenKeyEx(
          UIntPtr hKey,
          string subKey,
          int ulOptions,
          int samDesired,
          out UIntPtr hkResult);
        [DllImport(@"advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = @"RegQueryValueExW", SetLastError = true)]
        public static extern int RegQueryValueEx(
            UIntPtr hKey,
            string lpValueName,
            int lpReserved,
            out uint lpType,
            StringBuilder lpData,
            ref uint lpcbData);
        [DllImport(@"advapi32.dll", SetLastError = true)]
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
            String clsid = RegQueryKeyValue(HKEY_LOCAL_MACHINE, @"SOFTWARE\Classes\" + progId + @"\CLSID");
            if (clsid == null)
                return null;
            return RegQueryKeyValue(HKEY_LOCAL_MACHINE, @"SOFTWARE\Classes\CLSID\" + clsid + @"\InprocServer32"); 
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

                string dirWork = Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory;
                using (var tmpDir = new TemporaryDirectory(Path.Combine(dirWork, Path.GetRandomFileName())))
                {
                    var transitionsFile = Path.Combine(tmpDir.DirPath, @"transitions.txt");
                    File.WriteAllText(transitionsFile, stdinBuilder.ToString());

                    // Resharper disable LocalizableElement
                    argv.AddRange(new[] { "-m", templateName.Quote(), transitionsFile.Quote() });  // Read from stdin, multi-file format
                    // Resharper restore LocalizableElement

                    var psiExporter = new ProcessStartInfo(exeName)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        // Common directory includes the directory separator
                        WorkingDirectory = dirWork,
                        Arguments = string.Join(@" ", argv.ToArray()),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true
                    };

                    IProgressStatus status;
                    if (dictTranLists.Count == 1)
                        status = new ProgressStatus(string.Format(Resources.MethodExporter_ExportMethod_Exporting_method__0__, methodName));
                    else
                    {
                        status = new ProgressStatus(Resources.MethodExporter_ExportMethod_Exporting_methods);
                        status = status.ChangeSegments(0, dictTranLists.Count);
                    }
                    progressMonitor?.UpdateProgress(status);

                    psiExporter.RunProcess(null, @"MESSAGE: ", progressMonitor, ref status);

                    if (!status.IsError && !status.IsCanceled)
                    {
                        foreach (var fs in listFileSavers)
                            fs.Commit();
                    }
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
