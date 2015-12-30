/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class CommandLineImportTest : AbstractUnitTest
    {
        private const string ZIP_FILE = @"TestA\CommandLineImportTest.zip";

        [TestMethod]
        public void ConsoleImportPeptideSearchTest()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var docPath = testFilesDir.GetTestPath("blank.sky");
            var outPath = testFilesDir.GetTestPath("import-search.sky");
            var searchFilePath = testFilesDir.GetTestPath("CAexample.pep.xml");
            var fastaPath = testFilesDir.GetTestPath("bov-5-prot.fasta");

            // with mods and invalid cutoff score
            const string badCutoff = "1.1";
            var output = RunCommand("--in=" + docPath,
                "--out=" + outPath,
                "--import-search-file=" + searchFilePath,
                "--import-search-cutoff-score=" + badCutoff,
                "--import-search-add-mods",
                "--import-fasta=" + fastaPath);

            AssertEx.Contains(output, string.Format(
                Resources.CommandArgs_ParseArgsInternal_Warning__The_cutoff_score__0__is_invalid__It_must_be_a_value_between_0_and_1_, badCutoff));
            AssertEx.Contains(output, TextUtil.LineSeparate(Resources.CommandLine_ImportSearch_Creating_spectral_library_from_files_,
                Path.GetFileName(searchFilePath)));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportSearch_Adding__0__modifications_, 2));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____,
                Path.GetFileName(fastaPath)));

            var doc = ResultsUtil.DeserializeDocument(outPath);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("CAexample"));
            Assert.AreEqual(5, doc.PeptideGroupCount);
            Assert.AreEqual(26, doc.PeptideCount);
            Assert.AreEqual(26, doc.PeptideTransitionGroupCount);
            Assert.AreEqual(78, doc.PeptideTransitionCount);

            // without mods
            var outPath2 = testFilesDir.GetTestPath("import-search2.sky");
            output = RunCommand("--in=" + docPath,
                "--out=" + outPath2,
                "--import-search-file=" + searchFilePath,
                "--import-fasta=" + fastaPath);

            AssertEx.Contains(output, TextUtil.LineSeparate(Resources.CommandLine_ImportSearch_Creating_spectral_library_from_files_,
                Path.GetFileName(searchFilePath)));
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____,
                Path.GetFileName(fastaPath)));

            doc = ResultsUtil.DeserializeDocument(outPath2);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
            Assert.IsTrue(doc.Settings.MeasuredResults.ContainsChromatogram("CAexample"));
            Assert.AreEqual(5, doc.PeptideGroupCount);
            Assert.AreEqual(23, doc.PeptideCount);
            Assert.AreEqual(23, doc.PeptideTransitionGroupCount);
            Assert.AreEqual(69, doc.PeptideTransitionCount);

            // test setting cutoff and accepting mods when not importing a search
            output = RunCommand(
                "--import-search-cutoff-score=" + 0.99,
                "--import-search-add-mods");

            AssertEx.Contains(output, CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_FILE, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF));
            AssertEx.Contains(output, CommandArgs.WarnArgRequirementText(CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_FILE, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_MODS));
        }

        private static string RunCommand(params string[] inputArgs)
        {
            var consoleBuffer = new StringBuilder();
            var consoleOutput = new CommandStatusWriter(new StringWriter(consoleBuffer));
            CommandLineRunner.RunCommand(inputArgs, consoleOutput);
            return consoleBuffer.ToString();
        }
    }
}
