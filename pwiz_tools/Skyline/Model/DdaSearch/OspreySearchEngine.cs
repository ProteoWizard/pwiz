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
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.DdaSearch
{
    /// <summary>
    /// Searches DIA spectra with Osprey: it predicts a library from the FASTA (with the pretrained
    /// models, a saved fine-tuned model, or one it fine-tunes on chosen runs), or takes an existing
    /// library, and searches the runs with it, writing one finished library for all the runs into
    /// the document's Osprey working folder. That library becomes the document library as it is.
    /// </summary>
    public class OspreySearchEngine : AbstractDdaSearchEngine, IProgressMonitor
    {
        private readonly string _documentPath;
        private CancellationTokenSource _cancelToken;
        private Enzyme _enzyme;
        private int _maxMissedCleavages;
        private List<StaticMod> _fixedMods = new List<StaticMod>();
        private List<StaticMod> _variableMods = new List<StaticMod>();
        private int _maxVariableMods;

        /// <param name="documentPath">The document the library is for, which names Osprey's working folder and output</param>
        public OspreySearchEngine(string documentPath)
        {
            _documentPath = documentPath;
        }

        /// <summary>
        /// The search as the wizard's pages set it up: the library source, the model, the settings,
        /// and the peptide length, charge and variable modification limits the library is predicted
        /// for. <see cref="Run"/> adds the runs, the FASTA, the enzyme, the modifications and the
        /// output. Set before the run.
        /// </summary>
        public OspreyConfig Config { get; set; } = new OspreyConfig();

        /// <summary>
        /// Why the model a tuning search fine-tuned was not kept beside the document, for the user; null
        /// when it was, or when the search tuned nothing.
        /// </summary>
        public string TunedModelWarning { get; private set; }

        /// <summary>The configuration of the last run, with everything <see cref="Run"/> added; null before one.</summary>
        public OspreyConfig RunConfig { get; private set; }

        /// <summary>True when the library is predicted from the FASTA, rather than an existing library searched.</summary>
        public bool PredictsLibrary => string.IsNullOrEmpty(Config.LibraryPath);

        /// <summary>True when the search fine-tunes a new model on <see cref="OspreyConfig.TrainingInputs"/>.</summary>
        public bool IsTuningSearch => PredictsLibrary && Config.TrainingInputs.Count > 0;

        /// <summary>Osprey's working folder beside the document: its caches, predicted library and log.</summary>
        public string WorkDirectory => OspreyHelpers.GetWorkDirectory(_documentPath);

        /// <summary>The library Osprey writes, in its working folder; it is moved into place as the document library.</summary>
        public string OutputPath => Path.Combine(WorkDirectory, Path.GetFileName(OspreyHelpers.GetOutputLibraryPath(_documentPath)));

        /// <summary>Where a tuning search's model is kept for later searches: beside the document.</summary>
        public string DocumentModelPath => OspreyHelpers.GetDocumentModelPath(_documentPath);

        public override string[] FragmentIons => new[] { @"b,y" };
        public override string[] Ms2Analyzers => new[] { @"Default" };
        public override string EngineName => @"Osprey";
        public override string CutoffScoreName => ScoreType.GENERIC_QVALUE;
        public override string CutoffScoreLabel => PropertyNames.CutoffScore_PERCOLATOR_QVALUE;
        public override double DefaultCutoffScore => OspreyConfig.DEFAULT_FDR;
        public override Bitmap SearchEngineLogo => null;
        public override string SearchEngineBlurb => string.Empty;
        public override string LogFilePath => Path.Combine(WorkDirectory, OspreyHelpers.STDOUT_LOG_NAME);
        // It predicts the light peptides: a heavy modification given as a fixed one would make every peptide heavy
        public override bool SearchesIsotopeLabels => false;
        public override bool SearchResultIsLibrary => true;

        public override event NotificationEventHandler SearchProgressChanged;

        // The settings page sets the tolerance and the FDRs in the config
        public override void SetPrecursorMassTolerance(MzTolerance mzTolerance)
        {
        }

        public override void SetFragmentIonMassTolerance(MzTolerance mzTolerance)
        {
        }

        public override void SetFragmentIons(string ions)
        {
        }

        public override void SetMs2Analyzer(string analyzer)
        {
        }

        public override void SetCutoffScore(double cutoffScore)
        {
        }

        public override void SetEnzyme(Enzyme enzyme, int maxMissedCleavages)
        {
            _enzyme = enzyme;
            _maxMissedCleavages = maxMissedCleavages;
        }

        public override void SetModifications(IEnumerable<StaticMod> modifications, int maxVariableMods)
        {
            var structuralMods = modifications.Where(mod => mod.LabelAtoms == LabelAtoms.None).ToList();
            _fixedMods = structuralMods.Where(mod => !mod.IsVariable).ToList();
            _variableMods = structuralMods.Where(mod => mod.IsVariable).ToList();
            _maxVariableMods = maxVariableMods;
        }

        /// <summary>
        /// The modifications Carafe cannot predict a library with, which a predicted library refuses.
        /// </summary>
        public IList<StaticMod> GetUnsupportedModifications()
        {
            OspreyHelpers.GetCarafeModificationIds(_fixedMods.Concat(_variableMods), out var unsupported);
            return unsupported;
        }

        /// <summary>
        /// Every run's results are in the one library Osprey writes for all of them.
        /// </summary>
        public override string GetSearchResultFilepath(MsDataFileUri searchFilepath)
        {
            return OutputPath;
        }

        /// <summary>
        /// Osprey reads mzML and, through ProteoWizard, the vendor formats Skyline reads.
        /// </summary>
        public override bool GetSearchFileNeedsConversion(MsDataFileUri searchFilepath, out AbstractDdaConverter.MsdataFileFormat requiredFormat)
        {
            requiredFormat = AbstractDdaConverter.MsdataFileFormat.mzML;
            return false;
        }

        /// <summary>
        /// The configuration a run uses: <see cref="Config"/> with the runs, the FASTA, the enzyme, the
        /// modifications and the output added.
        /// </summary>
        public OspreyConfig GetRunConfig()
        {
            var config = Config.Clone();
            config.DataFiles = SpectrumFileNames?.Select(f => f.GetFilePath()).ToList() ?? new List<string>();
            config.FastaPath = FastaFileNames?.FirstOrDefault();
            config.OutputPath = OutputPath;
            config.WorkDirectory = WorkDirectory;
            config.EnzymeIndex = OspreyHelpers.GetCarafeEnzymeIndex(_enzyme);
            config.MaxMissedCleavages = _maxMissedCleavages;
            config.FixedModificationIds = OspreyHelpers.GetCarafeModificationIds(_fixedMods, out _);
            config.VariableModificationIds = OspreyHelpers.GetCarafeModificationIds(_variableMods, out _);
            config.MaxVariableMods = _maxVariableMods;
            return config;
        }

        public override bool Run(CancellationTokenSource cancelToken, IProgressStatus status)
        {
            _cancelToken = cancelToken;
            TunedModelWarning = null;
            try
            {
                RunConfig = GetRunConfig();
                // An earlier run's library would be taken for this one's if Osprey failed to write it
                FileEx.SafeDelete(RunConfig.OutputPath, true);
                var outputPath = OspreyHelpers.RunSearch(RunConfig, this, ref status);
                if (cancelToken.IsCancellationRequested)
                    return false;
                if (outputPath == null)
                    throw new IOException(ModelResources.OspreySearchEngine_Run_Osprey_did_not_produce_a_spectral_library_);

                if (IsTuningSearch)
                    CopyTunedModel(ref status);
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

        /// <summary>
        /// The model a tuning search fine-tuned is copied beside the document, where the user finds it
        /// and can add it to the model list for other searches. A model that cannot be copied is
        /// reported in the run's log and kept in <see cref="TunedModelWarning"/>; the search itself succeeded.
        /// </summary>
        private void CopyTunedModel(ref IProgressStatus status)
        {
            string modelPath = OspreyHelpers.GetTunedModelPath(RunConfig.OutputPath, WorkDirectory);
            string message = null;
            if (!File.Exists(modelPath))
            {
                message = string.Format(ModelResources.OspreySearchEngine_CopyTunedModel_Osprey_did_not_write_the_fine_tuned_model__0__, modelPath);
            }
            else
            {
                try
                {
                    File.Copy(modelPath, DocumentModelPath, true);
                }
                catch (Exception x)
                {
                    if (ExceptionUtil.IsProgrammingDefect(x))
                        throw;
                    message = TextUtil.LineSeparate(string.Format(ModelResources.OspreySearchEngine_CopyTunedModel_Failed_copying_the_fine_tuned_model_to__0__, DocumentModelPath),
                        x.Message);
                }
            }
            if (message != null)
            {
                TunedModelWarning = message;
                status = status.ChangeMessage(message);
                UpdateProgress(status);
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
