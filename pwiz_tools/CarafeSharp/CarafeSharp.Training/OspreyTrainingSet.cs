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
        /// Carafe's <c>-nce</c>: the collision energy of a run whose export records none (a Thermo
        /// run's own NCE is used when it has one, as Carafe does), and of a run whose energy is in
        /// eV, instead of its calibration; null for Carafe's default, <see cref="DEFAULT_NCE"/>.
        /// </summary>
        public double? Nce { get; set; }

        /// <summary>
        /// Calibrates the NCE of a run whose collision energy is in eV (<see cref="RunCollisionEnergy"/>)
        /// from its MS2 rows, as <see cref="NceCalibration.Calibrate"/> does with the start MS2
        /// model; null trains such a run at its energy, as Carafe does.
        /// </summary>
        public Func<IReadOnlyList<Ms2TrainingExample>, NceCalibration> CalibrateNce { get; set; }

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

        /// <summary>
        /// <c>-rt_align kde</c>: the reference model's hydrophobic index of peptide forms (the pretrained Chronologer's;
        /// NaN for a form it cannot encode). Set, each run's minutes are mapped onto it before the RT rows are chosen
        /// (<see cref="OspreyTrainingSet.Alignment"/>); null divides every run by one rt_max, as Carafe does.
        /// </summary>
        public Func<IReadOnlyList<PeptideForm>, double[]> PredictHi { get; set; }

        /// <summary>What <see cref="PredictHi"/> predicts with, recorded in the alignment.</summary>
        public string AlignmentReference { get; set; }

        /// <summary><c>-rt_select</c>: with aligned runs, which observation of a form found in several becomes its RT row.</summary>
        public RtSelectionType RtSelection { get; set; }

        /// <summary>
        /// The fewest peptide forms a run is aligned on (Osprey's fewest for a line); a run with fewer has its RT rows left
        /// out (<see cref="OspreyTrainingSet.UnalignedRuns"/>), and when no run can be aligned the set is not
        /// (<see cref="OspreyTrainingSet.Unaligned"/>).
        /// </summary>
        public int MinAlignmentPoints { get; set; } = RtMapFit.MIN_LINEAR_FIT_POINTS;

        /// <summary>The fewest peptide forms a run's map is Chronologer's KDE ridge on; on fewer, a LOESS or a line (<see cref="RtMapFit"/>).</summary>
        public int KdeMinPoints { get; set; } = RtMapFit.DEFAULT_KDE_MIN_POINTS;

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
            var energies = new Dictionary<string, RunCollisionEnergy>(StringComparer.Ordinal);
            double rtMax = GetRtMax(exports, options);
            var runNames = GetRunNames(exports);
            foreach (var export in exports)
            {
                var energy = GetCollisionEnergy(export, options);
                energies[export.Path] = energy;
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
                    candidates.Add(new Candidate(record, peptide, energy, instrument, activation, analyzer, runNames[export.Path]));
                }
            }

            // Aligned, every observation's minutes become its run's hydrophobic index first, so the rows chosen below
            // are on one scale whichever run they come from. A run that cannot be mapped gives no RT rows.
            string unaligned = null;
            var unalignedRuns = new List<string>();
            var alignment = options.PredictHi != null
                ? AlignRuns(candidates, RunMinutes(exports, runNames), rtMax, options, unalignedRuns, out unaligned)
                : null;
            var rtCandidates = candidates;
            if (alignment != null)
            {
                var aligned = new HashSet<string>(alignment.Runs.Select(r => r.Run), StringComparer.Ordinal);
                rtCandidates = candidates.Where(c => aligned.Contains(c.Run)).ToList();
                foreach (var run in rtCandidates.GroupBy(c => c.Run, StringComparer.Ordinal))
                {
                    var map = alignment.GetRun(run.Key);
                    foreach (var candidate in run)
                        candidate.Hi = map.Map(candidate.Record.ApexRt);
                }
            }

            // One spectrum per precursor: the run where it scored best, by run q and then score.
            var best = BestPerPrecursor(candidates);
            stats.DuplicatePrecursors = candidates.Count - best.Length;

            // One RT row per peptide form, from its best precursor; aligned, at the median map's minutes of its
            // hydrophobic index (or of the median over the runs of each run's best, with -rt_select median).
            var medianHi = alignment != null && options.RtSelection == RtSelectionType.median ? MedianHiByForm(rtCandidates) : null;
            var rt = (ReferenceEquals(rtCandidates, candidates) ? best : BestPerPrecursor(rtCandidates))
                .GroupBy(c => c.FormKey, StringComparer.Ordinal)
                .Select(Best)
                .OrderBy(c => c.Record.FileName, StringComparer.Ordinal).ThenBy(c => c.Record.EntryId)
                .Select(c => alignment == null
                    ? new RtTrainingExample(c.Peptide, c.Record.ApexRt / rtMax)
                    : AlignedRow(c, alignment, rtMax, medianHi != null ? medianHi[c.FormKey] : c.Hi))
                .ToArray();
            stats.RtRows = rt.Length;
            stats.RtMax = rtMax;

            var policy = new OspreyMaskingPolicy(options.Masking);
            var ms2 = new List<Ms2TrainingExample>(best.Length);
            var rowEnergies = new List<RunCollisionEnergy>(best.Length);
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
                    candidate.Energy.Nce, candidate.Instrument, masked.Intensities,
                    options.UseMasking ? masked.Invalid : new double[masked.SlotCount], candidate.Activation, candidate.Analyzer));
                rowEnergies.Add(candidate.Energy);
            }
            stats.Ms2Rows = ms2.Count;

            // A run whose energy is in eV: its rows at the NCE its own spectra calibrate, else
            // Carafe's default when none of its spectra was kept.
            foreach (var energy in energies.Values.Where(e => e.Source == RunCollisionEnergy.CALIBRATED))
            {
                int[] rows = Enumerable.Range(0, ms2.Count).Where(i => ReferenceEquals(rowEnergies[i], energy)).ToArray();
                if (rows.Length == 0)
                {
                    energy.Source = RunCollisionEnergy.DEFAULT;
                    continue;
                }
                energy.Calibration = options.CalibrateNce(rows.Select(i => ms2[i]).ToArray());
                energy.Nce = energy.Calibration.Nce;
                foreach (int i in rows)
                    ms2[i] = ms2[i].WithNce(energy.Nce);
            }
            return new OspreyTrainingSet(rt, ms2, stats, energies, alignment, unaligned, unalignedRuns);
        }

        /// <summary>
        /// Each run's name, by its export's path: the export's stem, or, where two exports from different folders share a
        /// stem, the folder and the stem, so that two acquisitions of the same file name are two runs.
        /// </summary>
        public static Dictionary<string, string> GetRunNames(IReadOnlyList<OspreyTrainingExport> exports)
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in exports.GroupBy(e => TrainingExportLocator.RunStem(e.Path), StringComparer.OrdinalIgnoreCase))
            {
                var paths = group.Select(e => e.Path).Distinct(StringComparer.Ordinal).ToArray();
                foreach (string path in paths)
                {
                    string stem = TrainingExportLocator.RunStem(path);
                    names[path] = paths.Length == 1 ? stem : Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path))) + @"/" + stem;
                }
            }
            // Folders of the same name too: then the full path.
            foreach (var clash in names.GroupBy(n => n.Value, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToArray())
            {
                foreach (var entry in clash)
                    names[entry.Key] = Path.GetFullPath(entry.Key);
            }
            return names;
        }

        /// <summary>
        /// Each run's map from its minutes to the reference hydrophobic index, fitted (<see cref="RtMapFit"/>) to one
        /// point per peptide form the run identified (its mean apex RT, as Chronologer's alignment averages a peptide's
        /// observations) against the form's predicted HI, on the forms the reference model can encode: Chronologer's KDE
        /// ridge on enough forms, else a LOESS or a line. A run that cannot be mapped is left out, saying why in
        /// <paramref name="unalignedRuns"/>; null, saying why in <paramref name="unaligned"/>, when no run can be.
        /// </summary>
        private static RtAlignment AlignRuns(IReadOnlyList<Candidate> candidates, IReadOnlyDictionary<string, double> runMinutes,
            double rtMax, OspreyTrainingSetOptions options, List<string> unalignedRuns, out string unaligned)
        {
            unaligned = null;
            // Each form's HI once, whichever runs found it.
            var forms = candidates.GroupBy(c => c.FormKey, StringComparer.Ordinal).Select(g => g.First()).ToArray();
            double[] predicted = options.PredictHi(forms.Select(c => c.Peptide).ToArray());
            var hiOf = new Dictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < forms.Length; i++)
                hiOf[forms[i].FormKey] = predicted[i];

            var runs = new List<(string Run, MonotoneMap MinutesToHi, string Fit)>();
            foreach (var run in candidates.GroupBy(c => c.Run, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var points = run.GroupBy(c => c.FormKey, StringComparer.Ordinal)
                    .Select(g => (Rt: g.Average(c => c.Record.ApexRt), Hi: hiOf[g.Key]))
                    .Where(p => !double.IsNaN(p.Hi))
                    .ToArray();
                if (points.Length < options.MinAlignmentPoints)
                {
                    unalignedRuns.Add(string.Format(@"run {0} has {1} peptide forms to align on, fewer than {2}",
                        run.Key, points.Length, options.MinAlignmentPoints));
                    continue;
                }
                try
                {
                    var (map, fit) = RtMapFit.Fit(points.Select(p => p.Rt).ToArray(), points.Select(p => p.Hi).ToArray(),
                        runMinutes[run.Key], options.KdeMinPoints);
                    runs.Add((run.Key, map, fit));
                }
                catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
                {
                    unalignedRuns.Add(string.Format(@"run {0}: {1}", run.Key, e.Message));
                }
            }
            if (runs.Count == 0)
            {
                unaligned = unalignedRuns.Count == 1 ? unalignedRuns[0] : @"no run can be mapped: " + string.Join(@"; ", unalignedRuns);
                unalignedRuns.Clear();
                return null;
            }
            try
            {
                return new RtAlignment(runs, options.AlignmentReference ?? @"(unnamed)", rtMax);
            }
            catch (InvalidOperationException e)
            {
                unaligned = e.Message;
                unalignedRuns.Clear();
                return null;
            }
        }

        /// <summary>Each run's length, its last MS2 retention time, by its name (<see cref="GetRunNames"/>).</summary>
        private static Dictionary<string, double> RunMinutes(IReadOnlyList<OspreyTrainingExport> exports, IReadOnlyDictionary<string, string> runNames)
        {
            return exports.GroupBy(e => runNames[e.Path], StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Max(e => e.RtMax), StringComparer.Ordinal);
        }

        /// <summary>One candidate per precursor, the best (<see cref="Best"/>), in the order of their files and entries.</summary>
        private static Candidate[] BestPerPrecursor(IEnumerable<Candidate> candidates)
        {
            return candidates
                .GroupBy(c => c.Key, StringComparer.Ordinal)
                .Select(Best)
                .OrderBy(c => c.Record.FileName, StringComparer.Ordinal).ThenBy(c => c.Record.EntryId)
                .ToArray();
        }

        /// <summary>The best observation: the lowest run q, then the highest score, then the first file.</summary>
        private static Candidate Best(IEnumerable<Candidate> observations)
        {
            return observations.OrderBy(c => c.Record.RunPrecursorQ).ThenByDescending(c => c.Record.Score)
                .ThenBy(c => c.Record.FileName, StringComparer.Ordinal).First();
        }

        private static RtTrainingExample AlignedRow(Candidate candidate, RtAlignment alignment, double rtMax, double hi)
        {
            return new RtTrainingExample(candidate.Peptide, alignment.ToMinutes(hi) / rtMax, hi);
        }

        /// <summary>Each form's median over the runs of each run's best-scoring observation, in hydrophobic index.</summary>
        private static Dictionary<string, double> MedianHiByForm(IReadOnlyList<Candidate> candidates)
        {
            return candidates.GroupBy(c => c.FormKey, StringComparer.Ordinal).ToDictionary(form => form.Key,
                form => Statistics.Median(form.GroupBy(c => c.Run, StringComparer.Ordinal).Select(run => Best(run).Hi).ToArray()),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// A run's collision energy and the NCE it trains with:
        /// <list type="bullet">
        /// <item>a Thermo run's own NCE, else <c>-nce</c>, else Carafe's default, as Carafe takes it;</item>
        /// <item>for a run whose energy is in eV (any other vendor), <c>-nce</c>, else the NCE
        /// <see cref="OspreyTrainingSetOptions.CalibrateNce"/> finds on its spectra (set by
        /// <see cref="Build"/>), else its energy as Carafe takes it.</item>
        /// </list>
        /// A run that names neither vendor nor model counts as Thermo, as Carafe reads it.
        /// </summary>
        public static RunCollisionEnergy GetCollisionEnergy(OspreyTrainingExport export, OspreyTrainingSetOptions options)
        {
            double? energy = export.DominantCollisionEnergy;
            if (energy.HasValue && ReportsElectronvolts(export))
            {
                if (options.Nce.HasValue)
                    return new RunCollisionEnergy(options.Nce.Value, RunCollisionEnergy.COMMAND_LINE, energy, RunCollisionEnergy.EV_UNIT);
                return options.CalibrateNce != null
                    ? new RunCollisionEnergy(OspreyTrainingSetOptions.DEFAULT_NCE, RunCollisionEnergy.CALIBRATED, energy, RunCollisionEnergy.EV_UNIT)
                    : new RunCollisionEnergy(energy.Value, RunCollisionEnergy.FROM_FILE, energy, RunCollisionEnergy.EV_UNIT);
            }
            if (energy.HasValue)
                return new RunCollisionEnergy(energy.Value, RunCollisionEnergy.FROM_FILE, energy, RunCollisionEnergy.NCE_UNIT);
            return options.Nce.HasValue
                ? new RunCollisionEnergy(options.Nce.Value, RunCollisionEnergy.COMMAND_LINE, null, null)
                : new RunCollisionEnergy(OspreyTrainingSetOptions.DEFAULT_NCE, RunCollisionEnergy.DEFAULT, null, null);
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

        /// <summary>
        /// The run's collision energies are in eV: its vendor is not Thermo, or, without a vendor,
        /// its model is not a Thermo one. pwiz reports a Thermo file's NCE as the energy.
        /// </summary>
        private static bool ReportsElectronvolts(OspreyTrainingExport export)
        {
            if (export.Metadata.TryGetValue(@"osprey.instrument_vendor", out string vendor) && !string.IsNullOrEmpty(vendor))
                return !IsThermo(export);
            return !string.IsNullOrWhiteSpace(export.InstrumentModel) && !IsThermo(export);
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

        private OspreyTrainingSet(IReadOnlyList<RtTrainingExample> rt, IReadOnlyList<Ms2TrainingExample> ms2, OspreyTrainingSetStats stats,
            IReadOnlyDictionary<string, RunCollisionEnergy> energies, RtAlignment alignment, string unaligned, IReadOnlyList<string> unalignedRuns)
        {
            Rt = rt;
            Ms2 = ms2;
            Stats = stats;
            _energies = energies;
            Alignment = alignment;
            Unaligned = unaligned;
            UnalignedRuns = unalignedRuns;
        }

        /// <summary>
        /// The runs' maps onto the hydrophobic index when <see cref="OspreyTrainingSetOptions.PredictHi"/> was set
        /// (<c>-rt_align kde</c>): every RT row's <see cref="RtTrainingExample.Hi"/> came from its run's map, and its
        /// normalized RT from the median map back to minutes; null otherwise.
        /// </summary>
        public RtAlignment Alignment { get; }

        /// <summary>
        /// Why the runs were not aligned although <see cref="OspreyTrainingSetOptions.PredictHi"/> was set: no run could
        /// be mapped (<see cref="RtMapFit"/>), so the RT rows are minutes over one rt_max, as without it; else null.
        /// </summary>
        public string Unaligned { get; }

        /// <summary>
        /// The runs left out of an alignment of the others, each with why it could not be mapped: their spectra train MS2,
        /// but they give no RT rows.
        /// </summary>
        public IReadOnlyList<string> UnalignedRuns { get; }

        private readonly IReadOnlyDictionary<string, RunCollisionEnergy> _energies;

        public IReadOnlyList<RtTrainingExample> Rt { get; }

        public IReadOnlyList<Ms2TrainingExample> Ms2 { get; }

        public OspreyTrainingSetStats Stats { get; }

        /// <summary>The collision energy, and the NCE trained with, of the run of an export <see cref="Build"/> read.</summary>
        public RunCollisionEnergy GetCollisionEnergy(OspreyTrainingExport export)
        {
            return _energies[export.Path];
        }

        private sealed class Candidate
        {
            public Candidate(OspreyTrainingRecord record, PeptideForm peptide, RunCollisionEnergy energy, string instrument, string activation,
                string analyzer, string run)
            {
                Run = run;
                Record = record;
                Peptide = peptide;
                Energy = energy;
                Instrument = instrument;
                Activation = activation;
                Analyzer = analyzer;
                FormKey = peptide.Sequence + @"|" + peptide.ModsText + @"|" + peptide.ModSitesText;
                Key = FormKey + @"|" + record.Charge;
            }

            public OspreyTrainingRecord Record { get; }
            public PeptideForm Peptide { get; }
            public RunCollisionEnergy Energy { get; }
            public string Instrument { get; }
            public string Activation { get; }
            public string Analyzer { get; }
            public string FormKey { get; }
            public string Key { get; }

            /// <summary>The run the record came from, by its export's stem.</summary>
            public string Run { get; }

            /// <summary>The hydrophobic index of the record's apex RT by its run's alignment map, or NaN unaligned.</summary>
            public double Hi { get; set; } = double.NaN;
        }
    }
}
