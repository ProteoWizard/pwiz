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
using System.IO;
using System.Linq;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Training
{
    /// <summary>Options for <see cref="OspreyTrainingSet.Build"/>, with Carafe's defaults.</summary>
    public sealed class OspreyTrainingSetOptions
    {
        /// <summary>Carafe's default collision energy (<c>CParameter.NCE</c>).</summary>
        public const double DEFAULT_NCE = 27.0;

        /// <summary>Carafe's default instrument, for a run whose instrument it does not recognize.</summary>
        public const string DEFAULT_INSTRUMENT = @"Eclipse";

        /// <summary>Carafe's <c>-fdr</c>: the second-pass run precursor q-value a training precursor needs.</summary>
        public double MaxRunQ { get; set; } = 0.01;

        /// <summary>Train on entrapment peptides too (they are exported to measure false discovery, not to learn from).</summary>
        public bool IncludeEntrapment { get; set; }

        /// <summary>
        /// Added to a run's last MS2 retention time to give its <c>rt_max</c>, as Carafe's is
        /// (the last MS2 RT + 0.1).
        /// </summary>
        public double RtMaxPadding { get; set; } = 0.1;

        /// <summary>
        /// Carafe's <c>-rt_max</c>, a floor on the RT normalizer (0 = none). Every RT row is
        /// divided by one normalizer, the larger of this and every run's rt_max, as Carafe's is.
        /// </summary>
        public double RtMax { get; set; }

        /// <summary>
        /// Carafe's <c>-nce</c>: the collision energy of a run whose export records none (the
        /// run's own is used when it has one, as Carafe does); null for Carafe's default,
        /// <see cref="DEFAULT_NCE"/>.
        /// </summary>
        public double? Nce { get; set; }

        /// <summary>
        /// Carafe's <c>-ms_instrument</c>, the instrument of every row; null takes each run's
        /// instrument model by Carafe's name for it (<see cref="OspreyTrainingSet.GetCarafeInstrument"/>), else
        /// <see cref="DEFAULT_INSTRUMENT"/>.
        /// </summary>
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

        /// <summary>The normalizer every RT row was divided by.</summary>
        public double RtMax { get; set; }
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
                @"RT {5} peptide forms (rt_max {13:F4}); MS2 {6} of {7} spectra kept ({8}); slots {9}, matched {10:P1}, valid of matched {11:P1}, " +
                @"masked of unmatched {12:P1}",
                Records, AboveQ, Entrapment, Unmapped, DuplicatePrecursors, RtRows, Ms2Rows, Ms2Candidates,
                string.Join(@", ", Ms2Rejected.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + @" " + p.Value)),
                Slots, Fraction(MatchedSlots, Slots), Fraction(ValidMatchedSlots, MatchedSlots),
                Fraction(MaskedUnmatchedSlots, Slots - MatchedSlots), RtMax);
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
        /// <summary>
        /// Carafe's names for the instrument models it recognizes, by the PSI-MS name Osprey
        /// reports (DIAMeta.get_ms_instrument maps the same models by CV accession).
        /// </summary>
        private static readonly Dictionary<string, string> CARAFE_INSTRUMENTS = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { @"Orbitrap Eclipse", @"Eclipse" },         // MS:1003029
            { @"Orbitrap Exploris 480", @"Exploris" },   // MS:1003028
            { @"Orbitrap Astral", @"Astral" },           // MS:1003378
            { @"Orbitrap Fusion", @"Fusion" },           // MS:1002416
            { @"Orbitrap Fusion Lumos", @"Lumos" },      // MS:1002732
            { @"Q Exactive", @"QE" },                    // MS:1001911
            { @"Q Exactive HF", @"QEHF" },               // MS:1002523
            { @"Exactive Plus", @"QE+" },                // MS:1002526
            { @"Q Exactive Plus", @"QE+" },              // MS:1002634
            { @"Q Exactive HF-X", @"QEHFX" },            // MS:1002877
            { @"TripleTOF 6600", @"SciexTOF" },          // MS:1002533
            // CarafeSharp's: a Stellar reads MS2 out only in its linear ion trap.
            { @"Stellar", PeptdeepConstants.LIT },       // MS:1003409
        };

        /// <summary>The histogram key for a spectrum that carries no value (Osprey's SourceRunMetadata.NONE_KEY).</summary>
        private const string NONE_KEY = @"none";

        private const string OTHER_ACTIVATION = @"other";

        public static OspreyTrainingSet Build(IReadOnlyList<OspreyTrainingExport> exports, OspreyTrainingSetOptions options)
        {
            var stats = new OspreyTrainingSetStats();
            var candidates = new List<Candidate>();
            double rtMax = GetRtMax(exports, options);
            foreach (var export in exports)
            {
                double nce = GetNce(export, options.Nce);
                string instrument = GetTrainingInstrument(export, options.Instrument) ?? OspreyTrainingSetOptions.DEFAULT_INSTRUMENT;
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
                    candidates.Add(new Candidate(record, peptide, nce, instrument));
                }
            }

            // One spectrum per precursor: the run where it scored best, by run q and then score.
            var best = candidates
                .GroupBy(c => c.Key, StringComparer.Ordinal)
                .Select(g => g.OrderBy(c => c.Record.RunPrecursorQ)
                    .ThenByDescending(c => c.Record.Score).ThenBy(c => c.Record.FileName, StringComparer.Ordinal).First())
                .OrderBy(c => c.Record.FileName, StringComparer.Ordinal).ThenBy(c => c.Record.EntryId)
                .ToArray();
            stats.DuplicatePrecursors = candidates.Count - best.Length;

            // One RT row per peptide form, from its best precursor.
            var rt = best
                .GroupBy(c => c.FormKey, StringComparer.Ordinal)
                .Select(g => g.OrderBy(c => c.Record.RunPrecursorQ)
                    .ThenByDescending(c => c.Record.Score).First())
                .OrderBy(c => c.Record.FileName, StringComparer.Ordinal).ThenBy(c => c.Record.EntryId)
                .Select(c => new RtTrainingExample(c.Peptide, c.Record.ApexRt / rtMax))
                .ToArray();
            stats.RtRows = rt.Length;
            stats.RtMax = rtMax;

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

        /// <summary>
        /// Carafe's name for an instrument model as Osprey reports it (its PSI-MS name), or null
        /// for a model Carafe does not recognize.
        /// </summary>
        public static string GetCarafeInstrument(string instrumentModel)
        {
            return instrumentModel != null && CARAFE_INSTRUMENTS.TryGetValue(instrumentModel.Trim(), out string name) ? name : null;
        }

        /// <summary>
        /// The instrument a run's spectra train as: <paramref name="userInstrument"/>
        /// (<c>-ms_instrument</c>) when given; else how Osprey's export says its MS2 spectra were
        /// acquired. Resonance CID (Thermo's CID) in either analyzer is <see cref="PeptdeepConstants.CID"/>;
        /// HCD read out in a linear ion trap is <see cref="PeptdeepConstants.LIT"/>; HCD read out in
        /// an Orbitrap (or another analyzer) is the instrument model's Carafe name, a Tribrid's
        /// being one of the Lumos family. Without the analyzers (an older Osprey, or no data file)
        /// the model decides, a Stellar being LIT. Null for a model Carafe does not name.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// The run's sampled MS2 spectra fall in more than one class (HCD and CID, or HCD read out
        /// in an ion trap and an Orbitrap): trained as one, some would train another class's slot.
        /// </exception>
        public static string GetTrainingInstrument(OspreyTrainingExport export, string userInstrument)
        {
            if (userInstrument != null)
                return userInstrument;
            var activations = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var pair in export.DissociationMethods)
            {
                string activation = ActivationClass(pair.Key);
                if (activation != null)
                    activations[activation] = (activations.TryGetValue(activation, out long n) ? n : 0) + pair.Value;
            }
            if (activations.Count > 1)
                throw MixedClasses(export, @"activations", activations);
            if (activations.ContainsKey(PeptdeepConstants.CID))
                return PeptdeepConstants.CID;
            if (activations.ContainsKey(OTHER_ACTIVATION))
                return GetCarafeInstrument(export.InstrumentModel);

            var analyzers = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var pair in export.Ms2MassAnalyzers.Where(p => p.Key != NONE_KEY))
            {
                string readout = pair.Key.IndexOf(@"ion trap", StringComparison.OrdinalIgnoreCase) >= 0 ? @"ion trap" : @"not an ion trap";
                analyzers[readout] = (analyzers.TryGetValue(readout, out long n) ? n : 0) + pair.Value;
            }
            if (analyzers.Count > 1)
                throw MixedClasses(export, @"HCD read out in", analyzers);
            if (analyzers.ContainsKey(@"ion trap"))
                return PeptdeepConstants.LIT;
            return GetCarafeInstrument(export.InstrumentModel);
        }

        /// <summary>The collision energy Carafe trains a run with: its own, else <paramref name="nce"/>, else 27.</summary>
        public static double GetNce(OspreyTrainingExport export, double? nce)
        {
            return export.DominantCollisionEnergy ?? nce ?? OspreyTrainingSetOptions.DEFAULT_NCE;
        }

        /// <summary>
        /// The class of a dissociation method as pwiz names it: CID (resonance), HCD (beam-type),
        /// other for an electron-based one (ETD, EThcD, ...), or null for none.
        /// </summary>
        private static string ActivationClass(string method)
        {
            var tokens = method.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0 || tokens.All(t => t == NONE_KEY))
                return null;
            if (tokens.Any(t => t.EndsWith(@"ETD", StringComparison.Ordinal) || t.EndsWith(@"ECD", StringComparison.Ordinal)))
                return OTHER_ACTIVATION;
            if (tokens.Contains(@"CID"))
                return PeptdeepConstants.CID;
            if (tokens.Contains(@"HCD"))
                return @"HCD";
            return OTHER_ACTIVATION;
        }

        private static InvalidDataException MixedClasses(OspreyTrainingExport export, string what, IReadOnlyDictionary<string, long> counts)
        {
            return new InvalidDataException(string.Format(
                @"{0}: its MS2 spectra mix {1} {2}. CarafeSharp trains one instrument class per run, and would train some of these " +
                @"spectra as another class; train the run on its own, or name the class for all of it with -ms_instrument.",
                export.Path, what, string.Join(@" and ", counts.Select(p => p.Key + @" (" + p.Value + @")"))));
        }

        /// <summary>A run's <c>rt_max</c>, the larger of <c>-rt_max</c> and its last MS2 RT plus the padding.</summary>
        public static double GetRtMax(OspreyTrainingExport export, OspreyTrainingSetOptions options)
        {
            return Math.Max(options.RtMax, export.RtMax + options.RtMaxPadding);
        }

        /// <summary>The one RT normalizer of a training set: the largest <see cref="GetRtMax(OspreyTrainingExport, OspreyTrainingSetOptions)"/>.</summary>
        public static double GetRtMax(IReadOnlyList<OspreyTrainingExport> exports, OspreyTrainingSetOptions options)
        {
            return exports.Aggregate(options.RtMax, (max, export) => Math.Max(max, GetRtMax(export, options)));
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
            public Candidate(OspreyTrainingRecord record, PeptideForm peptide, double nce, string instrument)
            {
                Record = record;
                Peptide = peptide;
                Nce = nce;
                Instrument = instrument;
                FormKey = peptide.Sequence + @"|" + peptide.ModsText + @"|" + peptide.ModSitesText;
                Key = FormKey + @"|" + record.Charge;
            }

            public OspreyTrainingRecord Record { get; }
            public PeptideForm Peptide { get; }
            public double Nce { get; }
            public string Instrument { get; }
            public string FormKey { get; }
            public string Key { get; }
        }
    }
}
