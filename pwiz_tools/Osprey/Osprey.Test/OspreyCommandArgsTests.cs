/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.CommandLine;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for the declarative <see cref="OspreyCommandArgs"/> model: that it reproduces the
    /// former hand-rolled switch's <see cref="OspreyConfig"/> population, that every argument is
    /// grouped and documented (drift killer), that short aliases equal their long forms, and that
    /// the generated help renders in each format. End-to-end CLI behavior is gated separately by
    /// the Stellar regression.
    /// </summary>
    [TestClass]
    public class OspreyCommandArgsTests
    {
        /// <summary>
        /// Parses tokens built from the Argument instances (<c>ARG_THREADS + 8</c>), split
        /// into argv the way a shell would by <see cref="ArgTokens.Split"/>.
        /// </summary>
        private static OspreyConfig Parse(params string[] args)
        {
            return OspreyCommandArgs.ParseArgs(ArgTokens.Split(args));
        }

        /// <summary>
        /// --input-list exists because the command line is a bounded resource: the 446-run CHS
        /// invocation is 28,621 of the 32,767 characters CreateProcess accepts, so -i hits a
        /// hard wall near 512 files. Everything here is one method because the parts are one
        /// behaviour - a listed path must be indistinguishable from an -i path afterwards.
        ///
        /// The two throw cases carry the most weight. A missing or empty list resolving to an
        /// empty input set would be a fast, successful-looking run that produces an empty blib,
        /// with the operator's whole cohort named in the file that was not read.
        /// </summary>
        [TestMethod]
        public void TestInputListExpansion()
        {
            string listPath = Path.Combine(Path.GetTempPath(),
                @"osprey-input-list-" + Guid.NewGuid().ToString(@"N") + @".txt");
            try
            {
                File.WriteAllLines(listPath, new[]
                {
                    @"# a comment line",
                    @"a.mzML",
                    string.Empty,
                    @"   b.mzML   ",   // surrounding whitespace is trimmed
                    @"# trailing comment"
                });

                // Listed paths land in InputFiles exactly as -i would have placed them.
                CollectionAssert.AreEqual(new[] { @"a.mzML", @"b.mzML" },
                    Parse(OspreyCommandArgs.ARG_INPUT_LIST + listPath).InputFiles.ToArray());

                // Composable with -i, and ORDER is preserved: -i first, then the list. Input
                // order is not decorative - FirstJoin is order-sensitive and file indices
                // follow this list.
                CollectionAssert.AreEqual(new[] { @"z.mzML", @"a.mzML", @"b.mzML" },
                    Parse(OspreyCommandArgs.ARG_INPUT + @"z.mzML", OspreyCommandArgs.ARG_INPUT_LIST + listPath).InputFiles.ToArray());

                // Composable with itself, the same way repeated -i is.
                CollectionAssert.AreEqual(new[] { @"a.mzML", @"b.mzML", @"a.mzML", @"b.mzML" },
                    Parse(OspreyCommandArgs.ARG_INPUT_LIST + listPath, OspreyCommandArgs.ARG_INPUT_LIST + listPath).InputFiles.ToArray());

                // A list that names nothing is fatal, not an empty cohort.
                File.WriteAllLines(listPath, new[] { @"# nothing but a comment", string.Empty });
                Assert.ThrowsException<InvalidDataException>(() => Parse(OspreyCommandArgs.ARG_INPUT_LIST + listPath));
            }
            finally
            {
                File.Delete(listPath);
            }

            // A missing list is fatal too - the path is the operator's whole input set.
            Assert.ThrowsException<FileNotFoundException>(
                () => Parse(OspreyCommandArgs.ARG_INPUT_LIST + Path.Combine(Path.GetTempPath(), @"osprey-no-such-list.txt")));
        }

        [TestMethod]
        public void TestArgToConfigMapping()
        {
            // General I/O. -i is variadic (no on-disk check at parse); -l/-o just record paths.
            var io = Parse(OspreyCommandArgs.ARG_INPUT + @"a.mzML", @"b.mzML", OspreyCommandArgs.ARG_LIBRARY + @"ref.blib", OspreyCommandArgs.ARG_OUTPUT + @"out.blib");
            CollectionAssert.AreEqual(new[] { @"a.mzML", @"b.mzML" }, io.InputFiles.ToArray());
            Assert.AreEqual(@"ref.blib", io.LibrarySource.Path);
            Assert.AreEqual(@"out.blib", io.OutputBlib);

            Assert.AreEqual(@"r.tsv", Parse(OspreyCommandArgs.ARG_REPORT + @"r.tsv").OutputReport);

            // --work-dir fans out to both; an explicit --output-dir / --cache-dir overrides one.
            var work = Parse(OspreyCommandArgs.ARG_WORK_DIR + @"w");
            Assert.AreEqual(@"w", work.OutputDir);
            Assert.AreEqual(@"w", work.CacheDir);
            var workOverrideOut = Parse(OspreyCommandArgs.ARG_WORK_DIR + @"w", OspreyCommandArgs.ARG_OUTPUT_DIR + @"o");
            Assert.AreEqual(@"o", workOverrideOut.OutputDir);
            Assert.AreEqual(@"w", workOverrideOut.CacheDir);
            var workOverrideCache = Parse(OspreyCommandArgs.ARG_WORK_DIR + @"w", OspreyCommandArgs.ARG_CACHE_DIR + @"c");
            Assert.AreEqual(@"w", workOverrideCache.OutputDir);
            Assert.AreEqual(@"c", workOverrideCache.CacheDir);

            // Resolution + tolerance, including the unit-resolution injected defaults.
            Assert.AreEqual(ResolutionMode.HRAM, Parse(OspreyCommandArgs.ARG_RESOLUTION + @"hram").ResolutionMode);
            var unit = Parse(OspreyCommandArgs.ARG_RESOLUTION + @"unit");
            Assert.AreEqual(ResolutionMode.UnitResolution, unit.ResolutionMode);
            Assert.AreEqual(ToleranceUnit.Mz, unit.FragmentTolerance.Unit);
            Assert.AreEqual(0.5, unit.FragmentTolerance.Tolerance);
            Assert.AreEqual(ToleranceUnit.Mz, unit.PrecursorTolerance.Unit);
            Assert.AreEqual(1.0, unit.PrecursorTolerance.Tolerance);
            Assert.AreEqual(20.0, Parse(OspreyCommandArgs.ARG_FRAGMENT_TOLERANCE + 20.0).FragmentTolerance.Tolerance);
            Assert.AreEqual(ToleranceUnit.Ppm, Parse(OspreyCommandArgs.ARG_FRAGMENT_UNIT + @"ppm").FragmentTolerance.Unit);
            Assert.AreEqual(ToleranceUnit.Mz, Parse(OspreyCommandArgs.ARG_FRAGMENT_UNIT + @"mz").FragmentTolerance.Unit);
            // th and da are accepted aliases the declared value list does not name. The token
            // builder enforces the list (Skyline's tests pin that), so an unlisted value the
            // PARSER accepts is passed as its own token, here and for the warn-and-default
            // cases below.
            Assert.AreEqual(ToleranceUnit.Mz, Parse(OspreyCommandArgs.ARG_FRAGMENT_UNIT, @"th").FragmentTolerance.Unit);
            Assert.AreEqual(ToleranceUnit.Mz, Parse(OspreyCommandArgs.ARG_FRAGMENT_UNIT, @"da").FragmentTolerance.Unit);
            Assert.IsFalse(Parse(OspreyCommandArgs.ARG_NO_PREFILTER).PrefilterEnabled);

            // FDR + protein inference, including warn-and-default enums.
            // A number renders the way Osprey parses it - invariant culture, see the static
            // constructor - so 0.05 is "0.05" under any locale.
            Assert.AreEqual(0.05, Parse(OspreyCommandArgs.ARG_RUN_FDR + 0.05).RunFdr);
            Assert.AreEqual(0.02, Parse(OspreyCommandArgs.ARG_EXPERIMENT_FDR + 0.02).ExperimentFdr);
            Assert.AreEqual(0.01, Parse(OspreyCommandArgs.ARG_PROTEIN_FDR + 0.01).ProteinFdr);
            Assert.AreEqual(8, Parse(OspreyCommandArgs.ARG_THREADS + 8).NThreads);
            // The classifier is no argument's value: the parse copies it from OSPREY_FDR_MODEL,
            // read once at process start (the parse itself is pinned in CoreTypesTest). The
            // environment cannot be varied here, so the value is passed in, and followed into the
            // training config: #4491 was gbdt silently training the SVM, and a check that the
            // config merely echoes the environment passes with the assignment deleted.
            AssertClassifierReachesTraining(FdrMethod.Gbdt, true, OspreyEnvironment.GbtMaxIterations);
            AssertClassifierReachesTraining(FdrMethod.Percolator, false, 10);
            Assert.AreEqual(FdrLevel.Peptide, Parse(OspreyCommandArgs.ARG_FDR_LEVEL + @"peptide").FdrLevel);
            Assert.AreEqual(FdrLevel.Precursor, Parse(OspreyCommandArgs.ARG_FDR_LEVEL, @"bogus").FdrLevel);     // warn -> default unchanged
            Assert.AreEqual(SharedPeptideMode.Razor, Parse(OspreyCommandArgs.ARG_SHARED_PEPTIDES + @"razor").SharedPeptides);
            Assert.AreEqual(SharedPeptideMode.All, Parse(OspreyCommandArgs.ARG_SHARED_PEPTIDES, @"bogus").SharedPeptides); // warn -> default

            // FDRBench: --fdrbench records the path, --fdrbench-per-run is a flat flag,
            // --fdrbench-pass selects the pass(es) as a bitmask (default 2; 1, 2, or both;
            // an unlisted value throws).
            Assert.AreEqual(@"fb.tsv", Parse(OspreyCommandArgs.ARG_FDRBENCH + @"fb.tsv").OutputFdrBench);
            Assert.IsTrue(Parse(OspreyCommandArgs.ARG_FDRBENCH_PER_RUN).FdrBenchPerRun);
            Assert.AreEqual(OspreyConfig.FDRBENCH_PASS_2, Parse(OspreyCommandArgs.ARG_INPUT + @"a.mzML").FdrBenchPass); // default
            Assert.AreEqual(OspreyConfig.FDRBENCH_PASS_1, Parse(OspreyCommandArgs.ARG_FDRBENCH_PASS + 1).FdrBenchPass);
            Assert.AreEqual(OspreyConfig.FDRBENCH_PASS_2, Parse(OspreyCommandArgs.ARG_FDRBENCH_PASS + 2).FdrBenchPass);
            Assert.AreEqual(OspreyConfig.FDRBENCH_PASS_1 | OspreyConfig.FDRBENCH_PASS_2,
                Parse(OspreyCommandArgs.ARG_FDRBENCH_PASS + @"both").FdrBenchPass);
            Assert.ThrowsException<ArgumentException>(() => Parse(OspreyCommandArgs.ARG_FDRBENCH_PASS, @"3"));

            // Decoys.
            Assert.IsTrue(Parse(OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY).DecoysInLibrary);
            Assert.AreEqual(@"m.tsv", Parse(OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY, OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST + @"m.tsv").DecoyPairingManifestPath);
            Assert.IsTrue(Parse(OspreyCommandArgs.ARG_WRITE_PIN).WritePin);

            // Performance: --parallel-files has an OPTIONAL value. Absent =
            // sequential default; no value = auto; <N> = explicit. The optional
            // value must not swallow the following flag.
            Assert.AreEqual(FileParallelismMode.Sequential, Parse(OspreyCommandArgs.ARG_INPUT + @"a.mzML").FileParallelism.Mode);
            Assert.AreEqual(FileParallelismMode.Auto, Parse(OspreyCommandArgs.ARG_PARALLEL_FILES).FileParallelism.Mode);
            var explicitN = Parse(OspreyCommandArgs.ARG_PARALLEL_FILES + 4).FileParallelism;
            Assert.AreEqual(FileParallelismMode.Explicit, explicitN.Mode);
            Assert.AreEqual(4, explicitN.Count);
            // 0 is the natural "off" -> sequential (consumed as a value, no stray warning).
            Assert.AreEqual(FileParallelismMode.Sequential, Parse(OspreyCommandArgs.ARG_PARALLEL_FILES + 0).FileParallelism.Mode);
            var autoThenInput = Parse(OspreyCommandArgs.ARG_PARALLEL_FILES, OspreyCommandArgs.ARG_INPUT + @"a.mzML");
            Assert.AreEqual(FileParallelismMode.Auto, autoThenInput.FileParallelism.Mode);
            CollectionAssert.AreEqual(new[] { @"a.mzML" }, autoThenInput.InputFiles.ToArray());

            // Diagnostics. --task is resolved in Main, so ParseArgs alone leaves SelectedTask null
            // but must accept both --task forms without throwing.
            Assert.IsTrue(Parse(OspreyCommandArgs.ARG_DIAGNOSTICS).Diagnostics);
            // --task=Name is the one joined form Program.Main pre-scans, so it is spelled here.
            Assert.IsNull(Parse(OspreyCommandArgs.ARG_TASK.ArgumentText + @"=" + SecondPassFdrTask.TASK_NAME, OspreyCommandArgs.ARG_LIBRARY + @"ref.blib", OspreyCommandArgs.ARG_OUTPUT + @"out.blib").SelectedTask);

            // Logging: --timestamp / --memstamp are value-less flags (default off);
            // --log-file takes a path.
            Assert.IsFalse(Parse(OspreyCommandArgs.ARG_INPUT + @"a.mzML").IsTimeStamped);
            Assert.IsFalse(Parse(OspreyCommandArgs.ARG_INPUT + @"a.mzML").IsMemStamped);
            Assert.IsNull(Parse(OspreyCommandArgs.ARG_INPUT + @"a.mzML").LogFilePath);
            Assert.IsTrue(Parse(OspreyCommandArgs.ARG_TIMESTAMP).IsTimeStamped);
            Assert.IsTrue(Parse(OspreyCommandArgs.ARG_MEMSTAMP).IsMemStamped);
            Assert.AreEqual(@"run.log", Parse(OspreyCommandArgs.ARG_LOG_FILE + @"run.log").LogFilePath);
        }

        private static void AssertClassifierReachesTraining(FdrMethod fdrModel, bool expectTrees,
            int expectMaxIterations)
        {
            var config = OspreyCommandArgs.ParseArgs(ArgTokens.Split(new[] { OspreyCommandArgs.ARG_INPUT + @"a.mzML" }),
                fdrModel);
            Assert.AreEqual(fdrModel, config.FdrMethod);
            var percConfig = PercolatorEngine.BuildProjectionPercolatorConfig(config, null, null);
            Assert.AreEqual(expectTrees, percConfig.UseGradientBoostedTrees);
            Assert.AreEqual(expectMaxIterations, percConfig.MaxIterations);
        }

        /// <summary>
        /// Every value a user can mistype must arrive at Main's parse sink as one of the three
        /// types it treats as a usage error, so the operator gets the flag's name and no stack.
        /// Numeric options used to reach int.Parse directly: `--threads bad` threw
        /// FormatException, which that filter does not name, so a typo was reported as an
        /// unhandled fatal error - "Input string was not in a correct format." over a stack
        /// through the parser. The assertions below are the contract Program.Main's
        /// `when (ex is ArgumentException || ex is FileNotFoundException || ex is InvalidDataException)`
        /// filter reads; adding a numeric option without ParseInt / ParseDouble breaks it.
        ///
        /// <para>A REMOVED argument lands there too, as any unknown one does. --fdr-method went
        /// with no alias (#4543): accepting it silently would leave a script that passes
        /// <c>--fdr-method gbdt</c> training the linear SVM with no sign it asked for anything
        /// else, the #4491 failure by another route.</para>
        /// </summary>
        [TestMethod]
        public void TestBadOptionValuesAreUsageErrors()
        {
            // --fdr-method is rejected exactly the way an argument that never existed is: same
            // exception type, same message but for the name, whatever value follows.
            const string removedArg = @"--fdr-method";
            const string neverArg = @"--no-such-argument";
            string neverMessage = Assert.ThrowsException<ArgumentException>(
                () => OspreyCommandArgs.ParseArgs(new[] { neverArg })).Message;
            foreach (var removedValue in new[] { @"gbdt", @"percolator", @"simple" })
            {
                var removed = Assert.ThrowsException<ArgumentException>(
                    () => OspreyCommandArgs.ParseArgs(new[] { removedArg, removedValue }), removedValue);
                Assert.AreEqual(neverMessage.Replace(neverArg, removedArg), removed.Message);
            }
            Assert.IsFalse(OspreyCommandArgs.AllArguments.Any(a => a.ArgumentText == removedArg),
                @"--fdr-method must not be declared, or it reappears in --help");

            var argThreads = OspreyCommandArgs.ARG_THREADS;
            foreach (var badValue in new[] { @"bad", @"1.5", @"99999999999999999999", string.Empty })
            {
                var threads = Assert.ThrowsException<ArgumentException>(
                    () => Parse(argThreads + badValue), argThreads + badValue);
                StringAssert.Contains(threads.Message, argThreads.ArgumentText);
            }

            // --parallel-files cannot reach that path with a bad value and must not: its value is
            // OPTIONAL, so the lookahead consumes only a digits-only token that fits an int and
            // otherwise leaves the token alone (auto mode). Its ParseInt is the belt to that
            // lookahead's braces, which is why only --threads is swept above.
            var argParallelFiles = OspreyCommandArgs.ARG_PARALLEL_FILES;
            Assert.AreEqual(FileParallelismMode.Auto,
                Parse(argParallelFiles + @"bad", OspreyCommandArgs.ARG_INPUT + @"a.mzML").FileParallelism.Mode);

            // The good values still parse, including the two --parallel-files spellings.
            Assert.AreEqual(8, Parse(argThreads + 8).NThreads);
            Assert.AreEqual(FileParallelismMode.Sequential, Parse(argParallelFiles + 0).FileParallelism.Mode);
            Assert.AreEqual(FileParallelismMode.Auto, Parse(argParallelFiles).FileParallelism.Mode);
            Assert.AreEqual(4, Parse(argParallelFiles + 4).FileParallelism.Count);
        }

        /// <summary>
        /// The tokens these tests hand the parser are built from the Argument instances, so
        /// the contract behind that is pinned here rather than assumed: the + operator joins
        /// with the host's separator (a space, the same one the usage text renders) and the
        /// test-side split keeps a value containing spaces whole; a number renders the way
        /// the HOST parses (Osprey: invariant; a host that leaves the provider unset gets the
        /// current culture); the same operator renders Skyline's --name=value when the host
        /// separator says so; and an argument can never be a flag's value, a value outside a
        /// fixed list, another argument's value, or
        /// null. Mutates two process-wide settings, so it must not share a process slice with
        /// another test building tokens.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestArgumentTokensFromInstances()
        {
            var argThreads = OspreyCommandArgs.ARG_THREADS;
            string flagOnly = argThreads;
            Assert.AreEqual(argThreads.ArgumentText, flagOnly);
            Assert.AreEqual(argThreads.ArgumentText + @" 8", argThreads + 8);
            Assert.AreEqual(argThreads + @"8", argThreads + 8);
            Assert.AreEqual(@"-" + OspreyCommandArgs.ARG_INPUT.ShortName, OspreyCommandArgs.ARG_INPUT.ShortArgumentText);
            Assert.IsNull(argThreads.ShortArgumentText, @"an argument without a short name has no short spelling");

            const string spacedPath = @"C:\my dir\run.log";
            Assert.AreEqual(spacedPath, Parse(OspreyCommandArgs.ARG_LOG_FILE + spacedPath).LogFilePath);

            // A comma-decimal culture built rather than looked up by name, so the assertions
            // do not depend on ICU data being present on the agent. CurrentCulture is
            // per-thread; the separator and the format provider are process-wide.
            var commaDecimal = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
            commaDecimal.NumberFormat.NumberDecimalSeparator = @",";
            var argTolerance = OspreyCommandArgs.ARG_FRAGMENT_TOLERANCE;
            string ospreySeparator = ArgUsage.ArgumentValueSeparator;
            var ospreyProvider = ArgUsage.ValueFormatProvider;
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = commaDecimal;
                // Osprey parses invariantly, so it renders invariantly whatever the thread's culture.
                Assert.AreEqual(argTolerance.ArgumentText + @" 0.5", argTolerance + 0.5);
                Assert.AreEqual(0.5, Parse(argTolerance + 0.5).FragmentTolerance.Tolerance);

                // A host that parses in the current culture leaves the provider unset and gets
                // the value the way its user would type it.
                ArgUsage.ValueFormatProvider = null;
                Assert.AreEqual(argTolerance.ArgumentText + @" 0,5", argTolerance + 0.5);
                ArgUsage.ValueFormatProvider = ospreyProvider;

                ArgUsage.ArgumentValueSeparator = @"=";
                Assert.AreEqual(argThreads.ArgumentText + @"=8", argThreads + 8);
            }
            finally
            {
                ArgUsage.ArgumentValueSeparator = ospreySeparator;
                ArgUsage.ValueFormatProvider = ospreyProvider;
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }

            Assert.ThrowsException<ValueUnexpectedException>(() => OspreyCommandArgs.ARG_TIMESTAMP + 1);
            Assert.ThrowsException<ValueInvalidException>(() => OspreyCommandArgs.ARG_FDR_LEVEL + @"bogus");
            Assert.ThrowsException<ArgumentException>(() => argThreads + OspreyCommandArgs.ARG_INPUT);
            Assert.ThrowsException<ArgumentNullException>(() => OspreyCommandArgs.ARG_LIBRARY + null);
        }

        [TestMethod]
        public void TestVariadicInputAccumulates()
        {
            // -i consumes the run of non-flag tokens and stops at the next flag.
            var config = Parse(OspreyCommandArgs.ARG_INPUT + @"a.mzML", @"b.mzML", OspreyCommandArgs.ARG_THREADS + 2);
            CollectionAssert.AreEqual(new[] { @"a.mzML", @"b.mzML" }, config.InputFiles.ToArray());
            Assert.AreEqual(2, config.NThreads);
        }

        [TestMethod]
        public void TestShortAliasEqualsLongForm()
        {
            CollectionAssert.AreEqual(
                Parse(OspreyCommandArgs.ARG_INPUT.ShortArgumentText, @"a.mzML").InputFiles.ToArray(),
                Parse(OspreyCommandArgs.ARG_INPUT + @"a.mzML").InputFiles.ToArray());
            Assert.AreEqual(
                Parse(OspreyCommandArgs.ARG_LIBRARY.ShortArgumentText, @"ref.blib").LibrarySource.Path,
                Parse(OspreyCommandArgs.ARG_LIBRARY + @"ref.blib").LibrarySource.Path);
            Assert.AreEqual(
                Parse(OspreyCommandArgs.ARG_OUTPUT.ShortArgumentText, @"out.blib").OutputBlib,
                Parse(OspreyCommandArgs.ARG_OUTPUT + @"out.blib").OutputBlib);
            Assert.AreEqual(
                Parse(OspreyCommandArgs.ARG_DIAGNOSTICS.ShortArgumentText).Diagnostics,
                Parse(OspreyCommandArgs.ARG_DIAGNOSTICS).Diagnostics);
        }

        [TestMethod]
        public void TestEveryArgIsGroupedAndDescribed()
        {
            // Drift killer: every declared argument belongs to exactly one group AND resolves a
            // non-empty description. Adding an arg without grouping/documenting it fails here.
            var groups = OspreyCommandArgs.UsageBlocks.OfType<ArgumentGroup<OspreyCommandArgs>>().ToList();

            var seen = new Dictionary<string, int>();
            foreach (var group in groups)
                foreach (var arg in group.Args)
                {
                    seen.TryGetValue(arg.Name, out int count);
                    seen[arg.Name] = count + 1;
                }

            foreach (var arg in OspreyCommandArgs.AllArguments)
            {
                Assert.AreEqual(1, seen[arg.Name], string.Format(@"Argument {0} must be in exactly one group", arg.Name));
                string description = ArgUsage.Provider.GetDescription(arg.Name);
                Assert.IsFalse(string.IsNullOrEmpty(description),
                    string.Format(@"Argument {0} has no description", arg.Name));
            }
        }

        [TestMethod]
        public void TestHelpRendering()
        {
            // Default (no format): unicode tables, like Skyline. Every group title and a
            // representative arg present, and box-drawing borders (not lower-128 ascii).
            string defaultHelp = OspreyCommandArgs.BuildUsage(null);
            foreach (var title in new[] { @"General I/O", @"Scoring & Tolerance", @"FDR & Protein Inference",
                @"Decoys", @"Performance", @"Distributed / HPC", @"Logging", @"Diagnostics & Info" })
                StringAssert.Contains(defaultHelp, title);
            StringAssert.Contains(defaultHelp, OspreyCommandArgs.ARG_INPUT.ArgumentText);
            StringAssert.Contains(defaultHelp, OspreyCommandArgs.ARG_PARALLEL_FILES.ArgumentText);
            StringAssert.Contains(defaultHelp, OspreyCommandArgs.ARG_TIMESTAMP.ArgumentText);
            StringAssert.Contains(defaultHelp, OspreyCommandArgs.ARG_HELP.ArgumentText);
            StringAssert.Contains(defaultHelp, ArgUsage.Provider.ArgumentHeader);
            Assert.IsTrue(defaultHelp.Contains('│') || defaultHelp.Contains('─'),
                @"default help should use unicode box-drawing borders");

            // "unicode" is an explicit alias for the default.
            Assert.AreEqual(defaultHelp, OspreyCommandArgs.BuildUsage(@"unicode"));

            // ascii on request: lower-128 borders only (no box-drawing).
            string ascii = OspreyCommandArgs.BuildUsage(@"ascii");
            StringAssert.Contains(ascii, OspreyCommandArgs.ARG_INPUT.ArgumentText);
            Assert.IsTrue(ascii.Contains('+'), @"ascii help should use '+' corner borders");
            Assert.IsFalse(ascii.Contains('│') || ascii.Contains('─'),
                @"ascii help must not contain unicode box-drawing characters");

            // sections: one section title per line, nothing else.
            string sections = OspreyCommandArgs.BuildUsage(@"sections");
            foreach (var title in new[] { @"General I/O", @"Diagnostics & Info" })
                StringAssert.Contains(sections, title);
            Assert.IsFalse(sections.Contains(OspreyCommandArgs.ARG_INPUT.ArgumentText), @"sections should list titles only");

            // section filter: only the matching group.
            string filtered = OspreyCommandArgs.BuildUsage(@"Decoys");
            StringAssert.Contains(filtered, OspreyCommandArgs.ARG_WRITE_PIN.ArgumentText);
            Assert.IsFalse(filtered.Contains(OspreyCommandArgs.ARG_RUN_FDR.ArgumentText), @"section filter should show only the matched group");

            // unknown section: a helpful message, no crash.
            StringAssert.Contains(OspreyCommandArgs.BuildUsage(@"NoSuchSection"), @"sections");

            // html: well-formed-ish document with a table.
            string html = OspreyCommandArgs.GenerateUsageHtml();
            StringAssert.Contains(html, @"<html>");
            StringAssert.Contains(html, @"<table>");
            StringAssert.Contains(html, @"</html>");
        }

        /// <summary>
        /// The published command-line usage page is generated from the argument declarations, so it
        /// can never silently drift from the code. This regenerates it and compares it
        /// (line-ending agnostic) against the committed copy under
        /// <c>Documentation/Help/en/CommandLine.html</c>. The test is self-updating: when they
        /// differ it overwrites the committed file with the freshly generated content and then
        /// fails, so the fix is simply to review and commit the regenerated file. (CI fails the same
        /// way, flagging an argument or generated-prose change that was not regenerated.) The
        /// per-language folder leaves room for ja / zh-CHS once the descriptions move to a .resx.
        /// </summary>
        [TestMethod]
        public void TestCommandLineHelpDocumentation()
        {
            string generated = OspreyCommandArgs.GenerateUsageHtml();
            string committedPath = Path.Combine(FindOspreySourceRoot(),
                @"Documentation", @"Help", @"en", @"CommandLine.html");

            // Compare EOL-agnostically: GenerateUsageHtml builds with Environment.NewLine, which
            // differs between the Windows (net472) and Linux (net8.0) test runs, and git may rewrite
            // the file's line endings on checkout. A content (not byte) match is what we care about,
            // and it keeps a pure EOL difference from triggering a spurious rewrite.
            string committed = File.Exists(committedPath) ? File.ReadAllText(committedPath) : null;
            if (committed != null && NormalizeEol(committed) == NormalizeEol(generated))
                return;

            // Out of date (or missing): self-heal by writing the regenerated page, then fail so the
            // developer reviews and commits it. Re-running after the commit passes.
            string committedDir = Path.GetDirectoryName(committedPath);
            if (!string.IsNullOrEmpty(committedDir))
                Directory.CreateDirectory(committedDir);
            File.WriteAllText(committedPath, generated);
            Assert.Fail(committed == null
                    ? @"Generated usage doc did not exist; wrote {0}. Review and commit it."
                    : @"Documentation/Help/en/CommandLine.html was out of date; regenerated it at {0}. Review and commit the change.",
                committedPath);
        }

        private static string NormalizeEol(string s)
        {
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// Walk up from the test assembly to the Osprey source root (the
        /// <c>Osprey.sln</c>-bearing directory). Mirrors CodeInspectionTest.
        /// </summary>
        private static string FindOspreySourceRoot()
        {
            string dir = Path.GetDirectoryName(typeof(OspreyCommandArgsTests).Assembly.Location);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, @"Osprey")) &&
                    Directory.Exists(Path.Combine(dir, @"Osprey.Test")) &&
                    File.Exists(Path.Combine(dir, @"Osprey.sln")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException(
                @"Could not locate Osprey source root from test assembly location.");
        }
    }
}
