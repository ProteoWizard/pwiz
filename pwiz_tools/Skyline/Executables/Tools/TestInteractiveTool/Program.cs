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
using SkylineTool;

namespace TestInteractiveTool
{
    static class Program
    {
        // Report column indexes.
        private const int PeptideLinkColumn = 2;
        private const int PeptideReplicateLinkColumn = 4;

        private static SkylineToolClient _toolClient;
        private static TestToolService _testService;
        private static int _documentChangeCount;

        public static void Main(string[] args)
        {
            // Useful for debugging the tool after it starts.
            //System.Diagnostics.Debugger.Launch();

            var toolConnection = args[0];

            // Open connection to Skyline.
            using (_toolClient = new SkylineToolClient(toolConnection, "Test Interactive Tool")) // Not L10N
            {
                _toolClient.DocumentChanged += OnDocumentChanged;
                _testService = new TestToolService(toolConnection + "-test");
                Console.WriteLine("Test service running");
                    
                _testService.WaitForExit();
                Console.WriteLine("Tool finished");
            }
        }

        private class TestToolService : RemoteService, ITestTool
        {
            public TestToolService(string connectionName)
                : base(connectionName)
            {
            }

            public void TestSelect(string link)
            {
                Console.WriteLine("Select " + link);
                SelectLink(link, PeptideLinkColumn);
            }

            public void TestSelectReplicate(string link)
            {
                Console.WriteLine("SelectReplicate " + link);
                SelectLink(link, PeptideReplicateLinkColumn);
            }

            public string TestVersion()
            {
                Console.WriteLine("Version");
                try
                {
                    return _toolClient.SkylineVersion.ToString();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("EXCEPTION TestVersion: " + ex.StackTrace);
                    throw;
                }
            }

            public string TestDocumentPath()
            {
                Console.WriteLine("DocumentPath");
                return _toolClient.DocumentPath;
            }

            public string GetDocumentChangeCount()
            {
                return _documentChangeCount.ToString("D");
            }
        }

        /// <summary>
        /// Count each time the Skyline document changes.
        /// </summary>
        private static void OnDocumentChanged(object sender, EventArgs e)
        {
            _documentChangeCount++;
        }

        private static void SelectLink(string row, int linkColumn)
        {
            var report = _toolClient.GetReport("Peak Area");
            var link = report.Cells[int.Parse(row)][linkColumn];
            _toolClient.Select(link);
        }
    }
}
