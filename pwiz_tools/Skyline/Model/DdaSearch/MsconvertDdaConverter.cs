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
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class MsconvertDdaConverter : AbstractDdaConverter, IProgressMonitor
    {
        private const string MSCONVERT_EXE = "msconvert";
        private const string OUTPUT_SUBDIRECTORY = "converted";

        private IProgressMonitor _parentProgressMonitor;
        private IProgressStatus _progressStatus;
        private int _currentSourceIndex;
        private int _stepCount;
        private int _lastPercentComplete;

        public string MsConvertOutputExtension { get; private set; }
        public string MsConvertOutputFormatParam { get; private set; }

        public MsconvertDdaConverter(ImportPeptideSearch importPeptideSearch) : base(importPeptideSearch)
        {
            MsConvertOutputExtension = @".mzML";
            MsConvertOutputFormatParam = @"--mzML";
        }

        public override void SetSpectrumFiles(MsDataFileUri[] spectrumFiles)
        {
            OriginalSpectrumSources = spectrumFiles;
            ConvertedSpectrumSources = new MsDataFileUri[OriginalSpectrumSources.Length];

            for (int i = 0; i < OriginalSpectrumSources.Length; ++i)
            {
                // TODO/CONSIDER: source path may not be writable
                string outputFilepath = Path.Combine(Path.GetDirectoryName(OriginalSpectrumSources[i].GetFilePath()) ?? "", OUTPUT_SUBDIRECTORY,
                    OriginalSpectrumSources[i].GetFileNameWithoutExtension() + MsConvertOutputExtension);
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

        public override bool Run(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            _parentProgressMonitor = progressMonitor;
            _progressStatus = progressStatus;

            try
            {
                UpdateProgress(status => status.ChangeMessage(Resources.MsconvertDdaConverter_Run_Starting_msconvert_conversion_));

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
                        UpdateProgress(status => status.ChangeMessage(string.Format(Resources.MsconvertDdaConverter_Run_Re_using_existing_converted__0__file_for__1__, MsConvertOutputExtension, spectrumSource.GetSampleOrFileName())));
                        continue;
                    }

                    if (File.Exists(outputFilepath))
                        FileEx.SafeDelete(outputFilepath);

                    string tmpFilepath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + MsConvertOutputExtension);

                    var pr = new ProcessRunner();
                    var psi = new ProcessStartInfo(MSCONVERT_EXE)
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        Arguments =
                            $"-v -z {MsConvertOutputFormatParam} " +
                            $"-o {Path.GetDirectoryName(tmpFilepath).Quote()} " +
                            $"--outfile {Path.GetFileName(tmpFilepath)} " +
                            " --acceptZeroLengthSpectra --simAsSpectra --combineIonMobilitySpectra" +
                            " --filter \"peakPicking true 1-\" " +
                            " --filter \"msLevel 2-\" " +
                            spectrumSource.ToString().Quote()
                    };

                    try
                    {
                        pr.Run(psi, null, this, ref _progressStatus, ProcessPriorityClass.BelowNormal);
                    }
                    catch (IOException e)
                    {
                        UpdateProgress(status => status.ChangeMessage(e.Message));
                        return false;
                    }

                    if (progressMonitor?.IsCanceled == true)
                    {
                        FileEx.SafeDelete(tmpFilepath, true);
                        return false;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(outputFilepath) ?? "");
                    File.Move(tmpFilepath, outputFilepath);
                    _progressStatus = _progressStatus.NextSegment();
                }

                // tell the search engine to search the converted files instead of the original files
                ImportPeptideSearch.SearchEngine.SetSpectrumFiles(ConvertedSpectrumSources);
                return true;
            }
            catch (Exception e)
            {
                UpdateProgress(status => status.ChangeErrorException(e));
                return false;
            }
        }

        private void UpdateProgress(Func<IProgressStatus, IProgressStatus> updater)
        {
            if (_parentProgressMonitor == null || _progressStatus == null)
                return;
            _progressStatus = updater(_progressStatus);
        }

        public bool IsCanceled => _parentProgressMonitor?.IsCanceled == true;
        public bool HasUI => false;

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            if (_parentProgressMonitor == null)
                throw new InvalidOperationException(@"null _parentProgressMonitor");

            if (_parentProgressMonitor.IsCanceled)
                return UpdateProgressResponse.cancel;

            var iterationMatcher = System.Text.RegularExpressions.Regex.Match(status.Message, @"(?<iterationIndex>\d+)/(?<iterationCount>\d+)");
            if (!iterationMatcher.Success)
                return UpdateProgressResponse.normal;

            string currentSourceName = OriginalSpectrumSources[_currentSourceIndex].GetSampleOrFileName();
            string sourceSpecificProgress = $@"[{currentSourceName} ({_currentSourceIndex + 1} of {OriginalSpectrumSources.Length})] {status.Message}";
            status = status.ChangeMessage(sourceSpecificProgress).ChangeSegments(_currentSourceIndex, OriginalSpectrumSources.Length * 2);

            int iterationIndex = Convert.ToInt32(iterationMatcher.Groups["iterationIndex"].Value);
            int iterationCount = Math.Max(iterationIndex + 1, Convert.ToInt32(iterationMatcher.Groups["iterationCount"].Value));

            var stepMatcher = System.Text.RegularExpressions.Regex.Match(status.Message, @"\[step (?<step>\d+) of (?<count>\d+)]");
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
    }
}