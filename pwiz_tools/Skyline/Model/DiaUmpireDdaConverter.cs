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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MathNet.Numerics;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    public class DiaUmpireDdaConverter : AbstractDdaConverter, IProgressMonitor
    {
        private const string MSCONVERT_EXE = "msconvert";
        private const string DIAUMPIRE_OUTPUT_SUFFIX = "-diaumpire";

        private readonly DiaUmpire.Config _diaUmpireConfig;
        private IProgressMonitor _parentProgressMonitor;
        private IProgressStatus _progressStatus;
        private int _currentSourceIndex;

        public DiaUmpireDdaConverter(ImportPeptideSearch importPeptideSearch, IsolationScheme isolationScheme, DiaUmpire.Config diaUmpireConfig) : base(importPeptideSearch)
        {
            var isolationWindows = isolationScheme.PrespecifiedIsolationWindows;
            var windowSizes = isolationWindows.Skip(1).Select(w => Math.Round(w.End - w.Start, 1));
            bool fixedSizeWindows = windowSizes.Distinct().Count() == 1;

            diaUmpireConfig.WindowScheme = DiaUmpire.WindowScheme.SWATH_Variable;
            if (fixedSizeWindows)
                diaUmpireConfig.WindowScheme = DiaUmpire.WindowScheme.SWATH_Fixed;

            diaUmpireConfig.VariableWindows = isolationWindows.Select(w => new DiaUmpire.TargetWindow { Start = w.Start, End = w.End });
            _diaUmpireConfig = diaUmpireConfig;
            _parentProgressMonitor = null;

            if (diaUmpireConfig.WindowScheme == DiaUmpire.WindowScheme.SWATH_Variable)
                Assume.IsTrue(diaUmpireConfig.VariableWindows.Any());

            MsConvertOutputExtension = _diaUmpireConfig.UseMzMlSpillFile ? @".mzML" : @".mz5";
            MsConvertOutputFormatParam = _diaUmpireConfig.UseMzMlSpillFile ? @"--mzML" : @"--mz5";
        }

        public DiaUmpire.Config DiaUmpireConfig => _diaUmpireConfig;
        public string MsConvertOutputExtension { get; private set; }
        public string MsConvertOutputFormatParam { get; private set; }
        public string DiaUmpireFileSuffix => DIAUMPIRE_OUTPUT_SUFFIX + MsConvertOutputExtension;

        public override void SetSpectrumFiles(MsDataFileUri[] spectrumFiles)
        {
            OriginalSpectrumSources = spectrumFiles;
            ConvertedSpectrumSources = new MsDataFileUri[OriginalSpectrumSources.Length];

            for (int i = 0; i < OriginalSpectrumSources.Length; ++i)
            {
                string outputDirectory = Path.GetDirectoryName(OriginalSpectrumSources[i].GetFilePath()) ?? string.Empty;
                if (Program.FunctionalTest || !DirectoryEx.IsWritable(outputDirectory))
                    outputDirectory = Path.GetDirectoryName(Program.MainWindow.DocumentFilePath) ?? string.Empty;

                var outputFilepath = Path.Combine(outputDirectory, OriginalSpectrumSources[i].GetFileNameWithoutExtension() + DiaUmpireFileSuffix);
                ConvertedSpectrumSources[i] = new MsDataFilePath(outputFilepath);
            }
        }

        public override void SetRequiredOutputFormat(MsdataFileFormat format)
        {
            string formatName = Enum.GetName(typeof(MsdataFileFormat), format);
            MsConvertOutputExtension = '.' + formatName;
            MsConvertOutputFormatParam = @"--" + formatName;
            SetSpectrumFiles(OriginalSpectrumSources);
        }

        public override bool Run(IProgressMonitor progressMonitor, IProgressStatus status)
        {
            _parentProgressMonitor = progressMonitor;
            _progressStatus = status;

            try
            {
                progressMonitor?.UpdateProgress(_progressStatus.ChangeMessage(ModelResources.DiaUmpireDdaConverter_Run_Starting_DIA_Umpire_conversion));

                int sourceIndex = 0;
                foreach (var spectrumSource in OriginalSpectrumSources)
                {
                    _currentSourceIndex = sourceIndex;
                    string outputFilepath = ConvertedSpectrumSources[_currentSourceIndex].GetFilePath();
                    ++sourceIndex;

                    // CONSIDER: read the file description to see what settings were used to generate the file;
                    // if the same settings were used, we can re-use the file, else regenerate
                    if (MsDataFileImpl.IsValidFile(outputFilepath))
                    {
                        var outputFileConfig = DiaUmpire.Config.GetConfigFromDiaUmpireOutput(outputFilepath);
                        if (outputFileConfig != null)
                        {
                            bool equivalentConfig = true;
                            foreach (var kvp in outputFileConfig.Parameters)
                            {
                                if (!AreValuesEquivalent(kvp.Value.ToString(), _diaUmpireConfig.Parameters[kvp.Key].ToString()))
                                {
                                    equivalentConfig = false;
                                    break;
                                }
                            }

                            // TODO: The VariableWindows UserParam is being truncated so can't be checked here if it's an mz5 output file; fix mz5 UserParams not able to be longer than 256 bytes?
                            //if (equivalentConfig && outputFileConfig.WindowScheme == DiaUmpire.WindowScheme.SWATH_Variable)
                            //    equivalentConfig = outputFileConfig.VariableWindows.SequenceEqual(_diaUmpireConfig.VariableWindows);

                            if (equivalentConfig)
                            {
                                progressMonitor?.UpdateProgress(status.ChangeMessage(
                                    string.Format(ModelResources.DiaUmpireDdaConverter_Run_Re_using_existing_DiaUmpire_file__with_equivalent_settings__for__0_,
                                        spectrumSource.GetSampleOrFileName())));
                                continue;
                            }
                        }
                    }

                    if (File.Exists(outputFilepath))
                        FileEx.SafeDelete(outputFilepath);

                    string tmpFilepath = Path.Combine(Path.GetTempPath(), PathEx.GetRandomFileName() + MsConvertOutputExtension); // N.B. FileEx.GetRandomFileName adds unusual characters in test mode
                    string tmpParams = Path.Combine(Path.GetDirectoryName(outputFilepath) ?? string.Empty, @$"diaumpire-{DateTime.Now.ToString(@"yyyyMMddhhmm")}.params");
                    //_diaUmpireConfig.Parameters["Thread"] = 1; // needed to compare DIAUMPIRE_DEBUG output
                    _diaUmpireConfig.WriteConfigToFile(tmpParams);

                    var pr = new ProcessRunner();
                    var psi = new ProcessStartInfo(MSCONVERT_EXE)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        Arguments =
                            $"-v --32 -z {MsConvertOutputFormatParam} " +
                            $"-o {Path.GetDirectoryName(tmpFilepath).Quote()} " +
                            $"--outfile {Path.GetFileName(tmpFilepath).Quote()} " +
                            " --acceptZeroLengthSpectra --simAsSpectra --combineIonMobilitySpectra" +
                            " --filter \"peakPicking true 1-\"" + 
                            " --filter " + $@"diaUmpire params={tmpParams.EscapedPathForNestedCommandLineQuotes()}".Quote() + " " +
                            spectrumSource.ToString().Quote()
                    };

                    try
                    {
                        status = status.ChangeMessage(String.Format(Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                            psi.FileName, psi.Arguments));
                        pr.Run(psi, null, this, ref _progressStatus, ProcessPriorityClass.BelowNormal);
                    }
                    catch (IOException e)
                    {
                        progressMonitor?.UpdateProgress(status.ChangeMessage(e.Message));
                        return false;
                    }

                    FileEx.SafeDelete(tmpParams, true);

                    if (progressMonitor?.IsCanceled == true)
                    {
                        FileEx.SafeDelete(tmpFilepath, true);
                        return false;
                    }

                    File.Move(tmpFilepath, outputFilepath);
                    _progressStatus = _progressStatus.NextSegment();
                }

                // tell the search engine to search the converted files instead of the original files
                ImportPeptideSearch.SearchEngine.SetSpectrumFiles(ConvertedSpectrumSources);
                return true;
            }
            catch (Exception e)
            {
                progressMonitor?.UpdateProgress(status.ChangeErrorException(e));
                return false;
            }
        }

        public bool IsCanceled => _parentProgressMonitor?.IsCanceled == true;
        public bool HasUI => false;

        private int _stepCount;
        private int _lastPercentComplete;

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            if (_parentProgressMonitor == null)
                throw new InvalidOperationException(@"null _parentProgressMonitor");

            if (_parentProgressMonitor.IsCanceled)
                return UpdateProgressResponse.cancel;

            var iterationMatcher = Regex.Match(status.Message, @"(?<iterationIndex>\d+)/(?<iterationCount>\d+)");
            if (!iterationMatcher.Success)
                return UpdateProgressResponse.normal;

            string currentSourceName = OriginalSpectrumSources[_currentSourceIndex].GetSampleOrFileName();
            string sourceSpecificProgress = $@"[{currentSourceName} ({_currentSourceIndex + 1} of {OriginalSpectrumSources.Length})] {status.Message}";
            status = status.ChangeMessage(sourceSpecificProgress).ChangeSegments(_currentSourceIndex, OriginalSpectrumSources.Length * 2);

            int iterationIndex = Convert.ToInt32(iterationMatcher.Groups["iterationIndex"].Value);
            int iterationCount = Math.Max(iterationIndex+1, Convert.ToInt32(iterationMatcher.Groups["iterationCount"].Value));

            var stepMatcher = Regex.Match(status.Message, @"\[step (?<step>\d+) of (?<count>\d+)]");
            int stepProgress = 0;
            if (stepMatcher.Success)
            {
                if (_stepCount == 0)
                    _stepCount = Convert.ToInt32(stepMatcher.Groups["count"].Value) + 1; // writing spectra is an extra step to consider
                stepProgress = (Convert.ToInt32(stepMatcher.Groups["step"].Value) - 1) * 100 / _stepCount;
            }
            //else if (status.Message.StartsWith(@"writing chromatograms"))
            //    stepProgress = stepCount * 100 / (stepCount + 2);
            else if (status.Message.StartsWith(@"writing spectra"))
                stepProgress = (_stepCount - 1) * 100 / _stepCount;
            else
                return _parentProgressMonitor.UpdateProgress(status.ChangePercentComplete(_lastPercentComplete)); // no change to percentComplete

            _lastPercentComplete = stepProgress + (iterationIndex * 100 / iterationCount) / _stepCount;
            status = status.ChangePercentComplete(_lastPercentComplete);

            return _parentProgressMonitor.UpdateProgress(status);
        }

        private static bool AreValuesEquivalent(string lhs, string rhs)
        {
            lhs = lhs.ToLowerInvariant().Replace(@"true", @"1").Replace(@"false", @"0");
            rhs = rhs.ToLowerInvariant().Replace(@"true", @"1").Replace(@"false", @"0");
            if (int.TryParse(lhs, out int lhsInt) && int.TryParse(rhs, out int rhsInt))
                return lhsInt == rhsInt;
            if (double.TryParse(lhs.Replace(@".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), out double lhsDbl) &&
                double.TryParse(rhs.Replace(@".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator), out double rhsDbl))
                return lhsDbl.AlmostEqual(rhsDbl, 5);
            return lhs == rhs;
        }
    }
}