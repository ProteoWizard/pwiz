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

namespace Test
{
    
    /// <summary>
    ///This is a test class for MainLogicTest and is intended
    ///to contain all MainLogicTest Unit Tests
    ///</summary>
    [TestClass]
    public class MainLogicTest
    {
        private readonly static string _workingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly static string _pwizRoot = Regex.Match(_workingDirectory, @"(.*)\\build-nt-x86\\.*").Groups[1].ToString();
        private readonly static string _vendorReadersDirectory = Path.Combine(_pwizRoot, @"pwiz\data\vendor_readers");
        private readonly static string[] _testPaths;

        static MainLogicTest ()
        {
            var testPaths = new List<string>();
            foreach (string dataPath in Directory.GetDirectories(_vendorReadersDirectory, "*.data", SearchOption.AllDirectories))
                foreach (string sourcePath in Directory.GetFiles(dataPath, "*").Union(Directory.GetDirectories(dataPath, "*")))
                {
                    string sourceType = ReaderList.FullReaderList.identify(sourcePath);

                    // HACK: Bruker FID isn't working with the unit test framework for some reason
                    if (!String.IsNullOrEmpty(sourceType) && sourceType != "Bruker FID")
                        testPaths.Add(sourcePath);
                }
            _testPaths = testPaths.ToArray();
        }

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        #endregion

        //Use TestInitialize to run code before running each test
        [TestInitialize]
        public void MyTestInitialize()
        {
            if (!File.Exists(Path.Combine(_workingDirectory, "msconvert.exe")))
                Assert.Fail("Command Line file not Found: " + Path.Combine(_workingDirectory, "msconvert.exe"));
        }

        //Use TestCleanup to run code after each test has run
        [TestCleanup]
        public void MyTestCleanup()
        {
            // delete all directories between tests
            Directory.GetDirectories(".", "*").ToList().ForEach(o => Directory.Delete(o, true));
        }

        private string RunFile(IEnumerable<string> testPaths, MainLogic logicAccessor, string[] extraArgs, string extension)
        {
            foreach (string filepath in testPaths)
            {
                var outputDirectory = Path.GetFileName(filepath);
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

                foreach (string runId in runIds)
                {
                    var outputFilepath = Directory.GetFiles(outputDirectory, "*" + runId + "." + extension).First();
                    var consoleOutput = Path.ChangeExtension(outputFilepath, ".console." + extension);
                    Assert.IsTrue(File.Exists(outputFilepath), "Console result file not found");
                    File.Move(outputFilepath, consoleOutput);
                }

                // start GUI conversion
                var config = logicAccessor.ParseCommandLine(outputDirectory, String.Format("{0}|{1}", String.Join("|", extraArgs).Replace("\"", String.Empty), filepath).Trim('|'));
                logicAccessor.Work(config);

                foreach (string runId in runIds)
                {
                    var outputFilepath = Directory.GetFiles(outputDirectory, "*" + runId + "." + extension).First();
                    var guiOutput = Path.ChangeExtension(outputFilepath, ".gui." + extension);
                    Assert.IsTrue(File.Exists(outputFilepath), "GUI result file not found");
                    File.Move(outputFilepath, guiOutput);
                }
            }

            return extension;
        }

        private void CompareFiles(string extension)
        {
            var consoleOutputs = Directory.GetFiles(".", "*.console." + extension, SearchOption.AllDirectories).OrderBy(o => o).ToList();
            var guiOutputs = Directory.GetFiles(".", "*.gui." + extension, SearchOption.AllDirectories).OrderBy(o => o).ToList();

            Assert.AreEqual(consoleOutputs.Count, guiOutputs.Count);
            for (int i = 0; i < consoleOutputs.Count; ++i)
            {
                string consoleOutput = consoleOutputs[i];
                string guiOutput = guiOutputs[i];

                var consoleInfo = new FileInfo(consoleOutput);
                var guiInfo = new FileInfo(guiOutput);

                // file sizes should be equal
                Assert.AreEqual(consoleInfo.Length, guiInfo.Length);

                conversionResult = new StringBuilder();
                var psi = new ProcessStartInfo(Path.Combine(_workingDirectory, "msdiff.exe"))
                              {
                                  Arguments = String.Format("\"{0}\" \"{1}\" -i", consoleOutput, guiOutput),
                                  UseShellExecute = false,
                                  CreateNoWindow = true,
                                  RedirectStandardOutput = true
                              };
                var proc = new Process {StartInfo = psi};
                proc.Start();
                proc.BeginOutputReadLine();
                proc.OutputDataReceived += DataReceived;
                proc.WaitForExit();

                var resultStringByLines = conversionResult.ToString().Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (resultStringByLines.Length > 0)
                    Assert.Fail(String.Format("{0} lines of difference found", resultStringByLines.Length));
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
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
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
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
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
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
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
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
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
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--cms2" },
                                    "cms2");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read vendor formats and write them as mzML.
        /// </summary>
        [TestMethod]
        public void Vendor_To_MzML_Test ()
        {
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
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
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
            var extension = RunFile(_testPaths,
                                    logicAccessor,
                                    new string[] { "--filter|\"msLevel 1\"" },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read any format, run peak picking on it, filter by MS level, and write as mzML.
        /// </summary>
        [TestMethod]
        public void FilterPeakPickingTest ()
        {
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
            var extension = RunFile(_testPaths,
                                    logicAccessor,
                                    new string[] { "--filter|\"peakPicking true 1\"", "--filter|\"msLevel 1\"" },
                                    "mzML");
            CompareFiles(extension);
        }

        /// <summary>
        /// Test that we can read any mzML, filter by activation type, and write as mzML.
        /// </summary>
        [TestMethod]
        public void FilterActivationTest ()
        {
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
            var extension = RunFile(_testPaths,
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
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
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
            var logicAccessor = new MainLogic(new ProgressForm.JobInfo());
            var extension = RunFile(_testPaths.Where(o => o.EndsWith(".mzML")),
                                    logicAccessor,
                                    new string[] { "--filter|\"scanNumber 1-50\"", "--filter|\"scanTime [1.2,4.2]\"", "--filter|\"mzWindow [400,800]\"" },
                                    "mzML");
            CompareFiles(extension);
        }
    }
}
