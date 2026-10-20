/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear
 *   (generate_spectral_library, generate_spectral_library_parquet,
 *   generate_spectral_library_parquet_skyline) and src/main/resources/py/v2/ai_pred.py
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// Carafe's library prediction (<c>-db</c> without <c>-ms</c>): digests the FASTA, lists
    /// every peptidoform and precursor, predicts MS2 and RT (and with <c>-ccs</c> each
    /// precursor's ion mobility), and writes the library as Carafe's
    /// TSV and/or a BiblioSpec .blib. Work proceeds in Carafe's batches of 200,000 peptidoforms,
    /// each predicted in chunks that a <see cref="LibraryChunkWriter"/> thread writes while the
    /// next chunk is predicted, so memory is bounded by a few chunks plus the peptide list;
    /// spectra are written in peptidoform mass order, charges ascending.
    /// </summary>
    public sealed class LibraryGenerator
    {
        /// <summary>Peptidoforms predicted and written together within a batch.</summary>
        public const int FORMS_PER_CHUNK = 10000;

        /// <summary>Values per fragment row of an <see cref="Ms2Prediction"/>.</summary>
        private static readonly int INTENSITY_STRIDE = PeptdeepConstants.CHARGED_FRAG_TYPES.Length;

        private readonly LibrarySettings _settings;
        private readonly TextWriter _log;
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly Stopwatch _ms2Clock = new Stopwatch();
        private readonly Stopwatch _rtClock = new Stopwatch();
        private readonly Stopwatch _ccsClock = new Stopwatch();
        private readonly Stopwatch _buildClock = new Stopwatch();
        private PretrainedModels _pretrained;
        private string _modelFileFolder;
        private DecoyPairGate _pairGate;
        private List<PairingManifestRow> _contractManifest;
        // The rt_max this run's library RT is scaled by; 0 (iRT) for the pretrained Chronologer, whose
        // hydrophobic index is not a fraction of the gradient.
        private double _libraryRtMax;

        /// <param name="settings">The library to predict.</param>
        /// <param name="log">Receives progress, or null.</param>
        /// <param name="pretrained">The pretrained models already opened (and checked), or null to open them when needed.</param>
        public LibraryGenerator(LibrarySettings settings, TextWriter log, PretrainedModels pretrained = null)
        {
            _settings = settings;
            _log = log ?? TextWriter.Null;
            _pretrained = pretrained;
        }

        /// <summary>The validity stamp the .blib records, or null for none (a library predicted outside a task).</summary>
        public ArtifactStamp Stamp { get; set; }

        /// <summary>The .blib written, or null.</summary>
        public string BlibPath { get; private set; }

        /// <summary>The TSV written, or null.</summary>
        public string TsvPath { get; private set; }

        /// <summary>Precursors written.</summary>
        public int SpectrumCount { get; private set; }

        /// <summary>
        /// The precursors left out, with a pairing manifest, because their target or decoy
        /// partner had too few fragments (<see cref="DecoyPairGate"/>); empty without one.
        /// </summary>
        public IReadOnlyList<LibrarySpectrum> PairDropped
        {
            get { return _pairGate?.Dropped ?? Array.Empty<LibrarySpectrum>(); }
        }

        /// <summary>A test hook called with each chunk's index before it is predicted, on the predicting thread.</summary>
        internal Action<int> BeforePredictChunk { get; set; }

        /// <summary>
        /// A test hook called with each chunk's index and the threads it is written with, before it
        /// is written, on the writer thread.
        /// </summary>
        internal Action<int, int> BeforeWriteChunk { get; set; }

        /// <summary>
        /// A test hook called with a chunk's index when it is about to wait for room in the full
        /// writer queue, on the predicting thread.
        /// </summary>
        internal Action<int> BeforeQueueWait { get; set; }

        public void Run()
        {
            try
            {
                Predict();
            }
            finally
            {
                // A saved model's unpacked copy, which the models were loaded from.
                if (_modelFileFolder != null && Directory.Exists(_modelFileFolder))
                    Directory.Delete(_modelFileFolder, true);
                _modelFileFolder = null;
            }
        }

        private void Predict()
        {
            _clock.Restart();
            Directory.CreateDirectory(_settings.OutputDirectory);
            CheckOutputsReplaceable(_settings, _settings.OutputDirectory);
            var modelDirectory = OpenModelDirectory();
            var outputs = LibraryOutputs.FromFormat(_settings.LibraryFormat, _settings.Fast);
            if (outputs.Warning != null)
                Log(outputs.Warning);
            // The command line refuses this too; a caller setting the settings directly gets the
            // same answer here, before the models load, rather than a null list after prediction.
            if (_settings.LibraryContract && (!outputs.WritesBlib || string.IsNullOrEmpty(_settings.PairingManifest)))
                throw new ArgumentException(@"The library contract needs a .blib output and the pairing manifest to check it against");

            // The models first: a missing or wrong pretrained archive fails before the digest.
            var device = TorchDevice.Resolve(_settings.Device, out string fallback);
            if (fallback != null)
                Log(fallback);
            using (var ms2 = LoadMs2Model(modelDirectory, device))
            using (var rt = LoadRtModel(modelDirectory, device))
            using (var ccs = _settings.PredictIonMobility ? LoadCcsModel(modelDirectory, device) : null)
            {
                var digester = new Digester(_settings.Digest);
                var peptides = LibraryDatabase.DigestPeptides(_settings.Database, digester, Log);
                var generator = new PeptideIsoformGenerator(_settings.Modifications, digester.ProteinNTermPeptides);
                var forms = LibraryPeptideForms.Enumerate(peptides, generator);
                Log(string.Format(CultureInfo.InvariantCulture, @"Generating peptide forms: {0}", forms.Count));
                var peptideToProteins = LibraryDatabase.MapPeptidesToProteins(_settings.Database, _settings.Digest);
                Log(string.Format(CultureInfo.InvariantCulture, @"Mapped {0} peptides to proteins", peptideToProteins.Count));
                if (_settings.LibraryContract)
                    CheckContractBeforePrediction(peptides, peptideToProteins);

                _libraryRtMax = rt.PredictsNormalizedRt ? _settings.RtMax : 0;
                if (_libraryRtMax <= 0 && _settings.RtMax > 0)
                    Log(string.Format(CultureInfo.InvariantCulture, @"Ignored rt_max {0}: the library RT of a Chronologer in the hydrophobic index is iRT", _settings.RtMax));
                var irt = _libraryRtMax > 0 ? (Slope: 0.0, Intercept: 0.0) : rt.FitIrtCalibration();
                // A library in the aligned runs' minutes was logged with the maps it reads (WithAlignment).
                if (_libraryRtMax > 0 && !(rt is AlignedRtPredictor))
                    Log(string.Format(CultureInfo.InvariantCulture, @"Library RT: rt_pred * rt_max ({0})", _libraryRtMax));
                else if (_libraryRtMax <= 0)
                    Log(string.Format(CultureInfo.InvariantCulture, @"Library RT: iRT = {0} * rt_pred + {1}", irt.Slope, irt.Intercept));
                Log(string.Format(CultureInfo.InvariantCulture, @"NCE: {0}, instrument: {1}, activation: {2}, analyzer: {3}", _settings.Nce,
                    _settings.Instrument, _settings.Activation ?? @"(none)", _settings.Analyzer ?? @"(none)"));
                var builder = new LibrarySpectrumBuilder(_settings, outputs, peptideToProteins);
                WriteLibrary(forms, outputs, builder, ms2, rt, ccs, irt);
                if (((rt as AlignedRtPredictor)?.Inner ?? rt) is ChronologerRtPredictor chronologer && chronologer.FallbackCount > 0)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        @"Chronologer could not encode {0} peptide forms; AlphaPeptDeep's pretrained model predicted their RT", chronologer.FallbackCount));
                }
            }
            Log(string.Format(CultureInfo.InvariantCulture, @"Wrote {0} precursors in {1:F1} s", SpectrumCount, _clock.Elapsed.TotalSeconds));
        }

        private void WriteLibrary(List<PeptideIsoform> forms, LibraryOutputs outputs, LibrarySpectrumBuilder builder,
            Ms2Model ms2, IRtPredictor rt, CcsModel ccs, (double Slope, double Intercept) irt)
        {
            TsvPath = outputs.WritesTsv ? Path.Combine(_settings.OutputDirectory, CarafeLibraryTsvWriter.FILE_NAME) : null;
            BlibPath = outputs.WritesBlib ? Path.Combine(_settings.OutputDirectory, BlibLibraryWriter.FILE_NAME) : null;
            var pairingPrecursors = BlibPath != null && !string.IsNullOrEmpty(_settings.PairingManifest)
                ? new List<DecoyPairPlanner.Precursor>()
                : null;
            _pairGate = CreatePairGate(forms);
            using (var tsv = TsvPath != null ? new CarafeLibraryTsvWriter(TsvPath, ccs != null) : null)
            using (var blib = BlibPath != null
                       ? new BlibLibraryWriter(BlibPath, Path.GetFileNameWithoutExtension(BlibPath), Stamp, _settings.LibraryContract,
                           _settings.CommandLine)
                       : null)
            {
                // Disposed before tsv and blib, so a failure stops the writer thread before their partial files are discarded.
                using (var writer = new LibraryChunkWriter(tsv, blib, pairingPrecursors, BeforeWriteChunk, BeforeQueueWait))
                {
                    PredictChunks(forms, builder, ms2, rt, ccs, irt, writer);
                    if (_pairGate != null)
                    {
                        var held = new List<LibrarySpectrum>();
                        _pairGate.Finish(held);
                        if (held.Count > 0)
                            writer.Add(held);
                        Log(string.Format(CultureInfo.InvariantCulture,
                            @"Pairs: dropped {0} precursors whose target or decoy partner had fewer than {1} fragments, so every pair " +
                            @"is written whole (Carafe keeps them unpaired)", _pairGate.Dropped.Count, _settings.MinFragments));
                        if (_pairGate.ReleasedUnpaired > 0)
                        {
                            Log(string.Format(CultureInfo.InvariantCulture,
                                @"Pairs: wrote {0} precursors without a partner, whose partner side has no precursor of their charge and " +
                                @"modifications to predict", _pairGate.ReleasedUnpaired));
                        }
                    }
                    var finishClock = Stopwatch.StartNew();
                    writer.Finish();
                    SpectrumCount = writer.Written;
                    Log(string.Format(CultureInfo.InvariantCulture,
                        @"Writing finished {0:F1} s after prediction: {1} precursors, writing {2:F1} s, prediction waiting on writer {3:F1} s",
                        finishClock.Elapsed.TotalSeconds, SpectrumCount, writer.WriteTime.TotalSeconds, writer.QueueWaitTime.TotalSeconds));
                }
                // Over what was written: a library whose pairs are not whole is discarded (the writers'
                // temps go with their disposal), not kept with a claim it cannot meet.
                if (_settings.LibraryContract)
                    CheckContractAfterWriting(pairingPrecursors);
                // A finished library that cannot replace the one at its name (open in another program
                // since CheckOutputsReplaceable) is saved beside it as "name (N)", the .blib and TSV with
                // one N, and the run fails, so a script does not go on with the old library.
                string requestedBlib = BlibPath, requestedTsv = TsvPath;
                Exception renameFailure = null;
                int suffix = 0;
                if (blib != null)
                {
                    if (pairingPrecursors != null)
                        WriteDecoyPairs(blib, pairingPrecursors);
                    try
                    {
                        blib.Complete();
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        renameFailure = e;
                        suffix = FindFreeSuffix(requestedBlib, requestedTsv);
                        BlibPath = AlternatePath(requestedBlib, suffix);
                        blib.CommitAs(BlibPath);
                    }
                    Log(@"The spectral library is saved to " + BlibPath);
                }
                if (tsv != null)
                {
                    if (suffix > 0)
                    {
                        TsvPath = AlternatePath(requestedTsv, suffix);
                        tsv.CompleteAs(TsvPath);
                    }
                    else
                    {
                        try
                        {
                            tsv.Complete();
                        }
                        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                        {
                            renameFailure = e;
                            suffix = FindFreeSuffix(requestedBlib, requestedTsv);
                            TsvPath = AlternatePath(requestedTsv, suffix);
                            tsv.CompleteAs(TsvPath);
                        }
                    }
                    Log(@"The spectral library is saved to " + TsvPath);
                }
                if (renameFailure != null)
                {
                    var saved = new[] { (requestedBlib, BlibPath), (requestedTsv, TsvPath) }.Where(p => p.Item1 != null && p.Item1 != p.Item2).ToList();
                    throw new IOException(string.Format(CultureInfo.InvariantCulture,
                        @"The new library could not replace {0} ({1}), so it is saved as {2}; {0} is unchanged.",
                        string.Join(@" and ", saved.Select(p => p.Item1)), renameFailure.Message, string.Join(@" and ", saved.Select(p => p.Item2))),
                        renameFailure);
                }
            }
        }

        /// <summary>
        /// Fails, before any work, when a library the run would replace in <paramref name="outputDirectory"/> cannot be
        /// replaced: open in another program (Skyline, on Windows) or read-only. Prediction, and training before it, can
        /// take hours.
        /// </summary>
        public static void CheckOutputsReplaceable(LibrarySettings settings, string outputDirectory)
        {
            var outputs = LibraryOutputs.FromFormat(settings.LibraryFormat, settings.Fast);
            var paths = new[]
            {
                outputs.WritesBlib ? Path.Combine(outputDirectory, BlibLibraryWriter.FILE_NAME) : null,
                outputs.WritesTsv ? Path.Combine(outputDirectory, CarafeLibraryTsvWriter.FILE_NAME) : null,
            };
            foreach (string path in paths.Where(p => p != null && File.Exists(p)))
            {
                try
                {
                    using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    throw new IOException(string.Format(CultureInfo.InvariantCulture,
                        @"{0} cannot be replaced ({1}). Close it in the program that has it open, or choose another {2} folder.", path, e.Message, CarafeCommandLine.ARG_O.ArgumentText), e);
                }
            }
        }

        /// <summary>The first N from 2 for which "name (N)" is free for every output path given.</summary>
        private static int FindFreeSuffix(params string[] paths)
        {
            for (int n = 2; ; n++)
            {
                if (paths.Where(p => p != null).All(p => !File.Exists(AlternatePath(p, n)) && !Directory.Exists(AlternatePath(p, n))))
                    return n;
            }
        }

        private static string AlternatePath(string path, int n)
        {
            return Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
                string.Format(CultureInfo.InvariantCulture, @"{0} ({1}){2}", Path.GetFileNameWithoutExtension(path), n, Path.GetExtension(path)));
        }

        /// <summary>
        /// The pair gate for a library written with a pairing manifest, every pair member it will
        /// be offered counted, or null without a manifest or when the manifest cannot be read;
        /// the DecoyPairs step then reports the failure, as Carafe's does.
        /// </summary>
        private DecoyPairGate CreatePairGate(List<PeptideIsoform> forms)
        {
            if (string.IsNullOrEmpty(_settings.PairingManifest))
                return null;
            DecoyPairGate gate;
            try
            {
                gate = new DecoyPairGate(DecoyPairPlanner.ReadManifest(_settings.PairingManifest));
            }
            catch (Exception e)
            {
                // As the DecoyPairs step does, any failure to read it leaves the library written as Carafe's is.
                Log(@"The pairing manifest could not be read to keep the library's pairs whole: " + e.Message);
                return null;
            }
            foreach (var form in forms)
            {
                if (!gate.IsMember(form.Sequence))
                    continue;
                var charges = LibraryPeptideForms.GetCharges(form, _settings.Charges, _settings.MinPrecursorMz, _settings.MaxPrecursorMz);
                if (charges.Count == 0)
                    continue;
                var peptide = form.ToAlphabase();
                string modKey = DecoyPairPlanner.ModKey(peptide.ModNames);
                foreach (int charge in charges)
                    gate.Expect(peptide.Sequence, charge, modKey);
            }
            return gate;
        }

        /// <summary>
        /// Predicts Carafe's batches of peptidoforms chunk by chunk, handing each chunk to the
        /// writer thread, which writes it while the next is predicted. Stops with the writer's
        /// exception as soon as writing fails.
        /// </summary>
        private void PredictChunks(List<PeptideIsoform> forms, LibrarySpectrumBuilder builder, Ms2Model ms2, IRtPredictor rt,
            CcsModel ccs, (double Slope, double Intercept) irt, LibraryChunkWriter writer)
        {
            int batchCount = (forms.Count + _settings.PeptidesPerBatch - 1) / Math.Max(1, _settings.PeptidesPerBatch);
            int chunkIndex = 0;
            int predicted = 0;
            // Predicting is the long step: a throttled percent over the peptide forms says the run is
            // alive between the per-batch lines (a batch is 200,000 forms).
            using (var progress = new ProgressReporter(@"Predicting spectra", forms.Count, null, ProgressReporter.IO_INTERVAL_SECONDS))
            {
                for (int batch = 0; batch < batchCount; batch++)
                {
                    int batchStart = batch * _settings.PeptidesPerBatch;
                    int batchEnd = Math.Min(forms.Count, batchStart + _settings.PeptidesPerBatch);
                    int batchPredicted = 0;
                    var batchClock = Stopwatch.StartNew();
                    for (int start = batchStart; start < batchEnd; start += FORMS_PER_CHUNK)
                    {
                        writer.ThrowIfFailed();
                        BeforePredictChunk?.Invoke(chunkIndex++);
                        var spectra = PredictChunk(forms, start, Math.Min(batchEnd, start + FORMS_PER_CHUNK), builder, ms2, rt, ccs, irt);
                        writer.Add(spectra);
                        batchPredicted += spectra.Count;
                        progress.Report(Math.Min(batchEnd, start + FORMS_PER_CHUNK));
                    }
                    predicted += batchPredicted;
                    string ccsTime = ccs != null
                        ? string.Format(CultureInfo.InvariantCulture, @", CCS {0:F1} s", _ccsClock.Elapsed.TotalSeconds)
                        : string.Empty;
                    Log(string.Format(CultureInfo.InvariantCulture,
                        @"Batch {0}/{1}: peptide forms {2}-{3}, {4} precursors predicted in {5:F1} s ({6} total, {7} written, {8:F1} s elapsed; " +
                        @"MS2 {9:F1} s, RT {10:F1} s{14}, assembly {11:F1} s, writing {12:F1} s, waiting on writer {13:F1} s)",
                        batch + 1, batchCount, batchStart + 1, batchEnd, batchPredicted, batchClock.Elapsed.TotalSeconds, predicted,
                        writer.Written, _clock.Elapsed.TotalSeconds, _ms2Clock.Elapsed.TotalSeconds, _rtClock.Elapsed.TotalSeconds,
                        _buildClock.Elapsed.TotalSeconds, writer.WriteTime.TotalSeconds, writer.QueueWaitTime.TotalSeconds, ccsTime));
                }
            }
        }

        /// <summary>
        /// Predicts and builds the spectra of peptidoforms [start, end): MS2 (and with a CCS model
        /// the ion mobility) for every precursor in the m/z window, RT once per peptidoform.
        /// </summary>
        private List<LibrarySpectrum> PredictChunk(List<PeptideIsoform> forms, int start, int end, LibrarySpectrumBuilder builder,
            Ms2Model ms2, IRtPredictor rt, CcsModel ccs, (double Slope, double Intercept) irt)
        {
            var isoforms = new List<PeptideIsoform>();
            var peptides = new List<PeptideForm>();
            var requests = new List<Ms2Request>();
            var formIndex = new List<int>();
            for (int i = start; i < end; i++)
            {
                var charges = LibraryPeptideForms.GetCharges(forms[i], _settings.Charges, _settings.MinPrecursorMz, _settings.MaxPrecursorMz);
                if (charges.Count == 0)
                    continue;
                var peptide = forms[i].ToAlphabase();
                foreach (int charge in charges)
                {
                    requests.Add(new Ms2Request(new PrecursorForm(peptide, charge), _settings.Nce, _settings.Instrument, _settings.Activation,
                        _settings.Analyzer));
                    formIndex.Add(isoforms.Count);
                }
                isoforms.Add(forms[i]);
                peptides.Add(peptide);
            }
            if (requests.Count == 0)
                return new List<LibrarySpectrum>();
            _ms2Clock.Start();
            var predictions = ms2.Predict(requests);
            _ms2Clock.Stop();
            _rtClock.Start();
            double[] rtPredictions = rt.Predict(peptides);
            _rtClock.Stop();
            double[] ccsPredictions = null;
            if (ccs != null)
            {
                _ccsClock.Start();
                ccsPredictions = ccs.Predict(requests.Select(r => r.Precursor).ToArray());
                _ccsClock.Stop();
            }
            _buildClock.Start();
            var spectra = new LibrarySpectrum[requests.Count];
            Parallel.For(0, requests.Count, i =>
            {
                int form = formIndex[i];
                double retentionTime = LibrarySpectrumBuilder.GetRetentionTime(rtPredictions[form], _libraryRtMax, irt.Slope, irt.Intercept);
                spectra[i] = builder.Build(isoforms[form], requests[i].Precursor, predictions[i].Intensities, INTENSITY_STRIDE, retentionTime,
                    ccsPredictions?[i]);
            });
            List<LibrarySpectrum> built;
            if (_pairGate == null)
            {
                built = spectra.Where(s => s != null).ToList();
            }
            else
            {
                built = new List<LibrarySpectrum>(spectra.Length);
                for (int i = 0; i < spectra.Length; i++)
                {
                    var peptide = requests[i].Precursor.Peptide;
                    _pairGate.Offer(peptide.Sequence, requests[i].Precursor.Charge, DecoyPairPlanner.ModKey(peptide.ModNames), spectra[i], built);
                }
            }
            _buildClock.Stop();
            return built;
        }

        /// <summary>
        /// Rules 1 to 5 of the Osprey library contract over the digested peptides and their
        /// proteins, before any prediction: refuses the run with the full report on a violation,
        /// so hours of prediction are not spent on a library Osprey would refuse. The FASTA must
        /// be built without Carafe's per-peptide accession suffix for this to pass.
        /// </summary>
        private void CheckContractBeforePrediction(HashSet<string> peptides, IReadOnlyDictionary<string, string> peptideToProteins)
        {
            var rows = new List<LibraryContractRow>(peptides.Count);
            foreach (string peptide in peptides.OrderBy(p => p, StringComparer.Ordinal))
            {
                var accessions = peptideToProteins.TryGetValue(peptide, out string proteins) && proteins != LibrarySpectrum.NO_PROTEIN
                    ? proteins.Split(';').Where(a => a.Length > 0).ToArray()
                    : Array.Empty<string>();
                rows.Add(new LibraryContractRow(peptide, null, 0, accessions));
            }
            var report = LibraryContract.Validate(rows, ReadContractManifest(), new[] { _settings.DecoyPrefix }, LibraryContractChecks.Rows);
            if (!report.IsValid)
                throw new InvalidOperationException(report.Describe());
            Log(string.Format(CultureInfo.InvariantCulture, @"Library contract: {0} peptides meet rules 1 to 5 against {1}",
                rows.Count, _settings.PairingManifest));
        }

        /// <summary>
        /// Rules 1 and 6 over the written precursors: one row per precursor, every pair whole at
        /// each charge. A violation discards the library rather than keep one that claims the
        /// contract and does not meet it.
        /// </summary>
        private void CheckContractAfterWriting(List<DecoyPairPlanner.Precursor> precursors)
        {
            var rows = new List<LibraryContractRow>(precursors.Count);
            foreach (var precursor in precursors)
                rows.Add(new LibraryContractRow(precursor.StrippedSequence, precursor.ModifiedSequence, precursor.Charge, null));
            var report = LibraryContract.Validate(rows, ReadContractManifest(), new[] { _settings.DecoyPrefix }, LibraryContractChecks.Pairs);
            if (!report.IsValid)
                throw new InvalidOperationException(report.Describe());
            Log(string.Format(CultureInfo.InvariantCulture,
                @"Library contract: {0} precursors meet rules 1 and 6; the library claims the contract", rows.Count));
        }

        /// <summary>The manifest's rows, read once for the check before prediction and the one after writing.</summary>
        private List<PairingManifestRow> ReadContractManifest()
        {
            return _contractManifest ?? (_contractManifest = PairingManifest.Read(_settings.PairingManifest, true, out _));
        }

        private void WriteDecoyPairs(BlibLibraryWriter blib, List<DecoyPairPlanner.Precursor> precursors)
        {
            try
            {
                var manifest = DecoyPairPlanner.ReadManifest(_settings.PairingManifest);
                var rows = DecoyPairPlanner.Plan(precursors, manifest, out int skipped);
                blib.WriteDecoyPairs(rows);
                Log(string.Format(CultureInfo.InvariantCulture, @"DecoyPairs: wrote {0} rows ({1} target/decoy pairs) from manifest {2}",
                    rows.Count, rows.Count / 2, _settings.PairingManifest));
                if (skipped > 0)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        @"DecoyPairs: skipped {0} pairs reusing a precursor already paired (peptides differing only by I/L), " +
                        @"where Carafe stops on a primary-key error", skipped));
                }
            }
            catch (Exception e) when (!_settings.LibraryContract)
            {
                // Carafe catches any DecoyPairs failure, logs it and keeps the library. A contract
                // library's DecoyPairs is part of what it claims, so there the failure is the run's.
                Log(@"Failed to write DecoyPairs table: " + e.Message);
            }
        }

        /// <summary>
        /// The model folder Carafe predicts from: <c>-model_dir</c>, else the output folder.
        /// With <c>-model_dir</c> its meta.json overrides the command line.
        /// </summary>
        private CarafeModelDirectory OpenModelDirectory()
        {
            if (_settings.ModelFile != null)
                return OpenModelFile();
            string folder = _settings.ModelDirectory ?? _settings.OutputDirectory;
            var modelDirectory = CarafeModelDirectory.Open(folder, _settings.PreferSafetensors);
            if (_settings.ModelDirectory != null)
                Log(@"Use the model in the folder: " + folder + @" for spectral library generation");
            foreach (string warning in modelDirectory.Warnings)
                Log(warning);
            if (_settings.ApplyModelDirectoryMeta)
            {
                modelDirectory.ApplyModelDirectoryOverrides(_settings);
                Log(string.Format(CultureInfo.InvariantCulture,
                    @"From {0}: precursor m/z {1}-{2}, fragment m/z {3}-{4}, NCE {5}, rt_max {6}",
                    CarafeModelDirectory.META_FILE, _settings.MinPrecursorMz, _settings.MaxPrecursorMz, _settings.MinFragmentMz,
                    _settings.MaxFragmentMz, _settings.Nce, _settings.RtMax));
                var rtMaxes = modelDirectory.Runs.Select(r => r.RtMax).Distinct().ToList();
                if (rtMaxes.Count > 1)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        @"The training runs in {0} have different rt_max ({1}): as Carafe does, the library takes the last, {2}, " +
                        @"but training normalized RT by the largest, {3}, so library RTs scale by {2}/{3}.",
                        CarafeModelDirectory.META_FILE, string.Join(@", ", rtMaxes.Select(v => v.ToString(CultureInfo.InvariantCulture))),
                        _settings.RtMax, rtMaxes.Max()));
                }
            }
            else if (_settings.ApplyTrainingRunMeta)
            {
                modelDirectory.ApplyTrainingRunOverrides(_settings);
                Log(string.Format(CultureInfo.InvariantCulture,
                    @"From the training run: precursor m/z {0}-{1}, NCE {2}, instrument {3}, rt_max {4}",
                    _settings.MinPrecursorMz, _settings.MaxPrecursorMz, _settings.Nce, _settings.Instrument, _settings.RtMax));
            }
            return modelDirectory;
        }

        /// <summary>
        /// A saved model (<c>-model</c>), checked and unpacked into a folder of its own, which
        /// <see cref="Run"/> deletes. The training run's NCE, instrument and rt_max apply where the
        /// command line gives none; the m/z ranges stay the command line's.
        /// </summary>
        private CarafeModelDirectory OpenModelFile()
        {
            var modelFile = CarafeModelFile.Open(_settings.ModelFile);
            _modelFileFolder = Path.Combine(Path.GetTempPath(), @"CarafeSharp_model_" + Guid.NewGuid().ToString(@"N"));
            var modelDirectory = modelFile.Extract(_modelFileFolder);
            Log(@"Use the saved model " + _settings.ModelFile + @" for spectral library generation: " + modelFile.Describe());
            modelFile.ApplyPredictionDefaults(_settings);
            Log(string.Format(CultureInfo.InvariantCulture,
                @"Precursor m/z {0}-{1}, fragment m/z {2}-{3} from the command line; NCE {4}, instrument {5}, activation {6}, analyzer {7}, rt_max {8}",
                _settings.MinPrecursorMz, _settings.MaxPrecursorMz, _settings.MinFragmentMz, _settings.MaxFragmentMz, _settings.Nce,
                _settings.Instrument, _settings.Activation ?? @"(none)", _settings.Analyzer ?? @"(none)", _settings.RtMax));
            return modelDirectory;
        }

        private Ms2Model LoadMs2Model(CarafeModelDirectory modelDirectory, Device device)
        {
            string path = modelDirectory.GetMs2ModelPath(_settings.TrainingType);
            if (path != null)
            {
                Log((Path.GetFileName(path) == ModelFiles.MS2_BASE_SAFETENSORS
                        ? @"Using the base model's MS2 model, which the fine-tuned one did not beat, "
                        : @"Using fine-tuned MS2 model ") + path);
                return CarafeModelDirectory.IsSafetensors(path) ? Ms2Model.FromSafetensors(path, device) : Ms2Model.FromPthFile(path, device);
            }
            Log(@"Using the pretrained MS2 model");
            return Ms2Model.FromPretrained(OpenPretrained(), device);
        }

        /// <summary>
        /// The RT model the library predicts with: <c>-rt_model</c>'s, else the one the folder's fine-tuned RT model is
        /// (a saved model's <see cref="CarafeModelFile.RtModel"/> came in as <c>-rt_model</c>'s default), else
        /// <see cref="LibrarySettings.DEFAULT_RT_MODEL"/>. A fine-tuned RT model of another kind is replaced by that
        /// kind's pretrained model.
        /// </summary>
        private IRtPredictor LoadRtModel(CarafeModelDirectory modelDirectory, Device device)
        {
            string path = modelDirectory.GetRtModelPath(_settings.TrainingType);
            var fineTunedType = path != null ? GetRtModelType(path) : (RtModelType?)null;
            var type = _settings.RtModelType ?? fineTunedType ?? LibrarySettings.DEFAULT_RT_MODEL;
            if (fineTunedType != null && fineTunedType != type)
            {
                Log(string.Format(@"-rt_model {0}: using the pretrained {0} RT model instead of the fine-tuned {1} RT model {2}",
                    type, fineTunedType, path));
                path = null;
            }
            if (type == RtModelType.chronologer)
                return WithAlignment(LoadChronologer(path, device), modelDirectory);
            if (_settings.RtReference != null)
                throw new InvalidOperationException(string.Format(@"{0} {1} needs the Chronologer RT model, not {2}.", CarafeCommandLine.ARG_RT_REFERENCE.ArgumentText, _settings.RtReference, type));
            if (path != null)
            {
                Log(@"Using fine-tuned RT model " + path);
                return CarafeModelDirectory.IsSafetensors(path) ? RtModel.FromSafetensors(path, device) : RtModel.FromPthFile(path, device);
            }
            Log(@"Using the pretrained RT model");
            return RtModel.FromPretrained(OpenPretrained(), device);
        }

        /// <summary>
        /// A Chronologer and the training runs' maps (<see cref="ModelFiles.RT_MAPS"/>, from <c>-rt_align kde</c>), when
        /// the model folder holds them and the library has an rt_max:
        /// <list type="bullet">
        /// <item>fine-tuned on runs of one gradient, it predicts their aligned minutes, which the library takes as they
        /// are, or with <c>-rt_reference</c> through the maps to one run's;</item>
        /// <item>predicting the hydrophobic index (pretrained, or fine-tuned on runs of different gradients), its library
        /// is in the minutes of the run <c>-rt_reference</c> names, else the runs' median, unless the runs are on
        /// different gradients (<see cref="RtAlignment.IsWide"/>), whose median is none of them: then iRT.</item>
        /// </list>
        /// Without the maps the predictor is as it is. <c>-rt_reference</c> needs them.
        /// </summary>
        private IRtPredictor WithAlignment(IRtPredictor rt, CarafeModelDirectory modelDirectory)
        {
            string path = Path.Combine(modelDirectory.DirectoryPath, ModelFiles.RT_MAPS);
            try
            {
                if (!File.Exists(path))
                {
                    if (_settings.RtReference != null)
                    {
                        throw new InvalidOperationException(string.Format(
                            @"{0} {1} needs a Chronologer trained with {2} kde, whose runs' maps ({3}) its model folder holds.",
                            CarafeCommandLine.ARG_RT_REFERENCE.ArgumentText, _settings.RtReference, CarafeCommandLine.ARG_RT_ALIGN.ArgumentText, ModelFiles.RT_MAPS));
                    }
                    return rt;
                }
                if (!(_settings.RtMax > 0))
                {
                    if (_settings.RtReference != null)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                            @"{0} {1} needs a library in minutes, which {2} {3} is not.", CarafeCommandLine.ARG_RT_REFERENCE.ArgumentText, _settings.RtReference, CarafeCommandLine.ARG_RT_MAX.ArgumentText, _settings.RtMax));
                    }
                    return rt;
                }
                var alignment = RtAlignment.Read(path);
                string run = _settings.RtReference != null ? alignment.FindRun(_settings.RtReference) : null;
                if (rt.PredictsNormalizedRt && alignment.IsWide)
                {
                    Log(string.Format(@"WARNING: this model predicts the median minutes of training runs on different gradients ({0}), " +
                        @"which are none of theirs.", alignment));
                }
                if (run != null)
                {
                    // A minutes model predicts the median minutes over the training normalizer, whatever this library's rt_max.
                    double trainingRtMax = double.IsNaN(alignment.RtMax) ? _settings.RtMax : alignment.RtMax;
                    var medianToRun = rt.PredictsNormalizedRt ? alignment.MedianToRunMinutes(run) : null;
                    Func<double, double> toMinutes = medianToRun != null ? rtNorm => medianToRun(rtNorm * trainingRtMax) : alignment.ToRunMinutes(run);
                    LogMapsTakeMinutes();
                    Log(string.Format(@"Library RT: minutes of training run {0} ({1}, {2}): {3}", run, CarafeCommandLine.ARG_RT_REFERENCE.ArgumentText, path, alignment));
                    return new AlignedRtPredictor(rt, toMinutes, _settings.RtMax);
                }
                if (rt.PredictsNormalizedRt)
                    return rt;
                if (alignment.IsWide)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        @"WARNING: the training runs are on different gradients ({0}), so their median minutes are none of theirs; " +
                        @"the library RT is iRT. Name a run with {1} for its minutes: {2}.",
                        alignment, CarafeCommandLine.ARG_RT_REFERENCE.ArgumentText, string.Join(@", ", alignment.Runs.Select(r => r.Run))));
                    return rt;
                }
                LogMapsTakeMinutes();
                Log(@"Library RT: minutes of the training runs' median map (" + path + @"): " + alignment);
                return new AlignedRtPredictor(rt, alignment.ToMinutes, _settings.RtMax);
            }
            catch
            {
                rt.Dispose();
                throw;
            }
        }

        /// <summary>A given <c>-rt_max</c> does not scale minutes the training runs' maps give.</summary>
        private void LogMapsTakeMinutes()
        {
            if (_settings.UserRtMax)
            {
                Log(string.Format(CultureInfo.InvariantCulture, @"Ignored {0} {1}: the library's minutes come from the training runs' maps",
                    CarafeCommandLine.ARG_RT_MAX.ArgumentText, _settings.RtMax));
            }
        }

        /// <summary>
        /// The kind of RT model a fine-tuned RT model file is: a saved Chronologer, else AlphaPeptDeep's (a safetensors,
        /// or a Carafe checkpoint).
        /// </summary>
        internal static RtModelType GetRtModelType(string rtModelPath)
        {
            return CarafeModelDirectory.IsSafetensors(rtModelPath) && ChronologerModel.IsChronologerFile(rtModelPath)
                ? RtModelType.chronologer
                : RtModelType.alphapeptdeep;
        }

        /// <summary>
        /// The fine-tuned Chronologer at <paramref name="path"/>, or the pretrained one when it is null, with AlphaPeptDeep's
        /// pretrained RT model for the peptides Chronologer cannot encode; the pretrained archive is needed for it.
        /// </summary>
        private IRtPredictor LoadChronologer(string path, Device device)
        {
            var files = ChronologerFiles.Open();
            ChronologerModel chronologer = null;
            RtModel fallback = null;
            try
            {
                if (path != null)
                {
                    Log(@"Using the fine-tuned Chronologer RT model " + path);
                    chronologer = ChronologerModel.FromSafetensors(path, files, device);
                }
                else
                {
                    Log(@"Using the pretrained Chronologer RT model " + files.WeightsPath);
                    chronologer = ChronologerModel.FromFiles(files, device);
                }
                Log(@"AlphaPeptDeep's pretrained RT model predicts the peptides Chronologer cannot encode");
                fallback = RtModel.FromPretrained(OpenPretrained(), device);
                return new ChronologerRtPredictor(chronologer, fallback);
            }
            catch
            {
                chronologer?.Dispose();
                fallback?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// The CCS model for <c>-ccs</c>: the model folder's Carafe <c>ccs_model.pt</c> for
        /// <c>-tf all</c>, else the pretrained one (a saved CarafeSharp model holds none).
        /// </summary>
        private CcsModel LoadCcsModel(CarafeModelDirectory modelDirectory, Device device)
        {
            string path = modelDirectory.GetCcsModelPath(_settings.TrainingType);
            if (path != null)
            {
                Log(@"Using fine-tuned CCS model " + path + @" for the ion mobility (1/K0)");
                return CcsModel.FromPthFile(path, device);
            }
            Log(@"Using the pretrained CCS model for the ion mobility (1/K0)");
            return CcsModel.FromPretrained(OpenPretrained(), device);
        }

        /// <summary>The pretrained models, opened (and their SHA-256 checked) once.</summary>
        private PretrainedModels OpenPretrained()
        {
            return _pretrained ??= PretrainedModels.Open(_settings.PretrainedModels);
        }

        private void Log(string message)
        {
            _log.WriteLine(message);
            _log.Flush();
        }
    }
}
