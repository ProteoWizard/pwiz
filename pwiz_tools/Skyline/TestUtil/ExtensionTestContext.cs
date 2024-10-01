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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Ionic.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public static class ExtensionTestContext
    {
        // Vendor readers creep under code coverage analysis, and they're opaque to us anyway, so avoid in that context
        // Also honor the Program.NoVendorReaders flag for test purposes
        private static bool AllowVendorReaders
        {
            get
            {
                return !(Program.NoVendorReaders || (@"1").Equals(Environment.GetEnvironmentVariable(@"COR_ENABLE_PROFILING")));
            }
        }

        public static bool GetBoolValue(this TestContext testContext, string property, bool defaultValue)
        {
            var value = testContext.Properties[property];
            return (value == null) ? defaultValue :
                string.Compare(value.ToString(), "true", true, CultureInfo.InvariantCulture) == 0;
        }

        public static TValue GetEnumValue<TValue>(this TestContext testContext, string property, TValue defaultValue)
        {
            var value = testContext.Properties[property];
            return (value == null) ? defaultValue :
                (TValue)Enum.Parse(typeof(TValue), value.ToString());
        }


        public static string GetTestDir(this TestContext testContext)
        {
            // when run with VSTest/MSTest (when .runsettings file is used), use the CustomTestResultsDirectory property if available
            // because there's no other way to override the TestDir
            return testContext.Properties["CustomTestResultsDirectory"]?.ToString() ?? testContext.TestDir;
        }

        public static string GetTestPath(this TestContext testContext, string relativePath)
        {
            return Path.GetFullPath(Path.Combine(GetProjectDirectory(), testContext.GetTestDir(), relativePath ?? string.Empty));
        }

        public static string GetTestResultsPath(this TestContext testContext, string relativePath = null)
        {
            return Path.GetFullPath(Path.Combine(GetProjectDirectory(), testContext.GetTestDir(), testContext.TestName, relativePath ?? string.Empty));
        }

        /// <summary>
        /// Ensures the <see cref="GetTestResultsPath"/> root folder is created and empty.
        /// </summary>
        public static void EnsureTestResultsDir(this TestContext testContext)
        {
            var testResultsDir = testContext.GetTestResultsPath();
            if (testResultsDir != null)
            {
                if (Directory.Exists(testResultsDir))
                    DirectoryEx.SafeDelete(testResultsDir);

                Directory.CreateDirectory(testResultsDir);
            }
        }

        public static String GetProjectDirectory()
        {
            for (String directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                 directory != null && directory.Length > 10;
                 directory = Path.GetDirectoryName(directory))
            {
                var testZipFiles = Path.Combine(directory, "TestZipFiles");
                if (Directory.Exists(testZipFiles))
                    return testZipFiles;
                if (File.Exists(Path.Combine(directory, "Skyline.sln")))
                    return directory;
            }

            // As last resort, hunt around the current working directory and its subdirectories to find the pwiz repository root
            // (e.g. when running TestRunner in Docker container or from SkylineTester.zip)
            var up = string.Empty;
            var relPath = @".";
            for (var depth = 0; depth < 10; depth++)
            {
                foreach(var subdir in Directory.GetDirectories(relPath).Append(relPath))
                    if (File.Exists(Path.Combine(subdir, "pwiz_tools", "Skyline", "Skyline.sln")))
                        return Path.GetFullPath(Path.Combine(subdir, "pwiz_tools", "Skyline"));
                up = Path.Combine(up, @"..");
                relPath = Path.Combine(Directory.GetCurrentDirectory(), up);
            }

            return null;
        }

        public static String GetProjectDirectory(string relativePath)
        {
            var projectDir = GetProjectDirectory();
            if (projectDir == null)
                return null;
            return Path.Combine(projectDir, relativePath);
        }

        public static String GetProjectDirectory(this TestContext testContext, string relativePath)
        {
            return GetProjectDirectory(relativePath);
        }

        public static void ExtractTestFiles(this TestContext testContext, string relativePathZip, string destDir, string[] persistentFiles, string persistentFilesDir)
        {
            string pathZip = testContext.GetProjectDirectory(relativePathZip);
            bool zipFileExists = File.Exists(pathZip);
            if (zipFileExists)
            {
                try
                {
                    Helpers.Try<Exception>(() =>
                    {
                        using ZipFile zipFile = ZipFile.Read(pathZip);
                        foreach (ZipEntry zipEntry in zipFile)
                        {
                            if (zipEntry.IsDirectory && !IsPersistentDir(persistentFiles, zipEntry.FileName))
                            {
                                // Directory creation is implicitly handled by Extract of files
                                // so skip that and avoid occasional "file in use" exceptions on directory creation.
                                // N.B. some tests expect the persisted directory structure to be duplicated locally,
                                // even if the files are not, so for those do the directory creation.
                                continue; 
                            }
                            if (IsPersistent(persistentFiles, zipEntry.FileName))
                                zipEntry.Extract(persistentFilesDir, ExtractExistingFileAction.DoNotOverwrite);  // leave persistent files alone                        
                            else
                                zipEntry.Extract(destDir, ExtractExistingFileAction.OverwriteSilently);
                        }
                    });
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Error reading test zip file: " + pathZip, ex);
                }
            }

            // If there exists a ".data" folder with the same base name as the .zip, copy all files from this.
            // This allows overriding some of the files from the .zip, or checking the entire .zip in as an exploded
            // .data folder
            var dataFolderName = Path.ChangeExtension(pathZip, ".data");
            if (Directory.Exists(dataFolderName))
            {
                CopyRecursively(dataFolderName, destDir, zipFileExists);
            }
            else if (!zipFileExists)
            {
                throw new ApplicationException(string.Format(
                    "Test zip file {0} and test folder {1} do not exist", pathZip, dataFolderName));
            }
        }

        private static bool IsPersistent(string[] persistentFiles, string zipEntryFileName)
        {
            return persistentFiles != null && persistentFiles.Any(f => zipEntryFileName.Replace('\\', '/').Contains(f.Replace('\\', '/')));
        }

        private static bool IsPersistentDir(string[] persistentFiles, string zipEntryDirName)
        {
            return persistentFiles != null && persistentFiles.Any(f => f.Replace('\\', '/').Contains(zipEntryDirName.Replace('\\', '/')));
        }

        public static string ExtMzml
        {
            get
            {
                return ".mzML"; //DataSourceUtil.EXT_MZML; ** Tests rely on capitalization
            }
        }

        public static bool CanImportMz5
        {
            get
            {
                return false;    // TODO: mz5 leaks and increases total memory variance
            }
        }

        public static string ExtMz5
        {
            get { return CanImportMz5 ? DataSourceUtil.EXT_MZ5 : ExtMzml; }
        }

        public static bool CanImportThermoRaw
        {
            get
            {
                return AllowVendorReaders;
            }
        }

        public static string ExtThermoRaw
        {
            get { return CanImportThermoRaw ? DataSourceUtil.EXT_THERMO_RAW.ToUpperInvariant() : ExtMzml; } // *** Case matters to ConsoleImportNonSRMFile
        }

        public static bool CanImportAgilentRaw
        {
            get
            {
                // return false to import mzML
                return AllowVendorReaders;
            }
        }

        public static string ExtAbWiff
        {
            get { return CanImportAbWiff ? DataSourceUtil.EXT_WIFF : ExtMzml; }
        }

        public static bool CanImportAbWiff
        {
            get
            {
                // return false to import mzML
                return AllowVendorReaders;
            }
        }

        public static string ExtAbWiff2
        {
            get { return CanImportAbWiff2 ? DataSourceUtil.EXT_WIFF2 : ExtMzml; }
        }

        public static bool CanImportAbWiff2
        {
            get
            {
                // return false to import mzML
                return AllowVendorReaders;
            }
        }

        public static string ExtAgilentRaw
        {
            get { return CanImportAgilentRaw ? DataSourceUtil.EXT_AGILENT_BRUKER_RAW : ExtMzml; }
        }

        public static bool CanImportMobilionRaw
        {
            get
            {
                // return false to import mzML
                return Environment.Is64BitProcess && AllowVendorReaders;
            }
        }

        public static string ExtMobilionRaw
        {
            get { return CanImportMobilionRaw ? DataSourceUtil.EXT_MOBILION_MBI : ExtMzml; }
        }

        public static bool CanImportShimadzuRaw
        {
            get
            {
                // return false to import mzML
                return !Program.SkylineOffscreen;    // currently leaks to process heap, so avoid it during nightly tests when offscreen
            }
        }

        public static string ExtShimadzuRaw
        {
            get { return CanImportShimadzuRaw ? DataSourceUtil.EXT_SHIMADZU_RAW : ExtMzml; }
        }

        public static bool CanImportWatersRaw
        {
            get
            {
                // return false to import mzML
                return AllowVendorReaders;
            }
        }

        public static string ExtWatersRaw
        {
            get { return CanImportWatersRaw ? DataSourceUtil.EXT_WATERS_RAW : ExtMzml; }
        }

        public static bool IsDebugMode
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        private static void CopyRecursively(string sourcePath, string destinationPath, bool overwrite)
        {
            Directory.CreateDirectory(destinationPath);
            foreach (string file in Directory.GetFiles(sourcePath))
            {
                string destinationFile = Path.Combine(destinationPath, Path.GetFileName(file));
                File.Copy(file, destinationFile, overwrite);
            }

            // Copy each subdirectory using recursion.
            foreach (string directory in Directory.GetDirectories(sourcePath))
            {
                string destinationDirectory = Path.Combine(destinationPath, Path.GetFileName(directory));
                CopyRecursively(directory, destinationDirectory, overwrite);
            }
        }
    }
}
