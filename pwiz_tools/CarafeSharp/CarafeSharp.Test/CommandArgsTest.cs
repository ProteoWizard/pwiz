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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.CommandLine;
using CarafeArgument = pwiz.Common.CommandLine.Argument<pwiz.CarafeSharp.Proteome.CarafeCommandLine>;
using CarafeArgumentGroup = pwiz.Common.CommandLine.ArgumentGroup<pwiz.CarafeSharp.Proteome.CarafeCommandLine>;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The declarative <see cref="CarafeCommandLine"/>: every Carafe 2.2.0 option declared,
    /// grouped and described (drift killer), Carafe's grammar and the standard one reaching the
    /// same declarations, and the help generated from them in each form. What each option DOES
    /// is covered by the settings tests (LibraryCommandLineTest, EntrapmentFastaBuilderTest,
    /// OspreyTrainingSetTest) and the executable's by CommandLineErrorTest.
    /// </summary>
    [TestClass]
    public class CommandArgsTest
    {
        // CarafeCommandLine renders the help at 78 columns.
        private const int HELP_WIDTH = 78;

        /// <summary>
        /// Carafe 2.2.0 (origin/main) AIGear's options, plus CarafeSharp's own (-pretrained,
        /// -model, -model_info, -activation, -analyzer, -rt_model, -rt_align, -rt_select,
        /// -rt_reference, -log_file, -timestamp, -memstamp), in Carafe's spelling. The declarations
        /// must cover exactly these - spelled the standard way, an underscore a hyphen - so a Carafe
        /// command line is accepted whole and nothing is declared that neither tool knows.
        /// </summary>
        private const string CARAFE_OPTIONS =
            @"i ms fixMod varMod maxVar db o pairing_manifest itol itolu sg nf na fdr cor " +
            @"ptm_site_prob ptm_site_qvalue min_mz min_n enzyme decoy_prefix miss_c minLength " +
            @"maxLength min_pep_mz max_pep_mz min_pep_charge max_pep_charge lf_type lf_format " +
            @"lf_frag_mz_min lf_frag_mz_max lf_top_n_frag lf_min_n_frag lf_frag_n_min rf_rt_win " +
            @"rt_win_offset rt_max data_type build_entrapment_fasta manifest entrapment_db " +
            @"entrapment_ratio entrapment_seed decoy_seed reconcile_manifest predicted_library " +
            @"build_koina_library koina_url koina_ms2_model koina_rt_model nce_ms n_ion_min " +
            @"c_ion_min nce ms_instrument device se mode tf seed python mod2mass user_var_mods " +
            @"model_dir ms2_model verbose ai_version pretrained model model_info activation analyzer rt_model rt_align " +
            @"rt_select rt_reference library_contract " +
            @"printPTM nm cs ez skyline valid use_all_peaks I2L clip_n_m rf xic export_mgf " +
            @"no_masking no_similarity_gate ignore_pairing_errors entrapment no_decoys mz_filter " +
            @"y1 fast ccs torch_compile help " +
            @"log_file timestamp memstamp";

        [TestMethod]
        public void TestEveryArgIsGroupedAndDescribed()
        {
            var groups = CarafeCommandLine.UsageBlocks.OfType<CarafeArgumentGroup>().ToList();
            var seen = new Dictionary<string, int>();
            foreach (var group in groups)
            {
                foreach (var arg in group.Args)
                {
                    seen.TryGetValue(arg.Name, out int count);
                    seen[arg.Name] = count + 1;
                }
            }

            // Every declared argument (every static CarafeArgument field, listed or not) is in
            // exactly one group; AllArguments is built FROM the groups, hence the reflection.
            var declared = typeof(CarafeCommandLine)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => typeof(CarafeArgument).IsAssignableFrom(f.FieldType))
                .Select(f => (CarafeArgument)f.GetValue(null))
                .ToList();
            Assert.AreEqual(CarafeCommandLine.AllArguments.Count(), declared.Count, @"Every declared argument must be in a help group");
            foreach (var arg in declared)
            {
                seen.TryGetValue(arg.Name, out int groupCount);
                Assert.AreEqual(1, groupCount, string.Format(@"Argument {0} must be in exactly one group", arg.Name));
            }

            // Exactly Carafe's options, by name, each declared in the standard spelling.
            var expected = CARAFE_OPTIONS.Split(' ').Select(CommandLineGrammar.StandardName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            var declaredNames = declared.Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expected, declaredNames);
            Assert.AreEqual(@"-h", CarafeCommandLine.ARG_HELP.ShortArgumentText);

            // Every argument the modes read has usage text under the key derived from its name;
            // a compatibility option may fall back to the shared line. No orphan keys.
            var compatibility = new HashSet<string>(CarafeCommandLine.CompatibilityArguments.Select(a => a.Name));
            var usageKeys = new HashSet<string>();
            foreach (var arg in declared)
            {
                string key = CarafeCommandLine.UsageKey(arg.Name);
                string text = CarafeCommandArgUsage.ResourceManager.GetString(key);
                if (text != null)
                    usageKeys.Add(key);
                else
                    Assert.IsTrue(compatibility.Contains(arg.Name), string.Format(@"Argument {0} has no usage text {1} in CarafeCommandArgUsage.resx", arg.Name, key));
                Assert.IsFalse(string.IsNullOrEmpty(ArgUsage.Provider.GetDescription(arg.Name)), string.Format(@"Argument {0} has no description", arg.Name));
            }
            var resourceSet = CarafeCommandArgUsage.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            Assert.IsNotNull(resourceSet);
            foreach (DictionaryEntry entry in resourceSet)
                Assert.IsTrue(usageKeys.Contains((string)entry.Key), string.Format(@"CarafeCommandArgUsage.resx key {0} matches no argument", entry.Key));
        }

        /// <summary>
        /// Carafe's grammar on the shared parser: the single-dash and the standard spelling reach
        /// one declaration, a value follows or is inline, a prefixed token is a value unless it
        /// names an option, the first of a repeat wins, a stray word is ignored, and an unknown
        /// option or a flag given a value is refused. The settings reached are the same objects
        /// LibraryCommandLineTest checks in detail.
        /// </summary>
        [TestMethod]
        public void TestCarafeGrammar()
        {
            var dash = CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-nce", @"30" });
            var standard = CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", @"--nce", @"30" });
            var inline = CarafeCommandLine.Parse(new[] { @"-db=x.fasta", @"--nce=30" });
            foreach (var commandLine in new[] { dash, standard, inline })
            {
                Assert.AreEqual(CarafeCommandMode.predict_library, commandLine.Mode);
                Assert.AreEqual(@"x.fasta", commandLine.LibrarySettings.Database);
                Assert.AreEqual(30.0, commandLine.LibrarySettings.Nce);
            }
            // The command line as given is kept for the library's LibMetadata (carafe.command).
            Assert.AreEqual(@"CarafeSharp -db x.fasta -nce 30", dash.LibrarySettings.CommandLine);
            Assert.AreEqual(@"CarafeSharp -db=x.fasta --nce=30", inline.LibrarySettings.CommandLine);
            // The declarations spell themselves the standard way, as Osprey's do, and build
            // tokens that way; -h is the one short alias.
            Assert.AreEqual(@"--db", CarafeCommandLine.ARG_DB.ArgumentText);
            Assert.AreEqual(@"--db x.fasta", CarafeCommandLine.ARG_DB + @"x.fasta");
            Assert.AreEqual(@"--tf ms2", CarafeCommandLine.ARG_TF + @"ms2");
            Assert.AreEqual(@"--help", CarafeCommandLine.ARG_HELP.ArgumentText);
            Assert.ThrowsException<ValueInvalidException>(() => CarafeCommandLine.ARG_TF + @"bogus");
            // A name with words is hyphenated in the help and the tokens, and Carafe's underscore
            // spelling names the same declaration in either dash form, so a recorded Carafe command
            // line runs unchanged. So does its camel case: -maxVar is --max-var. A separator where
            // the name has none, or one too many, matches nothing.
            Assert.AreEqual(@"--miss-c", CarafeCommandLine.ARG_MISS_C.ArgumentText);
            Assert.AreEqual(@"--lf-type blib", CarafeCommandLine.ARG_LF_TYPE + @"blib");
            foreach (var spelling in new[] { @"--miss-c", @"-miss-c", @"-miss_c", @"--miss_c" })
                Assert.AreEqual(3, CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", spelling, @"3" }).LibrarySettings.Digest.MaxMissedCleavages, spelling);
            Assert.AreEqual(@"--max-var", CarafeCommandLine.ARG_MAX_VAR.ArgumentText);
            Assert.AreEqual(@"--i2l", CarafeCommandLine.ARG_I2L.ArgumentText);
            foreach (var spelling in new[] { @"--max-var", @"-maxVar", @"-max_Var", @"--MaxVar" })
                Assert.AreEqual(3, CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", spelling, @"3" }).LibrarySettings.Modifications.MaxVariableModifications, spelling);
            Assert.IsTrue(CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", @"-I2L" }).LibrarySettings.Digest.ConvertIToL);
            // --enzyme takes the index Carafe takes or any name in the table, in any case.
            foreach (var enzyme in new[] { @"7", @"Lys-C", @"lys-c" })
                Assert.AreEqual(7, CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", @"--enzyme", enzyme }).LibrarySettings.Digest.EnzymeIndex, enzyme);
            Assert.AreEqual(@"Lys-C", EnzymeTable.GetByIndex(7).Name);
            Assert.AreEqual(@"blib", CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-lf_type=blib" }).LibrarySettings.LibraryFormat);
            Assert.AreEqual(@"-miss-c-", Assert.ThrowsException<UnknownArgumentException>(() => CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", @"-miss-c-", @"3" })).ArgumentText);
            Assert.AreEqual(@"-maxvar", Assert.ThrowsException<UnknownArgumentException>(() => CarafeCommandLine.Parse(new[] { @"--db", @"x.fasta", @"-maxvar", @"3" })).ArgumentText);

            // A value may start with a dash when it names no option: -mode - is Carafe's default
            // mode, and a negative seed is a number (refused later, by training, as Carafe does).
            Assert.AreEqual(CarafeCommandMode.predict_library, CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-mode", @"-" }).Mode);
            var negative = Assert.ThrowsException<ArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-i", @"a.training.parquet", @"-seed", @"-1" }));
            StringAssert.Contains(negative.Message, @"--seed must not be negative");
            // But a declared option ends the value: the value is missing, not "-o", and the
            // message names the option as typed.
            var missing = Assert.ThrowsException<ValueMissingException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"-o", @"out" }));
            Assert.AreSame(CarafeCommandLine.ARG_DB, missing.Argument);
            StringAssert.Contains(missing.Message, @"-db requires");
            StringAssert.Contains(Assert.ThrowsException<ValueMissingException>(() => CarafeCommandLine.Parse(new[] { @"--db" })).Message, @"--db requires");
            Assert.ThrowsException<ValueMissingException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-o" }));
            Assert.ThrowsException<ValueMissingException>(() => CarafeCommandLine.Parse(new[] { @"-db=" }));

            // First value wins; a stray word is ignored; a flag repeated is one flag.
            var repeated = CarafeCommandLine.Parse(new[] { @"-db", @"first.fasta", @"stray", @"-db", @"second.fasta", @"-fast", @"-fast" });
            Assert.AreEqual(@"first.fasta", repeated.LibrarySettings.Database);
            Assert.IsTrue(repeated.LibrarySettings.Fast);

            // An unknown option, in either spelling, and a flag given a value are refused with
            // the option as typed.
            var unknown = Assert.ThrowsException<UnknownArgumentException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-bogus" }));
            Assert.AreEqual(@"-bogus", unknown.ArgumentText);
            StringAssert.Contains(unknown.Message, @"Unrecognized option: -bogus");
            Assert.AreEqual(@"--bogus=1", Assert.ThrowsException<UnknownArgumentException>(() => CarafeCommandLine.Parse(new[] { @"--bogus=1" })).ArgumentText);
            var unexpected = Assert.ThrowsException<ValueUnexpectedException>(() => CarafeCommandLine.Parse(new[] { @"-db", @"x.fasta", @"-fast=true" }));
            Assert.AreSame(CarafeCommandLine.ARG_FAST, unexpected.Argument);

            // -h is Carafe's help; -help and --help are the same declaration, and a value picks the format.
            foreach (var help in new[] { @"-h", @"-help", @"--help" })
            {
                var commandLine = CarafeCommandLine.Parse(new[] { help });
                Assert.AreEqual(CarafeCommandMode.help, commandLine.Mode, help);
                Assert.IsNull(commandLine.HelpFormat, help);
            }
            Assert.AreEqual(@"html", CarafeCommandLine.Parse(new[] { @"-h", @"html" }).HelpFormat);
            Assert.AreEqual(@"sections", CarafeCommandLine.Parse(new[] { @"-h=sections" }).HelpFormat);
            // The optional value does not swallow the next option.
            Assert.AreEqual(CarafeCommandMode.help, CarafeCommandLine.Parse(new[] { @"-h", @"-db", @"x.fasta" }).Mode);
        }

        [TestMethod]
        public void TestHelpRendering()
        {
            var groups = CarafeCommandLine.UsageBlocks.OfType<CarafeArgumentGroup>().ToList();

            // Default (no format): unicode tables, every group title and every argument present,
            // each line within the table width.
            string defaultHelp = CarafeCommandLine.BuildUsage(null);
            foreach (var group in groups)
                StringAssert.Contains(defaultHelp, group.Title);
            foreach (var arg in CarafeCommandLine.AllArguments)
                StringAssert.Contains(defaultHelp, arg.ArgumentText);
            StringAssert.Contains(defaultHelp, ArgUsage.Provider.ArgumentHeader);
            Assert.IsTrue(defaultHelp.Contains('│') || defaultHelp.Contains('─'), @"default help should use unicode box-drawing borders");
            foreach (var line in defaultHelp.Split('\n').Select(l => l.TrimEnd('\r')))
                Assert.IsTrue(ConsoleTable.DisplayWidth(line) <= HELP_WIDTH, string.Format(@"help line is {0} columns wide: {1}", ConsoleTable.DisplayWidth(line), line));
            Assert.AreEqual(defaultHelp, CarafeCommandLine.BuildUsage(@"unicode"));

            // ascii on request: lower-128 borders only.
            string ascii = CarafeCommandLine.BuildUsage(@"ascii");
            StringAssert.Contains(ascii, CarafeCommandLine.ARG_DB.ArgumentText);
            Assert.IsTrue(ascii.Contains('+'), @"ascii help should use '+' corner borders");
            Assert.IsFalse(ascii.Contains('│') || ascii.Contains('─'), @"ascii help must not contain unicode box-drawing characters");

            // sections: one section title per line, nothing else; a section filter: that group.
            string sections = CarafeCommandLine.BuildUsage(@"sections");
            CollectionAssert.AreEqual(groups.Select(g => g.Title).ToArray(),
                sections.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));
            string filtered = CarafeCommandLine.BuildUsage(@"Models");
            StringAssert.Contains(filtered, CarafeCommandLine.ARG_MODEL_DIR.ArgumentText);
            Assert.IsFalse(filtered.Contains(CarafeCommandLine.ARG_DB.ArgumentText), @"section filter should show only the matched group");
            StringAssert.Contains(CarafeCommandLine.BuildUsage(@"NoSuchSection"), @"No help section matching 'NoSuchSection'");

            // html: a document with a table per group.
            string html = CarafeCommandLine.GenerateUsageHtml();
            StringAssert.Contains(html, @"<html>");
            StringAssert.Contains(html, @"</html>");
            Assert.AreEqual(groups.Count, html.Split(new[] { @"<table>" }, StringSplitOptions.None).Length - 1);

            // PrintUsage writes the chosen form to the writer.
            using (var writer = new StringWriter())
            {
                CarafeCommandLine.PrintUsage(@"html", writer);
                Assert.AreEqual(html, writer.ToString());
            }
        }

        /// <summary>
        /// The published command-line usage page is generated from the declarations, so it can
        /// never drift from the code. As Osprey's TestCommandLineHelpDocumentation: regenerates
        /// Documentation/Help/en/CommandLine.html, compares it (line-ending agnostic) with the
        /// committed copy, and when they differ overwrites the committed file and fails, so the
        /// fix is to review and commit the regenerated page.
        /// </summary>
        [TestMethod]
        public void TestCommandLineHelpDocumentation()
        {
            string generated = CarafeCommandLine.GenerateUsageHtml();
            string committedPath = Path.Combine(FindSourceRoot(), @"Documentation", @"Help", @"en", @"CommandLine.html");
            string committed = File.Exists(committedPath) ? File.ReadAllText(committedPath) : null;
            if (committed != null && NormalizeEol(committed) == NormalizeEol(generated))
                return;
            string committedDir = Path.GetDirectoryName(committedPath);
            if (!string.IsNullOrEmpty(committedDir))
                Directory.CreateDirectory(committedDir);
            File.WriteAllText(committedPath, generated);
            Assert.Fail(@"The command-line help page was out of date or missing and was regenerated; review and commit: " + committedPath);
        }

        private static string NormalizeEol(string s)
        {
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>Walks up from the test assembly to the CarafeSharp.sln-bearing directory.</summary>
        private static string FindSourceRoot()
        {
            string dir = Path.GetDirectoryName(typeof(CommandArgsTest).Assembly.Location);
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, @"CarafeSharp.sln")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException(@"Could not locate the CarafeSharp source root from the test assembly location.");
        }
    }
}
