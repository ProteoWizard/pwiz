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
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Finds the reference data the parity tests compare with, for every test that needs some.
    /// <para>
    /// The data comes in zip packages that <c>pwiz_tools/CarafeSharp/testdata.json</c> lists (copied
    /// beside this assembly). Each extracts to one folder, <c>&lt;root&gt;/&lt;package folder&gt;</c>:
    /// <c>&lt;root&gt;</c> is <c>%CARAFESHARP_TESTDATA%</c> when it is set, else
    /// <c>&lt;Downloads&gt;/Perftests</c>, where the Skyline and Osprey perf tests keep theirs, and
    /// <c>&lt;Downloads&gt;</c> is <c>%SKYLINE_DOWNLOAD_PATH%</c> or the user's Downloads folder.
    /// </para>
    /// <para>
    /// Every item a test reads also has an environment variable (<c>CARAFESHARP_CARAFE_REFERENCE</c>
    /// and the others below) that replaces the package's copies with folders of its own, several
    /// separated by <see cref="Path.PathSeparator"/>.
    /// </para>
    /// <para>
    /// Missing data fails, except when there is none at all. A test is inconclusive only when no
    /// package it reads is present and no variable for its items is set, which is what a machine
    /// without the data (and CI) sees. Otherwise every item the test reads must have data: a package
    /// without its <c>MANIFEST.sha256</c> (the zip's last entry) fails, and so does any file the test
    /// needs that is not there, with its path, an item with neither a package nor a variable, and a
    /// variable that names no path or a missing one.
    /// </para>
    /// </summary>
    public static class TestData
    {
        /// <summary>The folder the packages are extracted in, in place of <c>&lt;Downloads&gt;/Perftests</c>.</summary>
        public const string ROOT_VARIABLE = @"CARAFESHARP_TESTDATA";

        /// <summary>The Downloads folder, as the Skyline perf tests take it.</summary>
        public const string DOWNLOADS_VARIABLE = @"SKYLINE_DOWNLOAD_PATH";

        // The items' variables.
        public const string PRETRAINED_REFERENCE_VARIABLE = @"CARAFESHARP_CARAFE_REFERENCE";
        public const string FINETUNED_REFERENCE_VARIABLE = @"CARAFESHARP_CARAFE_FINETUNED";
        public const string LIBRARY_REFERENCES_VARIABLE = @"CARAFESHARP_LIBRARY_REFERENCES";
        public const string STAGE1_REFERENCE_VARIABLE = @"CARAFESHARP_STAGE1_REFERENCE";
        public const string STAGE1_BUILDS_VARIABLE = @"CARAFESHARP_STAGE1_BUILDS";
        public const string TRAINING_EXPORT_VARIABLE = @"CARAFESHARP_OSPREY_TRAINING_EXPORT";

        /// <summary>The package list, beside this assembly.</summary>
        public const string PACKAGE_LIST_FILE = @"testdata.json";

        /// <summary>The completion marker at a package's top folder: the checksums of its files, written last.</summary>
        public const string MANIFEST_FILE = @"MANIFEST.sha256";

        /// <summary>Starts a path in a package's <c>command.txt</c> files, for the package's top folder.</summary>
        public const string DATA_TOKEN = @"{DATA}/";

        /// <summary>The Downloads subfolder of the perf-test packages.</summary>
        public const string PERFTESTS_FOLDER = @"Perftests";

        /// <summary>The category of the tests that read the Astral package, which is optional.</summary>
        public const string ASTRAL_CATEGORY = @"Astral";

        /// <summary>The category of the GPU tests, which the CPU pass leaves out.</summary>
        public const string CUDA_CATEGORY = @"Cuda";

        /// <summary>Set to 1 by <c>build.ps1 -Torch cuda</c>: a GPU test without a usable GPU fails instead of being inconclusive.</summary>
        public const string REQUIRE_CUDA_VARIABLE = @"CARAFESHARP_REQUIRE_CUDA";

        // The known-folder registry value of the Downloads folder, which RegressionData.ps1 reads too.
        private const string SHELL_FOLDERS_KEY = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders";
        private const string DOWNLOADS_FOLDER_ID = @"{374DE290-123F-4565-9164-39C4925E467B}";

        // Folders in the packages.
        private const string STELLAR_ENTRAPMENT = @"stellar/carafe-osprey-entrapment";
        private const string STELLAR_NO_ENTRAPMENT = @"stellar/carafe-osprey";
        private const string ASTRAL_ENTRAPMENT = @"astral/Carafe-Osprey-entrapment";
        private const string INITIAL_LIBRARY = @"/osprey_initial_library";
        private const string NEW_LIBRARY = @"/osprey_new_library";

        private static readonly Lazy<IReadOnlyDictionary<string, Package>> PACKAGES =
            new Lazy<IReadOnlyDictionary<string, Package>>(ReadPackages);

        /// <summary>The Stellar reference runs, the stage-1 builds and the small library references.</summary>
        public static Package TestFiles
        {
            get { return GetPackage(@"testfiles"); }
        }

        /// <summary>The Astral reference run, for the tests in category Astral.</summary>
        public static Package AstralFiles
        {
            get { return GetPackage(@"astral"); }
        }

        /// <summary>Osprey's training export of the Stellar search.</summary>
        public static Package ExportFiles
        {
            get { return GetPackage(@"export"); }
        }

        /// <summary>Carafe libraries predicted with the pretrained models (<c>osprey_initial_library</c>).</summary>
        public static Item PretrainedLibraries
        {
            get
            {
                return new Item(PRETRAINED_REFERENCE_VARIABLE, TestFiles,
                    STELLAR_ENTRAPMENT + INITIAL_LIBRARY, STELLAR_NO_ENTRAPMENT + INITIAL_LIBRARY);
            }
        }

        /// <summary>
        /// Carafe fine-tuning folders (<c>osprey_new_library</c>): the training data, the fine-tuned
        /// models and their metrics, and the library predicted with them. The first is the Stellar
        /// entrapment run, whose search the training export is of.
        /// </summary>
        public static Item FineTunedLibraries
        {
            get
            {
                return new Item(FINETUNED_REFERENCE_VARIABLE, TestFiles,
                    STELLAR_ENTRAPMENT + NEW_LIBRARY, STELLAR_NO_ENTRAPMENT + NEW_LIBRARY);
            }
        }

        /// <summary>Small Carafe library runs, each with its TSV and Skyline .blib, or folders of such runs.</summary>
        public static Item LibraryReferences
        {
            get
            {
                return new Item(LIBRARY_REFERENCES_VARIABLE, TestFiles, @"library-references/carafe_main_varmod",
                    @"library-references/m3/ref/nocut-every50", @"library-references/m3/ref/trypsin-proteins");
            }
        }

        /// <summary>Carafe GUI run folders whose <c>*.carafe.sig</c> files record how their peptide FASTAs were built.</summary>
        public static Item Stage1References
        {
            get { return new Item(STAGE1_REFERENCE_VARIABLE, TestFiles, STELLAR_ENTRAPMENT, STELLAR_NO_ENTRAPMENT); }
        }

        /// <summary>Folders of entrapment FASTA builds, one per subfolder with its <c>command.txt</c>.</summary>
        public static Item Stage1Builds
        {
            get { return new Item(STAGE1_BUILDS_VARIABLE, TestFiles, @"stage1/builds"); }
        }

        /// <summary>Osprey training exports (<c>.training.parquet</c>) of the search the first fine-tuning folder trained on.</summary>
        public static Item TrainingExports
        {
            get { return new Item(TRAINING_EXPORT_VARIABLE, ExportFiles, @"stellar/Ste-2024-12-02_HeLa_4mz_sDIA_400-900_21.training.parquet"); }
        }

        public static Item AstralPretrainedLibraries
        {
            get { return new Item(null, AstralFiles, ASTRAL_ENTRAPMENT + INITIAL_LIBRARY); }
        }

        public static Item AstralFineTunedLibraries
        {
            get { return new Item(null, AstralFiles, ASTRAL_ENTRAPMENT + NEW_LIBRARY); }
        }

        public static Item AstralStage1References
        {
            get { return new Item(null, AstralFiles, ASTRAL_ENTRAPMENT); }
        }

        /// <summary>
        /// The folder the packages are extracted in: <c>%CARAFESHARP_TESTDATA%</c>, which must exist
        /// when it is set, else <c>&lt;Downloads&gt;/Perftests</c>.
        /// </summary>
        public static string Root
        {
            get
            {
                string root = Environment.GetEnvironmentVariable(ROOT_VARIABLE);
                if (string.IsNullOrEmpty(root))
                    return Path.Combine(GetDownloadsPath(), PERFTESTS_FOLDER);
                if (!Directory.Exists(root))
                    Assert.Fail(@"{0} names a folder that does not exist: {1}", ROOT_VARIABLE, root);
                return Path.GetFullPath(root);
            }
        }

        /// <summary>
        /// The user's Downloads folder, as the Skyline perf tests and Osprey's RegressionData.ps1 find it:
        /// <c>%SKYLINE_DOWNLOAD_PATH%</c>, else on Windows the known-folder value in the registry (a
        /// Downloads folder moved to another drive), else <c>Downloads</c> in the user's home folder.
        /// </summary>
        public static string GetDownloadsPath()
        {
            string path = Environment.GetEnvironmentVariable(DOWNLOADS_VARIABLE);
            if (!string.IsNullOrEmpty(path))
                return path;
            if (OperatingSystem.IsWindows())
            {
                path = ReadKnownDownloadsFolder();
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Downloads");
        }

        /// <summary>
        /// Marks the test inconclusive when none of <paramref name="items"/> has data: no variable set
        /// and no package present. That is the only case missing data does not fail.
        /// </summary>
        public static void InconclusiveUnlessAvailable(params Item[] items)
        {
            if (items.Any(i => i.IsAvailable))
                return;
            var variables = items.Where(i => i.Variable != null).Select(i => i.Variable).Distinct().ToArray();
            var packages = items.Select(i => i.Package).Distinct().ToArray();
            Assert.Inconclusive(@"No test data: extract {0} into {1}{2}.",
                string.Join(@" and ", packages.Select(p => p.Zip)), Root,
                variables.Length > 0 ? @", or set " + string.Join(@" or ", variables) : string.Empty);
        }

        /// <summary>Fails the test, naming the path, unless the file exists; returns the path.</summary>
        public static string RequireFile(string path)
        {
            if (!File.Exists(path))
                Assert.Fail(@"Missing test data file: " + path);
            return path;
        }

        /// <summary>Fails the test unless each of <paramref name="names"/> is a file in <paramref name="folder"/>.</summary>
        public static void RequireFiles(string folder, params string[] names)
        {
            foreach (string name in names)
                RequireFile(Path.Combine(folder, name));
        }

        /// <summary>
        /// Where a file a reference run recorded by its absolute path on another machine is here.
        /// <para>
        /// Inside a test data package it is always the package's copy, never the recorded location,
        /// so a package is tested as it would be on a machine without the original folders. The
        /// trailing parts of the recorded path are tried longest first, each under every folder from
        /// <paramref name="folder"/> up to the package's top folder, so a copy matching more of the
        /// path wins over a file of the same name nearer the run. Nothing outside the package is
        /// returned, and a file the package lacks fails the test.
        /// </para>
        /// <para>
        /// Outside a package (a folder an environment variable named), the recorded path is used when it
        /// exists, else the same search, up to the drive root, and the recorded path when that finds nothing.
        /// </para>
        /// Recorded paths are split at both separators, so a Windows path relocates on Linux too.
        /// </summary>
        public static string Relocate(string recordedPath, string folder)
        {
            string packageRoot = FindPackageRoot(folder);
            if (packageRoot == null && File.Exists(recordedPath))
                return recordedPath;
            // Leave out the drive or root: only the relative tail is tried under each folder.
            var parts = SplitPath(recordedPath).Where(p => p.IndexOf(':') < 0).ToArray();
            var levels = new List<string>();
            for (string dir = folder; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                levels.Add(dir);
                if (dir == packageRoot)
                    break;
            }
            for (int start = 0; start < parts.Length; start++)
            {
                foreach (string dir in levels)
                {
                    string candidate = Path.Combine(new[] { dir }.Concat(parts.Skip(start)).ToArray());
                    if (IsInPackage(candidate, packageRoot) && File.Exists(candidate))
                        return candidate;
                }
            }
            if (packageRoot != null)
                Assert.Fail(@"The test data package {0} has no copy of {1}, recorded by a run in {2}", packageRoot, recordedPath, folder);
            return recordedPath;
        }

        /// <summary>
        /// A <c>command.txt</c> argument with a leading <see cref="DATA_TOKEN"/> made a path in the
        /// package holding <paramref name="folder"/>; any other argument unchanged.
        /// </summary>
        public static string ExpandDataToken(string argument, string folder)
        {
            if (!argument.StartsWith(DATA_TOKEN, StringComparison.Ordinal))
                return argument;
            string packageRoot = FindPackageRoot(folder);
            if (packageRoot == null)
                Assert.Fail(@"{0} uses {1}, but no folder above it holds a test data package's {2}", folder, DATA_TOKEN, MANIFEST_FILE);
            string path = Path.Combine(new[] { packageRoot }.Concat(argument.Substring(DATA_TOKEN.Length).Split('/')).ToArray());
            if (!IsInPackage(path, packageRoot))
                Assert.Fail(@"{0} in {1} leads outside the test data package {2}", argument, folder, packageRoot);
            return path;
        }

        /// <summary>The file name of a path recorded on any system: the text after its last '\' or '/'.</summary>
        public static string FileName(string path)
        {
            string name = SplitPath(path).LastOrDefault();
            Assert.IsFalse(string.IsNullOrEmpty(name), @"No file name in " + path);
            return name;
        }

        /// <summary>The nearest folder at or above <paramref name="folder"/> that holds a package's <see cref="MANIFEST_FILE"/>, or null.</summary>
        public static string FindPackageRoot(string folder)
        {
            for (string dir = folder; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                if (File.Exists(Path.Combine(dir, MANIFEST_FILE)))
                    return dir;
            }
            return null;
        }

        private static string[] SplitPath(string path)
        {
            return path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>True when <paramref name="path"/>, with any '..' resolved, is inside <paramref name="packageRoot"/>, or there is no package.</summary>
        private static bool IsInPackage(string path, string packageRoot)
        {
            if (packageRoot == null)
                return true;
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageRoot)) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static Package GetPackage(string id)
        {
            if (!PACKAGES.Value.TryGetValue(id, out var package))
                Assert.Fail(@"{0} lists no package {1}", PACKAGE_LIST_FILE, id);
            return package;
        }

        private static IReadOnlyDictionary<string, Package> ReadPackages()
        {
            string path = Path.Combine(AppContext.BaseDirectory, PACKAGE_LIST_FILE);
            if (!File.Exists(path))
                throw new FileNotFoundException(@"The build did not copy the test data package list beside the test assembly", path);
            var packages = new Dictionary<string, Package>(StringComparer.Ordinal);
            using (var json = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (var element in json.RootElement.GetProperty(@"packages").EnumerateArray())
                {
                    var package = new Package(element.GetProperty(@"id").GetString(), element.GetProperty(@"zip").GetString(),
                        element.GetProperty(@"folder").GetString());
                    packages.Add(package.Id, package);
                }
            }
            return packages;
        }

        [SupportedOSPlatform(@"windows")]
        private static string ReadKnownDownloadsFolder()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(SHELL_FOLDERS_KEY))
                return key?.GetValue(DOWNLOADS_FOLDER_ID) is string value ? Environment.ExpandEnvironmentVariables(value) : null;
        }

        /// <summary>One zip of test data, as <c>testdata.json</c> lists it.</summary>
        public sealed class Package
        {
            public Package(string id, string zip, string folder)
            {
                Id = id;
                Zip = zip;
                Folder = folder;
            }

            public string Id { get; }

            /// <summary>The zip's file name.</summary>
            public string Zip { get; }

            /// <summary>The zip's one top folder, which it extracts to under <see cref="TestData.Root"/>.</summary>
            public string Folder { get; }

            public string RootPath
            {
                get { return Path.Combine(TestData.Root, Folder); }
            }

            /// <summary>
            /// True when the package folder exists. One without its <see cref="MANIFEST_FILE"/> is an
            /// incomplete extraction, or a copy assembled by hand, and fails the test.
            /// </summary>
            public bool IsPresent
            {
                get
                {
                    string root = RootPath;
                    if (!Directory.Exists(root))
                        return false;
                    if (!File.Exists(Path.Combine(root, MANIFEST_FILE)))
                    {
                        Assert.Fail(@"{0} has no {1}, so it is not a complete copy of {2}. Delete the folder and extract the zip again.",
                            root, MANIFEST_FILE, Zip);
                    }
                    return true;
                }
            }

            /// <summary>A path in the package, from its '/'-separated relative path.</summary>
            public string GetPath(string relativePath)
            {
                return Path.Combine(new[] { RootPath }.Concat(relativePath.Split('/')).ToArray());
            }

            public override string ToString()
            {
                return Folder;
            }
        }

        /// <summary>
        /// One kind of reference data: files or folders in a package, which an environment variable,
        /// when it is set, replaces with its own list.
        /// </summary>
        public sealed class Item
        {
            private readonly string[] _relativePaths;

            public Item(string variable, Package package, params string[] relativePaths)
            {
                Variable = variable;
                Package = package;
                _relativePaths = relativePaths;
            }

            /// <summary>The variable that overrides the package's copies, or null for none.</summary>
            public string Variable { get; }

            public Package Package { get; }

            /// <summary>True when the variable is set or the package is present.</summary>
            public bool IsAvailable
            {
                get { return !string.IsNullOrEmpty(VariableValue) || Package.IsPresent; }
            }

            /// <summary>
            /// The item's paths: the variable's list, made absolute, when it is set, else the package's
            /// copies. Each must exist, or the test fails naming it. With neither, the test fails too: a
            /// test resolves its items only once <see cref="InconclusiveUnlessAvailable"/> found data
            /// for one of them, and an item without data would otherwise drop out of it unnoticed.
            /// </summary>
            public IReadOnlyList<string> Resolve()
            {
                string value = VariableValue;
                if (!string.IsNullOrEmpty(value))
                {
                    var paths = value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(Path.GetFullPath).ToArray();
                    if (paths.Length == 0)
                        Assert.Fail(@"{0} is set but names no path: '{1}'", Variable, value);
                    foreach (string path in paths)
                    {
                        if (!File.Exists(path) && !Directory.Exists(path))
                            Assert.Fail(@"{0} names a path that does not exist: {1}", Variable, path);
                    }
                    return paths;
                }
                if (!Package.IsPresent)
                {
                    Assert.Fail(@"Missing test data: extract {0} into {1}{2}.", Package.Zip, Root,
                        Variable != null ? @", or set " + Variable : string.Empty);
                }
                var copies = _relativePaths.Select(Package.GetPath).ToArray();
                foreach (string path in copies)
                {
                    if (!File.Exists(path) && !Directory.Exists(path))
                        Assert.Fail(@"The test data package {0} has no {1}", Package.RootPath, path);
                }
                return copies;
            }

            private string VariableValue
            {
                get { return Variable == null ? null : Environment.GetEnvironmentVariable(Variable); }
            }
        }
    }
}
