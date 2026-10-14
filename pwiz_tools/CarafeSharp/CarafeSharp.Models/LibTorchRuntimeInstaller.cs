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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Installs a <see cref="LibTorchRuntime"/> from the NuGet packages of a <see cref="LibTorchPackageSet"/>,
    /// into a folder of its own named for the package set. The caller downloads each package, so it
    /// can report progress and allow cancellation its own way, and hands it to <see cref="AddPackage"/>:
    /// <code>
    /// installer.Begin();
    /// foreach (var package in installer.PackageSet.Packages)
    ///     installer.AddPackage(package, Download(package.GetUri(LibTorchPackageSet.NUGET_PACKAGE_SOURCE)));
    /// installer.Complete();
    /// </code>
    /// The packages are unpacked into a staging folder that only <see cref="Complete"/> renames to the
    /// runtime's folder, so an interrupted install never looks installed.
    /// </summary>
    public class LibTorchRuntimeInstaller
    {
        /// <summary>
        /// Written last into a finished runtime folder, naming the package set it came from.
        /// </summary>
        public const string INSTALLED_FILE_NAME = @"libtorch-runtime.txt";

        private const string STAGING_SUFFIX = @".partial";
        private const string NATIVE_FOLDER = @"native";
        private const string RUNTIMES_FOLDER = @"runtimes";
        private const string FRAGMENTS_FOLDER = @"fragments";
        private const string FRAGMENT_EXTENSION = @".fragment";
        private const string SHA_EXTENSION = @".sha";

        public LibTorchRuntimeInstaller(LibTorchPackageSet packageSet, string runtimesDirectory)
        {
            PackageSet = packageSet;
            RuntimesDirectory = runtimesDirectory;
        }

        public LibTorchPackageSet PackageSet { get; }

        /// <summary>
        /// The folder that holds installed runtimes, each in a folder named for its package set.
        /// </summary>
        public string RuntimesDirectory { get; }

        public string RuntimeDirectory => Path.Combine(RuntimesDirectory, PackageSet.Name);

        public bool IsInstalled => File.Exists(Path.Combine(RuntimeDirectory, INSTALLED_FILE_NAME));

        public LibTorchRuntime Runtime => new LibTorchRuntime(RuntimeDirectory);

        private string StagingDirectory => RuntimeDirectory + STAGING_SUFFIX;

        /// <summary>
        /// Starts an install, discarding anything an earlier, unfinished one left.
        /// </summary>
        public void Begin()
        {
            DeleteDirectory(StagingDirectory);
            Directory.CreateDirectory(StagingDirectory);
        }

        /// <summary>
        /// Checks a downloaded package against the hash the build recorded and unpacks its native
        /// libraries, and its fragments of libraries too large for one package, into the staging folder.
        /// </summary>
        public void AddPackage(LibTorchPackage package, string packagePath)
        {
            string sha512 = ComputeHash(SHA512.Create(), packagePath, Convert.ToBase64String);
            if (!string.Equals(sha512, package.Sha512, StringComparison.Ordinal))
            {
                throw new InvalidDataException(string.Format(@"The package {0} downloaded to {1} has SHA-512 {2}, not {3}.",
                    package, packagePath, sha512, package.Sha512));
            }

            using var archive = ZipFile.OpenRead(packagePath);
            foreach (var entry in archive.Entries)
            {
                // Native libraries are under runtimes/<rid>/native/ and fragments under fragments/<rid>/native/
                var parts = entry.FullName.Split('/');
                if (parts.Length != 4 || parts[2] != NATIVE_FOLDER || string.IsNullOrEmpty(parts[3]) ||
                    (parts[0] != RUNTIMES_FOLDER && parts[0] != FRAGMENTS_FOLDER))
                {
                    continue;
                }
                entry.ExtractToFile(Path.Combine(StagingDirectory, parts[3]), true);
            }
        }

        /// <summary>
        /// Joins each library that was split into fragments back together, checks the result against
        /// the SHA-256 its package carries, and moves the staging folder into place.
        /// </summary>
        public void Complete()
        {
            foreach (var shaPath in Directory.GetFiles(StagingDirectory, @"*" + SHA_EXTENSION))
            {
                string libraryPath = shaPath.Substring(0, shaPath.Length - SHA_EXTENSION.Length);
                Restitch(libraryPath);
                string sha256 = ComputeHash(SHA256.Create(), libraryPath, bytes => Convert.ToHexString(bytes));
                string expected = File.ReadAllText(shaPath).Trim();
                if (!string.Equals(sha256, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(string.Format(@"{0} has SHA-256 {1}, not {2}.",
                        Path.GetFileName(libraryPath), sha256.ToLowerInvariant(), expected));
                }
                File.Delete(shaPath);
            }
            var orphan = Directory.GetFiles(StagingDirectory, @"*" + FRAGMENT_EXTENSION + @"*").FirstOrDefault();
            if (orphan != null)
                throw new InvalidDataException(string.Format(@"{0} has no library to join.", Path.GetFileName(orphan)));

            File.WriteAllText(Path.Combine(StagingDirectory, INSTALLED_FILE_NAME), PackageSet.Name);
            DeleteDirectory(RuntimeDirectory);
            Directory.Move(StagingDirectory, RuntimeDirectory);
            DeleteOtherVersions();
        }

        /// <summary>
        /// Removes the runtimes other versions of this package set installed, several GB each, which
        /// nothing loads once this version is installed.
        /// </summary>
        private void DeleteOtherVersions()
        {
            foreach (var directory in Directory.EnumerateDirectories(RuntimesDirectory, PackageSet.Id + @".*"))
            {
                if (!string.Equals(Path.GetFileName(directory), PackageSet.Name, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(Path.Combine(directory, INSTALLED_FILE_NAME)))
                {
                    // Renamed first, which fails while another running copy of the application has the
                    // runtime loaded, so that one is left whole for a later install to remove
                    string deletingDirectory = directory + STAGING_SUFFIX;
                    try
                    {
                        DeleteDirectory(deletingDirectory);
                        Directory.Move(directory, deletingDirectory);
                        DeleteDirectory(deletingDirectory);
                    }
                    catch (Exception x) when (x is IOException || x is UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Removes the staging folder of an install that was not completed.
        /// </summary>
        public void Abandon()
        {
            DeleteDirectory(StagingDirectory);
        }

        /// <summary>
        /// Appends a library's fragments, .fragment1, .fragment2 and so on, to its first part.
        /// </summary>
        private static void Restitch(string libraryPath)
        {
            if (!File.Exists(libraryPath))
                throw new InvalidDataException(string.Format(@"No package held {0}.", Path.GetFileName(libraryPath)));
            using var output = new FileStream(libraryPath, FileMode.Append, FileAccess.Write);
            for (int i = 1; ; i++)
            {
                string fragmentPath = libraryPath + FRAGMENT_EXTENSION + i;
                if (!File.Exists(fragmentPath))
                    break;
                using (var input = File.OpenRead(fragmentPath))
                {
                    input.CopyTo(output);
                }
                File.Delete(fragmentPath);
            }
        }

        private static string ComputeHash(HashAlgorithm algorithm, string path, Func<byte[], string> format)
        {
            using (algorithm)
            {
                using var stream = File.OpenRead(path);
                return format(algorithm.ComputeHash(stream));
            }
        }

        private static void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
    }
}
