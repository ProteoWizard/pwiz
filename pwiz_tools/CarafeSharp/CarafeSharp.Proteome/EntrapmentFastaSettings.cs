/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/db/EntrapmentFastaGear.java
 *   (Config)
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

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// What <see cref="EntrapmentFastaBuilder"/> builds, with Carafe's <c>Config</c> defaults:
    /// target and decoy peptides, no entrapment, no m/z filter.
    /// </summary>
    public sealed class EntrapmentFastaSettings
    {
        /// <summary>Accession and entry-name suffix marking entrapment peptides (FDRBench convention).</summary>
        public const string DEFAULT_ENTRAPMENT_SUFFIX = @"_p_target";

        /// <summary>Header prefix marking decoy peptides.</summary>
        public const string DEFAULT_DECOY_PREFIX = @"decoy_";

        /// <summary>The per-protein peptide counter appended to accessions, as .NET format.</summary>
        public const string DEFAULT_PEPTIDE_SUFFIX_FORMAT = @"_pep{0:D5}";

        public const long DEFAULT_ENTRAPMENT_SEED = 42;
        public const long DEFAULT_DECOY_SEED = 24;
        public const double DEFAULT_MIN_MZ = 400.0;
        public const double DEFAULT_MAX_MZ = 900.0;

        /// <summary>
        /// Smallest entrapment ratio that has been characterized (r = 1, 0.5, 0.25 and 0.1 were
        /// measured). A deliberate floor on an untested region, not a proof lower values fail.
        /// </summary>
        public const double MIN_ENTRAPMENT_RATIO = 0.1;

        /// <summary>The protein FASTA to digest.</summary>
        public string InputFasta { get; set; }

        /// <summary>The peptide FASTA to write.</summary>
        public string OutputFasta { get; set; }

        /// <summary>The FDRBench pairing manifest to write, or null for none.</summary>
        public string Manifest { get; set; }

        public bool AddEntrapment { get; set; }
        public bool AddDecoys { get; set; } = true;

        /// <summary>Keep only peptides with a modified precursor m/z in [MinMz, MaxMz] at some charge.</summary>
        public bool ApplyMzFilter { get; set; }

        public double MinMz { get; set; } = DEFAULT_MIN_MZ;
        public double MaxMz { get; set; } = DEFAULT_MAX_MZ;
        public int[] Charges { get; set; } = { 2, 3 };

        /// <summary>Master seed for entrapment shuffles and for ratio selection.</summary>
        public long EntrapmentSeed { get; set; } = DEFAULT_ENTRAPMENT_SEED;

        /// <summary>
        /// Carafe's <c>-decoy_seed</c>. Parsed and stored but used by nothing: decoys are
        /// reversed and cycled, never shuffled.
        /// </summary>
        public long DecoySeed { get; set; } = DEFAULT_DECOY_SEED;

        /// <summary>
        /// A foreign-species protein FASTA to draw mass-matched entrapment peptides from, or null
        /// to shuffle each target (the default). See <see cref="ForeignEntrapmentSource"/>.
        /// </summary>
        public string EntrapmentSourceFasta { get; set; }

        /// <summary>Fraction of targets that carry an entrapment peptide, in [0.1, 1].</summary>
        public double EntrapmentRatio { get; set; } = 1.0;

        /// <summary>
        /// Apply <see cref="DecoySimilarityGate"/> and I/L-aware collision checks to generated
        /// sequences. An audit switch, not a tuning knob: off reproduces the pre-gate one-shot
        /// behavior so a rebuilt library can be proved to differ only by the gate, and such a
        /// library should not be searched. Foreign entrapment is gated either way.
        /// </summary>
        public bool SimilarityGate { get; set; } = true;

        /// <summary>
        /// Refuse to write a manifest that fails <see cref="EntrapmentPairingValidator"/>
        /// (Carafe's <c>-ignore_pairing_errors</c> turns this off, downgrading it to a warning).
        /// </summary>
        public bool FailOnPairingViolation { get; set; } = true;

        public string EntrapmentSuffix { get; set; } = DEFAULT_ENTRAPMENT_SUFFIX;
        public string DecoyPrefix { get; set; } = DEFAULT_DECOY_PREFIX;

        /// <summary>
        /// Append a per-protein peptide counter to FASTA accessions, so predictors that
        /// deduplicate by accession keep every entry. The manifest never carries it.
        /// </summary>
        public bool UniqueAccessions { get; set; } = true;

        /// <summary>Composite format for the counter, given the 1-based count.</summary>
        public string PeptideSuffixFormat { get; set; } = DEFAULT_PEPTIDE_SUFFIX_FORMAT;

        public DigestSettings Digest { get; set; } = new DigestSettings();

        /// <summary>The modifications the m/z filter enumerates peptidoforms with.</summary>
        public ModificationSettings Modifications { get; set; } = new ModificationSettings();
    }
}
