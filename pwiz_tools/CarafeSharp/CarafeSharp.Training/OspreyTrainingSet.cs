/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Linq;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Training
{
    /// <summary>Options for <see cref="OspreyTrainingSet.Build"/>, with Carafe's defaults.</summary>
    public sealed class OspreyTrainingSetOptions
    {
        /// <summary>Carafe's <c>-fdr</c>: the second-pass run precursor q-value a training precursor needs.</summary>
        public double MaxRunQ { get; set; } = 0.01;

        /// <summary>Train on entrapment peptides too (they are exported to measure false discovery, not to learn from).</summary>
        public bool IncludeEntrapment { get; set; }

        /// <summary>
        /// Added to the run's last MS2 retention time to give the RT normalizer, as Carafe's
        /// <c>rt_max</c> is (<c>rt_norm = rt / (last MS2 RT + 0.1)</c>).
        /// </summary>
        public double RtMaxPadding { get; set; } = 0.1;

        /// <summary>The collision energy to train with; null takes the export's dominant one.</summary>
        public double? Nce { get; set; }

        /// <summary>The instrument to train with; null takes the export's instrument model.</summary>
        public string Instrument { get; set; }

        public OspreyMaskingSettings Masking { get; set; } = new OspreyMaskingSettings();

        /// <summary>
        /// False trains on every ion of the kept spectra (Carafe's <c>-no_masking</c>); the
        /// spectra are still chosen by the masking policy's gates.
        /// </summary>
        public bool UseMasking { get; set; } = true;
    }

    /// <summary>
    /// Counts of what <see cref="OspreyTrainingSet.Build"/> kept and why it dropped the rest.
    /// </summary>
    public sealed class OspreyTrainingSetStats
    {
        public int Records { get; set; }
        public int AboveQ { get; set; }
        public int Entrapment { get; set; }
        public int Unmapped { get; set; }
        public int DuplicatePrecursors { get; set; }
        public int RtRows { get; set; }
        public int Ms2Candidates { get; set; }
        public int Ms2Rows { get; set; }
        public Dictionary<string, int> Ms2Rejected { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public long Slots { get; set; }
        public long MatchedSlots { get; set; }
        public long ValidMatchedSlots { get; set; }
        public long MaskedUnmatchedSlots { get; set; }
        public Dictionary<string, long> MaskedBy { get; } = new Dictionary<string, long>(StringComparer.Ordinal);

        public override string ToString()
        {
            return string.Format(
                @"{0} exported precursors: {1} above q, {2} entrapment, {3} unmappable, {4} repeats of a precursor in another run; " +
                @"RT {5} peptide forms; MS2 {6} of {7} spectra kept ({8}); slots {9}, matched {10:P1}, valid of matched {11:P1}, masked of unmatched {12:P1}",
                Records, AboveQ, Entrapment, Unmapped, DuplicatePrecursors, RtRows, Ms2Rows, Ms2Candidates,
                string.Join(@", ", Ms2Rejected.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + @" " + p.Value)),
                Slots, Fraction(MatchedSlots, Slots), Fraction(ValidMatchedSlots, MatchedSlots),
                Fraction(MaskedUnmatchedSlots, Slots - MatchedSlots));
        }

        private static double Fraction(long part, long whole)
        {
            return whole == 0 ? 0 : (double)part / whole;
        }
    }

    /// <summary>
    /// The RT and MS2 training rows CarafeSharp fine-tunes on, built from Osprey's training
    /// exports the way Carafe builds its training tables from a search result: confidently
    /// identified targets only, one spectrum per precursor (the run where it scored best), one
    /// RT row per peptide form, and each ladder ion masked or kept by an
    /// <see cref="OspreyMaskingPolicy"/>.
    /// </summary>
    public sealed class OspreyTrainingSet
    {
        public static OspreyTrainingSet Build(IReadOnlyList<OspreyTrainingExport> exports, OspreyTrainingSetOptions options)
        {
            var stats = new OspreyTrainingSetStats();
            var candidates = new List<Candidate>();
            foreach (var export in exports)
            {
                double rtMax = export.RtMax + options.RtMaxPadding;
                double nce = options.Nce ?? export.DominantCollisionEnergy ??
                    throw new InvalidOperationException(string.Format(
                        @"{0} records no collision energy; give one explicitly.", export.Path));
                string instrument = options.Instrument ?? export.InstrumentModel ?? string.Empty;
                foreach (var record in export.Records)
                {
                    stats.Records++;
                    if (!(record.RunPrecursorQ <= options.MaxRunQ))
                    {
                        stats.AboveQ++;
                        continue;
                    }
                    if (record.IsEntrapment && !options.IncludeEntrapment)
                    {
                        stats.Entrapment++;
                        continue;
                    }
                    if (!OspreyModificationMapper.TryMap(record.Sequence, record.ModifiedSequence, record.ModPositions,
                            record.ModMasses, record.ModUnimodIds, out var peptide, out _))
                    {
                        stats.Unmapped++;
                        continue;
                    }
                    candidates.Add(new Candidate(record, peptide, rtMax, nce, instrument));
                }
            }

            // One spectrum per precursor: the run where it scored best.
            var best = candidates
                .GroupBy(c => c.Key, StringComparer.Ordinal)
                .Select(g => g.OrderBy(c => c.Record.RunPrecursorQ).ThenBy(c => c.Record.Pep)
                    .ThenByDescending(c => c.Record.Score).ThenBy(c => c.Record.FileName, StringComparer.Ordinal).First())
                .OrderBy(c => c.Record.FileName, StringComparer.Ordinal).ThenBy(c => c.Record.EntryId)
                .ToArray();
            stats.DuplicatePrecursors = candidates.Count - best.Length;

            // One RT row per peptide form, from its best precursor.
            var rt = best
                .GroupBy(c => c.FormKey, StringComparer.Ordinal)
                .Select(g => g.OrderBy(c => c.Record.RunPrecursorQ).ThenBy(c => c.Record.Pep)
                    .ThenByDescending(c => c.Record.Score).First())
                .OrderBy(c => c.Record.FileName, StringComparer.Ordinal).ThenBy(c => c.Record.EntryId)
                .Select(c => new RtTrainingExample(c.Peptide, c.Record.ApexRt / c.RtMax))
                .ToArray();
            stats.RtRows = rt.Length;

            var policy = new OspreyMaskingPolicy(options.Masking);
            var ms2 = new List<Ms2TrainingExample>(best.Length);
            foreach (var candidate in best)
            {
                stats.Ms2Candidates++;
                var masked = policy.Apply(candidate.Record);
                stats.Slots += masked.SlotCount;
                stats.MatchedSlots += masked.MatchedCount;
                stats.ValidMatchedSlots += masked.ValidMatchedCount;
                stats.MaskedUnmatchedSlots += masked.MaskedUnmatchedCount;
                foreach (var pair in masked.MaskedBy)
                    stats.MaskedBy[pair.Key] = (stats.MaskedBy.TryGetValue(pair.Key, out long n) ? n : 0) + pair.Value;
                if (masked.RejectReason != null)
                {
                    stats.Ms2Rejected[masked.RejectReason] =
                        (stats.Ms2Rejected.TryGetValue(masked.RejectReason, out int n) ? n : 0) + 1;
                    continue;
                }
                ms2.Add(new Ms2TrainingExample(new PrecursorForm(candidate.Peptide, candidate.Record.Charge),
                    candidate.Nce, candidate.Instrument, masked.Intensities,
                    options.UseMasking ? masked.Invalid : new double[masked.SlotCount]));
            }
            stats.Ms2Rows = ms2.Count;
            return new OspreyTrainingSet(rt, ms2, stats);
        }

        private OspreyTrainingSet(IReadOnlyList<RtTrainingExample> rt, IReadOnlyList<Ms2TrainingExample> ms2, OspreyTrainingSetStats stats)
        {
            Rt = rt;
            Ms2 = ms2;
            Stats = stats;
        }

        public IReadOnlyList<RtTrainingExample> Rt { get; }

        public IReadOnlyList<Ms2TrainingExample> Ms2 { get; }

        public OspreyTrainingSetStats Stats { get; }

        private sealed class Candidate
        {
            public Candidate(OspreyTrainingRecord record, PeptideForm peptide, double rtMax, double nce, string instrument)
            {
                Record = record;
                Peptide = peptide;
                RtMax = rtMax;
                Nce = nce;
                Instrument = instrument;
                FormKey = peptide.Sequence + @"|" + peptide.ModsText + @"|" + peptide.ModSitesText;
                Key = FormKey + @"|" + record.Charge;
            }

            public OspreyTrainingRecord Record { get; }
            public PeptideForm Peptide { get; }
            public double RtMax { get; }
            public double Nce { get; }
            public string Instrument { get; }
            public string FormKey { get; }
            public string Key { get; }
        }
    }
}
