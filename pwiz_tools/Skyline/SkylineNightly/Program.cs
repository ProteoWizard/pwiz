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
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace SkylineNightly
{
    static class Program
    {
        public const string HELP_ARG = "/?";    // Not L10N
        public const string SCHEDULED_ARG = "scheduled"; // Not L10N
        public const string SCHEDULED_PERFTESTS_ARG = SCHEDULED_ARG + "_with_perftests"; // Not L10N
        public const string SCHEDULED_STRESSTESTS_ARG = SCHEDULED_ARG + "_with_stresstests"; // Not L10N
        public const string SCHEDULED_RELEASE_BRANCH_ARG = SCHEDULED_ARG + "_release_branch"; // Not L10N
        public const string SCHEDULED_INTEGRATION_ARG = SCHEDULED_ARG + "_integration_branch"; // Not L10N
        public const string SCHEDULED_INTEGRATION_TRUNK_ARG = SCHEDULED_ARG + "_integration_and_trunk"; // Not L10N
        public const string SCHEDULED_PERFTEST_AND_TRUNK_ARG = SCHEDULED_ARG + "_perftests_and_trunk"; // Not L10N
        public const string PARSE_ARG = "parse"; // Not L10N
        public const string POST_ARG = "post"; // Not L10N

        public static readonly string[] ARG_NAMES =
        {
            HELP_ARG,
            PARSE_ARG,
            POST_ARG,
            SCHEDULED_ARG,
            SCHEDULED_RELEASE_BRANCH_ARG,
            SCHEDULED_INTEGRATION_ARG,
            SCHEDULED_PERFTESTS_ARG,
            SCHEDULED_STRESSTESTS_ARG
        };

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

            Nightly.RunMode runMode;

            // ReSharper disable LocalizableElement
            string arg = args[0].ToLower();
            var nightly = new Nightly(arg);
            string message;
            string errMessage = string.Empty;
            switch (arg)
            {
                case HELP_ARG:
                    message = string.Format("Usage: SkylineNightly [" + string.Join(" | ", ARG_NAMES) + "]"); // Not L10N
                    break;
                    
                // Run the current integration branch
                case SCHEDULED_INTEGRATION_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    errMessage = nightly.RunAndPost(Nightly.RunMode.nightly_integration);
                    break;

                // For machines that can test all day and all night:
                // Run current integration branch, then normal
                case SCHEDULED_INTEGRATION_TRUNK_ARG:
                    message = string.Format("Completed part one of {0}", arg); // Not L10N
                    errMessage = nightly.RunAndPost(Nightly.RunMode.nightly_integration);
                    Report(nightly, message, errMessage);

                    message = string.Format("Completed part two of {0}", arg); // Not L10N
                    nightly = new Nightly(SCHEDULED_INTEGRATION_ARG);
                    errMessage = nightly.RunAndPost(Nightly.RunMode.nightly);
                    break;

                // For machines that can test all day and all night:
                // Run normal mode, then perftests
                case SCHEDULED_PERFTEST_AND_TRUNK_ARG: 
                    message = string.Format("Completed part one of {0}", arg); // Not L10N
                    errMessage = nightly.RunAndPost(Nightly.RunMode.nightly);
                    Report(nightly, message, errMessage);

                    message = string.Format("Completed part two of {0}", arg); // Not L10N
                    nightly = new Nightly(SCHEDULED_PERFTESTS_ARG);
                    errMessage = nightly.RunAndPost(Nightly.RunMode.nightly_with_perftests); // Not L10N
                    break;

                case SCHEDULED_PERFTESTS_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    errMessage = nightly.RunAndPost(Nightly.RunMode.nightly_with_perftests);
                    break;

                case SCHEDULED_STRESSTESTS_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    errMessage = nightly.RunAndPost(Nightly.RunMode.nightly_with_stresstests);
                    break;

                case SCHEDULED_RELEASE_BRANCH_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    errMessage = nightly.RunAndPost(Nightly.RunMode.release_branch_with_perftests);
                    break;

                case SCHEDULED_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    errMessage = nightly.RunAndPost(Nightly.RunMode.nightly);
                    break;

                case PARSE_ARG:
                    message = string.Format("Parse and post log {0}", nightly.GetLatestLog()); // Not L10N
                    nightly.StartLog(Nightly.RunMode.parse);
                    runMode = nightly.Parse();
                    message += string.Format(" as runmode {0}", runMode); // Not L10N
                    errMessage = nightly.Post(runMode);
                    break;

                case POST_ARG:
                    message = string.Format("Post existing XML {0}", nightly.GetLatestLog()); // Not L10N
                    nightly.StartLog(Nightly.RunMode.post);
                    runMode = nightly.Parse(null, true);
                    message += string.Format(" as runmode {0}", runMode); // Not L10N
                    errMessage = nightly.Post(runMode);
                    break;

                default:
                    var extension = Path.GetExtension(arg).ToLower();
                    if (extension == ".log") // Not L10N
                    {
                        nightly.StartLog(Nightly.RunMode.parse);
                        message = string.Format("Parse and post log {0}", arg); // Not L10N
                        runMode = nightly.Parse(arg); // Create the xml for this log file
                    }
                    else
                    {
                        nightly.StartLog(Nightly.RunMode.post);
                        message = string.Format("Post existing XML {0}", arg); // Not L10N
                        runMode = nightly.Parse(Path.ChangeExtension(arg, ".log"), true); // Scan the log file for this XML // Not L10N
                    }
                    message += string.Format(" as runmode {0}", runMode); // Not L10N
                    errMessage = nightly.Post(runMode, Path.ChangeExtension(arg, ".xml")); // Not L10N
                    break;
            }

            Report(nightly, message, errMessage);
            nightly.Finish(); // Kill the screengrab thread, if any, so we can exit
        }

        private static void Report(Nightly nightly, string message, string errMessage)
        {
            // Leave a note for the user, in a way that won't interfere with our next run
            nightly.Log("Done.  Exit message:"); // Not L10N
            nightly.Log(message);
            if (!string.IsNullOrEmpty(errMessage))
                nightly.Log(errMessage);
            var process = new Process
            {
                StartInfo =
                {
                    FileName = "notepad.exe", // Not L10N
                    Arguments = nightly.LogFileName
                }
            };
            process.Start();
        }
    }
}
