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

        public DiaUmpireDdaConverter(AbstractDdaSearchEngine searchEngine, IsolationScheme isolationScheme, DiaUmpire.Config diaUmpireConfig) : base(searchEngine)
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
        }

        public DiaUmpire.Config DiaUmpireConfig => _diaUmpireConfig;
        public string MsConvertOutputExtension => _diaUmpireConfig.UseMzMlSpillFile ? @".mzML" : @".mz5";
        public string MsConvertOutputFormatParam => _diaUmpireConfig.UseMzMlSpillFile ? @"--mzML" : @"--mz5";
        public string DiaUmpireFileSuffix => DIAUMPIRE_OUTPUT_SUFFIX + MsConvertOutputExtension;

        public override bool Run(IProgressMonitor progressMonitor, IProgressStatus status)
        {
            _parentProgressMonitor = progressMonitor;
            _progressStatus = status;

            try
            {
                OriginalSpectrumSources = SearchEngine.SpectrumFileNames;
                ConvertedSpectrumSources = new MsDataFileUri[OriginalSpectrumSources.Length];

                progressMonitor?.UpdateProgress(_progressStatus.ChangeMessage(Resources.DiaUmpireDdaConverter_Run_Starting_DIA_Umpire_conversion));

                int sourceIndex = 0;
                foreach (var spectrumSource in OriginalSpectrumSources)
                {
                    _currentSourceIndex = sourceIndex;

                    // TODO/CONSIDER: source path may not be writable
                    string outputFilepath = Path.Combine(Path.GetDirectoryName(spectrumSource.GetFilePath()) ?? "",
                        spectrumSource.GetFileNameWithoutExtension() + DiaUmpireFileSuffix);
                    ConvertedSpectrumSources[sourceIndex] = new MsDataFilePath(outputFilepath);
                    ++sourceIndex;

                    // CONSIDER: read the file description to see what settings were used to generate the file;
                    // if the same settings were used, we can re-use the file, else regenerate
                    if (MsDataFileImpl.IsValidFile(outputFilepath))
                    {
                        var outputFileConfig = DiaUmpire.Config.GetConfigFromDiaUmpireOutput(outputFilepath);
                        bool equivalentConfig = true;
                        foreach (var kvp in outputFileConfig.Parameters)
                        {
                            if (!AreValuesEquivalent(kvp.Value.ToString(), _diaUmpireConfig.Parameters[kvp.Key].ToString()))
                            {
                                equivalentConfig = false;
                                break;
                            }
                        }

                        if (equivalentConfig)
                        {
                            progressMonitor?.UpdateProgress(status.ChangeMessage(
                                string.Format(Resources.DiaUmpireDdaConverter_Run_Re_using_existing_DiaUmpire_file__with_equivalent_settings__for__0_,
                                    spectrumSource.GetSampleOrFileName())));
                            continue;
                        }
                    }

                    if (File.Exists(outputFilepath))
                        FileEx.SafeDelete(outputFilepath);

                    string tmpFilepath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + MsConvertOutputExtension);
                    string tmpParams = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + @".params");
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
                            $"--outfile {Path.GetFileName(tmpFilepath)} " +
                            //" --filter \"peakPicking true 1-\"" + 
                            " --filter " + $@"diaUmpire params={tmpParams}".Quote() + " " +
                            spectrumSource.ToString().Quote()
                    };

                    pr.Run(psi, null, this, ref _progressStatus, ProcessPriorityClass.BelowNormal);

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
                SearchEngine.SetSpectrumFiles(ConvertedSpectrumSources);
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

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            if (_parentProgressMonitor == null)
                throw new InvalidOperationException(@"null _parentProgressMonitor");

            if (_parentProgressMonitor.IsCanceled)
                return UpdateProgressResponse.cancel;

            if (!Regex.Match(status.Message, @"\d+/\d+").Success)
                return UpdateProgressResponse.normal;

            string currentSourceName = OriginalSpectrumSources[_currentSourceIndex].GetSampleOrFileName();
            string sourceSpecificProgress = $@"[{currentSourceName} ({_currentSourceIndex+1} of {OriginalSpectrumSources.Length})] {status.Message}";
            return _parentProgressMonitor.UpdateProgress(status.ChangeMessage(sourceSpecificProgress).ChangeSegments(_currentSourceIndex, OriginalSpectrumSources.Length * 2));
        }

        private static bool AreValuesEquivalent(string lhs, string rhs)
        {
            lhs = lhs.ToLowerInvariant().Replace(@"true", @"1").Replace(@"false", @"0");
            rhs = rhs.ToLowerInvariant().Replace(@"true", @"1").Replace(@"false", @"0");
            if (int.TryParse(lhs, out int lhsInt) && int.TryParse(rhs, out int rhsInt))
                return lhsInt == rhsInt;
            if (double.TryParse(lhs, out double lhsDbl) && double.TryParse(rhs, out double rhsDbl))
                return lhsDbl.AlmostEqual(rhsDbl, 5);
            return lhs == rhs;
        }
    }
}