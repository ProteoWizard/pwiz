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
            SCHEDULED_INTEGRATION_TRUNK_ARG,
            SCHEDULED_PERFTESTS_ARG,
            SCHEDULED_PERFTEST_AND_TRUNK_ARG,
            SCHEDULED_STRESSTESTS_ARG
        };

        private static void PerformTests(Nightly.RunMode runMode, string message)
        {
            var nightly = new Nightly(runMode);
            var errMessage = nightly.RunAndPost();
            nightly.Finish(message, errMessage);            
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

            Nightly.RunMode runMode;

            // ReSharper disable LocalizableElement
            string arg = args[0].ToLower();
            string message;
            string errMessage = string.Empty;
            Nightly nightly;
            switch (arg)
            {
                case HELP_ARG:
                    nightly = new Nightly(Nightly.RunMode.trunk);
                    message = string.Format("Usage: SkylineNightly [" + string.Join(" | ", ARG_NAMES) + "]"); // Not L10N
                    nightly.Finish(message, errMessage);
                    break;
                    
                // Run the current integration branch
                case SCHEDULED_INTEGRATION_ARG:
                    PerformTests(Nightly.RunMode.integration, string.Format("Completed {0}", arg)); // Not L10N
                    break;

                // For machines that can test all day and all night:
                // Run current integration branch, then normal
                case SCHEDULED_INTEGRATION_TRUNK_ARG:
                    PerformTests(Nightly.RunMode.integration, string.Format("Completed part one of {0}", arg)); // Not L10N
                    PerformTests(Nightly.RunMode.trunk, string.Format("Completed part two of {0}", arg)); // Not L10N
                    break;

                // For machines that can test all day and all night:
                // Run normal mode, then perftests
                case SCHEDULED_PERFTEST_AND_TRUNK_ARG:
                    PerformTests(Nightly.RunMode.trunk, string.Format("Completed part one of {0}", arg)); // Not L10N
                    PerformTests(Nightly.RunMode.perf, string.Format("Completed part two of {0}", arg)); // Not L10N
                    break;

                case SCHEDULED_PERFTESTS_ARG:
                    PerformTests(Nightly.RunMode.perf, string.Format("Completed {0}", arg)); // Not L10N
                    break;

                case SCHEDULED_STRESSTESTS_ARG:
                    PerformTests(Nightly.RunMode.stress, string.Format("Completed {0}", arg)); // Not L10N
                    break;

                case SCHEDULED_RELEASE_BRANCH_ARG:
                    PerformTests(Nightly.RunMode.release, string.Format("Completed {0}", arg)); // Not L10N
                    break;

                case SCHEDULED_ARG:
                    PerformTests(Nightly.RunMode.trunk, string.Format("Completed {0}", arg)); // Not L10N
                    break;

                // Reparse and post the most recent log
                case PARSE_ARG:
                    nightly = new Nightly(Nightly.RunMode.parse);
                    message = string.Format("Parse and post log {0}", nightly.GetLatestLog()); // Not L10N
                    nightly.StartLog(Nightly.RunMode.parse);
                    runMode = nightly.Parse(nightly.GetLatestLog());
                    message += string.Format(" as runmode {0}", runMode); // Not L10N
                    errMessage = nightly.Post(runMode, nightly.GetLatestLog());
                    nightly.Finish(message, errMessage);
                    break;

                // Post the XML for the latest log without regenerating it
                case POST_ARG:
                    nightly = new Nightly(Nightly.RunMode.post);
                    message = string.Format("Post existing XML for {0}", nightly.GetLatestLog()); // Not L10N
                    nightly.StartLog(Nightly.RunMode.post);
                    runMode = nightly.Parse(nightly.GetLatestLog(), true); // "true" means skip XML generation, just parse to figure out mode
                    message += string.Format(" as runmode {0}", runMode); // Not L10N
                    errMessage = nightly.Post(runMode, nightly.GetLatestLog());
                    nightly.Finish(message, errMessage);
                    break;

                default:
                    var extension = Path.GetExtension(arg).ToLower();
                    if (extension == ".log") // Not L10N
                    {
                        nightly = new Nightly(Nightly.RunMode.parse);
                        nightly.StartLog(Nightly.RunMode.parse);
                        message = string.Format("Parse and post log {0}", arg); // Not L10N
                        runMode = nightly.Parse(arg); // Create the xml for this log file
                    }
                    else
                    {
                        nightly = new Nightly(Nightly.RunMode.post);
                        nightly.StartLog(Nightly.RunMode.post);
                        message = string.Format("Post existing XML {0}", arg); // Not L10N
                        runMode = nightly.Parse(Path.ChangeExtension(arg, ".log"), true); // Scan the log file for this XML // Not L10N
                    }
                    message += string.Format(" as runmode {0}", runMode); // Not L10N
                    errMessage = nightly.Post(runMode, Path.ChangeExtension(arg, ".xml")); // Not L10N
                    nightly.Finish(message, errMessage);
                    break;
            }

        }
    }
}
