/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.CLI.analysis;
using pwiz.CLI.msdata;
using pwiz.CLI.util;
using pwiz.Common.SystemUtil;

namespace pwiz.ProteowizardWrapper
{
    public class DiaUmpire : MsDataFileImpl
    {
        private readonly SpectrumList_DiaUmpire.Config _diaUmpireConfig;
        private readonly IterationListenerToMonitor _ilrMonitor;

        public enum WindowScheme
        {
            // ReSharper disable InconsistentNaming
            SWATH_Variable = SpectrumList_DiaUmpire.Config.TargetWindow.Scheme.SWATH_Variable,
            SWATH_Fixed = SpectrumList_DiaUmpire.Config.TargetWindow.Scheme.SWATH_Fixed
            // ReSharper restore InconsistentNaming
        }

        public struct TargetWindow
        {
            public double Start;
            public double End;

            public override string ToString()
            {
                return $"{Start} {End}";
            }
        }

        public class Config
        {
            public WindowScheme WindowScheme;
            public IEnumerable<TargetWindow> VariableWindows;
            public IDictionary<string, object> Parameters { get; }
            public bool UseMzMlSpillFile { get; set; }

            public Config()
            {
                var defaultConfig = new SpectrumList_DiaUmpire.Config();
                defaultConfig.instrumentParameters.AdjustFragIntensity = false;

                Parameters = new Dictionary<string, object>();
                foreach (var prop in typeof(SpectrumList_DiaUmpire.Config.InstrumentParameter).GetProperties())
                    Parameters[prop.Name] = prop.GetValue(defaultConfig.instrumentParameters);
            }

            public enum InstrumentPreset
            {
                // ReSharper disable InconsistentNaming
                TripleTOF,
                QExactive,
                OrbitrapLTQ
                // ReSharper restore InconsistentNaming
            }

            public static Config GetDefaultsForInstrument(InstrumentPreset preset)
            {
                var config = new Config();
                //config.Parameters["AdjustFragIntensity"] = false;
                config.Parameters["BoostComplementaryIon"] = false;
                config.Parameters["EstimateBG"] = false; // slower but better performance

                config.Parameters["DeltaApex"] = 0.2f;
                config.Parameters["CorrThreshold"] = 0.1f;

                switch (preset)
                {
                    case InstrumentPreset.TripleTOF:
                        config.Parameters["MS1PPM"] = 10f;
                        config.Parameters["MS2PPM"] = 20f;
                        config.Parameters["SN"] = 0.5f;
                        config.Parameters["MS2SN"] = 0.5f;
                        break;
                    case InstrumentPreset.QExactive:
                        config.Parameters["MS1PPM"] = 10f;
                        config.Parameters["MS2PPM"] = 15f;
                        config.Parameters["MinMSIntensity"] = 0.0f;
                        config.Parameters["MinMSMSIntensity"] = 0.0f;
                        config.Parameters["SN"] = 0.5f;
                        config.Parameters["MS2SN"] = 0.5f;
                        config.Parameters["MaxCurveRTRange"] = 4f;
                        config.Parameters["RTOverlap"] = 0.05f;
                        break;
                    case InstrumentPreset.OrbitrapLTQ:
                        config.Parameters["MS1PPM"] = 20f;
                        config.Parameters["MS2PPM"] = 30f;
                        config.Parameters["MinMSIntensity"] = 0.0f;
                        config.Parameters["MinMSMSIntensity"] = 0.0f;
                        config.Parameters["SN"] = 1.5f;
                        config.Parameters["MS2SN"] = 1.5f;
                        break;
                }

                return config;
            }

            public static Config GetConfigFromDiaUmpireOutput(string filepath)
            {
                return MsDataFileInfo.RunPredicate(filepath, msd =>
                {
                    Config result = null;
                    foreach (var dp in msd.dataProcessingList)
                    {
                        foreach (var pm in dp.processingMethods)
                        {
                            foreach (var up in pm.userParams)
                            {
                                // assume this userParam is first
                                if (up.name == @"Pseudo-spectra generated by DIA-Umpire demultiplexing")
                                {
                                    result = new Config();
                                    continue;
                                }

                                // based on above assumption, if we get a userParam while result is still null, we're in the wrong processingMethod
                                if (result == null)
                                    break;

                                string name = up.name.Replace(@"SE.", "");
                                if (name == @"VariableWindows")
                                {
                                    result.WindowScheme = WindowScheme.SWATH_Variable;
                                    var variableWindows = new List<TargetWindow>();
                                    var windowRanges = up.value.ToString().Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries); // list of 123.4-234.5 strings
                                    foreach (var windowRange in windowRanges)
                                    {
                                        var rangeStartEnd = windowRange.Split('-');
                                        variableWindows.Add(new TargetWindow
                                        {
                                            Start = Convert.ToDouble(rangeStartEnd[0], CultureInfo.InvariantCulture),
                                            End = Convert.ToDouble(rangeStartEnd[1], CultureInfo.InvariantCulture)
                                        });
                                    }
                                    result.VariableWindows = variableWindows;
                                }
                                else
                                    result.Parameters[name] = up.value.ToString();
                            }
                        }

                        if (result != null)
                            return result;
                    }

                    // if we get this far, there was no DIA-Umpire method to extract settings from
                    return null;
                });
            }

