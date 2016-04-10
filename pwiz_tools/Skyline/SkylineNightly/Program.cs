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

            var nightly = new Nightly();
            Nightly.RunMode runMode;

            // ReSharper disable LocalizableElement
            string arg = args[0].ToLower();
            string message;
            switch (arg)
            {
                case HELP_ARG:
                    message = string.Format("Usage: SkylineNightly [" + string.Join(" | ", ARG_NAMES) + "]"); // Not L10N
                    break;

                case SCHEDULED_INTEGRATION_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    nightly.RunAndPost(Nightly.RunMode.nightly_integration);
                    break;

                case SCHEDULED_INTEGRATION_TRUNK_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    nightly.RunAndPost(Nightly.RunMode.nightly_integration);
                    nightly.RunAndPost(Nightly.RunMode.nightly);
                    break;

                case SCHEDULED_PERFTESTS_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    nightly.RunAndPost(Nightly.RunMode.nightly_with_perftests);
                    break;

                case SCHEDULED_STRESSTESTS_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    nightly.RunAndPost(Nightly.RunMode.nightly_with_stresstests);
                    break;

                case SCHEDULED_RELEASE_BRANCH_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    nightly.RunAndPost(Nightly.RunMode.release_branch_with_perftests);
                    break;

                case SCHEDULED_ARG:
                    message = string.Format("Completed {0}", arg); // Not L10N
                    nightly.RunAndPost(Nightly.RunMode.nightly);
                    break;

                case PARSE_ARG:
                    message = string.Format("Parse and post log {0}", nightly.GetLatestLog()); // Not L10N
                    nightly.StartLog();
                    runMode = nightly.Parse();
                    nightly.Post(runMode);
                    break;

                case POST_ARG:
                    message = string.Format("Post existing XML {0}", nightly.GetLatestLog()); // Not L10N
                    nightly.StartLog();
                    runMode = nightly.Parse(null, true);
                    nightly.Post(runMode);
                    break;

                default:
                    nightly.StartLog();
                    var extension = Path.GetExtension(arg).ToLower();
                    if (extension == ".log") // Not L10N
                    {
                        message = string.Format("Parse and post log {0}", arg); // Not L10N
                        runMode = nightly.Parse(arg); // Create the xml for this log file
                    }
                    else
                    {
                        message = string.Format("Post existing XML {0}", arg); // Not L10N
                        runMode = nightly.Parse(Path.ChangeExtension(arg, ".log"), true); // Scan the log file for this XML // Not L10N
                    }
                    nightly.Post(runMode, Path.ChangeExtension(arg, ".xml")); // Not L10N
                    break;
            }
            // ReSharper restore LocalizableElement
            nightly.Finish(); // Kill the screengrab thread, if any, so we can exit
            // Give some indication that this process ran on this machine
            MessageBox.Show(message);
        }
    }
}
