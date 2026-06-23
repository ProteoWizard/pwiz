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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.CommandLine;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
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
        private static OspreyConfig Parse(params string[] args)
        {
            return OspreyCommandArgs.ParseArgs(args);
        }

        [TestMethod]
        public void TestArgToConfigMapping()
        {
            // General I/O. -i is variadic (no on-disk check at parse); -l/-o just record paths.
            var io = Parse(@"-i", @"a.mzML", @"b.mzML", @"-l", @"ref.blib", @"-o", @"out.blib");
            CollectionAssert.AreEqual(new[] { @"a.mzML", @"b.mzML" }, io.InputFiles.ToArray());
            Assert.AreEqual(@"ref.blib", io.LibrarySource.Path);
            Assert.AreEqual(@"out.blib", io.OutputBlib);

            Assert.AreEqual(@"r.tsv", Parse(@"--report", @"r.tsv").OutputReport);

            // --work-dir fans out to both; an explicit --output-dir / --cache-dir overrides one.
            var work = Parse(@"--work-dir", @"w");
            Assert.AreEqual(@"w", work.OutputDir);
            Assert.AreEqual(@"w", work.CacheDir);
            var workOverrideOut = Parse(@"--work-dir", @"w", @"--output-dir", @"o");
            Assert.AreEqual(@"o", workOverrideOut.OutputDir);
            Assert.AreEqual(@"w", workOverrideOut.CacheDir);
            var workOverrideCache = Parse(@"--work-dir", @"w", @"--cache-dir", @"c");
            Assert.AreEqual(@"w", workOverrideCache.OutputDir);
            Assert.AreEqual(@"c", workOverrideCache.CacheDir);

            // Resolution + tolerance, including the unit-resolution injected defaults.
            Assert.AreEqual(ResolutionMode.HRAM, Parse(@"--resolution", @"hram").ResolutionMode);
            var unit = Parse(@"--resolution", @"unit");
            Assert.AreEqual(ResolutionMode.UnitResolution, unit.ResolutionMode);
            Assert.AreEqual(ToleranceUnit.Mz, unit.FragmentTolerance.Unit);
            Assert.AreEqual(0.5, unit.FragmentTolerance.Tolerance);
            Assert.AreEqual(ToleranceUnit.Mz, unit.PrecursorTolerance.Unit);
            Assert.AreEqual(1.0, unit.PrecursorTolerance.Tolerance);
            Assert.AreEqual(20.0, Parse(@"--fragment-tolerance", @"20").FragmentTolerance.Tolerance);
            Assert.AreEqual(ToleranceUnit.Ppm, Parse(@"--fragment-unit", @"ppm").FragmentTolerance.Unit);
            Assert.AreEqual(ToleranceUnit.Mz, Parse(@"--fragment-unit", @"mz").FragmentTolerance.Unit);
            Assert.AreEqual(ToleranceUnit.Mz, Parse(@"--fragment-unit", @"th").FragmentTolerance.Unit);
            Assert.AreEqual(ToleranceUnit.Mz, Parse(@"--fragment-unit", @"da").FragmentTolerance.Unit);
            Assert.IsFalse(Parse(@"--no-prefilter").PrefilterEnabled);

            // FDR + protein inference, including warn-and-default enums.
            Assert.AreEqual(0.05, Parse(@"--run-fdr", @"0.05").RunFdr);
            Assert.AreEqual(0.02, Parse(@"--experiment-fdr", @"0.02").ExperimentFdr);
            Assert.AreEqual(0.01, Parse(@"--protein-fdr", @"0.01").ProteinFdr);
            Assert.AreEqual(8, Parse(@"--threads", @"8").NThreads);
            Assert.AreEqual(FdrMethod.Simple, Parse(@"--fdr-method", @"simple").FdrMethod);
            Assert.AreEqual(FdrMethod.Percolator, Parse(@"--fdr-method", @"bogus").FdrMethod); // warn -> default
            Assert.AreEqual(FdrLevel.Peptide, Parse(@"--fdr-level", @"peptide").FdrLevel);
            Assert.AreEqual(FdrLevel.Precursor, Parse(@"--fdr-level", @"bogus").FdrLevel);     // warn -> default unchanged
            Assert.AreEqual(SharedPeptideMode.Razor, Parse(@"--shared-peptides", @"razor").SharedPeptides);
            Assert.AreEqual(SharedPeptideMode.All, Parse(@"--shared-peptides", @"bogus").SharedPeptides); // warn -> default

            // Decoys.
            Assert.IsTrue(Parse(@"--decoys-in-library").DecoysInLibrary);
            Assert.AreEqual(@"m.tsv", Parse(@"--decoys-in-library", @"--decoy-pairing-manifest", @"m.tsv").DecoyPairingManifestPath);
            Assert.IsTrue(Parse(@"--write-pin").WritePin);

            // Diagnostics. --task is resolved in Main, so ParseArgs alone leaves SelectedTask null
            // but must accept both --task forms without throwing.
            Assert.IsTrue(Parse(@"-d").Diagnostics);
            Assert.IsNull(Parse(@"--task=MergeNode", @"-l", @"ref.blib", @"-o", @"out.blib").SelectedTask);
        }

        [TestMethod]
        public void TestVariadicInputAccumulates()
        {
            // -i consumes the run of non-flag tokens and stops at the next flag.
            var config = Parse(@"-i", @"a.mzML", @"b.mzML", @"--threads", @"2");
            CollectionAssert.AreEqual(new[] { @"a.mzML", @"b.mzML" }, config.InputFiles.ToArray());
            Assert.AreEqual(2, config.NThreads);
        }

        [TestMethod]
        public void TestShortAliasEqualsLongForm()
        {
            CollectionAssert.AreEqual(
                Parse(@"-i", @"a.mzML").InputFiles.ToArray(),
                Parse(@"--input", @"a.mzML").InputFiles.ToArray());
            Assert.AreEqual(
                Parse(@"-l", @"ref.blib").LibrarySource.Path,
                Parse(@"--library", @"ref.blib").LibrarySource.Path);
            Assert.AreEqual(
                Parse(@"-o", @"out.blib").OutputBlib,
                Parse(@"--output", @"out.blib").OutputBlib);
            Assert.AreEqual(
                Parse(@"-d").Diagnostics,
                Parse(@"--diagnostics").Diagnostics);
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
            // ascii (default): bordered tables, every group title and a representative arg present.
            string ascii = OspreyCommandArgs.BuildUsage(null);
            foreach (var title in new[] { @"General I/O", @"Scoring & Tolerance", @"FDR & Protein Inference",
                @"Decoys", @"Distributed / HPC", @"Diagnostics & Info" })
                StringAssert.Contains(ascii, title);
            StringAssert.Contains(ascii, @"--input");
            StringAssert.Contains(ascii, @"--help");
            StringAssert.Contains(ascii, ArgUsage.Provider.ArgumentHeader);

            // sections: one section title per line, nothing else.
            string sections = OspreyCommandArgs.BuildUsage(@"sections");
            foreach (var title in new[] { @"General I/O", @"Diagnostics & Info" })
                StringAssert.Contains(sections, title);
            Assert.IsFalse(sections.Contains(@"--input"), @"sections should list titles only");

            // unicode: box-drawing borders.
            string unicode = OspreyCommandArgs.BuildUsage(@"unicode");
            Assert.IsTrue(unicode.Contains('│') || unicode.Contains('─'),
                @"unicode help should use box-drawing borders");

            // section filter: only the matching group.
            string filtered = OspreyCommandArgs.BuildUsage(@"Decoys");
            StringAssert.Contains(filtered, @"--write-pin");
            Assert.IsFalse(filtered.Contains(@"--run-fdr"), @"section filter should show only the matched group");

            // unknown section: a helpful message, no crash.
            StringAssert.Contains(OspreyCommandArgs.BuildUsage(@"NoSuchSection"), @"sections");

            // html: well-formed-ish document with a table.
            string html = OspreyCommandArgs.GenerateUsageHtml();
            StringAssert.Contains(html, @"<html>");
            StringAssert.Contains(html, @"<table>");
            StringAssert.Contains(html, @"</html>");
        }
    }
}
