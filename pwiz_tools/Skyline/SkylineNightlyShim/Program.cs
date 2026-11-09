/*
 * Original author: Brian Pratt <bspratt .at. proteinms dot net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2018 University of Washington - Seattle, WA
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


//
// Small wrapper program for SkylineNightly, which is what the scheduled task runs. It updates the local
// SkylineNightly.exe (and itself) from the TeamCity artifacts, then starts a SkylineNightly run. What the
// run is comes from SkylineNightly's saved settings, so any arguments the task passes are ignored.
//

// ReSharper disable LocalizableElement

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Ionic.Zip;
using SkylineNightly;

namespace SkylineNightlyShim
{
    static class Program
    {
        private const string TEAM_CITY_BUILD_TYPE_64_MASTER = "bt209";
        private const string SKYLINENIGHTLY_ZIP = "SkylineNightly.zip";

        static void Log(string what)
        {
            var now = DateTime.Now.ToLocalTime();
            Console.WriteLine(what);
            try
            {
                using (StreamWriter w = File.AppendText("SkylineNightlyShim.log"))
                {
                    w.WriteLine("{0} {1}: {2}", now.ToShortDateString(), now.ToShortTimeString(), what); 
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        static void AttemptUpdate(string fileName, ZipFile zipfile)
        {
            var tmpName = fileName + "_"; // On most versions of Windows you can rename an exe or dll even if it is running
            try
            {
                if (File.Exists(tmpName))
                {
                    File.Delete(tmpName);
                }
            }
            catch (Exception e)
            {
                Log("unable to clear out old copy of " + tmpName + ": "+ e);
            }

            try
            {
                if (File.Exists(fileName)) // A file new to this zip has nothing to move aside
                    File.Move(fileName, tmpName);
            }
            catch (Exception e)
            {
                Log("unable to rename as " + tmpName + ": " + e);
            }

            try
            {
                zipfile.ExtractSelectedEntries(fileName, ExtractExistingFileAction.OverwriteSilently);
            }
            catch (Exception e)
            {
                Log("unable to update " + fileName + ": " + e);
            }

            try
            {
                if (File.Exists(tmpName))
                {
                    File.Delete(tmpName);
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
                // Probably still in use, we can get it next time
            }

        }

        static void Main(string[] args)
        {

            // Refuse to launch SkylineNightly if the test machine isn't configured with
            // a TeamCity token — without one, the nightly run can't fetch SkylineTester
            // either, and a clear message from the shim beats an obscure failure later.
            string teamCityToken;
            try
            {
                teamCityToken = TeamCityNightlyAuth.GetRequiredToken();
            }
            catch (IOException e)
            {
                Log(e.Message);
                return;
            }

            // Do our work in the SkylineNightly directory. AppContext.BaseDirectory replaces
            // Assembly.CodeBase, which was a file: URL and needed the unescaping below; it is
            // already a plain directory path, and unlike Assembly.Location it survives a
            // single-file publish. It carries a trailing separator, which GetDirectoryName
            // never produced, so trim it to keep the logged and combined paths as they were.
            // TrimEndingDirectorySeparator rather than TrimEnd: it leaves a path root alone, so an
            // install at a drive root stays "C:\" instead of becoming "C:", which Windows reads as
            // drive-relative and would resolve the ZIP path and working directory against the
            // drive's current directory rather than this one.
            var nightlyDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            if (!string.IsNullOrEmpty(nightlyDirectory))
                Directory.SetCurrentDirectory(nightlyDirectory);

            try
            {
                // Attempt to update SkylineNightly.exe
                string zipFileLink = TeamCityNightlyAuth.GetArtifactUrl(TEAM_CITY_BUILD_TYPE_64_MASTER, SKYLINENIGHTLY_ZIP, TeamCityNightlyAuth.GetSkylineNightlyBranchQuery(), false);
                var fileName = Path.Combine(nightlyDirectory, SKYLINENIGHTLY_ZIP);
                Log("Update " + nightlyDirectory + " with " + zipFileLink);
                TeamCityNightlyAuth.DownloadArtifact(zipFileLink, fileName, teamCityToken);
                using (var zipFile = new ZipFile(fileName))
                {
                    // Every top-level file in the zip, not a fixed list of names: a fixed list is
                    // what broke the update when the zip's contents changed from .NET Framework
                    // to .NET (see CreateZipInstallerWindow in SkylineTester).
                    var names = new List<string>();
                    foreach (var entry in zipFile.Entries)
                    {
                        if (!entry.IsDirectory && !entry.FileName.Contains('/'))
                            names.Add(entry.FileName);
                    }
                    foreach (var name in names)
                        AttemptUpdate(name, zipFile);
                }
            }
            catch (Exception e)
            {
                Log(e.Message);
                Log("Trouble updating SkylineNightly.exe, proceeding with existing installation");
            }

            // Start the run SkylineNightly's settings describe
            Process nightly = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = "SkylineNightly.exe",
                    WorkingDirectory = nightlyDirectory,
                    Arguments = "run",
                    CreateNoWindow = true
                }
            };

            nightly.Start();
        }
    }

}

