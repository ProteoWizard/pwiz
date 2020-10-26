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

        public static string GetTestPath(this TestContext testContext, string relativePath)
        {
            return Path.Combine(testContext.TestDir, relativePath);
        }

        public static String GetProjectDirectory(string relativePath)
        {
            for (String directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                directory != null && directory.Length > 10;
                directory = Path.GetDirectoryName(directory))
            {
                var testZipFiles = Path.Combine(directory, "TestZipFiles");
                if (Directory.Exists(testZipFiles))
                    return Path.Combine(testZipFiles, relativePath);
                if (File.Exists(Path.Combine(directory, "Skyline.sln")))
                    return Path.Combine(directory, relativePath);
            }

            // as last resort, check if current directory is the pwiz repository root (e.g. when running TestRunner in Docker container)
            if (File.Exists(Path.Combine("pwiz_tools", "Skyline", "Skyline.sln")))
                return Path.Combine("pwiz_tools", "Skyline", relativePath);

            return null;
        }

        public static String GetProjectDirectory(this TestContext testContext, string relativePath)
        {
            return GetProjectDirectory(relativePath);
        }

        public static void ExtractTestFiles(this TestContext testContext, string relativePathZip, string destDir, string[] persistentFiles, string persistentFilesDir)
        {
            string pathZip = testContext.GetProjectDirectory(relativePathZip);
            try
            {
                Helpers.Try<Exception>(() =>
                {
                    using (ZipFile zipFile = ZipFile.Read(pathZip))
                    {
                        foreach (ZipEntry zipEntry in zipFile)
                        {
                            if (IsPersistent(persistentFiles, zipEntry.FileName))
                                zipEntry.Extract(persistentFilesDir, ExtractExistingFileAction.DoNotOverwrite);  // leave persistent files alone                        
                            else
                                zipEntry.Extract(destDir, ExtractExistingFileAction.OverwriteSilently);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error reading test zip file: " + pathZip, ex);
            }
        }

        private static bool IsPersistent(string[] persistentFiles, string zipEntryFileName)
        {
            return persistentFiles != null && persistentFiles.Any(f => zipEntryFileName.Replace('\\', '/').Contains(f.Replace('\\', '/')));
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
    }
}
