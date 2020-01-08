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
using Microsoft.Win32;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Shimadzu.LabSolutions.MethodConverter;
using Shimadzu.LabSolutions.MethodWriter;

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
        public const string SHIMADZU = "Shimadzu";
        public const string THERMO = "Thermo";
        public const string THERMO_TSQ = "Thermo TSQ";
        public const string THERMO_ENDURA = "Thermo Endura";
        public const string THERMO_QUANTIVA = "Thermo Quantiva";
        public const string THERMO_ALTIS = "Thermo Altis";
        public const string THERMO_FUSION = "Thermo Fusion";
        public const string THERMO_LTQ = "Thermo LTQ";
        public const string THERMO_Q_EXACTIVE = "Thermo Q Exactive";
        public const string WATERS = "Waters";
        public const string WATERS_XEVO_TQ = "Waters Xevo TQ";
        public const string WATERS_XEVO_QTOF = "Waters Xevo QTOF";
        public const string WATERS_SYNAPT_TRAP = "Waters Synapt (trap)";
        public const string WATERS_SYNAPT_TRANSFER = "Waters Synapt (transfer)";
        public const string WATERS_QUATTRO_PREMIER = "Waters Quattro Premier";

        public const string EXT_AB_SCIEX = ".dam";
        public const string EXT_AGILENT = ".m";
        public const string EXT_BRUKER = ".m";
        public const string EXT_SHIMADZU = ".lcm";
        public const string EXT_THERMO = ".meth";
        public const string EXT_WATERS = ".exp";

        public static readonly string[] METHOD_TYPES =
            {
                AGILENT6400,
                BRUKER_TOF,
                ABI_QTRAP,
                ABI_TOF,
                SHIMADZU,
                THERMO_TSQ,
                THERMO_LTQ,
                THERMO_QUANTIVA,
                THERMO_ALTIS,
                THERMO_FUSION,
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
                                       {SHIMADZU, EXT_SHIMADZU},
                                       {THERMO_TSQ, EXT_THERMO},
                                       {THERMO_LTQ, EXT_THERMO},
                                       {THERMO_QUANTIVA, EXT_THERMO},
                                       {THERMO_ALTIS, EXT_THERMO},
                                       {THERMO_FUSION, EXT_THERMO},
                                       {WATERS_XEVO_TQ, EXT_WATERS},
                                       {WATERS_QUATTRO_PREMIER, EXT_WATERS}
                                   };
        }

        public static string TransitionListExtension(string instrument)
        {
            return Equals(instrument, SHIMADZU)
                ? ShimadzuNativeMassListExporter.EXT_SHIMADZU_TRANSITION_LIST
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

        public virtual bool RetentionStartAndEnd { get; set; }

        public virtual int MultiplexIsolationListCalculationTime { get; set; }
        public virtual bool DebugCycles { get; set; }

        public virtual bool ExportEdcMass { get; set; }

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
                        return ExportThermoFusionMethod(doc, path, template);
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
            var exporter = InitExporter(new ShimadzuNativeMassListExporter(document));
            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            PerformLongExport(m => exporter.ExportNativeList(fileName, m));

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

        public AbstractMassListExporter ExportThermoFusionMethod(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoFusionMethodExporter(document));
            exporter.UseSlens = UseSlens;

            if (MethodType == ExportMethodType.Standard)
                exporter.RunLength = RunLength;
            exporter.RetentionStartAndEnd = RetentionStartAndEnd;
            PerformLongExport(m => exporter.ExportMethod(fileName, templateName, m));

            return exporter;
        }

        public AbstractMassListExporter ExportThermoFusionIsolationList(SrmDocument document, string fileName, string templateName)
        {
            var exporter = InitExporter(new ThermoFusionIsolationListExporter(document));
            exporter.UseSlens = UseSlens;
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

            writer.Write(nodeTranGroup.PrecursorCharge>0?@"Positive":@"Negative");
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
        public double? RunLength { get; set; }
        private readonly Dictionary<GroupStepKey, int> _peptidesSeen = new Dictionary<GroupStepKey, int>();
        private int LastFileNumber { get; set; }

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

            private bool Equals(GroupStepKey other)
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
            LastFileNumber = -1;
        }

        protected override string InstrumentType
        {
            get { return ExportInstrumentType.SHIMADZU; }
        }
        
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
                _peptidesSeen.Clear();
                LastFileNumber = fileNumber;
            }
            var compound = GetCompound(nodePep, nodeTranGroup) +
                  @"_" + nodeTranGroup.TransitionGroup.LabelType;
            if (step != 0)
                compound += (@"_" + step);
            writer.WriteDsvField(compound.Replace(' ', '_'), FieldSeparator);
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
    }

    public class ShimadzuNativeMassListExporter : ShimadzuMassListExporter
    {
        public const string EXT_SHIMADZU_TRANSITION_LIST = ".txt";
//        public const string EXE_BUILD_TSQ_METHOD = @"Method\Thermo\BuildTSQEZMethod";

        public ShimadzuNativeMassListExporter(SrmDocument document)
            : base(document)
        {
        }

        public void ExportNativeList(string fileName, IProgressMonitor progressMonitor)
        {
            if (!InitExport(fileName, progressMonitor))
                return;

            string baseName = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, Path.GetFileNameWithoutExtension(fileName) ?? string.Empty);
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
                    if (result != ConverterResult.OK)
                    {
                        var errorMessages = new Dictionary<ConverterResult, string>
                        {
                            {ConverterResult.InputIsEmpty, Resources.ShimadzuNativeMassListExporter_ExportNativeList_Input_string_is_empty_},
                            {ConverterResult.InputCannotBeParsed, Resources.ShimadzuNativeMassListExporter_ExportNativeList_Input_string_cannot_be_parsed_},
                            {ConverterResult.CannotOpenOutputFile, Resources.ShimadzuNativeMassListExporter_ExportNativeList_Cannot_open_output_file_},
                            {ConverterResult.InvalidParameter, Resources.ShimadzuNativeMassListExporter_ExportNativeList_Invalid_parameter__Cannot_create_output_method_},
                            {ConverterResult.OutOfRangeEventNoError, Resources.ShimadzuNativeMassListExporter_ExportNativeList_Number_of_events_exceed_maximum_allowed_by_LabSolutions__1000__},
                            {ConverterResult.EventNotContiguous, Resources.ShimadzuNativeMassListExporter_ExportNativeList_Input_events_are_not_contiguous_},
                            {ConverterResult.EventNotAscending, Resources.ShimadzuNativeMassListExporter_ExportNativeList_Input_events_are_not_in_ascending_order},
                            {ConverterResult.MaxTransitionError, string.Format(
                                Resources.ShimadzuNativeMassListExporter_ExportNativeList_The_transition_count__0__exceeds_the_maximum_allowed_for_this_instrument_type,
                                tranList.Split('\n').Length)},
                        };
                        if (!errorMessages.TryGetValue(result, out var errorMessage))
                            errorMessage = string.Format(Resources.ShimadzuNativeMassListExporter_ExportNativeList_Unexpected_response__0__from_Shimadzu_method_converter, result);
                        Assume.Fail(TextUtil.LineSeparate(Resources.ShimadzuNativeMassListExporter_ExportNativeList_Shimadzu_method_converter_encountered_an_error_, errorMessage));
                    }
                    fs.Commit();
                }
            }
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

            string baseName = Path.Combine(Path.GetDirectoryName(fileName) ?? string.Empty, Path.GetFileNameWithoutExtension(fileName) ?? string.Empty);
            string ext = Path.GetExtension(fileName);

            var methodWriter = new MassMethodWriter();

            foreach (KeyValuePair<string, StringBuilder> pair in MemoryOutput)
            {
                string suffix = pair.Key.Substring(MEMORY_KEY_ROOT.Length);
                suffix = Path.GetFileNameWithoutExtension(suffix);
                string methodName = baseName + suffix + ext;

                try
                {
                    // MethodWriter receives the template and overwrites it, so copy template to final output name
                    // The template is required to have .lcm extension
                    File.Copy(templateName, methodName, true);
                }
                catch (Exception x)
                {
                    throw new IOException(TextUtil.LineSeparate(string.Format(Resources.ShimadzuMethodExporter_ExportMethod_Error_copying_template_file__0__to_destination__1__, templateName, methodName), x.Message));
                }

                string tranList = pair.Value.ToString();
                WriterResult result;
                CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
                try
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                    result = methodWriter.WriteMethod(methodName, tranList);
                }
                finally
                {
                    // ReSharper disable once AssignNullToNotNullAttribute
                    Thread.CurrentThread.CurrentCulture = originalCulture;
                }
                if (result != WriterResult.OK)
                {
                    // Writing the method failed, delete the copied template file
                    if (File.Exists(methodName))
                    {
                        try
                        {
                            File.Delete(methodName);
                        }
// ReSharper disable once EmptyGeneralCatchClause
                        catch
                        {
                        }
                    }

                    var errorMessages = new Dictionary<WriterResult, string>
                    {
                        {WriterResult.InputIsEmpty, Resources.ShimadzuMethodExporter_ExportMethod_Input_string_is_empty_},
                        {WriterResult.InputCannotBeParsed, Resources.ShimadzuMethodExporter_ExportMethod_Input_string_cannot_be_parsed_},
                        {WriterResult.OutputIsEmpty, Resources.ShimadzuMethodExporter_ExportMethod_Output_path_is_not_specified_},
                        {WriterResult.CannotOpenFile, Resources.ShimadzuMethodExporter_ExportMethod_Cannot_open_output_file_},
                        {WriterResult.InvalidParameter, Resources.ShimadzuMethodExporter_ExportMethod_Invalid_parameter__Cannot_create_output_method_},
                        {WriterResult.UnsupportedFile, Resources.ShimadzuMethodExporter_ExportMethod_Output_file_type_is_not_supported_},
                        {WriterResult.SerializeIOException, Resources.ShimadzuMethodExporter_ExportMethod_Exception_raised_during_output_serialization_},
                        {WriterResult.OutOfRangeEventNoError, Resources.ShimadzuMethodExporter_ExportMethod_Number_of_events_exceed_the_maximum_allowed_by_LabSolutions__1000__},
                        {WriterResult.OutputMethodEmpty, Resources.ShimadzuMethodExporter_ExportMethod_Output_method_does_not_contain_any_events_},
                        {WriterResult.EventNotContiguous, Resources.ShimadzuMethodExporter_ExportMethod_Input_events_are_not_contiguous_},
                        {WriterResult.EventNotAscending, Resources.ShimadzuMethodExporter_ExportMethod_Input_events_are_not_in_ascending_order},
                        {WriterResult.MaxTransitionError, string.Format(
                            Resources.ShimadzuMethodExporter_ExportMethod_The_transition_count__0__exceeds_the_maximum_allowed_for_this_instrument_type_,
                            tranList.Split('\n').Length)},
                    };
                    if (!errorMessages.TryGetValue(result, out var errorMessage))
                        errorMessage = string.Format(Resources.ShimadzuMethodExporter_ExportMethod_Unexpected_response__0__from_Shimadzu_method_writer_, result);
                    Assume.Fail(TextUtil.LineSeparate(Resources.ShimadzuMethodExporter_ExportMethod_Shimadzu_method_writer_encountered_an_error_, errorMessage));
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

    public class ThermoFusionMethodExporter : ThermoFusionMassListExporter
    {
        public ThermoFusionMethodExporter(SrmDocument document)
            : base(document)
        {
            IsolationList = true;
        }

        public void ExportMethod(string fileName, string templateName, IProgressMonitor progressMonitor)
        {
            if (fileName != null)
                EnsureLibraries();

            if (!InitExport(fileName, progressMonitor))
                return;

            var argv = new List<string> {@"-f"};
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
            string ce = Math.Round(ceValue, 1).ToString(CultureInfo);
            double dpValue = GetDeclusteringPotential(nodePep, nodeTranGroup, nodeTran, step);
            // CONSIDER: Is there a minimum DP value?
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
            string compensationVoltage = Document.Settings.TransitionSettings.Prediction.CompensationVoltage != null
                ? string.Format(@",{0}", GetCompensationVoltage(nodePep, nodeTranGroup, nodeTran, step).GetValueOrDefault().ToString(@"0.00", CultureInfo))
                : null;

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

        protected override bool SkipTransition(PeptideGroupDocNode nodePepGroup, PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup,
            TransitionGroupDocNode nodeGroupPrimary, TransitionDocNode nodeTran)
        {
            if (Document.Settings.TransitionSettings.Prediction.CompensationVoltage != null &&
                ExportOptimize.CompensationVoltageTuneTypes.Contains(OptimizeType) &&
                nodeGroup.TransitionCount > 1 && PrimaryTransitionCount > 0)
            {
                // If we know the top ranked transition for every precursor and this is not it, skip writing it
                int? rank = GetRank(nodeGroup, nodeGroupPrimary, nodeTran);
                return !rank.HasValue || rank.Value > PrimaryTransitionCount;
            }
            return false;
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
                    return string.Format(@"{0}_{1}.", optPrefix, optValue.ToString(@"0.0", CultureInfo.InvariantCulture));
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
            else if (MethodType == ExportMethodType.Triggered)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"Primary");
                writer.Write(FieldSeparator);
                writer.Write(@"Trigger");
            }
            writer.Write(FieldSeparator);
            writer.Write(@"Fragmentor");
            writer.Write(FieldSeparator);
            writer.Write(@"Collision Energy");
            writer.Write(FieldSeparator);
            writer.Write(@"Cell Accelerator Voltage");
            if (MethodType != ExportMethodType.Standard)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"Ret Time (min)");
                writer.Write(FieldSeparator);
                writer.Write(@"Delta Ret Time");
            }
            writer.Write(FieldSeparator);
            writer.Write(@"Ion Name");
            if (Document.Settings.PeptideSettings.Libraries.HasLibraries)
            {
                writer.Write(FieldSeparator);
                writer.Write(@"Library Rank");
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
            writer.Write(istdTypes.Contains(nodeTranGroup.TransitionGroup.LabelType)    // ISTD?
                             ? @"TRUE"
                             : @"FALSE");
            writer.Write(FieldSeparator);
            writer.Write(SequenceMassCalc.PersistentMZ(nodeTranGroup.PrecursorMz).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(@"Unit");   // MS1 Res
            writer.Write(FieldSeparator);
            writer.Write(GetProductMz(SequenceMassCalc.PersistentMZ(nodeTran.Mz), step).ToString(CultureInfo));
            writer.Write(FieldSeparator);
            writer.Write(@"Unit");   // MS2 Res

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
                    ? @"TRUE"
                    : @"FALSE");
                writer.Write(FieldSeparator);
                // Trigger must be rank 1 transition, of analyte type and minimum precursor charge
                bool trigger = false;
                if (IsTriggerType(nodePep, nodeTranGroup, istdTypes) && rank.HasValue && rank.Value == 1)
                {
                    int minCharge = nodePep.TransitionGroups.Select(g => Math.Abs(g.PrecursorCharge)).Min();
                    if (Math.Abs(nodeTranGroup.PrecursorCharge) == minCharge)
                        trigger = true;
                }
                writer.Write(trigger ? @"TRUE" : @"FALSE");
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
            writer.WriteDsvField(nodeTran.Transition.GetFragmentIonName(CultureInfo.InvariantCulture), FieldSeparator, FieldSeparatorReplacement);
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

        public ThermoFusionMassListExporter(SrmDocument document)
            : base(document)
        {
        }

        // Write values separated by the field separator, and a line separator at the end.
        private void Write(TextWriter writer, params string[] vals)
        {
            writer.WriteLine(string.Join(FieldSeparator.ToString(CultureInfo.InvariantCulture), vals));
        }

        public string GetHeader(char fieldSeparator)
        {
            var hdr = !Tune3Columns
                ? @"m/z,z,t start (min),t end (min),CID Collision Energy (%)"
                : @"Compound,Formula,Adduct,m/z,z,t start (min),t stop (min),CID Collision Energy (%)";
            if (UseSlens)
                hdr += @",S-lens";
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
                    start = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT.Value - windowRT / 2) ?? 0).ToString(CultureInfo);
                    end = (RetentionTimeRegression.GetRetentionTimeDisplay(predictedRT.Value + windowRT / 2) ?? 0).ToString(CultureInfo);
                }
            }

            string z = nodeTranGroup.TransitionGroup.PrecursorCharge.ToString(CultureInfo);  // CONSIDER(bspratt): Is charge all that matters, or are we implying protonation?
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
            var writeColumns = new List<string> {precursorMz, z, start, end, collisionEnergy};
            if (Tune3Columns)
            {
                writeColumns.InsertRange(0, new []
                {
                    string.Format(@"{0} ({1})",
                        nodePep.Peptide.IsCustomMolecule ? nodeTranGroup.CustomMolecule.InvariantName : Document.Settings.GetModifiedSequence(nodePep).Sequence,
                        nodeTranGroup.TransitionGroup.LabelType),
                    string.Empty,
                    string.Empty
                });
            }
            if (UseSlens)
            {
                var slens = (ExplicitTransitionValues.Get(nodeTran).SLens ?? DEFAULT_SLENS).ToString(CultureInfo);
                writeColumns.Add(slens);
            }
            Write(writer, writeColumns.ToArray());
        }
    }

    public class ThermoFusionIsolationListExporter : ThermoFusionMassListExporter
    {
        public ThermoFusionIsolationListExporter(SrmDocument document)
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

                // Resharper disable LocalizableElement
                argv.AddRange(new[] { "-s", "-m", "\"" + templateName + "\"" });  // Read from stdin, multi-file format
                // Resharper restore LocalizableElement

                string dirWork = Path.GetDirectoryName(fileName);
                var psiExporter = new ProcessStartInfo(exeName)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    // Common directory includes the directory separator
                    WorkingDirectory = dirWork ?? string.Empty,
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
                progressMonitor.UpdateProgress(status);

                psiExporter.RunProcess(stdinBuilder.ToString(), @"MESSAGE: ", progressMonitor, ref status); 

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
