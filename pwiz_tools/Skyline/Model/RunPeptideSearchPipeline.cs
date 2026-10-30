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
using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// What Run Peptide Search does after the search, without asking the user anything: builds the
    /// document library from the search results, calibrates its retention times to an iRT standard,
    /// adds it to the document, then adds the proteins from the FASTA (with protein association,
    /// decoys and iRT standards) or, for feature detection, the detected features. Every choice is
    /// made up front in <see cref="Options"/>. Nothing here changes global settings or the Skyline
    /// document; <see cref="Result"/> says what the caller must apply.
    /// </summary>
    public class RunPeptideSearchPipeline
    {
        public const int DEFAULT_CIRT_PEPTIDES = 10;

        public class Options
        {
            public bool IsFeatureDetection { get; set; }
            public double CutoffScore { get; set; }
            public bool IncludeAmbiguousMatches { get; set; }

            /// <summary>
            /// Replace the document library rather than add the new results to it, when the document has one.
            /// </summary>
            public bool OverwriteDocumentLibrary { get; set; }

            /// <summary>
            /// The iRT standard to calibrate the library to; null or empty for none.
            /// </summary>
            public IrtStandard IrtStandard { get; set; }

            /// <summary>
            /// How many CiRT peptides become the standard when the library holds enough of them.
            /// </summary>
            public int CirtPeptideCount { get; set; } = DEFAULT_CIRT_PEPTIDES;

            public bool RecalibrateIrtStandard { get; set; }

            /// <summary>
            /// The FASTA the proteins come from; ignored for feature detection.
            /// </summary>
            public string FastaPath { get; set; }

            /// <summary>
            /// A FASTA of the target proteins to add, when it differs from the searched FASTA.
            /// </summary>
            public string FastaTargetsPath { get; set; }

            /// <summary>
            /// Where the proteins go in the document; null for the end.
            /// </summary>
            public IdentityPath InsertPath { get; set; }

            /// <summary>
            /// Drops imported peptides whose library q-values miss the cutoff; null for none.
            /// </summary>
            public GlobalQValueFilter QValueFilter { get; set; }

            public ProteinAssociation.ParsimonySettings ParsimonySettings { get; set; } = ProteinAssociation.ParsimonySettings.DEFAULT;
            public string DecoyGenerationMethod { get; set; }
            public double DecoysPerTarget { get; set; }
            public bool AutoTrain { get; set; }
            public bool RequirePrecursorTransition { get; set; }
        }

        public class Result
        {
            public SrmDocument Document { get; set; }
            public LibrarySpec LibrarySpec { get; set; }
            public string AmbiguousMatchesMessage { get; set; }

            /// <summary>
            /// The standard the library was calibrated to, or the requested one when it was not.
            /// </summary>
            public IrtStandard IrtStandard { get; set; }

            /// <summary>
            /// True when the requested standard was "Auto", several known standards matched the
            /// library and the one with the most peptides was used.
            /// </summary>
            public bool IrtStandardChosen { get; set; }

            /// <summary>
            /// The library peptides given iRT values; zero when the library was not calibrated.
            /// </summary>
            public int IrtPeptideCount { get; set; }

            /// <summary>
            /// The per-run regressions the library was calibrated with; null when it was not.
            /// </summary>
            public ProcessedIrtAverages IrtCalibration { get; set; }

            /// <summary>
            /// The predictor using the calibrated library, already in <see cref="Document"/>, which the
            /// caller saves to the settings lists; null when the library was not calibrated.
            /// </summary>
            public RetentionTimeRegression RetentionTimePredictor { get; set; }

            public AssociateProteinsResults ProteinAssociation { get; set; }

            /// <summary>
            /// True when feature detection found no features to add.
            /// </summary>
            public bool NoFeaturesDetected { get; set; }
        }

        private readonly ImportPeptideSearch _importPeptideSearch;
        private readonly LibraryManager _libraryManager;
        private readonly Options _options;
        private LibrarySpec _docLibSpec; // The library this run built and loaded

        public RunPeptideSearchPipeline(ImportPeptideSearch importPeptideSearch, LibraryManager libraryManager, Options options)
        {
            _importPeptideSearch = importPeptideSearch;
            _libraryManager = libraryManager;
            _options = options;
        }

        /// <summary>
        /// Releases the library a run built when no document uses it, because the run failed or was
        /// canceled, so running again can rebuild it.
        /// </summary>
        public void ReleaseLibrary()
        {
            if (_docLibSpec != null)
                _importPeptideSearch.ReleasePeptideSearchLibrary(_libraryManager, _docLibSpec);
            _docLibSpec = null;
        }

        /// <summary>
        /// Runs every step after the search on the calling thread. Throws when a step fails, with a
        /// message for the user.
        /// </summary>
        public Result Run(SrmDocument document, string documentPath, IProgressMonitor monitor, CancellationToken cancellationToken)
        {
            var result = new Result { IrtStandard = _options.IrtStandard };
            var existingLibraries = document.Settings.PeptideSettings.Libraries;

            var docLibSpec = BuildLibrary(document, documentPath, monitor, result);
            cancellationToken.ThrowIfCancellationRequested();
            result.LibrarySpec = docLibSpec;

            if (!_options.IsFeatureDetection)
                CalibrateIrts(docLibSpec, monitor, result);
            cancellationToken.ThrowIfCancellationRequested();

            var docNew = _importPeptideSearch.AddDocumentSpectralLibrary(document, docLibSpec);
            if (docNew == null)
                throw new InvalidDataException(string.Format(ModelResources.ImportPeptideSearch_Failed_to_build_the_library__0__, docLibSpec.Name));
            // Before the FASTA import, whose iRT standard handling reads the standard peptides from it
            if (result.RetentionTimePredictor != null)
                docNew = ImportPeptideSearch.ChangeRetentionTimePredictor(docNew, result.RetentionTimePredictor);

            if (_options.IsFeatureDetection)
            {
                var docFeatures = ImportPeptideSearch.AddDetectedFeatures(docNew, existingLibraries, monitor,
                    new ProgressStatus(ModelResources.ImportPeptideSearch_Adding_detected_features_to_document));
                result.NoFeaturesDetected = docFeatures == null;
                result.Document = docFeatures ?? docNew;
                return result;
            }

            result.Document = AddProteins(docNew, monitor, cancellationToken, result);
            return result;
        }

        private LibrarySpec BuildLibrary(SrmDocument document, string documentPath, IProgressMonitor monitor, Result result)
        {
            // Otherwise the builder adds the new results to the document's library. The library set aside
            // goes back when the build fails, so the document never refers to a library that is gone.
            var setAside = _options.OverwriteDocumentLibrary
                ? new SetAsideLibrary(document, _libraryManager, BiblioSpecLiteSpec.GetLibraryFileName(documentPath))
                : null;
            try
            {
                var docLibSpec = BuildLibraryFiles(document, documentPath, monitor, result);
                setAside?.Discard();
                return docLibSpec;
            }
            catch
            {
                setAside?.Restore();
                throw;
            }
        }

        private LibrarySpec BuildLibraryFiles(SrmDocument document, string documentPath, IProgressMonitor monitor, Result result)
        {
            var thresholdsByFile = _importPeptideSearch.SearchFilenames.ToDictionary(f => f, f => _options.CutoffScore);
            var builder = _importPeptideSearch.GetLibBuilder(document, documentPath, _options.IncludeAmbiguousMatches,
                _options.IsFeatureDetection);
            builder.ScoreThresholdsByFile = thresholdsByFile;

            ImportPeptideSearch.ClosePeptideSearchLibraryStreams(document);
            var container = new MemoryDocumentContainer();
            container.SetDocument(document, container.Document);
            var buildState = new LibraryManager.BuildState(null, null);
            if (!_libraryManager.BuildLibraryBackground(container, builder, monitor, buildState))
                throw new InvalidDataException(string.Format(ModelResources.ImportPeptideSearch_Failed_to_build_the_library__0__, Path.GetFileName(BiblioSpecLiteSpec.GetLibraryFileName(documentPath))));
            result.AmbiguousMatchesMessage = builder.AmbiguousMatchesMessage;

            var docLibSpec = _options.IsFeatureDetection ? builder.LibrarySpec : builder.LibrarySpec.ChangeDocumentLibrary(true);
            if (!_importPeptideSearch.LoadPeptideSearchLibrary(_libraryManager, docLibSpec, monitor))
                throw new InvalidDataException(string.Format(ModelResources.ImportPeptideSearch_Failed_to_build_the_library__0__, docLibSpec.Name));
            _docLibSpec = docLibSpec;
            return docLibSpec;
        }

        /// <summary>
        /// The files of the document's library, moved aside while a new library is built in their place.
        /// </summary>
        private sealed class SetAsideLibrary
        {
            private const string SET_ASIDE_EXT = @".overwritten";

            private readonly List<string> _paths = new List<string>();

            public SetAsideLibrary(SrmDocument document, LibraryManager libraryManager, string docBlibPath)
            {
                ImportPeptideSearch.ReleaseDocumentLibrary(document, libraryManager);
                foreach (var path in new[] { docBlibPath, BiblioSpecLiteSpec.GetRedundantName(docBlibPath) })
                {
                    if (!File.Exists(path))
                        continue;
                    FileEx.SafeDelete(path + SET_ASIDE_EXT, true);
                    File.Move(path, path + SET_ASIDE_EXT);
                    _paths.Add(path);
                }
            }

            public void Discard()
            {
                foreach (var path in _paths)
                    FileEx.SafeDelete(path + SET_ASIDE_EXT, true);
            }

            public void Restore()
            {
                try
                {
                    foreach (var path in _paths)
                    {
                        FileEx.SafeDelete(path);
                        File.Move(path + SET_ASIDE_EXT, path);
                    }
                }
                catch (Exception x)
                {
                    // The failure of the build is the error to report
                    Messages.WriteAsyncDebugMessage(@"Failed to restore the document library: {0}", x.Message);
                }
            }
        }

        private void CalibrateIrts(LibrarySpec docLibSpec, IProgressMonitor monitor, Result result)
        {
            // The FASTA import uses the requested standard even when the library is not calibrated
            _importPeptideSearch.IrtStandard = _options.IrtStandard;
            var library = _importPeptideSearch.DocLib;
            if (!LibraryIrtCalibration.CanCalibrate(library, _options.IrtStandard))
                return;

            var calibration = new LibraryIrtCalibration(library, docLibSpec, _options.IrtStandard, IrtRegressionType.DEFAULT);
            calibration.LoadRetentionTimes(monitor);
            int? numCirt = null;
            bool standardChosen = false;
            if (calibration.UsesCirtPeptides)
            {
                numCirt = Math.Min(_options.CirtPeptideCount, calibration.CirtPeptides.Length);
            }
            else if (calibration.NeedsStandardChoice)
            {
                calibration.ChooseStandard(ChooseStandard(calibration.AutoStandards));
                standardChosen = true;
            }

            try
            {
                calibration.ProcessRetentionTimes(numCirt, monitor);
            }
            catch (Exception x)
            {
                throw new IOException(TextUtil.LineSeparate(
                    Resources.BuildPeptideSearchLibraryControl_AddIrtLibraryTable_An_error_occurred_while_processing_retention_times_, x.Message), x);
            }

            if (!calibration.HasCalibratedPeptides)
                return;

            calibration.CreateIrtDb(_options.RecalibrateIrtStandard, monitor);
            result.IrtStandard = calibration.Standard;
            result.IrtStandardChosen = standardChosen;
            result.IrtPeptideCount = calibration.Processed.DbIrtPeptides.Count();
            result.IrtCalibration = calibration.Processed;
            result.RetentionTimePredictor = ImportPeptideSearch.CreateRetentionTimePredictor(docLibSpec);
            _importPeptideSearch.IrtStandard = calibration.Standard;
        }

        /// <summary>
        /// Of several known standards whose peptides are all in the library, the one with the most peptides.
        /// </summary>
        public static IrtStandard ChooseStandard(IEnumerable<IrtStandard> standards)
        {
            return standards.OrderByDescending(standard => standard.Peptides.Count).First();
        }

        private SrmDocument AddProteins(SrmDocument document, IProgressMonitor monitor, CancellationToken cancellationToken, Result result)
        {
            var irtStandard = _importPeptideSearch.IrtStandard;
            var docImport = ImportPeptideSearch.PrepareImportFasta(document);
            var fastaPath = string.IsNullOrEmpty(_options.FastaTargetsPath) ? _options.FastaPath : _options.FastaTargetsPath;
            List<PeptideGroupDocNode> newPeptideGroups;
            try
            {
                // The import reports each protein it adds, which belongs in a status line, not the run log
                var importMonitor = new StepProgressMonitor(monitor,
                    string.Format(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____, Path.GetFileName(fastaPath)));
                docImport = ImportPeptideSearch.ImportFasta(docImport, fastaPath, irtStandard, importMonitor, _options.InsertPath,
                    out _, out _, out newPeptideGroups);
            }
            catch (Exception x) when (!(x is OperationCanceledException))
            {
                throw new IOException(string.Format(Resources.SkylineWindow_ImportFastaFile_Failed_reading_the_file__0__1__,
                    fastaPath, x.Message), x);
            }
            if (!newPeptideGroups.Any())
                throw new InvalidDataException(Resources.ImportFastaControl_ImportFasta_Importing_the_FASTA_did_not_create_any_target_proteins_);

            // Before protein association, so its peptide counts and decoys see only what passed
            if (_options.QValueFilter != null)
                docImport = _options.QValueFilter.FilterDocument(docImport, newPeptideGroups);

            cancellationToken.ThrowIfCancellationRequested();
            var parameters = new AssociateProteinsResults.Parameters(docImport)
                .ChangeFastaFilePath(_options.FastaPath)
                .ChangeIrtStandard(irtStandard)
                .ChangeDecoyGenerationMethod(_options.DecoyGenerationMethod)
                .ChangeDecoysPerTarget(_options.DecoysPerTarget)
                .ChangeSharedPeptides(_options.ParsimonySettings.SharedPeptides)
                .ChangeParsimonySettings(_options.ParsimonySettings);
            var status = new ProgressStatus(string.Format(Resources.CommandLine_AssociateProteins_Associating_peptides_with_proteins_from_FASTA_file__0_,
                Path.GetFileName(_options.FastaPath)));
            monitor.UpdateProgress(status);
            var association = AssociateProteinsResults.Calculate(parameters, cancellationToken,
                percent => monitor.UpdateProgress(status.ChangePercentComplete(percent)));
            if (association.IsErrorResult)
                throw new InvalidDataException(association.ErrorMessage, association.ErrorException);
            if (association.DocumentFinal == null)
                throw new InvalidDataException(Resources.ImportFastaControl_ImportFasta_The_document_does_not_contain_any_peptides_);
            result.ProteinAssociation = association;

            var docFinal = ImportPeptideSearch.ChangeAutoTrain(association.DocumentFinal, _options.AutoTrain);
            if (docFinal == null)
                throw new InvalidDataException(ModelResources.ImportPeptideSearch_Cannot_automatically_train_mProphet_model_without_decoys__but_decoy_options_resulted_in_no_decoys_being_generated__Please_increase_number_of_decoys_per_target__or_disable_automatic_training_of_mProphet_model_);
            if (_options.RequirePrecursorTransition &&
                !docFinal.PeptideTransitions.Any(nodeTran => nodeTran.Transition.IonType == IonType.precursor))
            {
                throw new InvalidDataException(ModelResources.ImportPeptideSearch_The_document_must_contain_at_least_one_precursor_transition_in_order_to_proceed_);
            }
            return docFinal;
        }

        /// <summary>
        /// Reports a step under its own message, passing on only the percent complete of the messages
        /// the step posts, so a step that posts one per item does not fill the run log.
        /// </summary>
        private class StepProgressMonitor : IProgressMonitor
        {
            private readonly IProgressMonitor _monitor;
            private IProgressStatus _status;

            public StepProgressMonitor(IProgressMonitor monitor, string message)
            {
                _monitor = monitor;
                _status = new ProgressStatus(message);
                _monitor.UpdateProgress(_status);
            }

            public bool IsCanceled => _monitor.IsCanceled;

            public bool HasUI => _monitor.HasUI;

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                if (status.IsError || status.IsCanceled)
                    return _monitor.UpdateProgress(status);
                _status = _status.ChangePercentComplete(status.PercentComplete);
                return _monitor.UpdateProgress(_status);
            }
        }
    }
}
