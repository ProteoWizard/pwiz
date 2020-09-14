using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using pwiz.CLI.analysis;
using pwiz.CLI.msdata;
using pwiz.CLI.util;
using pwiz.Common.SystemUtil;

namespace pwiz.ProteowizardWrapper
{
    public class DiaUmpire : MsDataFileImpl
    {
        private SpectrumList_DiaUmpire.Config _diaUmpireConfig;
        private IterationListenerToMonitor _ilrMonitor;

        public enum WindowScheme
        {
            SWATH_Variable = SpectrumList_DiaUmpire.Config.TargetWindow.Scheme.SWATH_Variable,
            SWATH_Fixed = SpectrumList_DiaUmpire.Config.TargetWindow.Scheme.SWATH_Fixed
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
            public IDictionary<string, object> Parameters { get; }

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
                TripleTOF,
                QExactive,
                OrbitrapLTQ,
            }

            public static Config GetDefaultsForInstrument(InstrumentPreset preset)
            {
                var config = new Config();
                //config.Parameters["AdjustFragIntensity"] = false;
                //config.Parameters["BoostComplementaryIon"] = false;
                switch (preset)
                {
                    case InstrumentPreset.TripleTOF:
                        config.Parameters["MS1PPM"] = 30f;
                        config.Parameters["MS2PPM"] = 40f;
                        config.Parameters["SNThreshold"] = 0.5f;
                        config.Parameters["MS2SNThreshold"] = 0.5f;
                        config.Parameters["EstimateBG"] = true;
                        break;
                    case InstrumentPreset.QExactive:
                        config.Parameters["MS1PPM"] = 10f;
                        config.Parameters["MS2PPM"] = 15f;
                        config.Parameters["MinMSIntensity"] = 0.0f;
                        config.Parameters["MinMSMSIntensity"] = 0.0f;
                        config.Parameters["SNThreshold"] = 0.5f;
                        config.Parameters["MS2SNThreshold"] = 0.5f;
                        config.Parameters["EstimateBG"] = true;
                        config.Parameters["MaxCurveRTRange"] = 4f;
                        config.Parameters["RTOverlapThreshold"] = 0.05f;
                        config.Parameters["CorrThreshold"] = 0.1f;
                        break;
                    case InstrumentPreset.OrbitrapLTQ:
                        config.Parameters["MS1PPM"] = 20f;
                        config.Parameters["MS2PPM"] = 30f;
                        config.Parameters["MinMSIntensity"] = 0.0f;
                        config.Parameters["MinMSMSIntensity"] = 0.0f;
                        config.Parameters["SNThreshold"] = 1.5f;
                        config.Parameters["MS2SNThreshold"] = 1.5f;
                        config.Parameters["EstimateBG"] = false;
                        break;
                }

                return config;
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
                            prop.SetValue(config.instrumentParameters, Convert.ToSingle(Parameters[prop.Name]));
                            break;
                        case int i:
                            prop.SetValue(config.instrumentParameters, Convert.ToInt32(Parameters[prop.Name]));
                            break;
                        case bool b:
                            prop.SetValue(config.instrumentParameters, Convert.ToBoolean(Parameters[prop.Name]));
                            break;
                        default:
                            throw new InvalidDataException(@"unexpected type in SpectrumList_DiaUmpire.Config.InstrumentParameter");
                    }
                }

                return config;
            }
        }

        public DiaUmpire(string path, int sampleIndex,
                         WindowScheme windowScheme,
                         IEnumerable<TargetWindow> targetWindows,
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
            _diaUmpireConfig.diaTargetWindowScheme = (SpectrumList_DiaUmpire.Config.TargetWindow.Scheme) windowScheme;
            foreach (var window in targetWindows.Select(w => new SpectrumList_DiaUmpire.Config.TargetWindow(w.Start, w.End)))
                _diaUmpireConfig.diaVariableWindows.Add(window);

            if (progressMonitor != null)
                _ilrMonitor = new IterationListenerToMonitor(progressMonitor);
        }

        class IterationListenerToMonitor : IterationListener
        {
            private IProgressMonitor _progressMonitor;
            private IProgressStatus _status;
            private int _stepCount;

            public IterationListenerToMonitor(IProgressMonitor progressMonitor)
            {
                _progressMonitor = progressMonitor;
                _status = new ProgressStatus();
            }

            public override Status update(UpdateMessage updateMessage)
            {
                if (updateMessage.iterationCount > 0)
                {
                    updateMessage.message += $@" ({updateMessage.iterationIndex + 1} / {updateMessage.iterationCount})";
                    var stepMatcher = Regex.Match(updateMessage.message, @"\[step (?<step>\d+) of (?<count>\d+)]");
                    int stepProgress = 0;
                    if (stepMatcher.Success)
                    {
                        if (_stepCount == 0)
                            _stepCount = Convert.ToInt32(stepMatcher.Groups["count"].Value);
                        stepProgress = (Convert.ToInt32(stepMatcher.Groups["step"].Value) - 1) * 100 / _stepCount;
                    }
                    else if (updateMessage.message.StartsWith(@"writing chromatograms"))
                        stepProgress = _stepCount * 100 / (_stepCount + 2);
                    else if (updateMessage.message.StartsWith(@"writing spectra"))
                        stepProgress = (_stepCount + 1) * 100 / (_stepCount + 2);
                    _status = _status.ChangePercentComplete(stepProgress + (updateMessage.iterationIndex * 100 / updateMessage.iterationCount) / _stepCount);
                }
                else
                    _status = _status.ChangePercentComplete(-1);

                if (_status.Message != updateMessage.message)
                {
                    _status = _status.ChangeMessage(updateMessage.message);
                    _progressMonitor.UpdateProgress(_status);
                }

                return _progressMonitor.IsCanceled ? Status.Cancel : Status.Ok;
            }
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
            _msDataFile.run.spectrumList = SpectrumList;
            MSDataFile.write(_msDataFile, outputFilepath, config, ilr);
        }
    }
}
