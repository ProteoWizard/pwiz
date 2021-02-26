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
using Microsoft.Win32;
using SkylineBatch.Properties;

namespace SkylineBatch
{

    public class RInstallations
    {
        // Finds and saves information about the computer's R installation locations
        private const string RegistryLocationR = @"SOFTWARE\R-core\R\";

        public static string RLocation => Settings.Default.RDir ?? "C:\\Program Files\\R";

        #region R

        public static bool FindRDirectory()
        {
            if (string.IsNullOrWhiteSpace(Settings.Default.RDir))
            {
                RegistryKey rKey = null;
                try
                {
                    rKey = Registry.LocalMachine.OpenSubKey(RegistryLocationR);
                }
                catch (Exception)
                {
                    // ignored
                }
                if (rKey == null)
                    return false;
                var latestRPath = rKey.GetValue(@"InstallPath") as string;
                Settings.Default.RDir = Path.GetDirectoryName(latestRPath);
            }

            InitRscriptExeList();
            return Settings.Default.RVersions.Count > 0;
        }

        private static void InitRscriptExeList()
        {
            var rPaths = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(Settings.Default.RDir))
            {
                return;
            }
            var rVersions = Directory.GetDirectories(Settings.Default.RDir);
            foreach (var rVersion in rVersions)
            {
                var folderName = Path.GetFileName(rVersion);
                if (folderName.StartsWith("R-"))
                {
                    var rScriptPath = rVersion + "\\bin\\Rscript.exe";
                    if (File.Exists(rScriptPath))
                    {
                        rPaths.Add(folderName.Substring(2), rScriptPath);
                    }
                }

            }
            Settings.Default.RVersions = rPaths;
        }

        #endregion
    }
}