            /// <summary>
            /// writes config in a format compatible with DIA-Umpire (both Java and ProteoWizard implementations)
            /// </summary>
            public void WriteConfigToFile(string filepath)
            {
                using (var configFile = new StreamWriter(filepath))
                {
                    foreach(var p in Parameters)
                        configFile.WriteLine($"{p.Key} = {Convert.ToString(p.Value, CultureInfo.InvariantCulture)}");

                    var windowTypeStrings = new Dictionary<WindowScheme, string>
                    {
                        {WindowScheme.SWATH_Variable, @"V_SWATH"},
                        {WindowScheme.SWATH_Fixed, @"SWATH" }
                    };
                    configFile.WriteLine($"WindowType = {windowTypeStrings[WindowScheme]}");

                    if (WindowScheme != WindowScheme.SWATH_Variable)
                        return;

                    configFile.WriteLine(@"==window setting begin");
                    foreach(var window in VariableWindows)
                        configFile.WriteLine($"{window.Start.ToString(CultureInfo.InvariantCulture)}\t{window.End.ToString(CultureInfo.InvariantCulture)}");
                    configFile.WriteLine(@"==window setting end");
                }
            }

            internal static IEnumerable<string> GetDiaUmpireParameters()
            {
                return typeof(SpectrumList_DiaUmpire.Config.InstrumentParameter).GetFields().Select(f => f.Name);
            }

            internal SpectrumList_DiaUmpire.Config GetDiaUmpireConfig()
            {
                var config = new SpectrumList_DiaUmpire.Config();

                foreach (var prop in typeof(SpectrumList_DiaUmpire.Config.InstrumentParameter).GetProperties())
                {
                    switch (prop.GetValue(config.instrumentParameters))
                    {
                        case float f:
                        case double d:
                            prop.SetValue(config.instrumentParameters, Convert.ToSingle(Parameters[prop.Name], CultureInfo.InvariantCulture));
                            break;
                        case int i:
                            prop.SetValue(config.instrumentParameters, Convert.ToInt32(Parameters[prop.Name], CultureInfo.InvariantCulture));
                            break;
                        case bool b:
                            prop.SetValue(config.instrumentParameters, Parameters[prop.Name].ToString().ToLowerInvariant() == "true" ||
                                                                       Parameters[prop.Name].ToString() == "1");
                            break;
                        default:
                            throw new InvalidDataException(@"unexpected type in SpectrumList_DiaUmpire.Config.InstrumentParameter");
                    }
                }

                config.maxThreads = Convert.ToInt32(Parameters["Thread"]);
                config.spillFileFormat = UseMzMlSpillFile ? MSDataFile.Format.Format_mzML : MSDataFile.Format.Format_MZ5;
                config.exportMs1ClusterTable = config.exportMs2ClusterTable = true;

                config.diaTargetWindowScheme = (SpectrumList_DiaUmpire.Config.TargetWindow.Scheme) WindowScheme;
                foreach (var window in VariableWindows.Select(w => new SpectrumList_DiaUmpire.Config.TargetWindow(w.Start, w.End)))
                    config.diaVariableWindows.Add(window);

                return config;
            }
        }

        public DiaUmpire(string path, int sampleIndex,
                         Config diaUmpireConfig,
                         LockMassParameters lockmassParameters = null,
                         bool simAsSpectra = false, bool srmAsSpectra = false, bool acceptZeroLengthSpectra = true,
                         bool requireVendorCentroidedMS1 = false, bool requireVendorCentroidedMS2 = false,
                         bool ignoreZeroIntensityPoints = false,
                         int preferOnlyMsLevel = 0,
                         bool combineIonMobilitySpectra = true, // Ask for IMS data in 3-array format by default (not guaranteed)
                         bool trimNativeId = true,
                         IProgressMonitor progressMonitor = null)
            : base(path, sampleIndex, lockmassParameters, simAsSpectra, srmAsSpectra, acceptZeroLengthSpectra,
                requireVendorCentroidedMS1, requireVendorCentroidedMS2, ignoreZeroIntensityPoints, preferOnlyMsLevel,
                combineIonMobilitySpectra, trimNativeId)
        {
            _diaUmpireConfig = diaUmpireConfig.GetDiaUmpireConfig();

            if (progressMonitor != null)
                _ilrMonitor = new IterationListenerToMonitor(progressMonitor);
        }

        protected override SpectrumList SpectrumList
        {
            get
            {
                if (_spectrumList != null)
                    return _spectrumList;

                var ilr = new IterationListenerRegistry();
                if (_ilrMonitor != null)
                    ilr.addListenerWithTimer(_ilrMonitor, 5);
                _spectrumList = new SpectrumList_DiaUmpire(_msDataFile, base.SpectrumList, _diaUmpireConfig, ilr);
                return _spectrumList;
            }
        }

        public void WriteToFile(string outputFilepath, bool mz5Format)
        {
            var config = new MSDataFile.WriteConfig
            {
                compression = MSDataFile.Compression.Compression_Zlib,
                format = mz5Format ? MSDataFile.Format.Format_MZ5 : MSDataFile.Format.Format_mzML,
                indexed = true,
                precision = MSDataFile.Precision.Precision_32 // CONSIDER: is 64-bit precision needed for these pseudo-spectra?
            };

            var ilr = new IterationListenerRegistry();
            if (_ilrMonitor != null)
                ilr.addListenerWithTimer(_ilrMonitor, 5);
            //_msDataFile.run.id += "-inprocess";
            _msDataFile.run.spectrumList = SpectrumList;
            MSDataFile.write(_msDataFile, outputFilepath, config, ilr);
        }
    }
}
