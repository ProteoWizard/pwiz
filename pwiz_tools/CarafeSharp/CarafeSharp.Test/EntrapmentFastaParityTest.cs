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
    /// bytes. Two sources, each optional (the test is inconclusive when neither is set):
    /// <list type="bullet">
    /// <item><c>CARAFESHARP_STAGE1_REFERENCE</c>: a Carafe GUI output folder from before the
    /// similarity gate (e.g. the Stellar <c>carafe-osprey-entrapment</c> run of Carafe 2.2.0).
    /// Every <c>*.carafe.sig</c> in it records the command line that built a peptide FASTA; each is
    /// rerun with <c>-no_similarity_gate</c>, which reproduces pre-gate output.</item>
    /// <item><c>CARAFESHARP_STAGE1_BUILDS</c>: a folder of reference builds, one per subfolder,
    /// each holding <c>peptides.fasta</c>, <c>pairing.tsv</c> and <c>command.txt</c> (the rest of
    /// the Carafe command line, one argument per line, <c>-db</c> included).</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class EntrapmentFastaParityTest
    {
        public const string REFERENCE_VARIABLE = @"CARAFESHARP_STAGE1_REFERENCE";
        public const string BUILDS_VARIABLE = @"CARAFESHARP_STAGE1_BUILDS";

        private const string SIGNATURE_SUFFIX = @".carafe.sig";

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestFullFastaBuildsMatchCarafe()
        {
            string referenceDir = ReferenceDir(REFERENCE_VARIABLE);
            string buildsDir = ReferenceDir(BUILDS_VARIABLE);
            if (referenceDir == null && buildsDir == null)
                Assert.Inconclusive(@"Neither {0} nor {1} names a Carafe entrapment FASTA reference folder.", REFERENCE_VARIABLE, BUILDS_VARIABLE);

            string scratch = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"EntrapmentParity_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(scratch);
            try
            {
                var mismatches = new List<string>();
                int compared = 0;
                if (referenceDir != null)
                {
                    foreach (string signature in Directory.GetFiles(referenceDir, @"*" + SIGNATURE_SUFFIX).OrderBy(f => f, StringComparer.Ordinal))
                    {
                        CompareSignature(signature, referenceDir, scratch, mismatches);
                        compared++;
                    }
                }
                if (buildsDir != null)
                {
                    foreach (string build in Directory.GetDirectories(buildsDir).OrderBy(d => d, StringComparer.Ordinal))
                    {
                        if (!File.Exists(Path.Combine(build, @"command.txt")))
                            continue;
                        CompareBuild(build, scratch, mismatches);
                        compared++;
                    }
                }
                Assert.IsTrue(compared > 0, @"No reference builds found");
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
            // The executable, then Carafe's arguments; the GUI's paths carry no spaces.
            var args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
            string fastaName = FileName(ReplaceValue(args, @"-build_entrapment_fasta", null));
            string manifestName = FileName(ReplaceValue(args, @"-manifest", null));
            string db = ReplaceValue(args, @"-db", null);
            if (!File.Exists(db))
                ReplaceValue(args, @"-db", Path.Combine(Path.GetDirectoryName(referenceDir) ?? string.Empty, FileName(db)));
            args.Add(@"-no_similarity_gate");

            string name = FileName(signature);
            string outputDir = Path.Combine(scratch, name);
            ReplaceValue(args, @"-build_entrapment_fasta", Path.Combine(outputDir, fastaName));
            ReplaceValue(args, @"-manifest", Path.Combine(outputDir, manifestName));
            RunAndCompare(name, args, Path.Combine(referenceDir, fastaName), Path.Combine(referenceDir, manifestName),
                Path.Combine(outputDir, fastaName), Path.Combine(outputDir, manifestName), mismatches);
        }

        private void CompareBuild(string build, string scratch, List<string> mismatches)
        {
            string name = FileName(build);
            string outputDir = Path.Combine(scratch, name);
            var args = new List<string>
            {
                @"-build_entrapment_fasta", Path.Combine(outputDir, @"peptides.fasta"),
                @"-manifest", Path.Combine(outputDir, @"pairing.tsv"),
            };
            args.AddRange(File.ReadAllLines(Path.Combine(build, @"command.txt")).Where(line => line.Length > 0));
            RunAndCompare(name, args, Path.Combine(build, @"peptides.fasta"), Path.Combine(build, @"pairing.tsv"),
                Path.Combine(outputDir, @"peptides.fasta"), Path.Combine(outputDir, @"pairing.tsv"), mismatches);
        }

        private void RunAndCompare(string name, List<string> args, string expectedFasta, string expectedManifest,
            string actualFasta, string actualManifest, List<string> mismatches)
        {
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

        private static string FileName(string path)
        {
            string name = Path.GetFileName(path);
            Assert.IsFalse(string.IsNullOrEmpty(name), path);
            return name;
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

        private static string ReferenceDir(string variable)
        {
            string dir = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrEmpty(dir) || !Directory.Exists(dir) ? null : dir;
        }
    }
}
