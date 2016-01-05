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
                return !(Program.NoVendorReaders || ("1").Equals(Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING"))); // Not L10N
            }
        }

        public static string GetTestPath(this TestContext testContext, string relativePath)
        {
            return Path.Combine(testContext.TestDir, relativePath);
        }

        public static String GetProjectDirectory(this TestContext testContext, string relativePath)
        {
            for (String directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    directory != null && directory.Length > 10;
                    directory = Path.GetDirectoryName(directory))
            {
                var testZipFiles = Path.Combine(directory, "TestZipFiles");
                if (Directory.Exists(testZipFiles))
                    return Path.Combine(testZipFiles, relativePath);
                if (File.Exists(Path.Combine(directory, Program.Name + ".sln")))
                    return Path.Combine(directory, relativePath);
            }
            return null;
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
                            bool persist = false;
                            if (persistentFiles != null)
                            {
                                foreach (var persistentFile in persistentFiles)
                                {
                                    if (zipEntry.FileName.Replace('\\', '/').Contains(persistentFile.Replace('\\', '/')))
                                    {
                                        zipEntry.Extract(persistentFilesDir, ExtractExistingFileAction.DoNotOverwrite);  // leave persistent files alone                        
                                        persist = true;
                                        break;
                                    }
                                }
                            }
                            if (!persist)
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

        public static bool CanImportThermoRaw
        {
            get
            {
                return AllowVendorReaders;
            }
        }

        public static string ExtMz5
        {
            get { return ".mz5"; }
        }

        public static string ExtMzml
        {
            get { return ".mzML"; }
        }

        public static string ExtThermoRaw
        {
            get { return CanImportThermoRaw ? ".RAW" : ExtMzml; }
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
            get { return CanImportAbWiff ? ".wiff" : ExtMzml; }
        }

        public static bool CanImportAbWiff
        {
            get
            {
                // return false to import mzML
                return AllowVendorReaders;
            }
        }

        public static string ExtAgilentRaw
        {
            get { return CanImportAgilentRaw ? ".d" : ExtMzml; }
        }

        public static bool CanImportWatersRaw
        {
            get
            {
                // return false to import mzML
                return AllowVendorReaders && !IsDebugMode && !System.Diagnostics.Debugger.IsAttached;  // no waters library for debug build, or under debugger in release build
            }
        }

        public static string ExtWatersRaw
        {
            get { return CanImportWatersRaw ? ".raw" : ExtMzml; }
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