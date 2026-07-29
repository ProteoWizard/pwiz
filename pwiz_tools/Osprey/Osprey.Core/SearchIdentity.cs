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
    /// hashing — two responsibilities. The hash recipes are unchanged and
    /// MUST stay byte-identical with Rust's <c>osprey-core/src/config.rs</c>
    /// (invariant culture, Rust <c>{:?}</c> escaping, lowercase booleans);
    /// see the per-method comments.
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
                // Mirror Rust's `format!("decoy_pairing_manifest:{:?}\n", ...)`
                // where the value is `Some("path")` or `None`. Rust's {:?}
                // on String escapes \, ", \n, \r, \t, \0, and other control
                // chars (< 0x20) -- critical for Windows paths whose `\`
                // separators must become `\\` to match Rust's output. The
                // path is not normalised; the user's choice (relative or
                // absolute) is part of the hash so a moved manifest
                // invalidates the cache.
                sb.AppendFormat(ic, "decoy_pairing_manifest:{0}\n",
                    string.IsNullOrEmpty(_config.DecoyPairingManifestPath)
                        ? "None"
                        : "Some(\"" + EscapeForRustDebug(_config.DecoyPairingManifestPath) + "\")");
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
            string libPath = _config.LibrarySource != null ? _config.LibrarySource.Path : string.Empty;
            using (var sha256 = SHA256.Create())
            {
                var sb = new StringBuilder();
                string fileName = string.IsNullOrEmpty(libPath)
                    ? string.Empty
                    : Path.GetFileName(libPath);
                sb.AppendFormat("file_name:{0}\n", fileName);
                if (!string.IsNullOrEmpty(libPath) && File.Exists(libPath))
                {
                    var info = new FileInfo(libPath);
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
        /// Escape a string to match Rust's <c>{:?}</c> Debug formatter
        /// output for <c>&amp;str</c> / <c>String</c>. Handles the cases
        /// that actually appear in config values folded into the search
        /// hash: backslashes, double quotes, common C escapes
        /// (<c>\n</c>, <c>\r</c>, <c>\t</c>, <c>\0</c>), and other
        /// sub-0x20 control characters via the <c>\u{...}</c> form.
        /// Printable ASCII and non-ASCII bytes pass through unchanged --
        /// matching Rust's default Debug output as of the language
        /// versions in use by maccoss/osprey (1.75+). Used so cross-impl
        /// hashes agree on Windows paths whose <c>\</c> separators must
        /// render as <c>\\</c>.
        /// </summary>
        internal static string EscapeForRustDebug(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s ?? string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append(@"\n"); break;
                    case '\r': sb.Append(@"\r"); break;
                    case '\t': sb.Append(@"\t"); break;
                    case '\0': sb.Append(@"\0"); break;
                    default:
                        if (c < 0x20 || c == 0x7F)
                        {
                            // Rust's {:?} uses `\u{HEX}` with lowercase hex,
                            // no padding. AppendFormat's `{0:x}` works on
                            // net8.0 but produces `{x}` literally on net472
                            // under the double-brace escape sequence; use
                            // explicit ToString to avoid the regression.
                            sb.Append(@"\u{");
                            sb.Append(((int)c).ToString(@"x",
                                System.Globalization.CultureInfo.InvariantCulture));
                            sb.Append('}');
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
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
        /// merge node expects is computed over ALL files in the join, so
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
