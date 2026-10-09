/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using SharedBatch;

namespace SharedBatchTest
{
    public static class ExtensionTestContext
    {
        public static string GetTestPath(this TestContext testContext, string relativePath)
        {
            return Path.Combine(testContext.TestRunDirectory, relativePath);
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

        /// <summary>
        /// The output directory of the most recently built SkylineCmd.exe in this checkout, Release
        /// or Debug, or null if there is none. The batch-tool build scripts default to Debug, so a
        /// Release-only search fails every test that needs a real SkylineCmd.exe on a Debug tree.
        /// .NET Framework output under bin\x64\&lt;Config&gt; is deliberately not searched: a stale
        /// build left there would otherwise be run in place of the current one.
        /// </summary>
        public static string GetSkylineBinDirectory()
        {
            var binDir = GetProjectDirectory(@"bin");
            if (binDir == null)
                return null;

            string best = null;
            var bestTime = DateTime.MinValue;
            foreach (var configuration in new[] { @"Release", @"Debug" })
            {
                foreach (var dir in new[]
                         {
                             Path.Combine(binDir, configuration, @"net10.0-windows"),
                             Path.Combine(binDir, @"x64", configuration, @"net10.0-windows"),
                             Path.Combine(binDir, @"staging", configuration)
                         })
                {
                    var cmd = Path.Combine(dir, SkylineInstallations.SkylineCmdExe);
                    if (!File.Exists(cmd))
                        continue;
                    var time = File.GetLastWriteTime(cmd);
                    if (best == null || time > bestTime)
                    {
                        best = dir;
                        bestTime = time;
                    }
                }
            }
            return best;
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
