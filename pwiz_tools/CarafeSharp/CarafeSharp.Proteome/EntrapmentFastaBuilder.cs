/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentFastaGear.java
 *   (run, assignEntrapment, writeFasta, writeManifest, buildProteinLabel)
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// Builds Carafe's peptide-level FASTA and FDRBench pairing manifest from a protein FASTA,
    /// byte for byte as Carafe's <c>-build_entrapment_fasta</c> writes them. Each unique
    /// digested peptide becomes a target with, optionally, an entrapment (p_target) peptide, a
    /// decoy and a p_decoy, grouped under one <c>peptide_pair_index</c>. The FASTA gets one
    /// entry per sequence, headed <c>db|accession_pepNNNNN|entry</c> (joined with ';' across
    /// source proteins), with entrapment accessions and entry names suffixed <c>_p_target</c>
    /// and decoy headers prefixed <c>decoy_</c>. Output order is by target sequence, ordinal.
    /// </summary>
    public sealed class EntrapmentFastaBuilder
    {
        public const string MANIFEST_HEADER = "sequence\tdecoy\tproteins\tpeptide_type\tpeptide_pair_index";
        public const string PEPTIDE_TYPE_TARGET = @"target";
        public const string PEPTIDE_TYPE_P_TARGET = @"p_target";
        public const string PEPTIDE_TYPE_DECOY = @"decoy";
        public const string PEPTIDE_TYPE_P_DECOY = @"p_decoy";

        private static readonly UTF8Encoding UTF8_NO_BOM = new UTF8Encoding(false);

        private readonly EntrapmentFastaSettings _settings;
        private readonly TextWriter _log;

        public EntrapmentFastaBuilder(EntrapmentFastaSettings settings, TextWriter log = null)
        {
            _settings = settings;
            _log = log ?? TextWriter.Null;
        }

        /// <summary>Digests, generates, drops, sorts and writes, in Carafe's order.</summary>
        public EntrapmentFastaResult Run()
        {
            ValidateSettings();
            var result = new EntrapmentFastaResult();
            var digester = new Digester(_settings.Digest);
            Log(@"Reading FASTA: " + _settings.InputFasta);
            Log(@"Use enzyme: " + digester.Enzyme.Name);

            var quartets = DigestTargets(digester, result);
            // Built once and reused for every candidate. I and L are isobaric, so a generated
            // sequence whose I-to-L form matches a real target is effectively that target.
            var targetSet = new HashSet<string>(quartets.Select(q => q.Target));
            var targetSetIl = EntrapmentSequences.IlNormalizedSet(targetSet);
            if (_settings.AddEntrapment)
                AssignEntrapment(quartets, targetSet, targetSetIl, digester, result);
            AssignDecoys(quartets, targetSet, targetSetIl);
            result.QuartetsBuilt = quartets.Count;

            var kept = DropUnusable(quartets, targetSet, result);
            // Stable ordering: by target so pair indices are reproducible, and each quartet's
            // sources by accession then entry name (stable, as Java's List.sort is) so the
            // header's first accession is deterministic.
            kept.Sort((a, b) => string.CompareOrdinal(a.Target, b.Target));
            foreach (var quartet in kept)
            {
                var sorted = quartet.Sources
                    .OrderBy(s => s.Accession, StringComparer.Ordinal)
                    .ThenBy(s => s.EntryName, StringComparer.Ordinal)
                    .ToList();
                quartet.Sources.Clear();
                quartet.Sources.AddRange(sorted);
            }

            WriteFasta(kept, result);
            if (_settings.Manifest != null)
                WriteManifest(kept);
            return result;
        }

        /// <summary>
        /// The <c>db|accession|entry</c> label of one source protein for one peptide kind, with
        /// the optional per-protein peptide counter.
        /// </summary>
        public static string BuildProteinLabel(ProteinRecord record, bool pTarget, bool decoy,
            string entrapmentSuffix, string decoyPrefix, int? peptideCounter, string peptideSuffixFormat)
        {
            string accession = pTarget ? record.Accession + entrapmentSuffix : record.Accession;
            if (peptideCounter.HasValue)
                accession += string.Format(CultureInfo.InvariantCulture, peptideSuffixFormat, peptideCounter.Value);
            string entry = pTarget ? record.EntryName + entrapmentSuffix : record.EntryName;
            string label = record.Db + @"|" + accession + @"|" + entry;
            return decoy ? decoyPrefix + label : label;
        }

        private void ValidateSettings()
        {
            if (_settings.MinMz >= _settings.MaxMz)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    @"invalid m/z range: {0}-{1}", _settings.MinMz, _settings.MaxMz));
            }
            foreach (int z in _settings.Charges)
            {
                if (z < 1 || z > 10)
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, @"invalid charge state (allowed 1..10): {0}", z));
            }
        }

        /// <summary>
        /// Step 1: every unique peptide of every protein, with its source proteins in FASTA
        /// order. A peptide is filtered (unknown residue, then m/z window) only when first seen;
        /// once kept, later proteins only add sources.
        /// </summary>
        private List<EntrapmentQuartet> DigestTargets(Digester digester, EntrapmentFastaResult result)
        {
            var mzFilter = _settings.ApplyMzFilter
                ? new ModifiedPrecursorMzFilter(_settings.Modifications, _settings.Charges,
                    _settings.MinMz, _settings.MaxMz, digester.ProteinNTermPeptides)
                : null;
            var byTarget = new Dictionary<string, EntrapmentQuartet>();
            var quartets = new List<EntrapmentQuartet>();
            foreach (var entry in FastaReader.ReadFile(_settings.InputFasta, Log))
            {
                var record = ProteinRecord.Parse(entry.Header, entry.Sequence);
                if (record == null)
                    continue;
                result.Proteins++;
                foreach (string peptide in digester.Digest(record.Sequence))
                {
                    if (byTarget.TryGetValue(peptide, out var existing))
                    {
                        existing.Sources.Add(record);
                        continue;
                    }
                    if (!ResidueMasses.PeptideNeutralMass(peptide).HasValue)
                    {
                        result.DroppedUnknownAa++;
                        continue;
                    }
                    if (mzFilter != null && !mzFilter.Fits(peptide))
                    {
                        result.DroppedOutOfMz++;
                        continue;
                    }
                    var quartet = new EntrapmentQuartet(peptide);
                    quartet.Sources.Add(record);
                    byTarget.Add(peptide, quartet);
                    quartets.Add(quartet);
                }
            }
            result.UniqueTargets = quartets.Count;
            Log(string.Format(CultureInfo.InvariantCulture,
                @"Digested {0} proteins: {1} unique target peptides retained, {2} dropped (unknown AA), {3} dropped (out of m/z range)",
                result.Proteins, result.UniqueTargets, result.DroppedUnknownAa, result.DroppedOutOfMz));
            return quartets;
        }

        /// <summary>
        /// Step 2a: sets PTarget on every quartet selected to carry entrapment, by shuffling the
        /// target or by drawing a mass-matched foreign peptide. Quartets are processed in target
        /// order so a foreign draw, which consumes the pool, does not depend on FASTA order.
        /// </summary>
        private void AssignEntrapment(List<EntrapmentQuartet> quartets, ISet<string> targetSet,
            ISet<string> targetSetIl, Digester digester, EntrapmentFastaResult result)
        {
            double ratio = _settings.EntrapmentRatio;
            if (ratio > 1.0)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    @"entrapment ratio cannot exceed 1.0 (one entrapment peptide per target): {0}", ratio));
            }
            if (ratio < EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    @"entrapment ratio {0:F4} is below the tested minimum of {1:F2}", ratio,
                    EntrapmentFastaSettings.MIN_ENTRAPMENT_RATIO));
            }
            var selected = SelectEntrapmentTargets(quartets);
            result.EntrapmentNotSelected = quartets.Count - selected.Count;
            foreach (var quartet in selected)
                quartet.EntrapmentSelected = true;

            if (_settings.EntrapmentSourceFasta == null)
            {
                foreach (var quartet in selected)
                {
                    quartet.PTarget = EntrapmentSequences.GenerateShuffledEntrapment(quartet.Target,
                        _settings.EntrapmentSeed, targetSet, targetSetIl, _settings.SimilarityGate);
                    if (quartet.PTarget == null)
                        result.DroppedNoEntrapment++;
                }
                return;
            }
            AssignForeignEntrapment(selected, targetSet, targetSetIl, digester, result);
        }

        /// <summary>
        /// Every quartet in target order when the ratio selects them all, else a seeded
        /// Fisher-Yates draw of round(ratio * n), put back in target order.
        /// </summary>
        private List<EntrapmentQuartet> SelectEntrapmentTargets(List<EntrapmentQuartet> quartets)
        {
            var ordered = new List<EntrapmentQuartet>(quartets);
            ordered.Sort((a, b) => string.CompareOrdinal(a.Target, b.Target));
            int nSelected = unchecked((int)JavaText.Round(_settings.EntrapmentRatio * ordered.Count));
            if (nSelected >= ordered.Count)
                return ordered;

            var shuffled = new List<EntrapmentQuartet>(ordered);
            var rng = new JavaRandom(_settings.EntrapmentSeed);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            var selected = shuffled.GetRange(0, nSelected);
            selected.Sort((a, b) => string.CompareOrdinal(a.Target, b.Target));
            return selected;
        }

        private void AssignForeignEntrapment(List<EntrapmentQuartet> selected, ISet<string> targetSet,
            ISet<string> targetSetIl, Digester digester, EntrapmentFastaResult result)
        {
            Log(@"Drawing entrapment from foreign proteome: " + _settings.EntrapmentSourceFasta);
            var pool = ForeignEntrapmentSource.Build(_settings.EntrapmentSourceFasta, digester,
                targetSet, targetSetIl, _settings.ApplyMzFilter, _settings.Charges,
                _settings.MinMz, _settings.MaxMz, _log);
            result.EntrapmentIlCollisionsDropped = pool.IlCollisionsDropped;
            if (pool.Available < selected.Count)
            {
                // Not fatal, but it silently changes what was asked for, so say so first.
                Log(string.Format(CultureInfo.InvariantCulture,
                    @"WARNING: Foreign entrapment pool has {0} peptides for {1} selected targets; the achieved ratio will be below the requested {2:F3}",
                    pool.Available, selected.Count, _settings.EntrapmentRatio));
            }
            // Assignment order is deliberately uncorrelated with mass (it is sequence order):
            // sweeping in mass order lets each target consume the supply just above it.
            var drawOrder = selected.Where(q => ResidueMasses.PeptideNeutralMass(q.Target).HasValue).ToList();
            result.DroppedNoEntrapment += selected.Count - drawOrder.Count;

            var massDeltas = new List<double>(drawOrder.Count);
            foreach (var quartet in drawOrder)
            {
                double mass = ResidueMasses.PeptideNeutralMass(quartet.Target) ?? double.NaN;
                string foreign = pool.Assign(quartet.Target, mass);
                if (foreign == null)
                {
                    result.DroppedNoEntrapment++;
                    continue;
                }
                quartet.PTarget = foreign;
                result.EntrapmentFromForeign++;
                double? foreignMass = ResidueMasses.PeptideNeutralMass(foreign);
                if (foreignMass.HasValue)
                    massDeltas.Add(Math.Abs(mass - foreignMass.Value));
            }
            Log(string.Format(CultureInfo.InvariantCulture,
                @"Foreign entrapment assigned to {0} of {1} selected targets ({2} unmatched); {3} peptides left unused in the pool",
                result.EntrapmentFromForeign, selected.Count, result.DroppedNoEntrapment, pool.Available));
            if (massDeltas.Count > 0)
                ReportMassMatch(massDeltas, result);
        }

        /// <summary>
        /// Logs how closely foreign entrapment matched target mass. Co-location is what makes the
        /// swap a controlled comparison, so it is reported rather than left to be discovered.
        /// </summary>
        private void ReportMassMatch(List<double> massDeltas, EntrapmentFastaResult result)
        {
            int n = massDeltas.Count;
            var sorted = massDeltas.ToArray();
            Array.Sort(sorted);
            // The 6 Da neutral window as m/z at charge 2: within +/-3 m/z of the target.
            int inWindow = massDeltas.Count(d => d / 2.0 < ForeignEntrapmentSource.CO_LOCATION_WINDOW_DA / 2.0);
            result.EntrapmentMedianAbsMassDelta = sorted[n / 2];
            result.EntrapmentInIsolationWindowFraction = (double)inWindow / n;
            Log(string.Format(CultureInfo.InvariantCulture,
                @"Foreign entrapment mass match: median |dm| {0:F4} Da, 90th {1:F4}, 99th {2:F4}, max {3:F2}; {4:F2}% within +/-3 m/z of their target at charge 2",
                sorted[n / 2], sorted[(int)(0.90 * (n - 1))], sorted[(int)(0.99 * (n - 1))], sorted[n - 1],
                100.0 * result.EntrapmentInIsolationWindowFraction));
        }

        /// <summary>
        /// Step 2b: decoys the Osprey way, unique against every real target plus every
        /// entrapment peptide (entrapment is target-side in Osprey), but not against other
        /// decoys, as in Osprey.
        /// </summary>
        private void AssignDecoys(List<EntrapmentQuartet> quartets, HashSet<string> targetSet, ISet<string> targetSetIl)
        {
            var targetSideSet = new HashSet<string>(targetSet);
            if (_settings.AddEntrapment)
            {
                foreach (var quartet in quartets)
                {
                    if (quartet.PTarget != null)
                        targetSideSet.Add(quartet.PTarget);
                }
            }
            var targetSideSetIl = _settings.AddEntrapment
                ? EntrapmentSequences.IlNormalizedSet(targetSideSet)
                : targetSetIl;
            if (!_settings.AddDecoys)
                return;
            bool gate = _settings.SimilarityGate;
            foreach (var quartet in quartets)
            {
                quartet.Decoy = EntrapmentSequences.GenerateReverseDecoy(quartet.Target, targetSideSet, targetSideSetIl, gate);
                if (_settings.AddEntrapment && quartet.PTarget != null)
                    quartet.PDecoy = EntrapmentSequences.GenerateReverseDecoy(quartet.PTarget, targetSideSet, targetSideSetIl, gate);
            }
        }

        /// <summary>
        /// Step 3: drops a quartet selected for entrapment that got none, one whose entrapment
        /// is a real target (both generators already prevent it), and one missing a requested
        /// decoy. A target deliberately left without entrapment is kept.
        /// </summary>
        private List<EntrapmentQuartet> DropUnusable(List<EntrapmentQuartet> quartets, ISet<string> targetSet,
            EntrapmentFastaResult result)
        {
            bool addDecoys = _settings.AddDecoys;
            bool addEntrapment = _settings.AddEntrapment;
            var kept = new List<EntrapmentQuartet>(quartets.Count);
            foreach (var q in quartets)
            {
                bool drop = (q.EntrapmentSelected && q.PTarget == null) ||
                            (q.PTarget != null && targetSet.Contains(q.PTarget)) ||
                            (addDecoys && q.Decoy == null) ||
                            (addDecoys && addEntrapment && q.PTarget != null && q.PDecoy == null);
                if (drop && addDecoys && q.Decoy == null)
                    result.DroppedNoDecoy++;
                if (!drop)
                    kept.Add(q);
            }
            result.QuartetsDropped = quartets.Count - kept.Count;
            result.KeptQuartets = kept.Count;
            string composition = addEntrapment
                ? @"target+p_target+decoy+p_decoy"
                : (addDecoys ? @"target+decoy" : @"target only");
            Log(string.Format(CultureInfo.InvariantCulture,
                @"Collision-drop pass: {0} peptide groups dropped, {1} retained (each group = {2})",
                result.QuartetsDropped, result.KeptQuartets, composition));
            if (addEntrapment || addDecoys)
            {
                Log(string.Format(CultureInfo.InvariantCulture,
                    @"Similarity gate (max fragment overlap {0:F2}): {1} dropped with no acceptable entrapment, {2} with no acceptable decoy",
                    DecoySimilarityGate.MAX_FRAGMENT_OVERLAP, result.DroppedNoEntrapment, result.DroppedNoDecoy));
            }
            return kept;
        }

        /// <summary>
        /// Step 4: one entry per peptide. The per-protein counter advances in lockstep across
        /// every source of a shared peptide, so a joined header carries coherent suffixes.
        /// </summary>
        private void WriteFasta(List<EntrapmentQuartet> kept, EntrapmentFastaResult result)
        {
            Log(@"Writing FASTA: " + _settings.OutputFasta);
            var peptideCounters = new Dictionary<string, int>();
            using (var writer = CreateWriter(_settings.OutputFasta))
            {
                foreach (var quartet in kept)
                {
                    var counters = new int?[quartet.Sources.Count];
                    if (_settings.UniqueAccessions)
                    {
                        for (int i = 0; i < quartet.Sources.Count; i++)
                        {
                            string accession = quartet.Sources[i].Accession;
                            peptideCounters.TryGetValue(accession, out int count);
                            peptideCounters[accession] = ++count;
                            counters[i] = count;
                        }
                    }
                    if (quartet.Sources.Count > 1)
                        result.SharedEntries++;

                    WriteEntry(writer, JoinedLabel(quartet, counters, false, false), quartet.Target);
                    result.TargetEntries++;
                    if (quartet.PTarget != null)
                    {
                        WriteEntry(writer, JoinedLabel(quartet, counters, true, false), quartet.PTarget);
                        result.PTargetEntries++;
                    }
                    if (quartet.Decoy != null)
                    {
                        WriteEntry(writer, JoinedLabel(quartet, counters, false, true), quartet.Decoy);
                        result.DecoyEntries++;
                    }
                    if (quartet.PDecoy != null)
                    {
                        WriteEntry(writer, JoinedLabel(quartet, counters, true, true), quartet.PDecoy);
                        result.PDecoyEntries++;
                    }
                }
            }
            Log(string.Format(CultureInfo.InvariantCulture,
                @"Wrote FASTA: {0} target, {1} p_target, {2} decoy, {3} p_decoy entries ({4} shared across multiple source proteins)",
                result.TargetEntries, result.PTargetEntries, result.DecoyEntries, result.PDecoyEntries, result.SharedEntries));
        }

        /// <summary>
        /// Step 5: the manifest, refused (or, when allowed, only warned about) if it fails the
        /// pairing checks. Its proteins column carries no peptide counters.
        /// </summary>
        private void WriteManifest(List<EntrapmentQuartet> kept)
        {
            EntrapmentPairingValidator.Enforce(EntrapmentPairingValidator.ValidateQuartets(kept),
                _settings.FailOnPairingViolation, _log);
            Log(@"Writing manifest: " + _settings.Manifest);
            using (var writer = CreateWriter(_settings.Manifest))
            {
                writer.Write(MANIFEST_HEADER + "\n");
                int pairIndex = 0;
                foreach (var quartet in kept)
                {
                    WriteManifestRow(writer, quartet.Target, false, JoinedLabel(quartet, null, false, false), PEPTIDE_TYPE_TARGET, pairIndex);
                    if (quartet.PTarget != null)
                        WriteManifestRow(writer, quartet.PTarget, false, JoinedLabel(quartet, null, true, false), PEPTIDE_TYPE_P_TARGET, pairIndex);
                    if (quartet.Decoy != null)
                        WriteManifestRow(writer, quartet.Decoy, true, JoinedLabel(quartet, null, false, true), PEPTIDE_TYPE_DECOY, pairIndex);
                    if (quartet.PDecoy != null)
                        WriteManifestRow(writer, quartet.PDecoy, true, JoinedLabel(quartet, null, true, true), PEPTIDE_TYPE_P_DECOY, pairIndex);
                    pairIndex++;
                }
            }
            Log(@"Wrote manifest with " + kept.Count.ToString(CultureInfo.InvariantCulture) + @" pair_index groups");
        }

        /// <summary>
        /// The ';'-joined labels of every source, each with its counter when
        /// <paramref name="counters"/> gives one.
        /// </summary>
        private string JoinedLabel(EntrapmentQuartet quartet, int?[] counters, bool pTarget, bool decoy)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < quartet.Sources.Count; i++)
            {
                if (i > 0)
                    sb.Append(';');
                sb.Append(BuildProteinLabel(quartet.Sources[i], pTarget, decoy, _settings.EntrapmentSuffix,
                    _settings.DecoyPrefix, counters?[i], _settings.PeptideSuffixFormat));
            }
            return sb.ToString();
        }

        private static void WriteEntry(TextWriter writer, string label, string sequence)
        {
            writer.Write('>');
            writer.Write(label);
            writer.Write('\n');
            writer.Write(sequence);
            writer.Write('\n');
        }

        private static void WriteManifestRow(TextWriter writer, string sequence, bool decoy, string proteins,
            string peptideType, int pairIndex)
        {
            writer.Write(sequence);
            writer.Write(decoy ? "\tYes\t" : "\tNo\t");
            writer.Write(proteins);
            writer.Write('\t');
            writer.Write(peptideType);
            writer.Write('\t');
            writer.Write(pairIndex.ToString(CultureInfo.InvariantCulture));
            writer.Write('\n');
        }

        /// <summary>A UTF-8 (no BOM) writer, creating the parent folder as Carafe does. Lines end in LF.</summary>
        internal static StreamWriter CreateWriter(string path)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            return new StreamWriter(path, false, UTF8_NO_BOM, 1 << 16);
        }

        private void Log(string message)
        {
            _log.WriteLine(message);
        }
    }
}
