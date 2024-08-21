/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009-2010 University of Washington - Seattle, WA
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
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Desired level of clean-up in test directories
    /// </summary>
    public enum DesiredCleanupLevel { none, persistent_files, downloads, all }

    /// <summary>
    /// Creates and cleans up a directory containing the contents of a
    /// test ZIP file.
    /// </summary>
    public sealed class TestFilesDir
    {
        private TestContext TestContext { get; set; }

        /// <summary>
        /// Creates a sub-directory of the Test Results directory with the same
        /// basename as a ZIP file in the test project tree.
        /// </summary>
        /// <param name="testContext">The test context for the test creating the directory</param>
        /// <param name="relativePathZip">A root project relative path to the ZIP file</param>
        public TestFilesDir(TestContext testContext, string relativePathZip)
            : this(testContext, relativePathZip, null)
        {
            
        }

        /// <summary>
        /// Creates a sub-directory of the Test Results directory with extracted files
        /// from a ZIP file in the test project tree to a directory in TestResults.
        /// </summary>
        /// <param name="testContext">The test context for the test creating the directory</param>
        /// <param name="relativePathZip">A root project relative path to the ZIP file</param>
        /// <param name="directoryName">Name of directory to create in the test results</param>
        public TestFilesDir(TestContext testContext, string relativePathZip, string directoryName)
            : this(testContext, relativePathZip, directoryName, null)
        {

        }

        /// <summary>
        /// Creates a sub-directory of the Test Results directory with extracted files
        /// from a ZIP file in the test project tree to a directory in TestResults.
        /// </summary>
        /// <param name="testContext">The test context for the test creating the directory</param>
        /// <param name="relativePathZip">A root project relative path to the ZIP file</param>
        /// <param name="directoryName">Name of directory to create in the test results</param>
        /// <param name="persistentFiles">List of files we'd like to extract in the ZIP file's directory for (re)use</param>
        /// <param name="isExtractHere">If false then the zip base name is used as the destination directory</param>
        public TestFilesDir(TestContext testContext, string relativePathZip, string directoryName, string[] persistentFiles, bool isExtractHere = false)
        {
            TestContext = testContext;
            string zipBaseName = Path.GetFileNameWithoutExtension(relativePathZip);
            if (zipBaseName == null)
                Assert.Fail("Null zip base name");  // Resharper
            directoryName = GetExtractDir(directoryName, zipBaseName, false);   // Only persistent files can be extract here
            FullPath = TestContext.GetTestPath(directoryName);
            if (Directory.Exists(FullPath))
            {
                Helpers.TryTwice(() => Directory.Delete(FullPath, true));
            }
            // where to place persistent (usually large, expensive to extract) files if any
            PersistentFiles = persistentFiles;
            IsExtractHere = isExtractHere;
            if (PersistentFiles != null)
                PersistentFilesDir = GetExtractDir(Path.GetDirectoryName(relativePathZip), zipBaseName, isExtractHere);

            TestContext.ExtractTestFiles(relativePathZip, FullPath, PersistentFiles, PersistentFilesDir);

            // record the size of the persistent directory after extracting
            var targetDir = isExtractHere ? Path.Combine(PersistentFilesDir ?? "", directoryName) : PersistentFilesDir;
            var persistentDirInfo = string.IsNullOrEmpty(PersistentFilesDir) ? null : new DirectoryInfo(targetDir);
            if (persistentDirInfo != null && Directory.Exists(PersistentFilesDir))
            {
                var persistentFileInfos = persistentDirInfo.EnumerateFiles("*", SearchOption.AllDirectories).ToList();
                PersistentFilesDirTotalSize = persistentFileInfos.Sum(f => f.Length);
                PersistentFilesDirFileSet = persistentFileInfos.Select(f => PathEx.RemovePrefix(f.FullName, Path.GetDirectoryName(PersistentFilesDir) + "\\")).ToHashSet();
            }
        }

        private static string GetExtractDir(string directoryName, string zipBaseName, bool isExtractHere)
        {
            if (!isExtractHere)
            {
                directoryName = directoryName != null
                    ? Path.Combine(directoryName, zipBaseName)
                    : zipBaseName;
            }
            return directoryName;
        }

        public string FullPath { get; private set; }

        public string PersistentFilesDir { get; private set; }

        public string[] PersistentFiles { get; private set; }
        private bool IsExtractHere { get; }

        /// <summary>
        /// The sum of all file sizes in the persistent files dir after extracting the ZIP.
        /// </summary>
        public long? PersistentFilesDirTotalSize { get; private set; }

        /// <summary>
        /// Full list of all file paths in the persistent files dir after extracting the ZIP.
        /// </summary>
        public ISet<string> PersistentFilesDirFileSet { get; private set; }

        public string RootPath
        {
            get
            {
                // Handle the case where a DirectoryName has been added to differentiate
                // folders running with the same test files.
                var rootPath = FullPath;
                if (!Equals(TestContext.GetTestPath(string.Empty), Path.GetDirectoryName(rootPath)))
                    rootPath = Path.GetDirectoryName(rootPath);
                return rootPath;
            }
        }

        /// <summary>
        /// Returns a full path to a file in the unzipped directory.
        /// </summary>
        /// <param name="relativePath">Relative path, as stored in the ZIP file, to the file</param>
        /// <returns>Absolute path to the file for use in tests</returns>
        public string GetTestPath(string relativePath)
        {
            if ((PersistentFiles != null) && (PersistentFiles.Contains(relativePath) || PersistentFiles.Any(relativePath.Contains)))
            {
                // persistent file - probably so because it's large. 
                return Path.Combine(PersistentFilesDir, relativePath);
            }
            if (!Directory.Exists(FullPath)) // can happen when all files are in the persistant area, but we're creating a new one as part of the test
                Directory.CreateDirectory(FullPath);
            return Path.Combine(FullPath, relativePath);
        }

        /// <summary>
        /// Returns a full path to a file in the unzipped directory, with "_Intl" appended.
        /// </summary>
        /// <param name="relativePath">Relative path, as stored in the ZIP file, to the file</param>
        /// <returns>Absolute path to the file for use in tests</returns>
        public string GetTestPathIntl(string relativePath)
        {
            string testPath = GetTestPath(relativePath);
            return Path.Combine(Path.GetDirectoryName(testPath) ?? "",
                    Path.GetFileNameWithoutExtension(testPath) + "_Intl" +
                    Path.GetExtension(testPath));
        }

        /// <summary>
        /// Returns a full path to a file in the unzipped directory, with "_Intl" appended
        /// if the current culture does not use a comma for .csv.
        /// </summary>
        /// <param name="relativePath">Relative path, as stored in the ZIP file, to the file</param>
        /// <returns>Absolute path to the file for use in tests</returns>
        public string GetTestPathLocale(string relativePath)
        {
            if (TextUtil.CsvSeparator == TextUtil.SEPARATOR_CSV)
                return GetTestPath(relativePath);
            return GetTestPathIntl(relativePath);
        }


        public enum VendorDir
        {
            ABI,
            Agilent,
            Bruker,
/* Waiting for CCS<->DT support in .mbi reader
            Mobilion,
*/
            Shimadzu,
            Thermo,
            UIMF,
            UNIFI,
            Waters,
            DiaUmpire,
            BiblioSpec
        }

        /// <summary>
        /// Returns full path to the specified vendor reader's test data directory
        /// (e.g. pwiz/data/vendor_readers/Thermo/Reader_Thermo_Test.data)
        /// </summary>
        public static string GetVendorTestData(VendorDir vendorDir)
        {
            string projectDir = ExtensionTestContext.GetProjectDirectory("");
            if (projectDir == null)
                throw new InvalidOperationException("unable to find project directory with Skyline test files");

            string vendorReaderPath;
            string vendorStr = Enum.GetName(typeof(VendorDir), vendorDir) ?? throw new ArgumentException(@"VendorDir value unknown");
            if (File.Exists(Path.Combine(projectDir, @"Skyline.sln")))
            {
                if (vendorDir == VendorDir.DiaUmpire)
                {
                    return Path.Combine(projectDir, @"..\..\pwiz\analysis\spectrum_processing\SpectrumList_DiaUmpireTest.data");
                }
                else if (vendorDir == VendorDir.BiblioSpec)
                {
                    return Path.Combine(projectDir, @"..\..\pwiz_tools\BiblioSpec\tests\inputs");
                }
                else
                {
                    vendorReaderPath = Path.Combine(projectDir, @"..\..\pwiz\data\vendor_readers");
                    return Path.Combine(vendorReaderPath, vendorStr, $"Reader_{vendorStr}_Test.data");
                }
            }
            else
            {
                vendorReaderPath = Path.Combine(projectDir, @".."); // one up from TestZipFiles, and no vendorStr intermediate directory
                if (vendorDir == VendorDir.DiaUmpire)
                    return Path.Combine(vendorReaderPath, "SpectrumList_DiaUmpireTest.data");
                else if (vendorDir == VendorDir.BiblioSpec)
                    return Path.Combine(vendorReaderPath, "BiblioSpecTestData");
                else
                    return Path.Combine(vendorReaderPath, $"Reader_{vendorStr}_Test.data");
            }

        }

        /// <summary>
        /// Returns full path to a file in the specified vendor reader's test data directory
        /// (e.g. pwiz/data/vendor_readers/Thermo/Reader_Thermo_Test.data/test_file.raw)
        /// </summary>
        public static string GetVendorTestData(VendorDir vendorDir, string rawname)
        {
            return Path.Combine(GetVendorTestData(vendorDir), rawname);
        }

        /// <summary>
        /// <para>Attempts to move the directory to make sure no file handles are open.
        /// Used to delete the directory, but it can be useful to look at test
        /// artifacts, after the tests complete.</para>
        /// <para>In some contexts, however, it may be more important to be space
        /// efficient, and the chance to look at remaining artifacts is low (e.g. on
        /// TeamCity VMs), so deletion is used again when called for.</para>
        /// </summary>
        public void Cleanup()
        {
            // check that persistent files dir has not changed
            CheckForModifiedPersistentFilesDir();

            var desiredCleanupLevel = TestContext.GetEnumValue("DesiredCleanupLevel", DesiredCleanupLevel.none);

            CheckForFileLocks(RootPath, desiredCleanupLevel == DesiredCleanupLevel.all);
            // Also check for file locks on the persistent files directory
            // since it is essentially an extension of the test directory.
            if (!TestContext.Properties.Contains("ParallelTest")) // It is a shared directory in parallel tests, though, so leave it alone in parallel mode
            {
                if (!PathEx.IsDownloadsPathShared())
                {
                    CheckForFileLocks(PersistentFilesDir, desiredCleanupLevel != DesiredCleanupLevel.none);
                }
            }
        }

        private void CheckForModifiedPersistentFilesDir()
        {
            if (!PersistentFilesDirTotalSize.HasValue) return;

            // Do a garbage collection in case any finalizer is supposed to release a file handle
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var lastDirectoryName = Path.GetFileName(FullPath) ?? "";
            var targetDir = IsExtractHere ? Path.Combine(PersistentFilesDir ?? "", lastDirectoryName) : PersistentFilesDir;
            List<FileInfo> currentFileInfos;
            try
            {
                var persistentDirInfo = new DirectoryInfo(targetDir);
                currentFileInfos = persistentDirInfo.EnumerateFiles("*", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(@"Warning: while checking for modified persistent files directory, " + ex.Message);
                return;
            }

            long currentSize = currentFileInfos.Sum(f => f.Length);
            var currentFiles = currentFileInfos.Select(f => PathEx.RemovePrefix(f.FullName, Path.GetDirectoryName(PersistentFilesDir) + "\\")).ToHashSet();
            var newFiles = new HashSet<string>(currentFiles);
            newFiles.ExceptWith(PersistentFilesDirFileSet);
            var deletedFiles = new HashSet<string>(PersistentFilesDirFileSet);
            deletedFiles.ExceptWith(currentFiles);
            if (newFiles.Any() || deletedFiles.Any() || PersistentFilesDirTotalSize - currentSize > 0)
            {
                var changeSummary = new StringBuilder($"PersistentFilesDir ({PersistentFilesDir}) has been modified.\r\n");
                changeSummary.AppendLine($"  Original size: {PersistentFilesDirTotalSize}");
                changeSummary.AppendLine($"Size at cleanup: {currentSize}");
                if (newFiles.Any())
                {
                    changeSummary.AppendLine("New files:");
                    changeSummary.Append(TextUtil.LineSeparate(newFiles));
                }

                if (deletedFiles.Any())
                {
                    changeSummary.Append("Deleted files:");
                    changeSummary.Append(TextUtil.LineSeparate(deletedFiles));
                }

                throw new IOException(changeSummary.ToString());
            }
        }

        public static void CheckForFileLocks(string path, bool useDeletion = false)
        {
            // Do a garbage collection in case any finalizer is supposed to release a file handle
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            string GetProcessNamesLockingFile(string lockedDirectory, Exception exceptionShowingLockedFileName)
            {
                var output = string.Empty;
                try
                {
                    var fname = exceptionShowingLockedFileName.Message.Split('\'')[1];
                    var fullPathToFile = Path.Combine(lockedDirectory, fname);
                    var names = string.Join(@", ",
                        FileLockingProcessFinder.GetProcessesUsingFile(fullPathToFile).Select(p => p.ProcessName));
                    if (!names.IsNullOrEmpty())
                    {
                        output = $@" ({fullPathToFile} is locked by {names})";
                    }
                }
                catch
                {
                    // ignored - not a disaster if this doesn't always work
                }
                return output;
            }

            if (!Directory.Exists(path)) // Did test already clean up after itself?
                return;

            // If deletion is acceptable, then simply try to delete the directory
            // and the operation will throw a useful exception if it fails
            if (useDeletion)
            {
                RemoveReadonlyFlags(path);
                try
                {
                    Helpers.TryTwice(() => Directory.Delete(path, true));
                }
                catch (Exception e)
                {
                    throw new IOException($@"Directory.Delete(""{path}"",true) failed with ""{e.Message}""{GetProcessNamesLockingFile(path, e)}");
                }
                return;
            }

            // Move to a new name within the same directory
            string guidName = Guid.NewGuid().ToString();
            string parentPath = Path.GetDirectoryName(path);
            if (parentPath != null)
                guidName = Path.Combine(parentPath, guidName);

            try
            {
                Helpers.TryTwice(() => Directory.Move(path, guidName));
            }
            catch (IOException)
            {
                // Useful for debugging. Exception names file that is locked.
                try
                {
                    Helpers.TryTwice(() => Directory.Delete(path, true));
                }
                catch (Exception e)
                {
                    throw new IOException($@"Directory.Move(""{path}"",""{guidName}"") failed, attempt to delete instead resulted in ""{e.Message}""{GetProcessNamesLockingFile(path, e)}");
                }
            }

            // Move the file back to where it was, and fail if this throws
            try
            {
                Helpers.TryTwice(() => Directory.Move(guidName, path));
            }
            catch (IOException)
            {
                try
                {
                    // Useful for debugging. Exception names file that is locked.
                    Helpers.TryTwice(() => Directory.Delete(guidName, true));
                }
                catch (Exception e)
                {
                    throw new IOException($@"Directory.Move(""{guidName}"",(""{path}"") failed, attempt to delete instead resulted in ""{e.Message}""{GetProcessNamesLockingFile(path, e)}");
                }
            }
        }

        /// <summary>
        /// Recursively removes read-only flags from files in a directory
        /// making it possible to delete with Directory.Delete().
        /// Note that it does not check for shortcuts/symbolic links to
        /// other folders.
        /// </summary>
        public static void RemoveReadonlyFlags(string path)
        {
            string[] files = Directory.GetFiles(path);
            string[] dirs = Directory.GetDirectories(path);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            foreach (string dir in dirs)
            {
                RemoveReadonlyFlags(dir);
            }
        }
    }
}
