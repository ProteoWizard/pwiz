/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Measures whether a run was acquired at unit resolution (an ion trap such as Stellar) or at
    /// high resolution (Orbitrap, Astral, TOF), for <c>--resolution auto</c>.
    ///
    /// <para>The measured property is how closely the centroided peaks of an MS2 spectrum can sit.
    /// An ion trap resolves about one m/z unit, so two of its centroids are never much closer than
    /// the width of a unit-resolution peak: on Stellar the closest 1% of adjacent peaks are about
    /// 0.45 m/z apart. A high-resolution analyzer separates peaks a few hundredths of an m/z apart,
    /// and every DIA spectrum holds some: on Astral the closest 1% are about 0.03 m/z apart. So
    /// the statistic is the <see cref="SPACING_QUANTILE"/> quantile of the m/z gaps between
    /// adjacent peaks, pooled over the first MS2 spectra of the run, and a run is high resolution
    /// when it is below <see cref="HRAM_MAX_SPACING"/>. The threshold sits between the two by a
    /// factor of about two on the unit side and six on the high-resolution side.</para>
    ///
    /// <para>The sample is the first MS2 spectra in acquisition order, until
    /// <see cref="TARGET_SPACINGS"/> gaps are pooled or <see cref="MAX_SPECTRA"/> spectra are read:
    /// about 50 Stellar or 100 Astral spectra, a few milliseconds from a spectra cache. The
    /// spectra cache and the source file hold the same peaks in the same order, so a run measures
    /// the same either way, which is what lets every HPC node reach the same answer. A node that
    /// has neither (a first-pass or second-pass FDR node, handed only scores files) reads the
    /// answer the scoring node recorded instead: the search settings hash stamped in the scores
    /// file matches the settings of exactly one resolution.</para>
    /// </summary>
    public static class ResolutionDetector
    {
        /// <summary>The quantile of the adjacent-peak m/z gaps the decision reads.</summary>
        public const double SPACING_QUANTILE = 0.01;

        /// <summary>
        /// The largest <see cref="SPACING_QUANTILE"/> gap, in m/z, of a high-resolution run. Peaks
        /// this close are not separated by a unit-resolution analyzer.
        /// </summary>
        public const double HRAM_MAX_SPACING = 0.2;

        /// <summary>Adjacent-peak gaps pooled before the sample stops.</summary>
        public const int TARGET_SPACINGS = 10000;

        /// <summary>MS2 spectra read at most, for a run whose spectra hold few peaks.</summary>
        public const int MAX_SPECTRA = 1000;

        /// <summary>Gaps a measurement needs before it decides anything.</summary>
        public const int MIN_SPACINGS = 100;

        /// <summary>
        /// The resolution an analysis is searched at, measured from the first input whose spectra
        /// decide it and checked against the others; when no input's spectra decide it, read from
        /// the scores file written for the first input that has one. Null when neither decides it.
        /// </summary>
        public static ResolutionDetection Detect(OspreyConfig config)
        {
            var inputs = config.InputFiles;
            if (inputs == null || inputs.Count == 0)
                return null;
            var measurements = inputs.Select(MeasureInput).ToList();
            int decider = measurements.FindIndex(m => m != null && m.IsDecided);
            if (decider >= 0)
            {
                var reference = measurements[decider];
                var detection = new ResolutionDetection(reference.Mode, inputs[decider], reference);
                for (int i = 0; i < inputs.Count; i++)
                {
                    var other = measurements[i];
                    if (other != null && other.IsDecided && other.Mode != reference.Mode)
                        detection.Disagreements.Add(new KeyValuePair<string, ResolutionMeasurement>(inputs[i], other));
                }
                return detection;
            }
            foreach (string input in inputs)
            {
                var recorded = ReadRecordedMode(config, input, out string scoresPath);
                if (recorded.HasValue)
                    return new ResolutionDetection(recorded.Value, input, null) { ScoresPath = scoresPath };
            }
            int measured = measurements.FindIndex(m => m != null);
            return measured >= 0 ? new ResolutionDetection(null, inputs[measured], measurements[measured]) : null;
        }

        /// <summary>
        /// The measurement of one run from its spectra cache when the search would accept it,
        /// else from its source file. Null when it has neither, or neither can be read.
        /// </summary>
        public static ResolutionMeasurement MeasureInput(string inputFile)
        {
            string sourcePath = File.Exists(inputFile) || Directory.Exists(inputFile) ? inputFile : null;
            // A file that cannot be read measures nothing: a damaged or locked cache falls back to
            // the source, and an unreadable source to the scores files. The task that reads the
            // file reports why, and a task that never reads it (one handed scores files) has no
            // reason to fail on it here.
            var sample = new ResolutionMeasurement();
            try
            {
                if (SpectraCache.VisitMs2Spectra(SpectraCache.GetCachePath(inputFile), sourcePath, sample.Add))
                    return sample;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                sample = new ResolutionMeasurement();
            }
            if (sourcePath == null)
                return null;
            try
            {
                SpectrumFileReader.VisitMs2Spectra(sourcePath, sample.Add);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return null;
            }
            return sample;
        }

        /// <summary>The measurement of the given spectra, as far as the sample reads them.</summary>
        public static ResolutionMeasurement Measure(IEnumerable<Spectrum> spectra)
        {
            var sample = new ResolutionMeasurement();
            foreach (var spectrum in spectra)
            {
                if (!sample.Add(spectrum))
                    break;
            }
            return sample;
        }

        /// <summary>
        /// The resolution whose search settings produced the hash stamped in a scores file written
        /// for <paramref name="inputFile"/>, or null when there is no such file or neither
        /// resolution's settings match it.
        /// </summary>
        private static ResolutionMode? ReadRecordedMode(OspreyConfig config, string inputFile, out string scoresPath)
        {
            scoresPath = null;
            foreach (string path in new[] { ParquetScoreCache.GetScoresPath(inputFile), ParquetScoreCache.GetReconciledScoresPath(inputFile) })
            {
                if (!File.Exists(path))
                    continue;
                string recordedHash;
                try
                {
                    ParquetScoreCache.LoadFooterMetadata(path).TryGetValue(ParquetScoreCache.META_SEARCH_HASH, out recordedHash);
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    // An unreadable scores file is reported by the task that reads it.
                    continue;
                }
                foreach (var mode in new[] { ResolutionMode.HRAM, ResolutionMode.UnitResolution })
                {
                    var candidate = config.ShallowClone();
                    candidate.ApplyResolutionMode(mode);
                    if (string.Equals(candidate.Identity.SearchParameterHash(), recordedHash, StringComparison.Ordinal))
                    {
                        scoresPath = path;
                        return mode;
                    }
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The sample <see cref="ResolutionDetector"/> pools: adjacent-peak m/z gaps from MS2 spectra
    /// in the order they are added, and the decision they support.
    /// </summary>
    public sealed class ResolutionMeasurement
    {
        private readonly List<double> _spacings = new List<double>();
        private double? _quantile;

        /// <summary>MS2 spectra the sample read.</summary>
        public int SpectrumCount { get; private set; }

        /// <summary>Adjacent-peak gaps pooled.</summary>
        public int SpacingCount => _spacings.Count;

        /// <summary>True when the sample holds enough gaps to decide.</summary>
        public bool IsDecided => SpacingCount >= ResolutionDetector.MIN_SPACINGS;

        /// <summary>
        /// The <see cref="ResolutionDetector.SPACING_QUANTILE"/> quantile of the pooled gaps, in
        /// m/z: the gap at that rank in ascending order. NaN when no gap was pooled.
        /// </summary>
        public double SpacingQuantile
        {
            get
            {
                if (!_quantile.HasValue)
                {
                    if (_spacings.Count == 0)
                        return double.NaN;
                    var sorted = new List<double>(_spacings);
                    sorted.Sort(); // Array.Sort OK: plain doubles; equal values are identical, so tie order cannot matter
                    _quantile = sorted[(int)(ResolutionDetector.SPACING_QUANTILE * (sorted.Count - 1))];
                }
                return _quantile.Value;
            }
        }

        /// <summary>The resolution the sample measures. Meaningful only when <see cref="IsDecided"/>.</summary>
        public ResolutionMode Mode => SpacingQuantile < ResolutionDetector.HRAM_MAX_SPACING
            ? ResolutionMode.HRAM
            : ResolutionMode.UnitResolution;

        /// <summary>
        /// Adds one MS2 spectrum's gaps; false once the sample is complete, which is the signal
        /// for the reader to stop.
        /// </summary>
        public bool Add(Spectrum spectrum)
        {
            if (IsComplete)
                return false;
            _quantile = null;
            SpectrumCount++;
            var mzs = spectrum.Mzs;
            for (int i = 1; i < mzs.Length; i++)
            {
                double gap = mzs[i] - mzs[i - 1];
                // A repeated m/z says nothing about what the analyzer separates.
                if (gap > 0)
                    _spacings.Add(gap);
            }
            return !IsComplete;
        }

        private bool IsComplete => SpacingCount >= ResolutionDetector.TARGET_SPACINGS ||
                                   SpectrumCount >= ResolutionDetector.MAX_SPECTRA;
    }

    /// <summary>
    /// What <see cref="ResolutionDetector.Detect"/> found: the resolution, the input it was decided
    /// from and the evidence, and any input that measures differently.
    /// </summary>
    public sealed class ResolutionDetection
    {
        public ResolutionDetection(ResolutionMode? mode, string inputFile, ResolutionMeasurement measurement)
        {
            Mode = mode;
            InputFile = inputFile;
            Measurement = measurement;
        }

        /// <summary>The resolution, or null when the input's spectra held too few peaks to decide.</summary>
        public ResolutionMode? Mode { get; }

        /// <summary>The input the resolution was decided from.</summary>
        public string InputFile { get; }

        /// <summary>The measurement of <see cref="InputFile"/>'s spectra, or null when it was read from a scores file.</summary>
        public ResolutionMeasurement Measurement { get; }

        /// <summary>The scores file the resolution was read from, when no spectra were measured.</summary>
        public string ScoresPath { get; init; }

        /// <summary>Other inputs whose spectra measure as the other resolution.</summary>
        public List<KeyValuePair<string, ResolutionMeasurement>> Disagreements { get; } =
            new List<KeyValuePair<string, ResolutionMeasurement>>();
    }
}
