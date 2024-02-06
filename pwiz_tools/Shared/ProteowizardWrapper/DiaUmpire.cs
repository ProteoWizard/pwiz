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

namespace pwiz.ProteowizardWrapper
{
    public static class DiaUmpire
    {
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
                var defaultConfig = new InstrumentParameter();
                defaultConfig.AdjustFragIntensity = false;

                Parameters = new Dictionary<string, object>();
                foreach (var prop in typeof(InstrumentParameter).GetFields())
                    Parameters[prop.Name] = prop.GetValue(defaultConfig);
            }

            public class InstrumentParameter
            {
                public InstrumentParameter()
                {
                    // default parameters from TTOF5600
                    MS1PPM = 30;
                    MS2PPM = 40;
                    SN = 2;
                    MS2SN = 2;
                    MinMSIntensity = 5;
                    MinMSMSIntensity = 1;
                    MinRTRange = 0.1f;
                    MaxNoPeakCluster = 4;
                    MinNoPeakCluster = 2;
                    MaxMS2NoPeakCluster = 4;
                    MinMS2NoPeakCluster = 2;
                    MaxCurveRTRange = 1.5f;
                    Resolution = 17000;
                    RTtol = 0.1f;
                    Denoise = true;
                    EstimateBG = true;
                    RemoveGroupedPeaks = true;
                }

                // ReSharper disable InconsistentNaming
                public int Resolution;
                public float MS1PPM;
                public float MS2PPM;
                public float SN;
                public float MinMSIntensity;
                public float MinMSMSIntensity;
                public int NoPeakPerMin = 150;
                public float MinRTRange;
                public int StartCharge = 2;
                public int EndCharge = 5;
                public int MS2StartCharge = 2;
                public int MS2EndCharge = 4;
                public float MaxCurveRTRange;
                public float RTtol;
                public float MS2SN;
                public int MaxNoPeakCluster;
                public int MinNoPeakCluster;
                public int MaxMS2NoPeakCluster;
                public int MinMS2NoPeakCluster;
                public bool Denoise;
                public bool EstimateBG;
                public bool DetermineBGByID = false;
                public bool RemoveGroupedPeaks;
                public bool Deisotoping = false;
                public bool BoostComplementaryIon = true;
                public bool AdjustFragIntensity = true;
                public int RPmax = 25;
                public int RFmax = 300;
                public float RTOverlap = (float)0.3;
                public float CorrThreshold = (float)0.2;
                public float DeltaApex = (float)0.6;
                public float SymThreshold = (float)0.3;
                public int NoMissedScan = 1;
                public int MinPeakPerPeakCurve = 1;
                public float MinMZ = 200;
                public int MinFrag = 10;
                public float MiniOverlapP = (float)0.2;
                public bool CheckMonoIsotopicApex = false;
                public bool DetectByCWT = true;
                public bool FillGapByBK = true;
                public float IsoCorrThreshold = (float)0.2;
                public float RemoveGroupedPeaksCorr = (float)0.3;
                public float RemoveGroupedPeaksRTOverlap = (float)0.3;
                public float HighCorrThreshold = (float)0.7;
                public int MinHighCorrCnt = 10;
                public int TopNLocal = 6;
                public int TopNLocalRange = 100;
                public float IsoPattern = (float)0.3;
                public float StartRT = 0;
                public float EndRT = 9999;
                public bool TargetIDOnly = false;
                public bool MassDefectFilter = true;
                public float MinPrecursorMass = 600;
                public float MaxPrecursorMass = 15000;
                public bool UseOldVersion = false;
                public float RT_window_Targeted = -1;
                public int SmoothFactor = 5;
                public bool DetectSameChargePairOnly = false;
                public float MassDefectOffset = (float)0.1;
                public int MS2PairTopN = 5;
                public bool MS2Pairing = true;
                // ReSharper restore InconsistentNaming
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
                return typeof(InstrumentParameter).GetFields().Select(f => f.Name);
            }
            
        }
    }
}
