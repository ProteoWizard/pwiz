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
            //Debugger.Launch();

            var toolConnection = args[0];

            // Open connection to Skyline.
            using (_toolClient = new SkylineToolClient(toolConnection, "Test Interactive Tool")) // Not L10N
            {
                _toolClient.DocumentChanged += OnDocumentChanged;
                _testService = new TestToolService(toolConnection + "-test");
                Console.WriteLine("Test service running");
                _testService.Run();
            }
        }

        private class TestToolService : RemoteService, ITestTool
        {
            public TestToolService(string connectionName)
                : base(connectionName)
            {
            }

            public float TestFloat(float data)
            {
                return data*2;
            }

            public float[] TestFloatArray()
            {
                return new[] {1.0f, 2.0f};
            }

            public string TestString()
            {
                return "test";
            }

            public string[] TestStringArray()
            {
                return new[] {"two", "strings"};
            }

            public SkylineTool.Version[] TestVersionArray()
            {
                return new[]
                {
                    new SkylineTool.Version {Major = 1},
                    new SkylineTool.Version {Minor = 2}
                };
            }

            public Chromatogram[] TestChromatogramArray()
            {
                return new[]
                {
                    new Chromatogram
                    {
                        PrecursorMz = 1.0,
                        ProductMz = 2.0,
                        Times = new[] {1.0f, 2.0f},
                        Intensities = new[] {10.0f, 20.0f}
                    },
                    new Chromatogram
                    {
                        PrecursorMz = 2.0,
                        ProductMz = 4.0,
                        Times = new[] {3.0f, 4.0f, 5.0f},
                        Intensities = new[] {30.0f, 40.0f, 50.0f}
                    }
                };
            }

            public void ImportFasta(string textFasta)
            {
                _toolClient.ImportFasta(textFasta);
            }

            public void TestAddSpectralLibrary(string libraryName, string libraryPath)
            {
                _toolClient.AddSpectralLibrary(libraryName, libraryPath);
            }

            public void TestSelect(string link)
            {
                if (!string.IsNullOrEmpty(link))
                    Console.WriteLine("Select " + link);
                SelectLink(link, PeptideLinkColumn);
            }

            public void TestSelectReplicate(string link)
            {
                Console.WriteLine("SelectReplicate " + link);
                SelectLink(link, PeptideReplicateLinkColumn);
            }

            public SkylineTool.Version TestVersion()
            {
                Console.WriteLine("Version");
                try
                {
                    return _toolClient.GetSkylineVersion();
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
                return _toolClient.GetDocumentPath();
            }

            public int GetDocumentChangeCount()
            {
                return _documentChangeCount;
            }

            public int Quit()
            {
                return Process.GetCurrentProcess().Id;
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
            if (string.IsNullOrEmpty(row))
            {
                _toolClient.SetDocumentLocation(null);
                return;
            }

            var report = _toolClient.GetReport("Peak Area");
            var link = report.Cells[int.Parse(row)][linkColumn];
            _toolClient.SetDocumentLocation(DocumentLocation.Parse(link));
        }
    }
}
