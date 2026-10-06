/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Rebuilds whole entrapment FASTAs and pairing manifests Carafe wrote and requires the same
    /// bytes. Two sources (<see cref="TestData"/> finds them):
    /// <list type="bullet">
    /// <item>Carafe GUI output folders from before the similarity gate (the Carafe 2.2.0 runs of
    /// the examples, <c>CARAFESHARP_STAGE1_REFERENCE</c>). Every <c>*.carafe.sig</c> in one records
    /// the command line that built a peptide FASTA; each is rerun with <c>-no_similarity_gate</c>,
    /// which reproduces pre-gate output.</item>
    /// <item>Folders of reference builds, one per subfolder (<c>CARAFESHARP_STAGE1_BUILDS</c>),
    /// each holding <c>peptides.fasta</c>, <c>pairing.tsv</c> and <c>command.txt</c> (the rest of
    /// the Carafe command line, one argument per line, <c>-db</c> included). A path in it starting
    /// <c>{DATA}/</c> is in the test data package holding the build.</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class EntrapmentFastaParityTest
    {
        private const string SIGNATURE_SUFFIX = @".carafe.sig";
        private const string COMMAND_FILE = @"command.txt";
        private const string FASTA_FILE = @"peptides.fasta";
        private const string MANIFEST_FILE = @"pairing.tsv";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestFullFastaBuildsMatchCarafe()
        {
            CompareBuilds(TestData.Stage1References, TestData.Stage1Builds);
        }

        [TestMethod, TestCategory(TestData.ASTRAL_CATEGORY)]
        public void TestAstralFastaBuildsMatchCarafe()
        {
            CompareBuilds(TestData.AstralStage1References, null);
        }

        private void CompareBuilds(TestData.Item referenceItem, TestData.Item buildsItem)
        {
            var items = buildsItem == null ? new[] { referenceItem } : new[] { referenceItem, buildsItem };
            TestData.InconclusiveUnlessAvailable(items);
            var signatures = new List<(string Signature, string ReferenceDir)>();
            foreach (string referenceDir in referenceItem.Resolve())
            {
                var found = Directory.GetFiles(referenceDir, @"*" + SIGNATURE_SUFFIX).OrderBy(f => f, StringComparer.Ordinal).ToArray();
                Assert.IsTrue(found.Length > 0, @"No *" + SIGNATURE_SUFFIX + @" in " + referenceDir);
                signatures.AddRange(found.Select(f => (f, referenceDir)));
            }
            var builds = new List<string>();
            foreach (string buildsDir in buildsItem?.Resolve() ?? Array.Empty<string>())
            {
                var found = Directory.GetDirectories(buildsDir).Where(d => File.Exists(Path.Combine(d, COMMAND_FILE)))
                    .OrderBy(d => d, StringComparer.Ordinal).ToArray();
                Assert.IsTrue(found.Length > 0, @"No builds (subfolders with a " + COMMAND_FILE + @") in " + buildsDir);
                builds.AddRange(found);
            }

            string scratch = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"EntrapmentParity_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(scratch);
            try
            {
                var mismatches = new List<string>();
                foreach (var (signature, referenceDir) in signatures)
                    CompareSignature(signature, referenceDir, scratch, mismatches);
                foreach (string build in builds)
                    CompareBuild(build, scratch, mismatches);
                Assert.AreEqual(0, mismatches.Count, @"Differs from Carafe: " + string.Join(@"; ", mismatches));
            }
            finally
            {
                Directory.Delete(scratch, true);
            }
        }

        /// <summary>Reruns the command a Carafe GUI signature records, ungated.</summary>
        private void CompareSignature(string signature, string referenceDir, string scratch, List<string> mismatches)
        {
            string command;
            using (var json = JsonDocument.Parse(File.ReadAllText(signature)))
                command = json.RootElement.GetProperty(@"command").GetString() ?? string.Empty;
            // The executable, then Carafe's arguments; the GUI's paths carry no spaces. The paths are
            // those of the machine Carafe ran on, and only their file names are used here.
            var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
            string fastaName = TestData.FileName(ReplaceValue(args, @"-build_entrapment_fasta", null));
            string manifestName = TestData.FileName(ReplaceValue(args, @"-manifest", null));
            string db = ReplaceValue(args, @"-db", null);
            ReplaceValue(args, @"-db", TestData.RequireFile(TestData.Relocate(db, Path.GetDirectoryName(referenceDir))));
            args.Add(@"-no_similarity_gate");

            // Runs of different examples use the same signature names.
            string name = TestData.FileName(referenceDir) + @"_" + TestData.FileName(signature);
            string outputDir = Path.Combine(scratch, name);
            ReplaceValue(args, @"-build_entrapment_fasta", Path.Combine(outputDir, fastaName));
            ReplaceValue(args, @"-manifest", Path.Combine(outputDir, manifestName));
            RunAndCompare(name, args, Path.Combine(referenceDir, fastaName), Path.Combine(referenceDir, manifestName),
                Path.Combine(outputDir, fastaName), Path.Combine(outputDir, manifestName), mismatches);
        }

        private void CompareBuild(string build, string scratch, List<string> mismatches)
        {
            string name = TestData.FileName(build);
            string outputDir = Path.Combine(scratch, name);
            var args = new List<string>
            {
                @"-build_entrapment_fasta", Path.Combine(outputDir, FASTA_FILE),
                @"-manifest", Path.Combine(outputDir, MANIFEST_FILE),
            };
            args.AddRange(File.ReadAllLines(Path.Combine(build, COMMAND_FILE)).Where(line => line.Length > 0)
                .Select(line => TestData.ExpandDataToken(line, build)));
            RunAndCompare(name, args, Path.Combine(build, FASTA_FILE), Path.Combine(build, MANIFEST_FILE),
                Path.Combine(outputDir, FASTA_FILE), Path.Combine(outputDir, MANIFEST_FILE), mismatches);
        }

        private void RunAndCompare(string name, List<string> args, string expectedFasta, string expectedManifest,
            string actualFasta, string actualManifest, List<string> mismatches)
        {
            TestData.RequireFile(expectedFasta);
            TestData.RequireFile(expectedManifest);
            var settings = CarafeCommandLine.Parse(args).BuildSettings;
            var stopwatch = Stopwatch.StartNew();
            var result = new EntrapmentFastaBuilder(settings).Run();
            stopwatch.Stop();
            bool fastaMatches = FilesMatch(expectedFasta, actualFasta, out string fastaSha);
            bool manifestMatches = FilesMatch(expectedManifest, actualManifest, out string manifestSha);
            TestContext.WriteLine(@"{0}: {1} quartets in {2:F1} s; FASTA {3} {4}; manifest {5} {6}", name, result.KeptQuartets,
                stopwatch.Elapsed.TotalSeconds, fastaSha, fastaMatches ? @"match" : @"DIFFERS", manifestSha, manifestMatches ? @"match" : @"DIFFERS");
            if (!fastaMatches)
                mismatches.Add(name + @" FASTA: " + FirstDifference(expectedFasta, actualFasta));
            if (!manifestMatches)
                mismatches.Add(name + @" manifest: " + FirstDifference(expectedManifest, actualManifest));
        }

        /// <summary>Sets the value after <paramref name="option"/> when given, and returns the old one.</summary>
        private static string ReplaceValue(List<string> args, string option, string value)
        {
            int index = args.IndexOf(option);
            Assert.IsTrue(index >= 0 && index + 1 < args.Count, @"No value for " + option);
            string old = args[index + 1];
            if (value != null)
                args[index + 1] = value;
            return old;
        }

        private static bool FilesMatch(string expected, string actual, out string actualSha)
        {
            actualSha = Sha256File(actual);
            return actualSha == Sha256File(expected);
        }

        /// <summary>The first differing line, for diagnosis without a diff tool.</summary>
        private static string FirstDifference(string expected, string actual)
        {
            using (var expectedReader = new StreamReader(expected))
            using (var actualReader = new StreamReader(actual))
            {
                for (int line = 1; ; line++)
                {
                    string e = expectedReader.ReadLine();
                    string a = actualReader.ReadLine();
                    if (e == null && a == null)
                        return @"same lines, different bytes (line endings or encoding)";
                    if (e != a)
                        return string.Format(@"line {0}: expected '{1}', got '{2}'", line, e, a);
                }
            }
        }

        private static string Sha256File(string path)
        {
            using (var stream = File.OpenRead(path))
                return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
    }
}
