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

        /// <summary><c>-activation</c>: every run's activation, else each run's own from its export.</summary>
        public string Activation { get; set; }

        /// <summary><c>-analyzer</c>: every run's MS2 analyzer, else each run's own from its export.</summary>
        public string Analyzer { get; set; }

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
            // CarafeSharp's: peptdeep's Lumos family; its ion trap is the acquisition layer's LIT.
            { @"Stellar", @"Stellar" },                  // MS:1003409
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
                string instrument = options.Instrument ?? GetCarafeInstrument(export.InstrumentModel) ?? OspreyTrainingSetOptions.DEFAULT_INSTRUMENT;
                string activation = GetActivation(export, options.Activation);
                string analyzer = GetAnalyzer(export, options.Analyzer);
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
                    candidates.Add(new Candidate(record, peptide, nce, instrument, activation, analyzer));
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
                    options.UseMasking ? masked.Invalid : new double[masked.SlotCount], candidate.Activation, candidate.Analyzer));
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
        /// How a run's precursors were activated (<see cref="AcquisitionVocabulary"/>):
        /// <paramref name="userActivation"/> (<c>-activation</c>) when given, else from Osprey's
        /// export. Beam-type CID (pwiz's HCD) is beam-CID and trap-type CID is reCID, whoever made
        /// the instrument; plain CID is reCID from a Thermo instrument, whose CID is resonance CID
        /// in an ion trap, and beam-CID from any other, whose CID is beam-type. Null when the export
        /// says nothing or names only electron-based methods, which leave the columns zero.
        /// </summary>
        /// <exception cref="InvalidDataException">The run's sampled MS2 spectra mix activations.</exception>
        public static string GetActivation(OspreyTrainingExport export, string userActivation)
        {
            if (userActivation != null)
                return userActivation;
            bool thermo = IsThermo(export);
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var pair in export.DissociationMethods)
            {
                string activation = ActivationOf(pair.Key, thermo);
                if (activation != null)
                    counts[activation] = (counts.TryGetValue(activation, out long n) ? n : 0) + pair.Value;
            }
            if (counts.Count > 1)
                throw MixedClasses(export, @"activations", counts, @"-activation");
            return counts.Keys.FirstOrDefault();
        }

        /// <summary>
        /// The analyzer that read a run's MS2 spectra out (<see cref="AcquisitionVocabulary"/>):
        /// <paramref name="userAnalyzer"/> (<c>-analyzer</c>) when given, else from Osprey's export,
        /// a time-of-flight analyzer (an Astral's too) being ToF, an ion trap LIT and an Orbitrap
        /// Orbitrap. Without the analyzers (an older Osprey, or no data file) the model decides where
        /// it has one MS2 analyzer: a Stellar's LIT, an Astral's ToF. Null otherwise.
        /// </summary>
        /// <exception cref="InvalidDataException">The run's sampled MS2 spectra were read out in more than one kind of analyzer.</exception>
        public static string GetAnalyzer(OspreyTrainingExport export, string userAnalyzer)
        {
            if (userAnalyzer != null)
                return userAnalyzer;
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var pair in export.Ms2MassAnalyzers)
            {
                string analyzer = AnalyzerOf(pair.Key);
                if (analyzer != null)
                    counts[analyzer] = (counts.TryGetValue(analyzer, out long n) ? n : 0) + pair.Value;
            }
            if (counts.Count > 1)
                throw MixedClasses(export, @"analyzers", counts, @"-analyzer");
            if (counts.Count == 1)
                return counts.Keys.First();
            string model = export.InstrumentModel?.Trim();
            if (string.Equals(model, @"Stellar", StringComparison.OrdinalIgnoreCase))
                return AcquisitionVocabulary.LIT;
            if (string.Equals(model, @"Orbitrap Astral", StringComparison.OrdinalIgnoreCase))
                return AcquisitionVocabulary.TOF;
            return null;
        }

        /// <summary>The collision energy Carafe trains a run with: its own, else <paramref name="nce"/>, else 27.</summary>
        public static double GetNce(OspreyTrainingExport export, double? nce)
        {
            return export.DominantCollisionEnergy ?? nce ?? OspreyTrainingSetOptions.DEFAULT_NCE;
        }

        /// <summary>
        /// The activation of a dissociation method as pwiz names it (PSI-MS's short name, else its
        /// name): beam-CID, reCID, or null for none or an electron-based method (ETD, EThcD, EAD).
        /// </summary>
        private static string ActivationOf(string method, bool thermo)
        {
            if (method == NONE_KEY)
                return null;
            var tokens = method.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Any(t => t.EndsWith(@"ETD", StringComparison.Ordinal) || t.EndsWith(@"ECD", StringComparison.Ordinal) ||
                                t.EndsWith(@"EAD", StringComparison.Ordinal)) ||
                method.IndexOf(@"electron", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }
            if (tokens.Contains(@"HCD") || method.IndexOf(@"beam-type", StringComparison.OrdinalIgnoreCase) >= 0)
                return AcquisitionVocabulary.BEAM_CID;
            if (method.IndexOf(@"trap-type", StringComparison.OrdinalIgnoreCase) >= 0)
                return AcquisitionVocabulary.RE_CID;
            if (tokens.Contains(@"CID") || method.IndexOf(@"collision-induced", StringComparison.OrdinalIgnoreCase) >= 0)
                return thermo ? AcquisitionVocabulary.RE_CID : AcquisitionVocabulary.BEAM_CID;
            return null;
        }

        /// <summary>The kind of an analyzer as pwiz names it (its configuration's analyzers, joined), or null for none or another kind.</summary>
        private static string AnalyzerOf(string analyzer)
        {
            if (analyzer == NONE_KEY)
                return null;
            if (analyzer.IndexOf(@"time-of-flight", StringComparison.OrdinalIgnoreCase) >= 0)
                return AcquisitionVocabulary.TOF;
            if (analyzer.IndexOf(@"ion trap", StringComparison.OrdinalIgnoreCase) >= 0)
                return AcquisitionVocabulary.LIT;
            if (analyzer.IndexOf(@"orbitrap", StringComparison.OrdinalIgnoreCase) >= 0)
                return AcquisitionVocabulary.ORBITRAP;
            return null;
        }

        /// <summary>A Thermo instrument, by its vendor or, without one, by its model's name.</summary>
        private static bool IsThermo(OspreyTrainingExport export)
        {
            if (export.Metadata.TryGetValue(@"osprey.instrument_vendor", out string vendor) && !string.IsNullOrEmpty(vendor))
                return vendor.IndexOf(@"Thermo", StringComparison.OrdinalIgnoreCase) >= 0;
            string model = export.InstrumentModel ?? string.Empty;
            return CARAFE_INSTRUMENTS.TryGetValue(model.Trim(), out string name) && name != @"SciexTOF" ||
                   model.IndexOf(@"Orbitrap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   model.IndexOf(@"Exactive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   model.IndexOf(@"LTQ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static InvalidDataException MixedClasses(OspreyTrainingExport export, string what, IReadOnlyDictionary<string, long> counts,
            string option)
        {
            return new InvalidDataException(string.Format(
                @"{0}: its MS2 spectra mix {1} {2}. CarafeSharp trains one activation and one analyzer per run, and would train " +
                @"some of these spectra as another; train the run on its own, or name one for all of it with {3}.",
                export.Path, what, string.Join(@" and ", counts.Select(p => p.Key + @" (" + p.Value + @")")), option));
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
            public Candidate(OspreyTrainingRecord record, PeptideForm peptide, double nce, string instrument, string activation,
                string analyzer)
            {
                Record = record;
                Peptide = peptide;
                Nce = nce;
                Instrument = instrument;
                Activation = activation;
                Analyzer = analyzer;
                FormKey = peptide.Sequence + @"|" + peptide.ModsText + @"|" + peptide.ModSitesText;
                Key = FormKey + @"|" + record.Charge;
            }

            public OspreyTrainingRecord Record { get; }
            public PeptideForm Peptide { get; }
            public double Nce { get; }
            public string Instrument { get; }
            public string Activation { get; }
            public string Analyzer { get; }
            public string FormKey { get; }
            public string Key { get; }
        }
    }
}
