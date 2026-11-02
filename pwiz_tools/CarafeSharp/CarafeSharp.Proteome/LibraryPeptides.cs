/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using pwiz.CarafeSharp.Contracts;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The peptides a library is predicted for, digested from a protein FASTA in memory: the
    /// targets with their decoys and, when asked for, their entrapment peptides and the decoys of
    /// those, exactly as <see cref="EntrapmentFastaBuilder"/> writes them to a peptide FASTA and a
    /// pairing manifest, but handed to the predictor without the files. A library built this way
    /// is one step from FASTA to <c>.blib</c>: the peptides, the proteins each came from and the
    /// pairing travel together, and the predictor digests nothing a second time.
    /// <para>
    /// The protein text of a peptide is its source proteins' <c>db|accession|entry</c> labels
    /// joined by <c>;</c> (a decoy's prefixed, an entrapment peptide's suffixed), without the
    /// per-protein peptide counter the FASTA route appends to keep a predictor that deduplicates
    /// by accession from dropping entries; nothing here deduplicates, so the library carries the
    /// accessions as the proteome spells them.
    /// </para>
    /// </summary>
    public sealed class LibraryPeptides
    {
        private readonly EntrapmentFastaBuilder _builder;
        private readonly List<EntrapmentQuartet> _quartets;

        private LibraryPeptides(EntrapmentFastaBuilder builder, List<EntrapmentQuartet> quartets, EntrapmentFastaResult counts,
            HashSet<string> peptides, Dictionary<string, string> peptideToProteins, ISet<string> proteinNTermPeptides,
            List<DecoyPairPlanner.ManifestEntry> pairing)
        {
            _builder = builder;
            _quartets = quartets;
            Counts = counts;
            Peptides = peptides;
            PeptideToProteins = peptideToProteins;
            ProteinNTermPeptides = proteinNTermPeptides;
            Pairing = pairing;
        }

        /// <summary>
        /// Digests <paramref name="settings"/>' FASTA and generates its decoys and entrapment as the
        /// entrapment FASTA build does (<see cref="EntrapmentFastaBuilder.Build"/>), logging the
        /// same lines to <paramref name="log"/>.
        /// </summary>
        public static LibraryPeptides Build(EntrapmentFastaSettings settings, TextWriter log)
        {
            var builder = new EntrapmentFastaBuilder(settings, log);
            var counts = new EntrapmentFastaResult();
            var quartets = builder.Build(counts, out var digester);
            var peptides = new HashSet<string>(StringComparer.Ordinal);
            var peptideToProteins = new Dictionary<string, string>(StringComparer.Ordinal);
            var pairing = new List<DecoyPairPlanner.ManifestEntry>();
            string entrapmentMethod = settings.EntrapmentSourceFasta == null
                ? PairingManifest.ENTRAPMENT_METHOD_SHUFFLE
                : PairingManifest.ENTRAPMENT_METHOD_FOREIGN;
            int pairIndex = 0;
            foreach (var quartet in quartets)
            {
                Add(quartet.Target, builder.ProteinLabel(quartet, false, false), PeptideKind.Target, pairIndex, null);
                if (quartet.PTarget != null)
                    Add(quartet.PTarget, builder.ProteinLabel(quartet, true, false), PeptideKind.PTarget, pairIndex, entrapmentMethod);
                if (quartet.Decoy != null)
                    Add(quartet.Decoy, builder.ProteinLabel(quartet, false, true), PeptideKind.Decoy, pairIndex, null);
                if (quartet.PDecoy != null)
                    Add(quartet.PDecoy, builder.ProteinLabel(quartet, true, true), PeptideKind.PDecoy, pairIndex, null);
                pairIndex++;
            }
            return new LibraryPeptides(builder, quartets, counts, peptides, peptideToProteins, digester.ProteinNTermPeptides, pairing);

            void Add(string sequence, string proteins, PeptideKind kind, int index, string method)
            {
                // A sequence generated twice (a decoy that is another target's reverse) is one
                // library peptide, as it is one FASTA record's sequence; the first pairing wins,
                // as the manifest reader's dictionary does, and the protein text gains the later
                // record's proteins, as the FASTA route's peptide-to-protein map unions them.
                if (!peptides.Add(sequence))
                {
                    peptideToProteins[sequence] = MergeProteins(peptideToProteins[sequence], proteins);
                    return;
                }
                peptideToProteins.Add(sequence, proteins);
                pairing.Add(new DecoyPairPlanner.ManifestEntry(sequence, PeptideKinds.IsDecoySide(kind), PeptideKinds.Name(kind), index, method));
            }
        }

        /// <summary>
        /// Appends to <paramref name="existing"/> the <c>;</c>-separated labels of
        /// <paramref name="added"/> it does not already carry, in their order.
        /// </summary>
        private static string MergeProteins(string existing, string added)
        {
            var labels = new List<string>(existing.Split(';'));
            var seen = new HashSet<string>(labels, StringComparer.Ordinal);
            foreach (var label in added.Split(';'))
            {
                if (seen.Add(label))
                    labels.Add(label);
            }
            return string.Join(";", labels);
        }

        /// <summary>Every peptide to predict, targets and generated alike.</summary>
        public HashSet<string> Peptides { get; }

        /// <summary>Each peptide's protein text, the library's <c>ProteinID</c>.</summary>
        public IReadOnlyDictionary<string, string> PeptideToProteins { get; }

        /// <summary>The peptides seen at a protein N terminus, which alone may carry a protein N-term variable modification.</summary>
        public ISet<string> ProteinNTermPeptides { get; }

        /// <summary>The pairing, as the rows of the manifest the FASTA route writes.</summary>
        public IReadOnlyList<DecoyPairPlanner.ManifestEntry> Pairing { get; }

        /// <summary>The build's counts, as the entrapment FASTA build logs them.</summary>
        public EntrapmentFastaResult Counts { get; }

        /// <summary>True when entrapment peptides were generated.</summary>
        public bool HasEntrapment
        {
            get { return _quartets.Any(q => q.PTarget != null); }
        }

        /// <summary>
        /// Writes the pairing manifest to <paramref name="path"/>, as the FASTA route writes it,
        /// for a reader outside Osprey (FDRBench) that takes the entrapment pairing from the file.
        /// </summary>
        public void WriteManifest(string path)
        {
            _builder.WriteManifest(_quartets, path);
        }
    }
}
