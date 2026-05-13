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

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Custom static analysis checks that ReSharper can't express. Modeled on
    /// Skyline's <c>CodeInspectionTest.CodeInspection</c> in
    /// <c>pwiz_tools/Skyline/Test/CodeInspectionTest.cs</c>, but scoped to
    /// the OspreySharp tree and its specific cross-impl-parity hazards.
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
            "OspreySharp.Test",
            "bin",
            "obj"
        };

        /// <summary>
        /// .NET <c>Array.Sort</c> uses introsort, which is UNSTABLE and reorders
        /// equal-keyed elements unpredictably. Rust's <c>slice::sort_by</c> is
        /// stable, so cross-impl scoring code that ties on a key (e.g. two
        /// centroids at the same m/z, two peptides at the same RT, two CWT
        /// candidates with the same coelution score) diverges on the
        /// post-sort tie-ordering and silently produces different downstream
        /// values. The substituted, stable pattern is:
        /// <code>
        /// int[] order = Enumerable.Range(0, n).OrderBy(i => key[i]).ToArray();
        /// // then permute parallel arrays through `order`
        /// </code>
        /// For sorting a single primitive array purely to find a median or
        /// percentile (no parallel data, no downstream tie-sensitive use),
        /// add an inline exemption comment on the same line:
        /// <c>Array.Sort(values); // Array.Sort OK: median of single primitive array</c>.
        /// </summary>
        [TestMethod]
        public void TestNoUnstableArraySort()
        {
            string sourceRoot = FindOspreySharpSourceRoot();
            var violations = new List<string>();
            // \bArray\.Sort\s*\( catches all overloads: (T[]), (T[],T[]), (T[],Comparison<T>), etc.
            var pattern = new Regex(@"\bArray\.Sort\s*\(");
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
                        "{0}:{1}: forbidden Array.Sort. Replace with stable Enumerable.Range(0, n).OrderBy(...) " +
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
        /// Find the OspreySharp source root by walking up from the test
        /// assembly location until we see an OspreySharp.sln-bearing dir.
        /// </summary>
        private static string FindOspreySharpSourceRoot()
        {
            string dir = Path.GetDirectoryName(typeof(CodeInspectionTest).Assembly.Location);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, "OspreySharp")) &&
                    Directory.Exists(Path.Combine(dir, "OspreySharp.Test")) &&
                    File.Exists(Path.Combine(dir, "OspreySharp.sln")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException(
                "Could not locate OspreySharp source root from test assembly location.");
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
