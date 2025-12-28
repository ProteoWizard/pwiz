/*
 * Original author: Ali Marsh <alimarsh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * Copyright 2020 University of Washington - Seattle, WA
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
using Microsoft.Win32;
using SkylineBatch.Properties;

namespace SkylineBatch
{

    public class RInstallations
    {
        // Finds and saves information about the computer's R installation locations
        private const string RegistryLocationR = @"SOFTWARE\R-core\R";

        /// <summary>
        /// Test seam: Set mock R versions for testing without requiring actual R installation.
        /// Set to non-null dictionary to bypass system R detection.
        /// </summary>
        public static Dictionary<string, string> TestRVersions { get; set; }

        public static bool FindRDirectory()
        {
            // Test seam: Use mock R versions if set (bypasses system R detection for testing)
            if (TestRVersions != null)
            {
                Settings.Default.RVersions = TestRVersions;
                return TestRVersions.Count > 0;
            }

            if (Settings.Default.RDirs == null) Settings.Default.RDirs = new List<string>();
            if (Settings.Default.RDirs.Count == 0)
            {
                // Try 64-bit registry first (most common for R installations)
                FindRInstallationsInRegistry(RegistryView.Registry64);
                
                // Also check 32-bit registry
                FindRInstallationsInRegistry(RegistryView.Registry32);

                // Always add the Documents\R folder as a potential location
                string documentsRPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "R");
                if (!Settings.Default.RDirs.Contains(documentsRPath))
                {
                    Settings.Default.RDirs.Add(documentsRPath);
                }
            }

            Settings.Default.RVersions = GetRInstallationDict(Settings.Default.RDirs);
            return Settings.Default.RVersions.Count > 0;
        }

        private static void FindRInstallationsInRegistry(RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var rCoreKey = baseKey.OpenSubKey(RegistryLocationR))
                {
                    if (rCoreKey == null)
                        return;

                    // Enumerate version-specific subkeys (e.g., "4.5.1", "R64")
                    foreach (var versionKeyName in rCoreKey.GetSubKeyNames())
                    {
                        try
                        {
                            using (var versionKey = rCoreKey.OpenSubKey(versionKeyName))
                            {
                                if (versionKey == null)
                                    continue;

                                var installPath = versionKey.GetValue(@"InstallPath") as string;
                                if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                                {
                                    // Add the parent directory (e.g., C:\Program Files\R for C:\Program Files\R\R-4.5.1)
                                    var parentDir = Path.GetDirectoryName(installPath);
                                    if (!string.IsNullOrEmpty(parentDir) && !Settings.Default.RDirs.Contains(parentDir))
                                    {
                                        Settings.Default.RDirs.Add(parentDir);
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore errors reading individual version keys
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors accessing registry
            }
        }

        public static void AddRDirectory(string newRDir)
        {
            // CONSIDER: Adding watch to R folders for new installations
            if (!Directory.Exists(newRDir))
                throw new ArgumentException(string.Format(Resources.RInstallations_AddRDirectory_R_installation_directory_not_found___0_, newRDir) + Environment.NewLine +
                                            Resources.RInstallations_AddRDirectory_Please_enter_a_valid_directory_);
            var RDirectoryFound = false;
            var input = newRDir;
            while (true) // breaks when R directory is found
            {
                var childFolderNames = Directory.GetDirectories(newRDir);
                foreach (var folderName in childFolderNames)
                    if (Path.GetFileName(folderName).StartsWith("R-"))
                    {
                        RDirectoryFound = true;
                        break;
                    }
                if (RDirectoryFound) break;
                try
                {
                    newRDir = Path.GetDirectoryName(newRDir);
                }
                catch (Exception)
                {
                    newRDir = null;
                }
                if (newRDir == null)
                    throw new ArgumentException(string.Format(Resources.RInstallations_AddRDirectory_No_R_installations_were_found_in___0_, input) + Environment.NewLine +
                                                Resources.RInstallations_AddRDirectory_Please_choose_a_directory_with_R_installations_);
            }

            if (!Settings.Default.RDirs.Contains(newRDir))
                Settings.Default.RDirs.Add(newRDir);
            Settings.Default.RVersions = GetRInstallationDict(Settings.Default.RDirs);
            Settings.Default.Save();
        }

        private static Dictionary<string, string> GetRInstallationDict(List<string> RDirs)
        {
            var rPaths = new Dictionary<string, string>();
            foreach (var RDir in RDirs)
            {
                if (!Directory.Exists(RDir))
                    continue;
                string[] rVersions = Directory.GetDirectories(RDir);

                foreach (var rVersion in rVersions)
                {
                    var folderName = Path.GetFileName(rVersion);
                    if (folderName.StartsWith("R-"))
                    {
                        var rScriptPath = rVersion + "\\bin\\Rscript.exe";
                        if (File.Exists(rScriptPath))
                        {
                            if (rPaths.ContainsKey(folderName.Substring(2)))
                                rPaths[folderName.Substring(2)] = rScriptPath;
                            else
                                rPaths.Add(folderName.Substring(2), rScriptPath);
                        }
                    }
                }
            }
            return rPaths;
        }

        /// <summary>
        /// Gets the most recent installed R version.
        /// Ensures R directories are discovered first.
        /// </summary>
        /// <returns>The version string of the most recent R installation</returns>
        /// <exception cref="InvalidOperationException">Thrown if no R installation is found</exception>
        public static string GetMostRecentInstalledRVersion()
        {
            // Ensure R directories are discovered
            if (!FindRDirectory())
            {
                throw new InvalidOperationException(
                    "No R installation found. Please install R or add an R installation directory in SkylineBatch settings.");
            }

            var rVersions = Settings.Default.RVersions;
            if (rVersions == null || rVersions.Count == 0)
            {
                throw new InvalidOperationException(
                    "No R versions found. Please install R or add an R installation directory in SkylineBatch settings.");
            }

            // Get all version strings and sort them as Version objects to find the most recent
            var sortedVersions = rVersions.Keys
                .Select(versionString =>
                {
                    if (Version.TryParse(versionString, out var version))
                        return new { VersionString = versionString, Version = version };
                    return null;
                })
                .Where(v => v != null)
                .OrderByDescending(v => v.Version)
                .ToList();

            if (sortedVersions.Count == 0)
            {
                // Fallback to first available if version parsing fails
                return rVersions.Keys.First();
            }

            return sortedVersions.First().VersionString;
        }
    }
}
