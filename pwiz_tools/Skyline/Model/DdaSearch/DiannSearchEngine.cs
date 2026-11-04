/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Model.DdaSearch
{
    /// <summary>
    /// Searches DIA spectra with DIA-NN: it predicts a library from the FASTA, then searches the
    /// runs with it, writing one library of what it found for all the runs into the document's folder.
    /// </summary>
    public class DiannSearchEngine : AbstractDdaSearchEngine, IProgressMonitor
    {
        // ReSharper disable LocalizableElement
        public const string MET_EXCISION = "MetExcision";
        public const string THREADS = "Threads";
        // ReSharper restore LocalizableElement

        private readonly string _outputDirectory;
        private CancellationTokenSource _cancelToken;
        private Enzyme _enzyme;
        private int _maxMissedCleavages;
        private double _ms1Accuracy;
        private double _ms2Accuracy;
        private double _qValue = DiannConfig.DEFAULT_QVALUE;
        private List<StaticMod> _fixedMods = new List<StaticMod>();
        private List<StaticMod> _variableMods = new List<StaticMod>();
        private int _maxVariableMods = DiannConfig.DEFAULT_MAX_VAR_MODS;

        /// <param name="outputDirectory">Where DIA-NN writes its library, report and log: the document's folder</param>
        public DiannSearchEngine(string outputDirectory)
        {
            _outputDirectory = outputDirectory;
            AdditionalSettings = new Dictionary<string, Setting>();
            foreach (var setting in DiannConfig.DefaultAdditionalSettings.Values)
                AdditionalSettings[setting.Name] = new Setting(setting);
            AdditionalSettings[MET_EXCISION] = new Setting(MET_EXCISION, true);
            AdditionalSettings[THREADS] = new Setting(THREADS, Environment.ProcessorCount, 1, 1024);
        }

        /// <summary>
        /// Testing support: reuse a predicted library already in the output folder instead of predicting it again.
        /// </summary>
        public bool ReuseCachedLibrary { get; set; }

        /// <summary>
        /// Testing support: reuse the .quant files of an earlier search of the same runs.
        /// </summary>
        public bool ReuseQuantFiles { get; set; }

        public override string[] FragmentIons => new[] { @"b,y" };
        public override string[] Ms2Analyzers => new[] { @"Default" };
        // DIA-NN takes mass accuracies in ppm only, with 0 for its automatic choice
        public override MzToleranceUnits[] PrecursorIonToleranceUnitTypes { get; } = { new MzToleranceUnits(@"ppm", MzTolerance.Units.ppm) };
        public override MzToleranceUnits[] FragmentIonToleranceUnitTypes { get; } = { new MzToleranceUnits(@"ppm", MzTolerance.Units.ppm) };
        public override string EngineName => @"DIA-NN";
        public override string CutoffScoreName => ScoreType.GENERIC_QVALUE;
        public override string CutoffScoreLabel => PropertyNames.CutoffScore_PERCOLATOR_QVALUE;
        public override double DefaultCutoffScore => DiannConfig.DEFAULT_QVALUE;
        public override Bitmap SearchEngineLogo => null;
        public override string SearchEngineBlurb => string.Empty;
        public override string LogFilePath => Path.Combine(_outputDirectory, DiannHelpers.STDOUT_LOG_NAME);
        // It predicts the light peptides: a heavy modification given as a fixed one would make every peptide heavy
        public override bool SearchesIsotopeLabels => false;

        public override event NotificationEventHandler SearchProgressChanged;

        public override void SetPrecursorMassTolerance(MzTolerance mzTolerance)
        {
            _ms1Accuracy = mzTolerance.Value;
        }

        public override void SetFragmentIonMassTolerance(MzTolerance mzTolerance)
        {
            _ms2Accuracy = mzTolerance.Value;
        }

        public override void SetFragmentIons(string ions)
        {
            // DIA-NN chooses the fragment ions itself
        }

        public override void SetMs2Analyzer(string analyzer)
        {
            // DIA-NN has no MS2 analyzer choice
        }

        public override void SetEnzyme(Enzyme enzyme, int maxMissedCleavages)
        {
            _enzyme = enzyme;
            _maxMissedCleavages = maxMissedCleavages;
        }

        public override void SetCutoffScore(double cutoffScore)
        {
            _qValue = cutoffScore;
        }

        public override void SetModifications(IEnumerable<StaticMod> modifications, int maxVariableMods)
        {
            // Isotope labels are not structural modifications for DIA-NN
            var structuralMods = modifications.Where(mod => mod.LabelAtoms == LabelAtoms.None).ToList();
            _fixedMods = structuralMods.Where(mod => !mod.IsVariable).ToList();
            _variableMods = structuralMods.Where(mod => mod.IsVariable).ToList();
            _maxVariableMods = maxVariableMods;
        }

        /// <summary>
        /// Every run's results are in the one library DIA-NN writes for all of them.
        /// </summary>
        public override string GetSearchResultFilepath(MsDataFileUri searchFilepath)
        {
            return Path.Combine(_outputDirectory, DiannHelpers.OUTPUT_LIB_NAME);
        }

        public override bool GetSearchFileNeedsConversion(MsDataFileUri searchFilepath, out AbstractDdaConverter.MsdataFileFormat requiredFormat)
        {
            requiredFormat = AbstractDdaConverter.MsdataFileFormat.mzML;
            var filePath = searchFilepath.GetFilePath();
            bool isTimsTof = Directory.Exists(filePath) &&
                             File.Exists(Path.Combine(filePath, DiannHelpers.TIMSTOF_ANALYSIS_FILE));
            // A sample index picks one sample of a multi-sample WIFF or WIFF2, which DIA-NN cannot do
            return !DiannHelpers.ReadsFormat(DataSourceUtil.GetSourceType(filePath), isTimsTof,
                searchFilepath.GetSampleIndex() >= 0);
        }

        public override bool Run(CancellationTokenSource cancelToken, IProgressStatus status)
        {
            _cancelToken = cancelToken;
            try
            {
                var config = new DiannConfig
                {
                    Ms1Accuracy = _ms1Accuracy,
                    Ms2Accuracy = _ms2Accuracy,
                    QValue = _qValue,
                    MaxMissedCleavages = _maxMissedCleavages,
                    MaxVarMods = _maxVariableMods,
                    MetExcision = (bool) AdditionalSettings[MET_EXCISION].Value,
                    Threads = (int) AdditionalSettings[THREADS].Value,
                    ReuseCachedLibrary = ReuseCachedLibrary,
                    ReuseQuantFiles = ReuseQuantFiles
                };
                foreach (var name in DiannConfig.DefaultAdditionalSettings.Keys)
                    config.AdditionalSettings[name] = AdditionalSettings[name];
                config.ApplyAdditionalSettings();

                var outputLibPath = DiannHelpers.RunSearch(SpectrumFileNames.Select(f => f.GetFilePath()),
                    FastaFileNames[0], _outputDirectory, config, this, ref status, cancelToken.Token,
                    _fixedMods, _variableMods, _enzyme);
                if (cancelToken.IsCancellationRequested)
                    return false;

                // A library with no precursors fails the library build in ways that hide the cause
                if (outputLibPath == null)
                    throw new IOException(ModelResources.DiannSearchEngine_Run_DIA_NN_search_did_not_produce_a_spectral_library_output_);
                if (!DiannHelpers.TryCountLibraryRows(outputLibPath, out long precursorCount))
                    throw new IOException(string.Format(ModelResources.DiannSearchEngine_Run_The_DIA_NN_spectral_library_file_could_not_be_read, outputLibPath));
                if (precursorCount == 0)
                    throw new IOException(string.Format(ModelResources.DiannSearchEngine_Run_DIA_NN_found_no_precursors_at_q_value__0_, _qValue));
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception x)
            {
                UpdateProgress(status.ChangeErrorException(x));
                return false;
            }
        }

        public bool IsCanceled => _cancelToken?.IsCancellationRequested ?? false;

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            SearchProgressChanged?.Invoke(this, status);
            return IsCanceled ? UpdateProgressResponse.cancel : UpdateProgressResponse.normal;
        }

        public bool HasUI => false;
    }
}
