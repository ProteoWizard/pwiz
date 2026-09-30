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
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

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
        public void TestNoUnstableSort()
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
                "Unstable Array.Sort / List<T>.Sort uses found in production code. Both .NET " +
                "Array.Sort and List<T>.Sort are introsort (UNSTABLE) and reorder ties " +
                "differently from Rust's stable slice::sort_by, silently breaking cross-impl " +
                "parity in scoring code. For a single list, use `OrderBy(...).ThenBy(...).ToList()` " +
                "(stable) or give the comparator a unique secondary key so it never returns 0; " +
                "for parallel arrays, permute through " +
                "`Enumerable.Range(0, n).OrderBy(i => key[i]).ToArray()`. If sorting a single " +
                "primitive array for a median or percentile (no tie-sensitive downstream use), " +
                "add an inline comment '// Array.Sort OK: <reason>' on the same line.\n" +
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
        /// Every <c>[TAG]</c> log prefix comes from <c>LogTag</c>, never from a string literal.
        /// <c>OspreyLog.Write</c> is the one place that decides whether a tagged line is written;
        /// a hand-typed <c>"[COUNT] ..."</c> skips that decision and lands in the default log
        /// whatever <c>--perf-stats</c> says, which is how <c>[PATH]</c> and <c>[TRAIN]</c> once
        /// leaked into every run. It also makes the line invisible to anyone looking for the
        /// tag's uses. Write <c>log.LogInfo(LogTag.COUNT, "...")</c> instead.
        ///
        /// A literal is flagged when its text starts with a bracketed upper-case word followed
        /// by <c>]</c> or a space (<c>"[COUNT] "</c>, <c>"[MEM "</c>), after any leading spaces
        /// or format holes (<c>"  [COUNT] "</c>, <c>"{0}[TIMING] "</c>, <c>$"{indent}[BENCH] "</c>):
        /// the runtime filter that once caught those shapes is gone, so this test is the only
        /// guard. Comments are ignored. For
        /// a genuine exception - a literal that is not a log line - add an inline comment
        /// beginning <c>// Log tag OK:</c> on the same line.
        /// </summary>
        [TestMethod]
        public void TestLogTagsComeFromLogTag()
        {
            string sourceRoot = FindOspreySourceRoot();
            var violations = new List<string>();
            var pattern = new Regex("@?\\$?\"(?:\\s|\\{[^{}\"]*\\})*\\[[A-Z][A-Z0-9-]*[\\] ]");
            const string exemptionTag = "// Log tag OK:";

            foreach (var file in EnumerateProductionCsFiles(sourceRoot))
            {
                string[] lines;
                try { lines = File.ReadAllLines(file); }
                catch (IOException) { continue; }

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int commentIdx = IndexOfLineComment(line);
                    string codePart = commentIdx >= 0 ? line.Substring(0, commentIdx) : line;
                    if (!pattern.IsMatch(codePart))
                        continue;
                    if (line.Contains(exemptionTag))
                        continue;
                    string rel = RelativePath(sourceRoot, file).Replace('\\', '/');
                    violations.Add(string.Format("{0}:{1}: {2}", rel, i + 1, line.TrimEnd()));
                }
            }

            Assert.AreEqual(0, violations.Count,
                "Log tag written as a string literal. Use the LogTag constant with an IOspreyLog " +
                "sink - log.LogInfo(LogTag.COUNT, \"...\") - so OspreyLog.Write decides whether the " +
                "line is emitted. If the literal is not a log line, add an inline '// Log tag OK: " +
                "<reason>' on the same line.\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        /// Developer words that must not reach a user through a resource: the first table of
        /// docs/21-user-facing-text.md. The English .resx VALUES are scanned (translations follow
        /// the English), so a banned word cannot come back through a reworded message. Keys may
        /// say anything - they are code. A value that genuinely needs one of these words (none
        /// does today) would be the place to add an exemption, not a reason to weaken a pattern.
        /// </summary>
        private static readonly string[] BANNED_RESOURCE_WORDS =
        {
            @"\bsidecars?\b", @"\bentry\b", @"\bentries\b", @"\bbundles?\b", @"\bstrat(um|a)\b",
            @"\bbase_ids?\b", @"\bhydrat", @"\bcompaction\b", @"\bsurvivors?\b", @"\bstubs?\b",
            @"\bscalars?\b", @"\bfrozen\b", @"\bprojections?\b", @"\bbyproducts?\b", @"\binterned\b",
            @"\bresident\b", @"\bStage[ -]?[1-7]\b", @"OSPREY_[A-Z_]+", @"\b\w+\.cs\b", @"\b\w+Task\b",
            @"\bRun\w+\(", @"\(s\)"
        };

        [TestMethod]
        public void TestResourcesUseUserVocabulary()
        {
            string sourceRoot = FindOspreySourceRoot();
            var patterns = new List<Regex>();
            foreach (var pattern in BANNED_RESOURCE_WORDS)
                patterns.Add(new Regex(pattern, RegexOptions.CultureInvariant));
            var violations = new List<string>();
            int resourceCount = 0;
            foreach (var file in EnumerateEnglishResxFiles(sourceRoot))
            {
                var resxRoot = XDocument.Load(file).Root;
                Assert.IsNotNull(resxRoot, file);
                foreach (var data in resxRoot.Elements("data"))
                {
                    string value = (string) data.Element("value") ?? string.Empty;
                    resourceCount++;
                    foreach (var pattern in patterns)
                    {
                        var match = pattern.Match(value);
                        if (match.Success)
                        {
                            violations.Add(string.Format("{0} {1}: '{2}' in \"{3}\"",
                                Path.GetFileName(file), (string) data.Attribute("name"), match.Value, value));
                        }
                    }
                }
            }

            Assert.IsTrue(resourceCount > 0, "no Osprey resources found under " + sourceRoot);
            Assert.AreEqual(0, violations.Count,
                "A resource uses developer vocabulary. Reword it in the user's terms " +
                "(docs/21-user-facing-text.md, \"Words that never appear in user text\"):\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        /// Every production project opts in to ReSharper's LocalizableElement inspection with the
        /// same two lines as Skyline.csproj.DotSettings, so a plain string literal in user text
        /// fails the inspection gate. Deleting a project's .DotSettings would silently turn the
        /// gate off for that project; this is what notices.
        /// </summary>
        [TestMethod]
        public void TestEveryProjectEnforcesLocalization()
        {
            string sourceRoot = FindOspreySourceRoot();
            var missing = new List<string>();
            foreach (var csproj in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(csproj);
                if (name == "Osprey.Test")
                    continue;
                string settings = csproj + ".DotSettings";
                string text = File.Exists(settings) ? File.ReadAllText(settings) : string.Empty;
                if (!text.Contains("Localization/Localizable/@EntryValue\">Yes<") ||
                    !text.Contains("Localization/LocalizableInspector/@EntryValue\">Pessimistic<"))
                {
                    missing.Add(name);
                }
            }
            Assert.AreEqual(0, missing.Count,
                "Project(s) without the LocalizableElement opt-in (copy Osprey.Core.csproj.DotSettings): " +
                string.Join(", ", missing));
        }

        /// <summary>
        /// Skyline's rule (<c>CommonExceptionUtil.IsProgrammingDefect</c>): an exception type that
        /// is NOT a programming defect - InvalidDataException, every IOException, access denied,
        /// cancellation, UserMessageException and Osprey's subclasses of them - is shown to the
        /// user as its message, so that message must come from a resource. A parse error is one of
        /// these: it means a damaged file the user can correct or delete. Only defect types
        /// (InvalidOperationException, ArgumentException, ...) may carry a string literal. For a
        /// genuine exception add an inline comment beginning <c>// User exception literal OK:</c>.
        /// </summary>
        [TestMethod]
        public void TestUserExceptionsUseResources()
        {
            string sourceRoot = FindOspreySourceRoot();
            var pattern = new Regex(@"new\s+(InvalidDataException|IOException|FileNotFoundException|" +
                @"DirectoryNotFoundException|EndOfStreamException|UnauthorizedAccessException|" +
                @"OperationCanceledException|UserMessageException|SpectraCacheException|BlibOutputException)" +
                @"\s*\(\s*(string\.Format\(\s*(CultureInfo\.\w+\s*,\s*)?)?[@$]*""");
            const string exemptionTag = "// User exception literal OK:";
            var violations = new List<string>();
            foreach (var file in EnumerateProductionCsFiles(sourceRoot))
            {
                string[] lines = File.ReadAllLines(file);
                var code = new System.Text.StringBuilder();
                var lineStarts = new List<int>();
                foreach (string line in lines)
                {
                    lineStarts.Add(code.Length);
                    int commentIdx = IndexOfLineComment(line);
                    code.Append(commentIdx >= 0 ? line.Substring(0, commentIdx) : line).Append('\n');
                }
                foreach (Match m in pattern.Matches(code.ToString()))
                {
                    int lineIndex = lineStarts.BinarySearch(m.Index);
                    if (lineIndex < 0)
                        lineIndex = ~lineIndex - 1;
                    if (lines[lineIndex].Contains(exemptionTag))
                        continue;
                    violations.Add(string.Format("{0}:{1}: {2}", RelativePath(sourceRoot, file).Replace('\\', '/'),
                        lineIndex + 1, lines[lineIndex].Trim()));
                }
            }
            Assert.AreEqual(0, violations.Count,
                "A user-facing exception (not a programming defect) has a string-literal message. " +
                "Put the message in the project's .resx, or throw a defect type such as " +
                "InvalidOperationException if the user cannot act on it:\n" + string.Join("\n", violations));
        }

        /// <summary>
        /// A command-line argument is written from its declaration, never retyped. Two checks:
        /// <list type="bullet">
        /// <item>No string literal in any Osprey .cs file - product or test - names an argument
        /// OspreyCommandArgs declares (<c>"--task X"</c>, <c>@"--fdrbench-pass 1"</c>). Use the
        /// typed instance (<c>OspreyCommandArgs.ARG_TASK + name</c>, <c>ARG_X.ArgumentText</c>)
        /// or, below the executable, <c>OspreyArgNames.Text</c>, so a renamed argument cannot
        /// leave a stale spelling behind. A flag the parser does NOT declare (a retired or bogus
        /// one a test feeds in to see it refused) is not matched.</item>
        /// <item>No English .resx value contains a flag or a file extension. A translator could
        /// translate or drop either, and the user has to type or match it exactly, so it goes in
        /// as a <c>{N}</c> argument (docs/21-user-facing-text.md).</item>
        /// </list>
        /// For a genuine exception add an inline comment beginning <c>// Arg literal OK:</c>.
        /// </summary>
        [TestMethod]
        public void TestArgumentTextComesFromArguments()
        {
            string sourceRoot = FindOspreySourceRoot();
            const string exemptionTag = "// Arg literal OK:";
            var names = OspreyCommandArgs.AllArguments.Select(a => Regex.Escape(a.Name)).ToList();
            Assert.IsTrue(names.Count > 0, "OspreyCommandArgs declares no arguments");
            var knownArg = new Regex(@"(?<![\w-])" + Regex.Escape(OspreyArgNames.PREFIX) +
                                     "(" + string.Join("|", names) + @")(?![\w-])");
            var violations = new List<string>();
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                string rel = RelativePath(sourceRoot, file).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/"))
                    continue;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int commentIdx = IndexOfLineComment(line);
                    string codePart = commentIdx >= 0 ? line.Substring(0, commentIdx) : line;
                    if (line.Contains(exemptionTag))
                        continue;
                    foreach (string literal in StringLiterals(codePart))
                    {
                        var match = knownArg.Match(literal);
                        if (match.Success)
                        {
                            violations.Add(string.Format("{0}:{1}: '{2}' in {3}", rel, i + 1, match.Value, line.Trim()));
                            break;
                        }
                    }
                }
            }

            // Flags (long or short) and file extensions, the tokens a user types or matches. A
            // hyphen after a format hole is a compound word ("{0}-fold"), not a flag.
            var resxToken = new Regex(@"(?<![\w}-])--?[a-z][a-z0-9-]*|(?<![\w{}])\.(?:[\w-]+\.)*" +
                                      @"(?:blib|elib|tsv|csv|parquet|bin|json|task|mzML|sky|libcache|html|sln)\b");
            foreach (var file in EnumerateEnglishResxFiles(sourceRoot))
            {
                var resxRoot = XDocument.Load(file).Root;
                Assert.IsNotNull(resxRoot, file);
                foreach (var data in resxRoot.Elements("data"))
                {
                    string value = (string) data.Element("value") ?? string.Empty;
                    var match = resxToken.Match(value);
                    if (match.Success)
                    {
                        violations.Add(string.Format("{0} {1}: '{2}' in \"{3}\"",
                            Path.GetFileName(file), (string) data.Attribute("name"), match.Value, value));
                    }
                }
            }

            Assert.AreEqual(0, violations.Count,
                "Command-line argument or file-name text typed by hand. In code, use the typed " +
                "OspreyCommandArgs.ARG_* instance (or OspreyArgNames below the executable); in a " +
                "resource, make the flag or extension a {N} argument supplied from its constant:\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        /// Source files are UTF-8 WITHOUT a byte order mark, as in Skyline's CodeInspection
        /// (<c>InspectUtf8Bom</c>). A BOM is invisible in an editor but churns diffs, trips Unix
        /// tools and is easy to add by accident: a script writing Python's <c>utf-8-sig</c> or
        /// .NET's <c>Encoding.UTF8</c> adds one, which is how the Osprey .resx files and several .cs
        /// files acquired theirs. Now that the tree holds Japanese and Chinese .resx, an editor
        /// "helpfully" saving with a BOM is more likely still. Like Skyline's inspection, this one
        /// removes each BOM it finds (keeping the file timestamps) and then fails, so the fix is
        /// to review and commit the rewritten files.
        /// </summary>
        [TestMethod]
        public void TestNoUtf8Bom()
        {
            string sourceRoot = FindOspreySourceRoot();
            var fixedFiles = new List<string>();
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string rel = RelativePath(sourceRoot, file).Replace('\\', '/');
                if (rel.Split('/').Any(part => BOM_SKIPPED_DIRECTORIES.Contains(part, StringComparer.OrdinalIgnoreCase)))
                    continue;
                if (!BOM_CHECKED_EXTENSIONS.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                    continue;
                if (RemoveUtf8Bom(file))
                    fixedFiles.Add(rel);
            }

            Assert.AreEqual(0, fixedFiles.Count,
                "Found and removed a UTF-8 byte order mark from these files. Review the change with " +
                "'git diff' and commit it; source files are UTF-8 without a BOM:\n" +
                string.Join("\n", fixedFiles));
        }

        private static readonly string[] BOM_CHECKED_EXTENSIONS =
        {
            ".cs", ".resx", ".csproj", ".sln", ".props", ".targets", ".DotSettings", ".config", ".xml",
            ".xsd", ".wxs", ".manifest", ".json", ".md", ".html", ".tsv", ".txt", ".ps1", ".bat", ".sh",
            ".py", ".jam"
        };

        private static readonly string[] BOM_SKIPPED_DIRECTORIES = { "bin", "obj", "TestResults", ".vs" };

        /// <summary>
        /// Strips a leading UTF-8 BOM from <paramref name="path"/>, preserving its timestamps, and
        /// returns true when there was one.
        /// </summary>
        private static bool RemoveUtf8Bom(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 3 || bytes[0] != 0xEF || bytes[1] != 0xBB || bytes[2] != 0xBF)
                return false;
            var creationTime = File.GetCreationTimeUtc(path);
            var lastWriteTime = File.GetLastWriteTimeUtc(path);
            var withoutBom = new byte[bytes.Length - 3];
            Array.Copy(bytes, 3, withoutBom, 0, withoutBom.Length);
            File.WriteAllBytes(path, withoutBom);
            File.SetCreationTimeUtc(path, creationTime);
            File.SetLastWriteTimeUtc(path, lastWriteTime);
            return true;
        }

        /// <summary>
        /// No file is written with an encoding that emits a UTF-8 BOM, as in Skyline's CodeInspection
        /// rule for <c>Encoding.UTF8</c>. <c>Encoding.UTF8</c> carries a BOM preamble, so passing it to
        /// a file writer puts a BOM at the start of the file - invisible in an editor, but a reader
        /// comparing the first field of a TSV header or the start of a JSON intermediate file sees
        /// three extra bytes. The .NET defaults (<c>new StreamWriter(path)</c>,
        /// <c>File.WriteAllText(path, text)</c>) already write UTF-8 without a BOM, so the fix is
        /// to drop the argument or use <c>new UTF8Encoding(false)</c>. Also caught: the other ways
        /// to ask for a BOM (<c>new UTF8Encoding(true)</c>, <c>Encoding.GetEncoding("utf-8")</c>,
        /// <c>XmlWriterSettings { Encoding = Encoding.UTF8 }</c>). <c>Encoding.UTF8.GetBytes</c> and
        /// reading with <c>Encoding.UTF8</c> never write a preamble and are not matched. Test code is
        /// included; a test that writes a BOM on purpose (to exercise a reader) adds an inline comment
        /// beginning <c>// UTF8 BOM OK:</c>.
        /// </summary>
        [TestMethod]
        public void TestNoBomWritingEncoding()
        {
            string sourceRoot = FindOspreySourceRoot();
            var violations = new List<string>();
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                string rel = RelativePath(sourceRoot, file).Replace('\\', '/');
                if (rel.Split('/').Any(part => BOM_SKIPPED_DIRECTORIES.Contains(part, StringComparer.OrdinalIgnoreCase)))
                    continue;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Contains(@"// UTF8 BOM OK:"))
                        continue;
                    int comment = IndexOfLineComment(line);
                    string code = comment >= 0 ? line.Substring(0, comment) : line;
                    if (code.TrimStart().StartsWith(@"///") || code.TrimStart().StartsWith(@"*"))
                        continue;
                    if (BOM_WRITING_ENCODING.IsMatch(code))
                        violations.Add(string.Format(@"{0}:{1}: {2}", rel, i + 1, line.Trim()));
                }
            }

            Assert.AreEqual(0, violations.Count,
                "A file writer is given an encoding that writes a UTF-8 BOM. Encoding.UTF8 includes a BOM; " +
                "drop the argument (the .NET default is UTF-8 without BOM) or use 'new UTF8Encoding(false)':\n" +
                string.Join("\n", violations));
        }

        /// <summary>
        /// Skyline's pattern (a writer call with <c>Encoding.UTF8</c> on the same line, not
        /// <c>UTF8Encoding</c>) plus the Append* writers and the other BOM-emitting encodings.
        /// </summary>
        private static readonly Regex BOM_WRITING_ENCODING = new Regex(
            @"(new XmlTextWriter|File\.WriteAllText|File\.WriteAllLines|File\.AppendAllText|File\.AppendAllLines|\.SaveAsXml|new StreamWriter)\(.*Encoding\.UTF8[^E]" +
            @"|Encoding\s*=\s*Encoding\.UTF8\b" +
            @"|new (System\.Text\.)?UTF8Encoding\(\s*true" +
            @"|Encoding\.GetEncoding\(\s*@?""utf-?8""");

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

        /// <summary>
        /// The contents of the string literals on one line of code (comments already removed):
        /// regular, verbatim and interpolated. A literal left open at the end of the line (a
        /// multi-line verbatim string) yields what the line holds of it. Character literals are
        /// skipped so a <c>'"'</c> does not open a string.
        /// </summary>
        private static IEnumerable<string> StringLiterals(string code)
        {
            int i = 0;
            while (i < code.Length)
            {
                char c = code[i];
                if (c == '\'')
                {
                    // Character literal: skip to its closing quote, honoring one escape.
                    i++;
                    if (i < code.Length && code[i] == '\\')
                        i++;
                    int close = code.IndexOf('\'', Math.Min(i + 1, code.Length));
                    i = close < 0 ? code.Length : close + 1;
                    continue;
                }
                if (c != '"')
                {
                    i++;
                    continue;
                }
                bool verbatim = (i > 0 && code[i - 1] == '@') || (i > 1 && code[i - 1] == '$' && code[i - 2] == '@');
                var sb = new System.Text.StringBuilder();
                i++;
                while (i < code.Length)
                {
                    char s = code[i];
                    if (verbatim && s == '"' && i + 1 < code.Length && code[i + 1] == '"')
                    {
                        sb.Append('"');
                        i += 2;
                        continue;
                    }
                    if (!verbatim && s == '\\' && i + 1 < code.Length)
                    {
                        sb.Append(s).Append(code[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (s == '"')
                        break;
                    sb.Append(s);
                    i++;
                }
                i++;
                yield return sb.ToString();
            }
        }

        private static IEnumerable<string> EnumerateEnglishResxFiles(string root)
        {
            var translated = new Regex(@"\.[a-z]{2}(-[A-Za-z]+)?\.resx$", RegexOptions.IgnoreCase);
            foreach (var file in Directory.EnumerateFiles(root, "*.resx", SearchOption.AllDirectories))
            {
                string rel = RelativePath(root, file).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/") || translated.IsMatch(file))
                    continue;
                yield return file;
            }
        }
    }
}
