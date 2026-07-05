/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Custom static analysis checks that ReSharper can't express. Modeled on
    /// Skyline's <c>CodeInspectionTest.CodeInspection</c> in
    /// <c>pwiz_tools/Skyline/Test/CodeInspectionTest.cs</c>, but scoped to
    /// the Osprey tree and its specific cross-impl-parity hazards.
    ///
    /// Exemption protocol: any forbidden pattern can be allowed on a single
    /// line by appending an inline comment beginning with the pattern's
    /// exemption tag (e.g. <c>// Array.Sort OK: ...</c>). This keeps the
    /// rule strict by default and forces a deliberate, reviewable choice
    /// per call site.
    /// </summary>
    [TestClass]
    public class CodeInspectionTest
    {
        /// <summary>
        /// Files under these directories are skipped. Test code can use
        /// <c>Array.Sort</c> freely for local fixture setup; only production
        /// code participates in cross-impl parity, where unstable sort
        /// breaks tie-ordering relative to Rust.
        /// </summary>
        private static readonly string[] SkippedDirectories =
        {
            "Osprey.Test",
            "bin",
            "obj"
        };

        /// <summary>
        /// Both .NET <c>Array.Sort</c> and <c>List&lt;T&gt;.Sort</c> use introsort,
        /// which is UNSTABLE and reorders equal-keyed elements unpredictably. Rust's
        /// <c>slice::sort_by</c> is stable, so cross-impl scoring code that ties on a
        /// key (e.g. two centroids at the same m/z, two peptides at the same RT, two
        /// CWT candidates with the same coelution score) diverges on the post-sort
        /// tie-ordering and silently produces different downstream values. The
        /// canonical incident was <c>ProteinFdr</c>'s <c>winners.Sort(...)</c> with a
        /// HashMap-iteration-order tiebreak: invisible until upstream calibration drift
        /// let ties fire, then silently parity-divergent. The substituted, stable
        /// pattern is either <c>OrderBy(...).ThenBy(...).ToList()</c> (LINQ, stable) or
        /// an explicit index permutation:
        /// <code>
        /// int[] order = Enumerable.Range(0, n).OrderBy(i => key[i]).ToArray();
        /// // then permute parallel arrays through `order`
        /// </code>
        /// For a call whose comparator can never return 0 (a unique/total-order key),
        /// or whose output ordering is never inspected downstream, or a single primitive
        /// array sorted purely for a median/percentile, add an inline exemption comment
        /// on the same line, stating WHY it is tie-safe:
        /// <c>values.Sort(); // Array.Sort OK: median of single primitive array</c>.
        /// The tag is <c>// Array.Sort OK:</c> for BOTH <c>Array.Sort</c> and
        /// <c>List&lt;T&gt;.Sort</c> exemptions, so one grep finds every one.
        /// </summary>
        [TestMethod]
        public void TestNoUnstableArraySort()
        {
            string sourceRoot = FindOspreySourceRoot();
            var violations = new List<string>();
            // \b\w+\s*\??\.Sort\s*\( catches Array.Sort AND List<T>.Sort (both introsort,
            // both unstable), across all overloads: (), (Comparison<T>), (IComparer<T>),
            // (T[]), (T[],T[]), (T[],Comparison<T>), etc. The optional \?? also catches a
            // null-conditional receiver (foo?.Sort(...)) so a future one can't slip the guard.
            var pattern = new Regex(@"\b\w+\s*\??\.Sort\s*\(");
            const string exemptionTag = "// Array.Sort OK:";

            foreach (var file in EnumerateProductionCsFiles(sourceRoot))
            {
                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch (IOException) { continue; }

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (!pattern.IsMatch(line))
                        continue;
                    // Strip any line-comment text after // before checking the
                    // pattern, so a comment like `// historical Array.Sort()
                    // was unstable` doesn't trip the rule.
                    int commentIdx = IndexOfLineComment(line);
                    string codePart = commentIdx >= 0 ? line.Substring(0, commentIdx) : line;
                    if (!pattern.IsMatch(codePart))
                        continue;
                    if (line.Contains(exemptionTag))
                        continue;
                    string rel = RelativePath(sourceRoot, file)
                        .Replace('\\', '/');
                    violations.Add(string.Format(
                        "{0}:{1}: forbidden Array.Sort/List<T>.Sort. Replace with stable OrderBy(...).ThenBy(...) " +
                        "(or Enumerable.Range(0, n).OrderBy(...) for parallel arrays), " +
                        "or add an inline exemption comment '{2} <reason>' on the same line. Source: {3}",
                        rel, i + 1, exemptionTag, line.TrimEnd()));
                }
            }

            Assert.AreEqual(0, violations.Count,
                "Unstable Array.Sort uses found in production code. .NET Array.Sort is introsort " +
                "(UNSTABLE) and reorders ties differently from Rust's stable slice::sort_by, " +
                "silently breaking cross-impl parity in scoring code. Use " +
                "`Enumerable.Range(0, n).OrderBy(i => key[i]).ToArray()` and permute parallel " +
                "arrays through that order. If sorting a single primitive array for a median or " +
                "percentile (no parallel data, no tie-sensitive downstream use), add an inline " +
                "comment '// Array.Sort OK: <reason>' on the same line.\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        /// Reconciliation must pair a target with its decoy by base_id (the
        /// entry_id low 31 bits), never by stripping a "DECOY_" prefix from the
        /// modified sequence. Prefix-stripping silently misses library-supplied
        /// decoys (Carafe / FDRBench manifest) whose modseq carries no prefix, so
        /// they are dropped from reconciliation and bias second-pass FDR optimistic
        /// (osprey 0abe0ff). Guard: no "DECOY_" string literal may appear in the
        /// reconciliation code (comments describing the rationale are fine). If a
        /// literal is ever genuinely required, add an inline exemption comment
        /// beginning "// DECOY_ pairing OK:" on the same line.
        /// </summary>
        [TestMethod]
        public void TestReconciliationPairsDecoysByBaseIdNotPrefix()
        {
            string sourceRoot = FindOspreySourceRoot();
            var violations = new List<string>();
            // A DECOY_ string literal in code (e.g. "DECOY_" or @"DECOY_") is the
            // fingerprint of prefix-based pairing; the base_id path never needs it.
            var pattern = new Regex("\"DECOY_");
            const string exemptionTag = "// DECOY_ pairing OK:";
            const string reconDir = "Osprey.FDR/Reconciliation/";

            foreach (var file in EnumerateProductionCsFiles(sourceRoot))
            {
                string rel = RelativePath(sourceRoot, file).Replace('\\', '/');
                if (rel.IndexOf(reconDir, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch (IOException) { continue; }

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    // Only inspect the code part -- comments legitimately mention
                    // "DECOY_" to explain why base_id pairing is used instead.
                    int commentIdx = IndexOfLineComment(line);
                    string codePart = commentIdx >= 0 ? line.Substring(0, commentIdx) : line;
                    if (!pattern.IsMatch(codePart))
                        continue;
                    if (line.Contains(exemptionTag))
                        continue;
                    string rel2 = rel;
                    violations.Add(string.Format("{0}:{1}: {2}", rel2, i + 1, line.TrimEnd()));
                }
            }

            Assert.AreEqual(0, violations.Count,
                "Reconciliation code must pair decoys by base_id (entry_id & 0x7FFFFFFF), not by " +
                "stripping a \"DECOY_\" prefix from the modified sequence. Prefix-stripping misses " +
                "library-supplied decoys (no prefix) and biases second-pass FDR optimistic " +
                "(osprey 0abe0ff). Remove the \"DECOY_\" literal and pair by base_id, or -- if it is " +
                "truly needed -- add an inline '// DECOY_ pairing OK: <reason>' on the same line.\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        /// Find the Osprey source root by walking up from the test
        /// assembly location until we see an Osprey.sln-bearing dir.
        /// </summary>
        private static string FindOspreySourceRoot()
        {
            string dir = Path.GetDirectoryName(typeof(CodeInspectionTest).Assembly.Location);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, "Osprey")) &&
                    Directory.Exists(Path.Combine(dir, "Osprey.Test")) &&
                    File.Exists(Path.Combine(dir, "Osprey.sln")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException(
                "Could not locate Osprey source root from test assembly location.");
        }

        /// <summary>
        /// Path-relative-to-root that works on net472 (where Path.GetRelativePath
        /// is not available). Returns forward-slash form. Assumes <paramref name="path"/>
        /// is under <paramref name="root"/>.
        /// </summary>
        private static string RelativePath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root);
            string fullPath = Path.GetFullPath(path);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !fullRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(fullRoot.Length)
                : fullPath;
        }

        private static IEnumerable<string> EnumerateProductionCsFiles(string root)
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string rel = RelativePath(root, file).Replace('\\', '/');
                bool skip = false;
                foreach (var skipped in SkippedDirectories)
                {
                    if (rel.StartsWith(skipped + "/", StringComparison.OrdinalIgnoreCase) ||
                        rel.IndexOf("/" + skipped + "/", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        skip = true;
                        break;
                    }
                }
                if (!skip)
                    yield return file;
            }
        }

        /// <summary>
        /// Index of the first <c>//</c> that begins a line comment, ignoring
        /// occurrences inside string literals. Returns -1 if no line comment.
        /// </summary>
        private static int IndexOfLineComment(string line)
        {
            bool inString = false;
            bool inVerbatim = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inVerbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                        inVerbatim = false;
                    }
                    continue;
                }
                if (inString)
                {
                    if (c == '\\' && i + 1 < line.Length) { i++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                if (c == '@' && i + 1 < line.Length && line[i + 1] == '"') { inVerbatim = true; i++; continue; }
                if (c == '"') { inString = true; continue; }
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') return i;
            }
            return -1;
        }
    }
}
