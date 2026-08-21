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
        private static string PerformTests(Nightly.RunMode runMode, string arg, string decorateSrcDirName = null,
            bool reuseCheckout = false, bool localSkylineTester = false)
        {
            var nightly = new Nightly(runMode, decorateSrcDirName, null, reuseCheckout, localSkylineTester);
            var nightlyTask = Nightly.NightlyTask;
            // A task with no enabled trigger has no next run time, which the Task Scheduler reports as a
            // zero date. That must not be read as "the next run has already started", or every manual
            // "SkylineNightly run <mode>" silently does nothing at all - no window, no log, exit code 0.
            var nextRunTime = nightlyTask?.NextRunTime ?? DateTime.MinValue;
            if (nextRunTime > DateTime.Now && DateTime.UtcNow.Add(nightly.TargetDuration).ToLocalTime() > nextRunTime)
            {
                // Don't run, because the projected end time is after the start of the next scheduled start
                nightly.Finish(string.Format(@"Skipped {0}: a {1}h run would overrun the next scheduled start at {2}",
                    arg, nightly.TargetDuration.TotalHours, nextRunTime), string.Empty);
                return null;
            }
            var errMessage = nightly.RunAndPost();
            var message = string.Format(@"Completed {0}", arg);
            nightly.Finish(message, errMessage);
            return errMessage;
        }

        private static void PerformTests(Nightly.RunMode runMode1, Nightly.RunMode runMode2, string arg,
            bool reuseCheckout = false, bool localSkylineTester = false)
        {
            var result = PerformTests(runMode1, string.Format(@"part one of {0}", arg), runMode1 == runMode2 ? @"A" : null,
                reuseCheckout, localSkylineTester);
            if (Equals(result, Nightly.SkylineTesterStoppedByUser))
            {
                return; // If user killed the first half, assume we don't want the second half
            }
            // Don't kill existing test processes for the second run, we'd like to keep any hangs around for forensics
            PerformTests(runMode2, string.Format(@"part two of {0}", arg), runMode1 == runMode2 ? @"B" : null,
                reuseCheckout, localSkylineTester);
        }

        /// <summary>
        /// Reuse the source tree from the previous run instead of deleting and re-cloning it.
        /// SkylineNightly keeps its checkout inside the SkylineTester folder it normally wipes each
        /// run, and separately tells SkylineTester to nuke and re-clone; this turns off both, so the
        /// tree is synced with "git pull" instead.
        /// </summary>
        private const string REUSE_CHECKOUT_OPTION = @"--reuse-checkout";

        /// <summary>
        /// Install a locally built SkylineTester.zip instead of downloading one from TeamCity.
        /// Lets a change to SkylineTester itself be exercised without first pushing the branch and
        /// waiting for TeamCity to publish a new zip. Produce the zip with
        /// "build.bat Release SkylineTester.zip".
        /// </summary>
        private const string LOCAL_TESTER_OPTION = @"--local";

        private static bool HasOption(string[] args, string option)
        {
            return args.Any(a => string.Equals(a, option, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsKnownOption(string arg)
        {
            return string.Equals(arg, REUSE_CHECKOUT_OPTION, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(arg, LOCAL_TESTER_OPTION, StringComparison.OrdinalIgnoreCase);
        }

        private static string UsageMessage()
        {
            string commands = string.Join(@" | ", SkylineNightly.RunModes.Select(r => r.ToString()).ToArray());
            return string.Format(@"Usage: SkylineNightly run [{0}] [{1}] [{2}] [{3}]",
                commands, commands, REUSE_CHECKOUT_OPTION, LOCAL_TESTER_OPTION);
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
                // Options are order-independent and are removed before the parsing below, which
                // dispatches on args.Length and would otherwise count them as run modes. Bad usage
                // is thrown rather than reported here, so it reaches the existing handler below.
                var reuseCheckout = HasOption(args, REUSE_CHECKOUT_OPTION);
                var localSkylineTester = HasOption(args, LOCAL_TESTER_OPTION);
                var unknownOption = args.FirstOrDefault(a =>
                    a.StartsWith(@"--", StringComparison.Ordinal) && !IsKnownOption(a));
                if (unknownOption != null)
                    throw new Exception(string.Format(@"Unknown option {0}. {1}", unknownOption, UsageMessage()));
                args = args.Where(a => !a.StartsWith(@"--", StringComparison.Ordinal)).ToArray();
                if (args.Length == 0)
                    throw new Exception(UsageMessage());

                var command = args[0].ToLower();

                Nightly.RunMode runMode;

                string message;
                string errMessage = string.Empty;
                Nightly nightly;

                switch (command)
                {
                    case @"run":
                    {
                        switch (args.Length)
                        {
                            case 2:
                            {
                                PerformTests((Nightly.RunMode) Enum.Parse(typeof(Nightly.RunMode), args[1]), args[1],
                                    null, reuseCheckout, localSkylineTester);
                                break;
                            }
                            case 3:
                            {
                                PerformTests((Nightly.RunMode) Enum.Parse(typeof(Nightly.RunMode), args[1]),
                                    (Nightly.RunMode) Enum.Parse(typeof(Nightly.RunMode), args[2]),
                                    args[1] + @" then " + args[2], reuseCheckout, localSkylineTester);
                                break;
                            }
                            default: throw new Exception(@"Wrong number of run modes specified, has to be 1 or 2");
                        }

                        break;
                    }
                    case "indefinitely":
                    {
                        while (string.IsNullOrEmpty(PerformTests((Nightly.RunMode) Enum.Parse(typeof(Nightly.RunMode), args[1]), args[1],
                                   null, reuseCheckout, localSkylineTester)))
                        {
                        }

                        break;
                    }
                    case @"/?":
                    {
                        nightly = new Nightly(Nightly.RunMode.trunk);
                        message = UsageMessage();
                        nightly.Finish(message, errMessage);
                        break;
                    }
                    case @"parse":
                    {
                        nightly = new Nightly(Nightly.RunMode.parse);
                        message = string.Format(@"Parse and post log {0}", nightly.GetLatestLog());
                        nightly.StartLog(Nightly.RunMode.parse);
                        runMode = nightly.Parse();
                        message += string.Format(@" as runmode {0}", runMode);
                        errMessage = nightly.Post(runMode);
                        nightly.Finish(message, errMessage);
                        break;
                    }
                    case @"post":
                    {
                        nightly = new Nightly(Nightly.RunMode.post);
                        message = string.Format(@"Post existing XML for {0}", nightly.GetLatestLog());
                        nightly.StartLog(Nightly.RunMode.post);
                        runMode = nightly.Parse(null, true); // "true" means skip XML generation, just parse to figure out mode
                        message += string.Format(@" as runmode {0}", runMode);
                        errMessage = nightly.Post(runMode);
                        nightly.Finish(message, errMessage);
                        break;
                    }
                    default:
                    {
                        var extension = Path.GetExtension(args[0]).ToLower();
                        var dir = Path.GetDirectoryName(args[0]);
                        if (extension == @".log")
                        {
                            nightly = new Nightly(Nightly.RunMode.parse, null, dir);
                            nightly.StartLog(Nightly.RunMode.parse);
                            message = string.Format(@"Parse and post log {0}", args[0]);
                            runMode = nightly.Parse(args[0]); // Create the xml for this log file
                        }
                        else
                        {
                            nightly = new Nightly(Nightly.RunMode.post, null, dir);
                            nightly.StartLog(Nightly.RunMode.post);
                            message = string.Format(@"Post existing XML {0}", args[0]);
                            runMode = nightly.Parse(Path.ChangeExtension(args[0], @".log"), true); // Scan the log file for this XML
                        }
                        message += string.Format(@" as runmode {0}", runMode);
                        errMessage = nightly.Post(runMode, Path.ChangeExtension(args[0], @".xml"));
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
