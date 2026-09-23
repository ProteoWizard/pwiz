/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Security.Cryptography;
using System.Text;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Computes the bit-parity-critical identity hashes for an Osprey run —
    /// the SHA-256 keys that gate cached-artifact reuse and cross-impl
    /// (Rust) byte-equivalence. Split out of <see cref="OspreyConfig"/>,
    /// which previously owned both the mutable configuration bag AND this
    /// hashing — two responsibilities. The recipes follow Rust's
    /// <c>osprey-core/src/config.rs</c> (invariant culture, lowercase
    /// booleans, Rust <c>{:?}</c> rendering of collections); see the
    /// per-method comments.
    ///
    /// <para>One term diverges from Rust DELIBERATELY:
    /// <see cref="DecoyPairingManifestTerm"/> identifies the manifest by file
    /// name + size + mtime where Rust still writes its path. Nothing observes
    /// the disagreement — each implementation validates only its OWN cached
    /// artifacts, and no cross-impl comparator reads a search hash — and the
    /// path form accepted a stale score against an edited manifest, which the
    /// identity form does not. Keeping a wrong key to preserve an unobservable
    /// match would be the worse trade.</para>
    ///
    /// Reads its inputs from the supplied <see cref="OspreyConfig"/> at call
    /// time, so it reflects the config's hash-affecting fields as of each
    /// call (matching the historical behavior when these methods lived on
    /// <see cref="OspreyConfig"/>). Obtain one via <see cref="OspreyConfig.Identity"/>.
    /// </summary>
    public sealed class SearchIdentity
    {
        private readonly OspreyConfig _config;

        public SearchIdentity(OspreyConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Compute SHA-256 hash of parameters that affect first-pass scoring.
        /// If this hash changes, cached .scores.parquet files are invalid.
        /// </summary>
        public string SearchParameterHash()
        {
            // Cross-impl bit-equivalence with Rust requires:
            //  - Booleans: Rust prints "true"/"false" (lowercase). C# default
            //    bool.ToString() is "True"/"False". Use lowercase explicitly.
            //  - Numbers: invariant culture (no locale-dependent separators).
            using (var sha256 = SHA256.Create())
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                Func<bool, string> b = v => v ? "true" : "false";
                var sb = new StringBuilder();
                sb.AppendFormat(ic, "resolution_mode:{0}\n", _config.ResolutionMode);
                sb.AppendFormat(ic, "fragment_tolerance:{0},{1}\n", _config.FragmentTolerance.Tolerance, _config.FragmentTolerance.Unit);
                sb.AppendFormat(ic, "precursor_tolerance:{0},{1}\n", _config.PrecursorTolerance.Tolerance, _config.PrecursorTolerance.Unit);
                sb.AppendFormat(ic, "prefilter_enabled:{0}\n", b(_config.PrefilterEnabled));
                sb.AppendFormat(ic, "decoy_method:{0}\n", _config.DecoyMethod);
                sb.AppendFormat(ic, "decoys_in_library:{0}\n", b(_config.DecoysInLibrary));
                // Sort prefixes so ordering changes don't churn the hash.
                // Lower-case to make case-only edits no-ops (matching the
                // runtime comparison). Mirrors Rust's
                // format!("decoy_prefixes:{:?}\n", prefixes) where {:?} on
                // Vec<String> yields ["a", "b"] (double-quoted, comma-
                // space-separated).
                var prefixes = new List<string>(_config.DecoyPrefixes != null ? _config.DecoyPrefixes.Count : 0);
                if (_config.DecoyPrefixes != null)
                {
                    foreach (var p in _config.DecoyPrefixes)
                        prefixes.Add(p == null ? string.Empty : p.ToLowerInvariant());
                }
                prefixes.Sort(StringComparer.Ordinal); // Array.Sort OK: sorted only to render a stable display string of distinct decoy prefixes; equal strings are byte-identical so tie order is irrelevant
                var prefixList = new StringBuilder("[");
                for (int i = 0; i < prefixes.Count; i++)
                {
                    if (i > 0) prefixList.Append(", ");
                    prefixList.Append('"').Append(prefixes[i]).Append('"');
                }
                prefixList.Append(']');
                sb.AppendFormat(ic, "decoy_prefixes:{0}\n", prefixList.ToString());
                sb.AppendFormat(ic, "decoy_pairing_manifest:{0}\n",
                    DecoyPairingManifestTerm());
                sb.AppendFormat(ic, "decoy_pair_min_fraction:{0}\n", _config.DecoyPairMinFraction);
                sb.AppendFormat(ic, "rt_cal.enabled:{0}\n", b(_config.RtCalibration.Enabled));
                sb.AppendFormat(ic, "rt_cal.fallback_rt_tolerance:{0}\n", _config.RtCalibration.FallbackRtTolerance);
                sb.AppendFormat(ic, "rt_cal.rt_tolerance_factor:{0}\n", _config.RtCalibration.RtToleranceFactor);
                sb.AppendFormat(ic, "rt_cal.min_rt_tolerance:{0}\n", _config.RtCalibration.MinRtTolerance);
                sb.AppendFormat(ic, "rt_cal.max_rt_tolerance:{0}\n", _config.RtCalibration.MaxRtTolerance);
                sb.AppendFormat(ic, "rt_cal.loess_bandwidth:{0}\n", _config.RtCalibration.LoessBandwidth);
                sb.AppendFormat(ic, "rt_cal.min_calibration_points:{0}\n", _config.RtCalibration.MinCalibrationPoints);
                sb.AppendFormat(ic, "rt_cal.calibration_sample_size:{0}\n", _config.RtCalibration.CalibrationSampleSize);
                sb.AppendFormat(ic, "rt_cal.calibration_retry_factor:{0}\n", _config.RtCalibration.CalibrationRetryFactor);
                sb.AppendFormat(ic, "reconciliation.top_n_peaks:{0}\n", _config.Reconciliation.TopNPeaks);

                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var result = new StringBuilder(64);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    result.Append(hashBytes[i].ToString("x2"));
                }
                return result.ToString();
            }
        }

        /// <summary>
        /// Compute a fast identity hash for the library file (file name + size
        /// + mtime). Filesystem metadata only -- no content hashing. The
        /// directory portion is deliberately NOT in the hash so the same
        /// library identifies identically across Rust / .NET / OS variations
        /// (drive letter case, forward vs back slash, relative vs absolute,
        /// HPC node-local vs shared paths). Mirrors the
        /// <c>reconciliation_parameter_hash</c> precedent that hashes only
        /// sorted file stems for the input set. Same recipe as Rust's
        /// <c>library_identity_hash</c>.
        /// </summary>
        public string LibraryIdentityHash()
        {
            return FileIdentityHash(_config.LibrarySource != null
                ? _config.LibrarySource.Path
                : string.Empty);
        }

        /// <summary>
        /// The <c>decoy_pairing_manifest</c> term folded into
        /// <see cref="SearchParameterHash"/>: <c>None</c> when no manifest is configured,
        /// otherwise <c>Some(&lt;identity hash&gt;)</c> over the manifest's file name, size
        /// and mtime -- the same recipe as <see cref="LibraryIdentityHash"/>, through the
        /// same <see cref="FileIdentityHash"/>.
        ///
        /// <para>This term used to be the manifest's FULL PATH, which had the invalidation
        /// backwards: moving a manifest re-scored every parquet, while EDITING one in place
        /// left every scored parquet reading valid against a file that no longer says what
        /// it said. The manifest decides decoy classification, target/decoy pairing and the
        /// protein accessions protein FDR runs on, so accepting a stale score against an
        /// edited manifest is an FDR-relevant answer, not a cache-efficiency question.</para>
        ///
        /// <para>The no-manifest term is exactly <c>None</c>, unchanged, so a
        /// generated-decoy analysis hashes byte-identically to before this change and
        /// invalidates nothing. Only manifest-using analyses re-score, once.</para>
        ///
        /// <para>The full-path form existed to mirror Rust's
        /// <c>format!("decoy_pairing_manifest:{:?}\n", ...)</c> on a <c>PathBuf</c>
        /// (<c>crates/osprey-core/src/config.rs</c>). That mirroring bought nothing a
        /// cross-impl run can observe: each implementation validates only its OWN cached
        /// artifacts, and the cross-impl comparators read blibs, the Stage-7 protein FDR
        /// dump and the per-file FDR sidecars -- none of which carries a search hash.</para>
        /// </summary>
        public string DecoyPairingManifestTerm()
        {
            string path = _config.DecoyPairingManifestPath;
            return string.IsNullOrEmpty(path)
                ? @"None"
                : @"Some(" + FileIdentityHash(path) + @")";
        }

        /// <summary>
        /// A fast identity hash for one file: file name + size + mtime, filesystem metadata
        /// only, no content hashing. The DIRECTORY portion is deliberately excluded so the
        /// same file identifies identically across Rust / .NET / OS variations (drive letter
        /// case, forward vs back slash, relative vs absolute, HPC node-local vs shared
        /// paths) -- and so moving a file is free while editing it in place is not.
        /// Size and mtime are omitted for a path that does not exist, leaving the name to
        /// stand alone. Same recipe as Rust's <c>library_identity_hash</c>.
        /// </summary>
        private static string FileIdentityHash(string path)
        {
            using (var sha256 = SHA256.Create())
            {
                var sb = new StringBuilder();
                string fileName = string.IsNullOrEmpty(path)
                    ? string.Empty
                    : Path.GetFileName(path);
                sb.AppendFormat("file_name:{0}\n", fileName);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var info = new FileInfo(path);
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                        "size:{0}\n", info.Length);
                    // Unix seconds matching Rust's library_identity_hash
                    // (SystemTime::duration_since(UNIX_EPOCH).as_secs()).
                    long mtimeSecs = (long)(info.LastWriteTimeUtc
                        - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                        "mtime:{0}\n", mtimeSecs);
                }
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var result = new StringBuilder(64);
                for (int i = 0; i < hashBytes.Length; i++)
                    result.Append(hashBytes[i].ToString("x2"));
                return result.ToString();
            }
        }

        /// <summary>
        /// SHA-256 of (search hash + reconciliation parameters + run FDR
        /// + sorted file stems). Mirrors Rust
        /// <c>OspreyConfig::reconciliation_parameter_hash</c> in
        /// <c>crates/osprey-core/src/config.rs</c> and is written into
        /// reconciled <c>.scores.parquet</c> footer metadata under
        /// <c>osprey.reconciliation_hash</c>. The hash invalidates the
        /// cache on any reconciliation parameter change OR on any change
        /// to the multi-file set (file_stems are sorted to make the
        /// hash invariant to invocation order).
        /// </summary>
        public string ReconciliationParameterHash()
        {
            var stems = new List<string>(_config.InputFiles?.Count ?? 0);
            if (_config.InputFiles != null)
            {
                foreach (var path in _config.InputFiles)
                {
                    string stem = Path.GetFileNameWithoutExtension(path);
                    if (!string.IsNullOrEmpty(stem))
                        stems.Add(stem);
                }
            }
            return ReconciliationParameterHashForStems(stems);
        }

        /// <summary>
        /// Compute the reconciliation parameter hash for an explicit set of
        /// file stems. Used by per-file Stage 6 rescore workers, whose
        /// <see cref="OspreyConfig.InputFiles"/> only carries this worker's single
        /// parquet — the hash that the downstream <c>--task SecondPassFDR</c>
        /// SecondPassFDR node expects is computed over ALL files in the join, so
        /// the worker must read the full set from the planner's
        /// <c>reconciliation.json</c> envelope and pass it in here. The
        /// stems are sorted + deduped internally so the hash is invariant
        /// to caller ordering. Mirrors Rust
        /// <c>OspreyConfig::reconciliation_parameter_hash_for_stems</c>.
        /// </summary>
        public string ReconciliationParameterHashForStems(IReadOnlyList<string> fileStems)
        {
            using (var sha256 = SHA256.Create())
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.Append(SearchParameterHash());
                sb.AppendFormat(ic, "reconciliation.enabled:{0}\n",
                    _config.Reconciliation.Enabled ? "true" : "false");
                sb.AppendFormat(ic, "reconciliation.consensus_fdr:{0}\n",
                    _config.Reconciliation.ConsensusFdr);
                sb.AppendFormat(ic, "run_fdr:{0}\n", _config.RunFdr);
                // Mirror Rust's `format!("file_stems:{:?}\n", stems)` output
                // exactly. {:?} on Vec<String> yields ["a", "b"] with the
                // brackets and double-quoted, comma-space-separated values.
                // Stems are sorted + deduped here so the hash matches the
                // Rust side, which also sorts + dedups before hashing.
                var stems = new List<string>(fileStems?.Count ?? 0);
                if (fileStems != null)
                {
                    foreach (var stem in fileStems)
                    {
                        if (!string.IsNullOrEmpty(stem))
                            stems.Add(stem);
                    }
                }
                stems.Sort(StringComparer.Ordinal); // Array.Sort OK: sorted only to dedup adjacent identical stems below; equal keys are byte-identical so tie order is irrelevant
                // Dedup in place (stems is sorted, so duplicates are
                // adjacent). Rust does `dedup()` on a sorted Vec; same here.
                int write = 0;
                for (int read = 0; read < stems.Count; read++)
                {
                    if (read == 0 || !string.Equals(stems[read], stems[read - 1], StringComparison.Ordinal))
                    {
                        stems[write++] = stems[read];
                    }
                }
                if (write < stems.Count)
                    stems.RemoveRange(write, stems.Count - write);

                var stemsList = new StringBuilder("[");
                for (int i = 0; i < stems.Count; i++)
                {
                    if (i > 0) stemsList.Append(", ");
                    stemsList.Append('"').Append(stems[i]).Append('"');
                }
                stemsList.Append(']');
                sb.AppendFormat(ic, "file_stems:{0}\n", stemsList.ToString());

                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var result = new StringBuilder(64);
                for (int i = 0; i < hashBytes.Length; i++)
                    result.Append(hashBytes[i].ToString("x2"));
                return result.ToString();
            }
        }
    }
}
