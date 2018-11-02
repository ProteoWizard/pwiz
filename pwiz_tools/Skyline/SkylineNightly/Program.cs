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

namespace SkylineNightly
{
    static class Program
    {
        private static bool PerformTests(Nightly.RunMode runMode, string arg, string decorateSrcDirName = null)
        {
            var nightly = new Nightly(runMode, decorateSrcDirName);
            var errMessage = nightly.RunAndPost();
            var message = string.Format("Completed {0}", arg); // Not L10N
            nightly.Finish(message, errMessage);
            return string.IsNullOrEmpty(errMessage);
        }

        private static void PerformTests(Nightly.RunMode runMode1, Nightly.RunMode runMode2, string arg)
        {
            PerformTests(runMode1, string.Format("part one of {0}", arg), runMode1 == runMode2 ? "A" : null);  // Not L10N
            // Don't kill existing test processes for the second run, we'd like to keep any hangs around for forensics
            PerformTests(runMode2, string.Format("part two of {0}", arg), runMode1 == runMode2 ? "B" : null);  // Not L10N
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
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

                Nightly.RunMode runMode;

                string message;
                string errMessage = string.Empty;
                Nightly nightly;

                switch (command)
                {
					case "run": // Not L10N
                    {
                        switch (args.Length)
                        {
                            case 2:
                            {
                                PerformTests((Nightly.RunMode) Enum.Parse(typeof(Nightly.RunMode), args[1]), args[1]);
                                break;
                            }
                            case 3:
                            {
                                PerformTests((Nightly.RunMode) Enum.Parse(typeof(Nightly.RunMode), args[1]),
                                    (Nightly.RunMode) Enum.Parse(typeof(Nightly.RunMode), args[2]),
									args[1] + " then " + args[2]); // Not L10N
                                break;
                            }
							default: throw new Exception("Wrong number of run modes specified, has to be 1 or 2"); // Not L10N
                        }

                        break;
                    }
                    case "indefinitely":
                    {
                        while (PerformTests((Nightly.RunMode) Enum.Parse(typeof(Nightly.RunMode), args[1]), args[1]))
                        {
                        }

                        break;
                    }
                    case "/?": // Not L10N
                    {
                        nightly = new Nightly(Nightly.RunMode.trunk);
						string commands = string.Join(" | ", // Not L10N
                            SkylineNightly.RunModes.Select(r => r.ToString()).ToArray());
                        message = string.Format("Usage: SkylineNightly run [{0}] [{1}]", commands, commands); // Not L10N
                        nightly.Finish(message, errMessage);
                        break;
                    }
					case "parse": // Not L10N
                    {
                        nightly = new Nightly(Nightly.RunMode.parse);
                        message = string.Format("Parse and post log {0}", nightly.GetLatestLog()); // Not L10N
                        nightly.StartLog(Nightly.RunMode.parse);
                        runMode = nightly.Parse();
                        message += string.Format(" as runmode {0}", runMode); // Not L10N
                        errMessage = nightly.Post(runMode);
                        nightly.Finish(message, errMessage);
                        break;
                    }
					case "post": // Not L10N
                    {
                        nightly = new Nightly(Nightly.RunMode.post);
                        message = string.Format("Post existing XML for {0}", nightly.GetLatestLog()); // Not L10N
                        nightly.StartLog(Nightly.RunMode.post);
                        runMode = nightly.Parse(null, true); // "true" means skip XML generation, just parse to figure out mode
                        message += string.Format(" as runmode {0}", runMode); // Not L10N
                        errMessage = nightly.Post(runMode);
                        nightly.Finish(message, errMessage);
                        break;
                    }
                    default:
                    {
                        var extension = Path.GetExtension(args[0]).ToLower();
                        if (extension == ".log") // Not L10N
                        {
                            nightly = new Nightly(Nightly.RunMode.parse);
                            nightly.StartLog(Nightly.RunMode.parse);
                            message = string.Format("Parse and post log {0}", args[0]); // Not L10N
                            runMode = nightly.Parse(args[0]); // Create the xml for this log file
                        }
                        else
                        {
                            nightly = new Nightly(Nightly.RunMode.post);
                            nightly.StartLog(Nightly.RunMode.post);
                            message = string.Format("Post existing XML {0}", args[0]); // Not L10N
                            runMode = nightly.Parse(Path.ChangeExtension(args[0], ".log"), true); // Scan the log file for this XML // Not L10N
                        }
                        message += string.Format(" as runmode {0}", runMode); // Not L10N
                        errMessage = nightly.Post(runMode, Path.ChangeExtension(args[0], ".xml")); // Not L10N
                        nightly.Finish(message, errMessage);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
				MessageBox.Show(@"Exception Caught: " + ex.Message, @"SkylineNightly.exe"); // Not L10N
            }
        }
    }
}
