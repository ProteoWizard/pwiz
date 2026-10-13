/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Installs a libtorch runtime from packages laid out as the libtorch-cuda part packages are on
    /// nuget.org: native libraries under runtimes/win-x64/native, and a library too large for one
    /// package split into a primary part with its SHA-256 and fragments in further packages.
    /// </summary>
    [TestClass]
    public class LibTorchRuntimeInstallerTest
    {
        private const string PACKAGE_SET_ID = @"libtorch-test-win-x64";
        private const string VERSION = @"1.0.0";
        private const string SMALL_LIBRARY = @"small.dll";
        private const string SPLIT_LIBRARY = @"split.dll";

        public TestContext TestContext { get; set; }

        private string _folder;

        [TestInitialize]
        public void CreateFolder()
        {
            _folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"LibTorchRuntime_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void TestLibTorchRuntimeInstall()
        {
            var smallBytes = MakeBytes(1000, 1);
            var splitBytes = MakeBytes(2500, 2);
            var packageFiles = new Dictionary<string, string>
            {
                { @"part1", MakePackage(@"part1", (@"runtimes/win-x64/native/" + SMALL_LIBRARY, smallBytes)) },
                { @"part2-primary", MakePackage(@"part2-primary",
                    (@"runtimes/win-x64/native/" + SPLIT_LIBRARY, splitBytes.Take(1000).ToArray()),
                    (@"runtimes/win-x64/native/" + SPLIT_LIBRARY + @".sha", Encoding.ASCII.GetBytes(Sha256Hex(splitBytes)))) },
                { @"part2-fragment1", MakePackage(@"part2-fragment1",
                    (@"fragments/win-x64/native/" + SPLIT_LIBRARY + @".fragment1", splitBytes.Skip(1000).Take(1000).ToArray())) },
                { @"part2-fragment2", MakePackage(@"part2-fragment2",
                    (@"fragments/win-x64/native/" + SPLIT_LIBRARY + @".fragment2", splitBytes.Skip(2000).ToArray())) },
            };
            var packageSet = WritePackageSetFile(packageFiles);
            var installer = new LibTorchRuntimeInstaller(packageSet, Path.Combine(_folder, @"runtimes"));

            // A package that does not match the build's hash is refused
            installer.Begin();
            Assert.ThrowsException<InvalidDataException>(() =>
                installer.AddPackage(packageSet.Packages[0], GetPackageFile(packageFiles, packageSet.Packages[1])));
            installer.Abandon();
            Assert.IsFalse(installer.IsInstalled);

            // A runtime whose joined library does not match its SHA-256 is never installed
            installer.Begin();
            foreach (var package in packageSet.Packages.Where(package => !package.Id.EndsWith(@"fragment2")))
                installer.AddPackage(package, GetPackageFile(packageFiles, package));
            Assert.ThrowsException<InvalidDataException>(installer.Complete);
            installer.Abandon();
            Assert.IsFalse(installer.IsInstalled);

            Install(installer, packageFiles);
            Assert.IsTrue(installer.IsInstalled);
            CollectionAssert.AreEqual(smallBytes, File.ReadAllBytes(Path.Combine(installer.RuntimeDirectory, SMALL_LIBRARY)));
            CollectionAssert.AreEqual(splitBytes, File.ReadAllBytes(Path.Combine(installer.RuntimeDirectory, SPLIT_LIBRARY)));
            CollectionAssert.AreEquivalent(new[] { SMALL_LIBRARY, SPLIT_LIBRARY, LibTorchRuntimeInstaller.INSTALLED_FILE_NAME },
                Directory.GetFiles(installer.RuntimeDirectory).Select(Path.GetFileName).ToArray());
            Assert.IsFalse(Directory.Exists(installer.RuntimeDirectory + @".partial"));

            // Installing again replaces the runtime
            Install(installer, packageFiles);
            Assert.IsTrue(installer.IsInstalled);
            CollectionAssert.AreEqual(splitBytes, File.ReadAllBytes(Path.Combine(installer.RuntimeDirectory, SPLIT_LIBRARY)));
        }

        private static void Install(LibTorchRuntimeInstaller installer, Dictionary<string, string> packageFiles)
        {
            installer.Begin();
            foreach (var package in installer.PackageSet.Packages)
                installer.AddPackage(package, GetPackageFile(packageFiles, package));
            installer.Complete();
        }

        private static string GetPackageFile(Dictionary<string, string> packageFiles, LibTorchPackage package)
        {
            return packageFiles[package.Id.Substring(PACKAGE_SET_ID.Length + 1)];
        }

        /// <summary>
        /// Writes the list LibTorchPackageSet.targets writes, from the packages as built, and reads it back.
        /// </summary>
        private LibTorchPackageSet WritePackageSetFile(Dictionary<string, string> packageFiles)
        {
            var lines = new List<string> { PACKAGE_SET_ID + "\t" + VERSION };
            foreach (var packageFile in packageFiles)
            {
                using var sha512 = SHA512.Create();
                string hash = Convert.ToBase64String(sha512.ComputeHash(File.ReadAllBytes(packageFile.Value)));
                lines.Add(string.Join("\t", PACKAGE_SET_ID + @"-" + packageFile.Key, VERSION, hash, new FileInfo(packageFile.Value).Length));
            }
            string path = Path.Combine(_folder, PACKAGE_SET_ID + @".packages.txt");
            File.WriteAllText(path, string.Join("\n", lines) + "\n");
            var packageSet = LibTorchPackageSet.Read(path);
            Assert.AreEqual(PACKAGE_SET_ID + @"." + VERSION, packageSet.Name);
            Assert.AreEqual(packageFiles.Count, packageSet.Packages.Count);
            Assert.AreEqual(packageFiles.Values.Sum(file => new FileInfo(file).Length), packageSet.TotalSize);
            return packageSet;
        }

        private string MakePackage(string part, params (string Name, byte[] Bytes)[] entries)
        {
            string path = Path.Combine(_folder, PACKAGE_SET_ID + @"-" + part + @"." + VERSION + @".nupkg");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            // Entries a NuGet package has that hold no native library
            WriteEntry(archive, PACKAGE_SET_ID + @"-" + part + @".nuspec", Encoding.UTF8.GetBytes(@"<package />"));
            WriteEntry(archive, @"lib/netstandard2.0/_._/empty.txt", Array.Empty<byte>());
            foreach (var entry in entries)
                WriteEntry(archive, entry.Name, entry.Bytes);
            return path;
        }

        private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
        {
            using var stream = archive.CreateEntry(name).Open();
            stream.Write(bytes, 0, bytes.Length);
        }

        private static byte[] MakeBytes(int count, int seed)
        {
            var bytes = new byte[count];
            new Random(seed).NextBytes(bytes);
            return bytes;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
        }
    }
}
