/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ionic.Zip;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace SkylineTester
{
    public partial class CreateZipInstallerWindow : Form
    {
        // Excluded files must be lower-case!
        private static readonly List<string> EXCLUDED_FILES = new List<string>
        {
            "testresults",
            "skylinetester.zip",
            "skylinetesterwithtestdata.zip",
            "skylinenightly.zip",
            "bibliospec.zip",
            "testrunner.log",
            "microsoft.visualstudio.qualitytools.unittestframework.dll", // Ignore if this appears in a build dir - gets added explicitly
            "testrunnermemory.log",
            "cacheddownloadsfortests" // ~1 GB of test downloads; SkylineTester fetches its own
        };

        /// <summary>
        /// True for output that only exists because TestRunner RUNS from the directory this
        /// zip is built from.
        /// </summary>
        /// <remarks>
        /// On net472 the zip was assembled from Skyline's build output, which contained
        /// nothing but build products. The net8 build assembles a single merged
        /// bin\staging\&lt;Config&gt; and TestRunner executes there, so the same directory
        /// accumulates per-test tool installs, per-test data archives and scratch files. The
        /// "add every subdirectory" pass below swept all of it in, taking the archive past
        /// 20 GB and over the 4 GB zip limit. Excluding it restores the ~100 MB of build
        /// products the 2023 no-test-zips change intended, plus the bundled runtime.
        /// </remarks>
        private static bool IsTestRunResidue(string fileOrDirectory)
        {
            var name = Path.GetFileName(fileOrDirectory) ?? string.Empty;

            // Per-test tool installs, 564 of them / ~14 GB on a machine that has run the
            // functional suite. Both spellings are deliberate: the tests install into
            // non-ASCII paths on purpose to catch i18n bugs, so both the plain "Tools_" and
            // the o-umlaut "Tools_" spelling occur, and the pattern has to match either.
            if (Regex.IsMatch(name, "^T(oo|öö)ls_", RegexOptions.IgnoreCase))
                return true;

            var extension = (Path.GetExtension(name) ?? string.Empty).ToLowerInvariant();

            // Per-test data archives. Shipping these is exactly what the 2023 change below
            // stopped doing; on net8 they arrive by a different route (staged into the bin
            // directory) rather than through FindZipFiles, so they need excluding here too.
            // The WithTestData variant still collects them from the source tree.
            if (extension == ".zip")
                return true;

            // DotNetZip writes its output to a <name>.tmp beside the target and renames on
            // success, so a failed run leaves a multi-GB file for the next run to pick up.
            if (extension == ".tmp")
                return true;

            return false;
        }

        public string ZipDirectory { get; private set; }

        public CreateZipInstallerWindow()
        {
            InitializeComponent();
            textBoxZipDirectory.Text = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            // TODO: classic Browse-For-Folder, for parity with .NET Framework; revisit to adopt the newer picker
            using (var dlg = FormUtil.CreateFolderBrowserDialog())
            {
                dlg.Description = "Select a folder to contain the zip file.";
                dlg.ShowNewFolderButton = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    textBoxZipDirectory.Text = dlg.SelectedPath;
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            ZipDirectory = textBoxZipDirectory.Text;
            if (!Directory.Exists(ZipDirectory))
            {
                MessageBox.Show("That folder does not exist.");
                return;
            }

            Close();
        }

        /// <summary>
        /// File names of every runtime and native asset a .deps.json declares, flattened to
        /// bare names because the staging dir is a single merged bin.
        /// </summary>
        /// <remarks>
        /// Used to assemble the BiblioSpec distro. The alternative, a hand-maintained member
        /// list, was viable when the tools were standalone native executables; the net8 ports
        /// are framework-dependent and drag the whole vendor assembly stack behind them.
        /// Returns nothing if the file is absent or unreadable so a malformed deps.json cannot
        /// abort the zip; the caller reports whatever it could not find on disk.
        /// </remarks>
        private static IEnumerable<string> RuntimeClosureFromDeps(string depsJsonPath)
        {
            var result = new List<string>();
            if (!File.Exists(depsJsonPath))
                return result;
            try
            {
                using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(depsJsonPath)))
                {
                    if (!doc.RootElement.TryGetProperty("targets", out var targets))
                        return result;
                    foreach (var target in targets.EnumerateObject())
                    foreach (var library in target.Value.EnumerateObject())
                    foreach (var section in new[] { "runtime", "native", "runtimeTargets" })
                    {
                        if (!library.Value.TryGetProperty(section, out var assets))
                            continue;
                        foreach (var asset in assets.EnumerateObject())
                        {
                            // runtimeTargets is per-RID: every platform's copy is listed, but
                            // only the build RID's assets get flattened into the output dir.
                            // Taking bare names indiscriminately would ask the zip for
                            // libhdf5.so and report it missing. This is also where
                            // SQLite.Interop.dll lives - BlibBuild cannot write a .blib
                            // without it, and no other section mentions it.
                            if (section == "runtimeTargets" && !IsWindowsRuntimeAsset(asset.Value))
                                continue;
                            result.Add(Path.GetFileName(asset.Name.Replace('/', '\\')));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("WARNING: could not read {0}: {1}", depsJsonPath, e.Message);
            }
            return result;
        }

        /// <summary>True when a deps.json runtimeTargets asset targets a Windows RID.</summary>
        private static bool IsWindowsRuntimeAsset(System.Text.Json.JsonElement asset)
        {
            if (!asset.TryGetProperty("rid", out var rid))
                return false;
            var value = rid.GetString();
            return value != null && value.StartsWith("win", StringComparison.OrdinalIgnoreCase);
        }

        public static void CreateZipFile(string zipPath, bool addTestZipFiles = false)
        {
            zipPath = zipPath ?? string.Empty; // For quiet ReSharper code inspection

            Console.WriteLine();
            Console.WriteLine("# Creating " + Path.GetFileName(zipPath) + "...");
            Console.WriteLine();

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            Directory.SetCurrentDirectory(exeDirectory);
            
            var solutionDirectory = exeDirectory;
            while (!File.Exists(Path.Combine(solutionDirectory, "Skyline.sln")))
            {
                solutionDirectory = Path.GetDirectoryName(solutionDirectory);
                if (string.IsNullOrEmpty(solutionDirectory))
                    throw new ApplicationException("Can't find solution directory");                    
            }

            using (var zipFile = new ZipFile(zipPath))
            {
                // DotNetZip has a _bug_ which causes an extraction error without this
                // (see http://stackoverflow.com/questions/15337186/dotnetzip-badreadexception-on-extract)
                zipFile.ParallelDeflateThreshold = -1;
                zipFile.AlternateEncodingUsage = ZipOption.Always;
                zipFile.AlternateEncoding = System.Text.Encoding.UTF8;
                // The original zip format caps entry sizes and archive offsets at 4 GB; past
                // that DotNetZip throws at Save() ("Compressed or Uncompressed size, or offset
                // exceeds the maximum value"), after having already done all the compression.
                // SkylineTester.zip is built from the whole staged output and is well over that
                // now, so ask for Zip64 headers only on the entries that actually need them --
                // Always would add them everywhere and break older extractors for no reason.
                zipFile.UseZip64WhenSaving = Zip64Option.AsNecessary;

                if ((String.Empty + Path.GetFileName(zipPath)).ToLower() == "skylinenightly.zip")
                {
                    // Add files to top level of zip file.
                    // net8 layout. Both .exe files are apphost launchers only: without the
                    // matching .dll, .deps.json and .runtimeconfig.json beside them the
                    // extracted zip cannot start at all, so they are members too. The old
                    // net472 list needed just the .exe plus a .exe.config; net8 emits
                    // <name>.dll.config instead, and never a .exe.config.
                    var files = new[]
                    {
                        "SkylineNightlyShim.exe",
                        "SkylineNightlyShim.dll",
                        "SkylineNightlyShim.dll.config",
                        "SkylineNightlyShim.deps.json",
                        "SkylineNightlyShim.runtimeconfig.json",
                        "SkylineNightly.exe",
                        "SkylineNightly.dll",
                        "SkylineNightly.dll.config",
                        "SkylineNightly.deps.json",
                        "SkylineNightly.runtimeconfig.json",
                        "SkylineNightly.pdb",
                        "Microsoft.Diagnostics.Runtime.dll",
                        "Microsoft.Win32.TaskScheduler.dll",
                        "ProDotNetZip.dll"
                    };
                    foreach (var file in files)
                    {
                        Console.WriteLine(file);
                        zipFile.AddFile(file);
                    }
                }

                else if ((String.Empty + Path.GetFileName(zipPath)).ToLower() == "bibliospec.zip")
                {
                    // Create a BiblioSpec distro.
                    //
                    // net472 could name the members: the tools were essentially standalone
                    // native executables plus a handful of vendor DLLs. The net8 ports are
                    // framework-dependent managed apps, so a runnable distro needs their whole
                    // dependency closure (86 assemblies for BlibBuild alone, including the
                    // Clearcore2 / Bruker / Shimadzu vendor stacks). Enumerating that by hand
                    // would be wrong the first time somebody adds a package reference, so read
                    // it from each tool's .deps.json, which the build already maintains.
                    //
                    // BlibToMs2 comes from the staging dir like the others now. The old code
                    // reached for Shared\BiblioSpec\obj\x64\BlibToMs2.exe, an artifact of the
                    // C++ build that the net8 tree does not produce.
                    var tools = new[] { "BlibBuild", "BlibFilter", "BlibToMs2" };
                    var files = new List<string>();
                    foreach (var tool in tools)
                    {
                        // The apphost .exe cannot start without these three beside it.
                        files.Add(tool + ".exe");
                        files.Add(tool + ".dll");
                        files.Add(tool + ".deps.json");
                        files.Add(tool + ".runtimeconfig.json");
                        files.AddRange(RuntimeClosureFromDeps(tool + ".deps.json"));
                    }
                    // Native vendor libraries and data files that NO .deps.json mentions: the
                    // readers P/Invoke them by name at runtime rather than referencing them, so
                    // the closure above cannot see them. This is the part of the original
                    // net472 member list that is still load-bearing.
                    files.AddRange(new[]
                    {
                        "MassLynxRaw.dll",
                        "timsdata.dll",
                        "baf2sql_c.dll",
                        "cdt.dll",
                        "modifications.xml"
                    });
                    files.Add(Directory.GetCurrentDirectory().Contains("Debug") ? "msparserD.dll" : "msparser.dll");

                    var missing = files.Distinct().Where(f => !File.Exists(f)).ToList();
                    files = files.Distinct().Where(File.Exists).ToList();
                    foreach (var file in files)
                    {
                        Console.WriteLine(file);
                        zipFile.AddFile(file, string.Empty);
                    }
                    // The msparser schemas moved into a msparser-config\ subdirectory in the
                    // net8 layout; keep that shape in the zip so msparser still finds them.
                    const string msparserConfig = "msparser-config";
                    if (Directory.Exists(msparserConfig))
                    {
                        foreach (var xsd in Directory.EnumerateFiles(msparserConfig, "*.xsd"))
                        {
                            Console.WriteLine(xsd);
                            zipFile.AddFile(xsd, msparserConfig);
                        }
                    }
                    // Report rather than silently ship a short distro; a name that disappears
                    // from the staging dir is a build regression worth seeing.
                    if (missing.Count > 0)
                        Console.WriteLine("NOTE: {0} closure entries absent from the staging dir and skipped: {1}",
                            missing.Count, string.Join(", ", missing.Take(10)));
                }

                else
                {
                    // Add SkylineTester at top level of zip file.
                    Console.WriteLine("SkylineTester.exe");
                    zipFile.AddFile("SkylineTester.exe");

                    // Add .skytr files at top level of zip file.
                    var skytrDirectory = Path.Combine(solutionDirectory, @"SkylineTester\Run files");
                    foreach (var skytrFile in Directory.EnumerateFiles(skytrDirectory, "*.skytr"))
                        AddFile(skytrFile, zipFile, ".");

                    // Add each subdirectory in the bin directory.
                    foreach (var directory in Directory.EnumerateDirectories("."))
                    {
                        if (Include(directory))
                        {
                            var name = Path.GetFileName(directory) ?? "";
                            Console.WriteLine(Path.Combine(SkylineTesterWindow.SkylineTesterFiles, name));
                            zipFile.AddDirectory(directory, Path.Combine(SkylineTesterWindow.SkylineTesterFiles, name));
                        }
                    }

                    // Add each file in the bin directory.
                    foreach (var file in Directory.EnumerateFiles("."))
                    {
                        if (Include(file))
                            AddFile(file, zipFile);
                    }

                    // MCC 2/14/2023: disabled adding test zips after discussion with Brendan that if we need
                    // to test Skyline outside a source tree, we can find a way to get the zip files on the fly or
                    // have a separate artifact for that. This will cut SkylineTester.zip from ~600MB to ~100MB.

                    if (addTestZipFiles)
                    {
                        // Add test zip files.
                        var zipFilesList = new List<string>();
                        FindZipFiles(solutionDirectory, zipFilesList);
                        var zipFilesDirectory = Path.Combine(SkylineTesterWindow.SkylineTesterFiles, "TestZipFiles");
                        foreach (var testZipFile in zipFilesList)
                        {
                            var testZipDirectory = Path.GetDirectoryName(testZipFile);
                            if (string.IsNullOrEmpty(testZipDirectory))
                                continue;
                            testZipDirectory = Path.Combine(zipFilesDirectory,
                                testZipDirectory.Substring(solutionDirectory.Length + 1));
                            if (Directory.Exists(testZipFile))
                            {
                                AddFolder(testZipFile, zipFile, Path.Combine(testZipDirectory, Path.GetFileName(testZipFile)));
                            }
                            else
                            {
                                AddFile(testZipFile, zipFile, testZipDirectory);
                            }
                        }

                        // Add tutorial audit logs
                        zipFile.AddDirectory(Path.Combine(solutionDirectory, @"TestTutorial\TutorialAuditLogs"),
                            @"SkylineTester Files\TestZipFiles\TestTutorial\TutorialAuditLogs");

                        // Add pwiz vendor reader test data
                        var vendorTestData = new List<string>();
                        foreach (TestFilesDir.VendorDir vendorDir in Enum.GetValues(typeof(TestFilesDir.VendorDir)))
                            FindVendorReaderTestData(TestFilesDir.GetVendorTestData(vendorDir), vendorTestData);
                        foreach (var file in vendorTestData)
                        {
                            var parentDirectory = Path.GetDirectoryName(file);
                            if (string.IsNullOrEmpty(parentDirectory))
                                continue;
                            int indexTestData =
                                parentDirectory.IndexOf(@"Test.data", StringComparison.InvariantCulture);
                            if (indexTestData >= 0)
                            {
                                int relativePathStart = parentDirectory.LastIndexOf('\\', indexTestData);
                                parentDirectory = parentDirectory.Substring(relativePathStart + 1);
                            }
                            else
                            {
                                parentDirectory = parentDirectory.Substring(
                                    Path.GetDirectoryName(solutionDirectory)?.Length + 1 ?? 0);
                            }
                            AddFile(file, zipFile,
                                Path.Combine(SkylineTesterWindow.SkylineTesterFiles, parentDirectory));
                        }
                    }

                    // Add unit testing DLL.
                    const string relativeUnitTestingDll =
                        @"PublicAssemblies\Microsoft.VisualStudio.QualityTools.UnitTestFramework.dll";
                    var unitTestingDll = SkylineTesterWindow.GetExistingVsIdeFilePath(relativeUnitTestingDll);
                    if (unitTestingDll == null)
                        throw new ApplicationException(string.Format("Can't find {0}", relativeUnitTestingDll));
                    AddFile(unitTestingDll, zipFile);
                }

                Console.WriteLine();
                Console.WriteLine("# Saving...");
                zipFile.Save();
                Console.WriteLine();
                Console.WriteLine("# {0} size: {1:F1} MB", Path.GetFileName(zipPath), new FileInfo(PathEx.SafePath(zipPath)).Length / (1024.0*1024));
                Console.WriteLine("# Done.");
                Console.WriteLine();
            }
        }

        static bool Include(string fileOrDirectory)
        {
            var name = Path.GetFileName(fileOrDirectory);
            if (name == null || EXCLUDED_FILES.Contains(name.ToLower()))
                return false;
            return !IsTestRunResidue(fileOrDirectory);
        }

        static void AddFile(string filePath, ZipFile zipFile, string zipDirectory = SkylineTesterWindow.SkylineTesterFiles)
        {
            var name = Path.GetFileName(filePath);
            if (name == null)
                return;
            Console.WriteLine(Path.Combine(zipDirectory, name));
            zipFile.AddFile(filePath, zipDirectory);
        }

        static void AddFolder(string folderPath, ZipFile zipFile, string zipDirectory)
        {
            foreach (var file in Directory.GetFiles(folderPath))
            {
                zipFile.AddFile(file, zipDirectory);
            }

            foreach (var directory in Directory.GetDirectories(folderPath))
            {
                AddFolder(directory, zipFile, Path.Combine(zipDirectory, Path.GetFileName(directory)));
            }
        }

        static void FindZipFiles(string directory, List<string> zipFilesList)
        {
            // Does this directory contains any .cs files?
            if (Directory.GetFiles(directory, "*.cs").Length == 0)
                return;

            // Get all zip files in the current directory.
            zipFilesList.AddRange(Directory.GetFiles(directory, "*.zip"));
            zipFilesList.AddRange(Directory.GetDirectories(directory, "*.data"));

            // Get all sub-directories in current directory:
            var subDirectories = Directory.GetDirectories(directory);

            // And iterate through them:
            foreach (string subDirectory in subDirectories)
            {
                FindZipFiles(subDirectory, zipFilesList);
            }
        }

        static void FindVendorReaderTestData(string directory, List<string> vendorReaderTestData)
        {
            foreach (var entry in Directory.GetFileSystemEntries(directory))
            {
                if (!entry.EndsWith(".mzML") && !entry.EndsWith(".gitattributes") && File.Exists(entry))
                    vendorReaderTestData.Add(Path.GetFullPath(entry));
                else if (Directory.Exists(entry))
                    FindVendorReaderTestData(entry, vendorReaderTestData);
            }
        }
    }
}

