//
// $Id$
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using MSConvertGUI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CLI.msdata;
using pwiz.Common.Collections;

namespace Test
{

    /// <summary>
    ///This is a test class for MainLogicTest and is intended
    ///to contain all MainLogicTest Unit Tests
    ///</summary>
    [TestClass]
    public class MainLogicTest
    {
        private static readonly string _workingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string _pwizRoot;
        private static readonly string _vendorReadersDirectory;
        private static readonly string[] _testPaths;
        private static string _testOutputRoot;

        static MainLogicTest ()
        {
            // Match build output paths like build-nt-x86\msvc-release-x86_64\ or build-nt-x86\msvc-debug\
            var match = Regex.Match(_workingDirectory, @"(.*)[\\/]build-nt-x86[\\/]");
            if (!match.Success)
            {
                _testPaths = Array.Empty<string>();
                return;
            }
            _pwizRoot = match.Groups[1].ToString();
            _vendorReadersDirectory = Path.Combine(_pwizRoot, @"pwiz\data\vendor_readers");

            if (!Directory.Exists(_vendorReadersDirectory))
            {
                _testPaths = Array.Empty<string>();
                return;
            }

            var testPaths = new List<string>();
            foreach (string dataPath in Directory.GetDirectories(_vendorReadersDirectory, "*.data", SearchOption.AllDirectories))
            {
                // Take only the first mzML file from each vendor directory to keep tests fast
                string firstMzML = Directory.GetFiles(dataPath, "*.mzML").OrderBy(f => f).FirstOrDefault();
                if (firstMzML != null)
                    testPaths.Add(firstMzML);

                // Also include the first non-mzML source (vendor format) from each directory
                foreach (string sourcePath in Directory.GetFiles(dataPath, "*").Union(Directory.GetDirectories(dataPath, "*")))
                {
                    if (sourcePath.EndsWith(".mzML", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string sourceType = ReaderList.FullReaderList.identify(sourcePath);
                    if (!String.IsNullOrEmpty(sourceType) && sourceType != "Bruker FID")
                    {
                        testPaths.Add(sourcePath);
                        break; // only first vendor source per directory
                    }
                }
            }
            _testPaths = testPaths.ToArray();
        }

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        //Use TestInitialize to run code before running each test
        [TestInitialize]
        public void MyTestInitialize()
        {
            if (_pwizRoot == null)
                Assert.Inconclusive("Could not determine pwiz root from working directory: " + _workingDirectory);

            if (!File.Exists(Path.Combine(_workingDirectory, "msconvert.exe")))
                Assert.Inconclusive("msconvert.exe not found in: " + _workingDirectory);

            // Create a clean temp directory for test output
            _testOutputRoot = Path.Combine(Path.GetTempPath(), "MSConvertGUITest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_testOutputRoot);
        }

        //Use TestCleanup to run code after each test has run
        [TestCleanup]
        public void MyTestCleanup()
        {
            if (_testOutputRoot != null && Directory.Exists(_testOutputRoot))
            {
                try { Directory.Delete(_testOutputRoot, true); }
                catch { /* best effort cleanup */ }
            }
        }

        private string _lastError;

        private MainLogic CreateMainLogic()
        {
            var logic = new MainLogic(new ProgressForm.JobInfo(), new Map<string, int>(), new object());
            _lastError = null;
            logic.LogUpdate = (msg, info) => { _lastError = msg; };
            return logic;
        }

        private string RunFile(IEnumerable<string> testPaths, MainLogic logicAccessor, string[] extraArgs, string extension)
        {
            foreach (string filepath in testPaths)
            {
                // Use a sanitized filename as the output subdirectory
                var safeName = Path.GetFileNameWithoutExtension(filepath);
                var outputDirectory = Path.Combine(_testOutputRoot, safeName);
                if (!Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                string[] runIds = ReaderList.FullReaderList.readIds(filepath);

                var psi = new ProcessStartInfo(Path.Combine(_workingDirectory, "msconvert.exe"))
                              {
                                  Arguments = String.Format("{0} --64 --outdir \"{1}\" \"{2}\"",
                                                            String.Join(" ", extraArgs).Replace('|', ' '),
                                                            outputDirectory,
                                                            filepath),
                                  UseShellExecute = false,
                                  CreateNoWindow = true
                              };
                var proc = new Process { StartInfo = psi };

                // start console conversion
                proc.Start();
                proc.WaitForExit();
                Assert.AreEqual(0, proc.ExitCode, "msconvert.exe failed for: " + filepath);

                // Find output files by runId — use simple enumeration to avoid pattern char issues
                bool hasConsoleOutput = false;
                foreach (string runId in runIds)
                {
                    var outputFilepath = FindOutputFile(outputDirectory, runId, extension);
                    if (outputFilepath == null || new FileInfo(outputFilepath).Length == 0)
                        continue; // some formats produce no/empty output for certain inputs (e.g. MGF with no MS2)
                    hasConsoleOutput = true;
                    var consoleOutput = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(outputFilepath) + ".console." + extension);
                    if (File.Exists(consoleOutput)) File.Delete(consoleOutput);
                    File.Move(outputFilepath, consoleOutput);
                }

                if (!hasConsoleOutput)
                    continue; // skip GUI conversion if console produced nothing

                // start GUI conversion (--64 matches the console invocation above)
                var config = logicAccessor.ParseCommandLine(outputDirectory, String.Format("--64|{0}|{1}", String.Join("|", extraArgs).Replace("\"", String.Empty), filepath).Trim('|'));
                logicAccessor.QueueWork(config);
                MainLogic.Work();

                foreach (string runId in runIds)
                {
                    var outputFilepath = FindOutputFile(outputDirectory, runId, extension);
                    if (outputFilepath == null)
                        continue;
                    var guiOutput = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(outputFilepath) + ".gui." + extension);
                    if (File.Exists(guiOutput)) File.Delete(guiOutput);
                    File.Move(outputFilepath, guiOutput);
                }
            }

            return extension;
        }

        /// <summary>
        /// Find an output file matching a runId without using glob patterns (avoids issues with special chars in runIds).
        /// msconvert replaces invalid filename chars (e.g. : with _), so we check both the original and sanitized runId.
        /// </summary>
        private static string FindOutputFile(string directory, string runId, string extension)
        {
            var suffix = "." + extension;
            // msconvert replaces characters invalid in filenames
            var sanitizedRunId = runId.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
            return Directory.GetFiles(directory)
                .Where(f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Contains(".console.") &&
                            !Path.GetFileName(f).Contains(".gui.") &&
                            (Path.GetFileName(f).Contains(runId) || Path.GetFileName(f).Contains(sanitizedRunId)))
                .FirstOrDefault();
        }

        private void CompareFiles(string extension)
        {
            var consoleOutputs = Directory.GetFiles(_testOutputRoot, "*.console." + extension, SearchOption.AllDirectories).OrderBy(o => o).ToList();
            var guiOutputs = Directory.GetFiles(_testOutputRoot, "*.gui." + extension, SearchOption.AllDirectories).OrderBy(o => o).ToList();

            Assert.AreEqual(consoleOutputs.Count, guiOutputs.Count,
                "Mismatch between console and GUI output file counts");

            var msdiffPath = Path.Combine(_workingDirectory, "msdiff.exe");
            for (int i = 0; i < consoleOutputs.Count; ++i)
            {
                string consoleOutput = consoleOutputs[i];
                string guiOutput = guiOutputs[i];

                var consoleInfo = new FileInfo(consoleOutput);
                var guiInfo = new FileInfo(guiOutput);

                Assert.IsTrue(consoleInfo.Length > 0, "Console output is empty: " + consoleOutput);
                Assert.IsTrue(guiInfo.Length > 0, "GUI output is empty: " + guiOutput);

                // Use msdiff for semantic comparison (ignoring metadata like command-line args)
                if (!File.Exists(msdiffPath))
                    continue;

                // Copy to temp paths without special chars (msdiff can't handle commas in paths)
                var msdiffDir = Path.Combine(Path.GetTempPath(), "msdiff_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(msdiffDir);
                var tempConsole = Path.Combine(msdiffDir, "console" + Path.GetExtension(consoleOutput));
                var tempGui = Path.Combine(msdiffDir, "gui" + Path.GetExtension(guiOutput));
                File.Copy(consoleOutput, tempConsole);
                File.Copy(guiOutput, tempGui);

                try
                {
                    conversionResult = new StringBuilder();
                    var psi = new ProcessStartInfo(msdiffPath)
                    {
                        Arguments = String.Format("\"{0}\" \"{1}\" -i", tempConsole, tempGui),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };
                    var proc = new Process { StartInfo = psi };
                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.OutputDataReceived += DataReceived;
                    proc.WaitForExit();

                    var resultStringByLines = conversionResult.ToString()
                        .Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (resultStringByLines.Length > 0)
                        Assert.Fail(String.Format("{0} lines of difference found between {1} and {2}",
                            resultStringByLines.Length, consoleOutput, guiOutput));
                }
                finally
                {
                    try { Directory.Delete(msdiffDir, true); } catch { }
                }
            }
        }

        private StringBuilder conversionResult;
        private void DataReceived(object sender, DataReceivedEventArgs e)
        {
            conversionResult.AppendLine(e.Data);
        }

        /// <summary>
        /// Test that we can read and write any mzML.
        /// </summary>
        [TestMethod]
        public void MzML_To_MzML_Test()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read mzML and write it as mzXML.
        /// </summary>
        [TestMethod]
        public void MzML_To_MzXML_Test()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] {"--mzXML"},
                                    "mzXML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read mzML and write it as MGF.
        /// </summary>
        [TestMethod]
        public void MzML_To_MGF_Test ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] {"--mgf"},
                                    "mgf");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read mzML and write it as MS2.
        /// </summary>
        [TestMethod]
        public void MzML_To_MS2_Test ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--ms2" },
                                    "ms2");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read mzML and write it as CMS2.
        /// </summary>
        [TestMethod]
        public void MzML_To_CMS2_Test ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--cms2" },
                                    "cms2");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read mzML and write it as mzMLb.
        /// </summary>
        [TestMethod]
        public void MzML_To_MzMLb_Test()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                logicAccessor,
                new string[] { "--mzMLb" },
                "mzMLb");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read vendor formats and write them as mzML.
        /// </summary>
        [TestMethod]
        public void Vendor_To_MzML_Test ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => !o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read any format, filter by MS level, and write as mzML.
        /// </summary>
        [TestMethod]
        public void FilterMsLevelTest ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--filter|\"msLevel 1\"" },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read mzML, run peak picking on it, filter by MS level, and write as mzML.
        /// </summary>
        [TestMethod]
        public void FilterPeakPickingTest ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--filter|\"peakPicking true 1\"", "--filter|\"msLevel 1\"" },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read mzML, run non-flanking zero removal on it, filter by MS level, and write as mzML.
        /// </summary>
        [TestMethod]
        public void FilterZeroSamplesTest()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--filter|\"zeroSamples removeExtra 1-3\"", "--filter|\"msLevel 1\"" },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read mzML, filter by activation type, and write as mzML.
        /// </summary>
        [TestMethod]
        public void FilterActivationTest ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--filter|\"activation ETD\"" },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read any mzML, run ETD filter on it, and write as mzML.
        /// </summary>
        [TestMethod]
        public void FilterETDFilterTest ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--filter|\"msLevel 2-\"", "--filter|\"activation ETD\"", "--filter|\"ETDFilter true true true false 3.1 mz\"" },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read any mzML, run subset filters on it, and write as mzML.
        /// </summary>
        [TestMethod]
        public void FilterSubsetTest ()
        {
            var logicAccessor = CreateMainLogic();
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--filter|\"scanNumber 1-50\"", "--filter|\"scanTime [1.2,4.2]\"", "--filter|\"mzWindow [400,800]\"" },
                                    "mzML");
            CompareFiles(extension);
        }
    }
}
