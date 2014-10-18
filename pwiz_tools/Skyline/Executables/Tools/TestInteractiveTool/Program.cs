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
using System.Threading;
using Version = SkylineTool.Version;

namespace TestInteractiveTool
{
    static class Program
    {
        // Report column indexes.
        private const int PeptideLinkColumn = 2;
        private const int PeptideReplicateLinkColumn = 4;

        private static readonly AutoResetEvent QuitEvent = new AutoResetEvent(false);
        private static SkylineTool.SkylineToolClient _toolClient;
        private static int _documentChangeCount;

        public static void Main(string[] args)
        {
            // Useful for debugging the tool after it starts.
            //System.Diagnostics.Debugger.Launch();

            Console.WriteLine("Tool starting...");

            // Open connection to Skyline.
            _toolClient = new SkylineTool.SkylineToolClient("Test Interactive Tool", args[0]); // Not L10N
            _toolClient.DocumentChanged += OnDocumentChanged;

            // Create a service so Skyline tests can drive this tool.
            using (new SkylineTool.SkylineToolService(typeof(TestToolService), args[0] + "-test"))
            {
                // Wait to be killed.
                Thread.Sleep(Timeout.Infinite);
            }
        }

        /// <summary>
        /// Count each time the Skyline document changes.
        /// </summary>
        private static void OnDocumentChanged(object sender, EventArgs e)
        {
            _documentChangeCount++;
        }

        /// <summary>
        /// This is the testing service that allows Skyline tests to drive this tool.
        /// </summary>
        private class TestToolService  : SkylineTool.ISkylineTool
        {
            public int RunTest(string testName)
            {
                Console.WriteLine(testName);

                // Split comma-separated arguments.
                var args = testName.Split(',');
                switch (args[0])
                {
                    // Select a peptide in Skyline.
                    case "select":
                        SelectLink(args[1], PeptideLinkColumn);
                        break;

                    // Select a peptide and replicate in Skyline.
                    case "selectreplicate":
                        SelectLink(args[1], PeptideReplicateLinkColumn);
                        break;

                    // Verify that SkylineVersion works.
                    case "version":
                    {
                        var version = _toolClient.SkylineVersion;
                        return (version.Major >= 0 && version.Minor >= 0 && version.Build >= 0 && version.Revision >= 0)
                            ? 1
                            : 0;
                    }

                    // Verify that DocumentPath works.
                    case "path":
                    {
                        var path = _toolClient.DocumentPath;
                        return path.EndsWith(args[1]) ? 1 : 0;
                    }

                    // Return the number of document changes that have been seen.
                    case "documentchanges":
                        return _documentChangeCount;

                    // Quit the tool.
                    case "quit":
                        return Process.GetCurrentProcess().Id;
                }

                return 0;
            }

            private static void SelectLink(string row, int linkColumn)
            {
                var thread = new Thread(() =>
                {
                    var report = _toolClient.GetReport("Peak Area");
                    var link = report.Cells[int.Parse(row)][linkColumn];
                    _toolClient.Select(link);
                });
                thread.Start();
            }

            public string GetReport(string toolName, string reportName)
            {
                throw new NotImplementedException();
            }

            public void Select(string link)
            {
                throw new NotImplementedException();
            }

            public string DocumentPath
            {
                get { throw new NotImplementedException(); }
            }

            public Version Version
            {
                get { throw new NotImplementedException(); }
            }

            public void NotifyDocumentChanged()
            {
                // Do nothing.  Don't throw or the error will prevent construction of the Client object.
            }
        }
    }
}
