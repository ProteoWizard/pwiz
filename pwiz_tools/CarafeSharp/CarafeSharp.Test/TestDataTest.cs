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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// How <see cref="TestData"/> finds the parity tests' data, on a package it builds in a
    /// scratch folder: which runs are inconclusive and which fail, the variables' lists, and
    /// the relocation of the paths Carafe recorded on another machine.
    /// </summary>
    [TestClass]
    public class TestDataTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestDataResolution()
        {
            string folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"TestData_" + Guid.NewGuid().ToString(@"N"));
            string savedRoot = Environment.GetEnvironmentVariable(TestData.ROOT_VARIABLE);
            string savedReference = Environment.GetEnvironmentVariable(TestData.PRETRAINED_REFERENCE_VARIABLE);
            Directory.CreateDirectory(folder);
            try
            {
                Environment.SetEnvironmentVariable(TestData.PRETRAINED_REFERENCE_VARIABLE, null);
                CheckPackagePresence(folder);
                CheckVariableLists(folder);
                CheckRelocation(Path.Combine(folder, TestData.TestFiles.Folder), folder);
            }
            finally
            {
                Environment.SetEnvironmentVariable(TestData.ROOT_VARIABLE, savedRoot);
                Environment.SetEnvironmentVariable(TestData.PRETRAINED_REFERENCE_VARIABLE, savedReference);
                Directory.Delete(folder, true);
            }
        }

        /// <summary>
        /// No package is inconclusive; a package without its manifest, or without an item, fails, and
        /// so does an item without data once another item of the test has some.
        /// </summary>
        private static void CheckPackagePresence(string root)
        {
            Environment.SetEnvironmentVariable(TestData.ROOT_VARIABLE, Path.Combine(root, @"missing"));
            Assert.ThrowsException<AssertFailedException>(() => TestData.Root);

            Environment.SetEnvironmentVariable(TestData.ROOT_VARIABLE, root);
            var item = TestData.PretrainedLibraries;
            Assert.IsFalse(item.IsAvailable);
            Assert.ThrowsException<AssertInconclusiveException>(() => TestData.InconclusiveUnlessAvailable(item));
            Assert.ThrowsException<AssertFailedException>(() => item.Resolve());

            string package = Path.Combine(root, TestData.TestFiles.Folder);
            Directory.CreateDirectory(package);
            Assert.ThrowsException<AssertFailedException>(() => item.IsAvailable);
            File.WriteAllText(Path.Combine(package, TestData.MANIFEST_FILE), string.Empty);
            Assert.IsTrue(item.IsAvailable);
            TestData.InconclusiveUnlessAvailable(item);
            Assert.ThrowsException<AssertFailedException>(() => item.Resolve());
            string entrapment = CreateFolder(package, @"stellar", @"carafe-osprey-entrapment", @"osprey_initial_library");
            string noEntrapment = CreateFolder(package, @"stellar", @"carafe-osprey", @"osprey_initial_library");
            CollectionAssert.AreEqual(new[] { entrapment, noEntrapment }, item.Resolve().ToArray());
            Assert.ThrowsException<AssertFailedException>(() => TestData.RequireFile(Path.Combine(entrapment, @"parameter.txt")));
        }

        /// <summary>A variable replaces the package's copies with its own list, each of which must exist.</summary>
        private static void CheckVariableLists(string root)
        {
            string first = CreateFolder(root, @"first");
            string second = CreateFolder(root, @"second");
            Environment.SetEnvironmentVariable(TestData.PRETRAINED_REFERENCE_VARIABLE, first + Path.PathSeparator + second);
            CollectionAssert.AreEqual(new[] { first, second }, TestData.PretrainedLibraries.Resolve().ToArray());
            Environment.SetEnvironmentVariable(TestData.PRETRAINED_REFERENCE_VARIABLE, first + Path.PathSeparator + Path.Combine(root, @"missing"));
            Assert.ThrowsException<AssertFailedException>(() => TestData.PretrainedLibraries.Resolve());
            // Set but naming no path is a mistake, not an empty list of references.
            Environment.SetEnvironmentVariable(TestData.PRETRAINED_REFERENCE_VARIABLE, @" " + Path.PathSeparator + @" ");
            Assert.IsTrue(TestData.PretrainedLibraries.IsAvailable);
            Assert.ThrowsException<AssertFailedException>(() => TestData.PretrainedLibraries.Resolve());
            // A relative path is taken from the working folder and made absolute.
            string relative = Path.GetRelativePath(Environment.CurrentDirectory, first);
            Environment.SetEnvironmentVariable(TestData.PRETRAINED_REFERENCE_VARIABLE, relative);
            CollectionAssert.AreEqual(new[] { first }, TestData.PretrainedLibraries.Resolve().ToArray());
            Environment.SetEnvironmentVariable(TestData.PRETRAINED_REFERENCE_VARIABLE, null);
        }

        /// <summary>
        /// Paths Carafe recorded on a Windows machine: inside a package always its own copy, found by
        /// the longest matching tail; outside one the recorded path when it exists.
        /// </summary>
        private static void CheckRelocation(string package, string root)
        {
            string run = CreateFolder(package, @"library-references", @"m3", @"ref", @"nocut-every50");
            string fasta = CreateFile(package, @"library-references", @"m3", @"fixtures", @"peptides_every50.fasta");
            string pairing = CreateFile(package, @"stellar", @"carafe-osprey-entrapment", @"osprey_train_db_pairing.tsv");
            string siblingPairing = CreateFile(package, @"stellar", @"carafe-osprey", @"osprey_train_db_pairing.tsv");
            string parent = Path.GetDirectoryName(run);
            Assert.AreEqual(package, TestData.FindPackageRoot(run));
            Assert.AreEqual(fasta, TestData.Relocate(@"D:/Dev/ai/.tmp/sessions/x/m3/fixtures/peptides_every50.fasta", parent));
            // Of two copies with the file's name, the one more of the recorded path matches.
            const string recordedPairing = @"D:\GitHub-Repo\osprey\stellar\carafe-osprey-entrapment\osprey_train_db_pairing.tsv";
            Assert.AreEqual(pairing, TestData.Relocate(recordedPairing, parent));
            // It wins even over a same-named copy nearer the run: the sibling run's own pairing file.
            string siblingRun = CreateFolder(package, @"stellar", @"carafe-osprey", @"osprey_initial_library");
            Assert.AreEqual(pairing, TestData.Relocate(recordedPairing, siblingRun));
            Assert.AreEqual(siblingPairing, TestData.Relocate(@"D:\elsewhere\osprey_train_db_pairing.tsv", siblingRun));
            // A file the package lacks fails, even where the recorded path exists on this machine.
            string outside = CreateFile(root, @"elsewhere", @"hela-filtered.fasta");
            Assert.ThrowsException<AssertFailedException>(() => TestData.Relocate(outside, parent));
            Assert.ThrowsException<AssertFailedException>(() => TestData.Relocate(@"..\..\elsewhere\hela-filtered.fasta", package));

            // {DATA}/ is the package's top folder, and cannot lead out of it.
            Assert.AreEqual(fasta, TestData.ExpandDataToken(@"{DATA}/library-references/m3/fixtures/peptides_every50.fasta", run));
            Assert.AreEqual(@"-enzyme", TestData.ExpandDataToken(@"-enzyme", run));
            Assert.ThrowsException<AssertFailedException>(() => TestData.ExpandDataToken(@"{DATA}/x.fasta", root));
            Assert.ThrowsException<AssertFailedException>(() => TestData.ExpandDataToken(@"{DATA}/../elsewhere/hela-filtered.fasta", run));

            // Outside a package, the recorded path when it exists, else a file of its name above the folder.
            string loose = CreateFolder(root, @"loose", @"run");
            Assert.IsNull(TestData.FindPackageRoot(loose));
            Assert.AreEqual(outside, TestData.Relocate(outside, loose));
            string beside = CreateFile(root, @"loose", @"library.fasta");
            Assert.AreEqual(beside, TestData.Relocate(@"C:\gone\library.fasta", loose));

            Assert.AreEqual(@"b.fasta", TestData.FileName(@"C:\a\b.fasta"));
            Assert.AreEqual(@"b.fasta", TestData.FileName(@"/a/b.fasta"));
        }

        private static string CreateFolder(string root, params string[] parts)
        {
            string path = Path.Combine(root, Path.Combine(parts));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string CreateFile(string root, params string[] parts)
        {
            string path = Path.Combine(root, Path.Combine(parts));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
            File.WriteAllText(path, string.Empty);
            return path;
        }
    }
}
