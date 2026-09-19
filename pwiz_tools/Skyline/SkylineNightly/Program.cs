/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using SkylineNightly.Properties;

namespace SkylineNightly
{
    static class Program
    {
        private static string PerformTests(RunSpec runSpec, string arg, string decorateSrcDirName = null)
        {
            var nightly = new Nightly(runSpec, decorateSrcDirName);
            var nightlyTask = Nightly.NightlyTask;
            if (nightlyTask != null && DateTime.UtcNow.Add(nightly.TargetDuration).ToLocalTime() > nightlyTask.NextRunTime)
            {
                // Don't run, because the projected end time is after the start of the next scheduled start
                return null;
            }
            var errMessage = nightly.RunAndPost();
            var message = string.Format(@"Completed {0}", arg);
            nightly.Finish(message, errMessage);
            return errMessage;
        }

        private static void PerformTests(RunSpec runSpec1, RunSpec runSpec2, string arg)
        {
            bool sameRun = Equals(runSpec1, runSpec2);
            var result = PerformTests(runSpec1, string.Format(@"part one of {0}", arg), sameRun ? @"A" : null);
            if (Equals(result, Nightly.SkylineTesterStoppedByUser))
            {
                return; // If user killed the first half, assume we don't want the second half
            }
            // Don't kill existing test processes for the second run, we'd like to keep any hangs around for forensics
            PerformTests(runSpec2, string.Format(@"part two of {0}", arg), sameRun ? @"B" : null);
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (Settings.Default.SettingsUpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.SettingsUpgradeRequired = false;
                Settings.Default.Save();
            }

            if (args.Length == 0)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SkylineNightly());
                return;
            }

            try
            {
                var command = args[0].ToLower();

                RunSpec runSpec;

                string message;
                string errMessage = string.Empty;
                Nightly nightly;

                switch (command)
                {
                    case @"run":
                    {
                        // With no runs given, which is what the shim passes, the saved settings say what to run
                        var runSpecs = args.Length == 1
                            ? RunSpec.GetSavedRuns()
                            : args.Skip(1).Select(RunSpec.Parse).ToArray();
                        switch (runSpecs.Length)
                        {
                            case 1:
                            {
                                PerformTests(runSpecs[0], runSpecs[0].ToString());
                                break;
                            }
                            case 2:
                            {
                                PerformTests(runSpecs[0], runSpecs[1], runSpecs[0] + @" then " + runSpecs[1]);
                                break;
                            }
                            default: throw new Exception(@"Wrong number of runs specified, has to be 1 or 2");
                        }

                        break;
                    }
                    case "indefinitely":
                    {
                        while (string.IsNullOrEmpty(PerformTests(RunSpec.Parse(args[1]), args[1])))
                        {
                        }

                        break;
                    }
                    case @"/?":
                    {
                        nightly = Nightly.ForCommand(@"help");
                        string branches = string.Join(@"|", Enum.GetNames(typeof(Branch)));
                        string types = string.Join(@"|", SkylineNightly.RunTypes.Select(t => t.ToString()).ToArray());
                        message = string.Format(@"Usage: SkylineNightly run [{0}]/[{1}] [[{0}]/[{1}]]", branches, types);
                        nightly.Finish(message, errMessage);
                        break;
                    }
                    case @"parse":
                    {
                        nightly = Nightly.ForCommand(command);
                        message = string.Format(@"Parse and post log {0}", nightly.GetLatestLog());
                        nightly.StartLog();
                        runSpec = nightly.Parse();
                        message += string.Format(@" as run {0}", runSpec);
                        errMessage = nightly.Post(runSpec);
                        nightly.Finish(message, errMessage);
                        break;
                    }
                    case @"post":
                    {
                        nightly = Nightly.ForCommand(command);
                        message = string.Format(@"Post existing XML for {0}", nightly.GetLatestLog());
                        nightly.StartLog();
                        runSpec = nightly.Parse(null, true); // "true" means skip XML generation, just parse to figure out the run
                        message += string.Format(@" as run {0}", runSpec);
                        errMessage = nightly.Post(runSpec);
                        nightly.Finish(message, errMessage);
                        break;
                    }
                    default:
                    {
                        var extension = Path.GetExtension(args[0]).ToLower();
                        var dir = Path.GetDirectoryName(args[0]);
                        if (extension == @".log")
                        {
                            nightly = Nightly.ForCommand(@"parse", dir);
                            nightly.StartLog();
                            message = string.Format(@"Parse and post log {0}", args[0]);
                            runSpec = nightly.Parse(args[0]); // Create the xml for this log file
                        }
                        else
                        {
                            nightly = Nightly.ForCommand(@"post", dir);
                            nightly.StartLog();
                            message = string.Format(@"Post existing XML {0}", args[0]);
                            runSpec = nightly.Parse(Path.ChangeExtension(args[0], @".log"), true); // Scan the log file for this XML
                        }
                        message += string.Format(@" as run {0}", runSpec);
                        errMessage = nightly.Post(runSpec, Path.ChangeExtension(args[0], @".xml"));
                        nightly.Finish(message, errMessage);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(@"Exception Caught: " + ex.Message, @"SkylineNightly.exe");
            }
        }
    }
}
