//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BumberDash;
using BumberDash.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace Tests
{


    /// <summary>
    ///This is a test class for ProgramHandlerTest and is intended
    ///to contain all ProgramHandlerTest Unit Tests
    ///</summary>
    [TestClass()]
    public class ProgramHandlerTest
    {
        //private readonly static string _workingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\Tests");
        private readonly static string _workingDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly static string _outputDirectory = Path.Combine(_workingDirectory, "Data\\Output");

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            if (!Directory.Exists(_outputDirectory))
                Directory.CreateDirectory(_outputDirectory);
        }
        //
        //Use ClassCleanup to run code after all tests in a class have run
        [ClassCleanup()]
        public static void MyClassCleanup()
        {
            if (File.Exists(Path.Combine(_outputDirectory, "directag_intensity_ranksum_bins.cache")))
                File.Delete(Path.Combine(_outputDirectory, "directag_intensity_ranksum_bins.cache"));
            if (Directory.Exists(_outputDirectory))
                Directory.Delete(_outputDirectory);
        }
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion


        ///// <summary>
        /////A test for DataReceived
        /////</summary>
        //[TestMethod()]
        //[DeploymentItem("BumberDash.exe")]
        //public void DataReceivedTest()
        //{
        //    ProgramHandler_Accessor target = new ProgramHandler_Accessor(); // TODO: Initialize to an appropriate value
        //    object sender = null; // TODO: Initialize to an appropriate value
        //    DataReceivedEventArgs e = null; // TODO: Initialize to an appropriate value
        //    target.DataReceived(sender, e);
        //    Assert.Inconclusive("A method that does not return a value cannot be verified.");
        //}

        ///// <summary>
        /////A test for ProgramHandler Constructor
        /////</summary>
        //[TestMethod()]
        //public void ProgramHandlerConstructorTest()
        //{
        //    ProgramHandler target = new ProgramHandler();
        //    Assert.Inconclusive("TODO: Implement code to verify target");
        //}

        private void RunFile(string neededFile, string neededDB, string destinationProgram)
        {
            RunFile(neededFile,neededDB,string.Empty,destinationProgram);
        }

        private StringBuilder log;
        private void RunFile(string neededFile, string neededDB, string neededLibrary, string destinationProgram)
        {
            log= new StringBuilder();
            var errorFound = false;
            var waitHandle = new AutoResetEvent(false);
            var target = new ProgramHandler();

            var hi = new HistoryItem
                         {
                             JobName = Path.GetDirectoryName(neededFile),
                             JobType = destinationProgram,
                             OutputDirectory = _outputDirectory,
                             ProteinDatabase = neededDB,
                             SpectralLibrary = destinationProgram == JobType.Library
                                                   ? neededLibrary
                                                   : null,
                             Cpus = 0,
                             CurrentStatus = string.Empty,
                             StartTime = null,
                             EndTime = null,
                             RowNumber = 0,
                             InitialConfigFile = new ConfigFile()
                                                     {
                                                         FilePath = "--Custom--",
                                                         PropertyList = new List<ConfigProperty>()
                                                     },
                             TagConfigFile = destinationProgram == JobType.Tag
                                                 ? new ConfigFile()
                                                       {
                                                           FilePath = "--Custom--",
                                                           PropertyList = new List<ConfigProperty>()
                                                       }
                                                 : null
                         };
            //File List
            hi.FileList = new List<InputFile>
                              {
                                  new InputFile
                                      {
                                          FilePath = "\"" + neededFile + "\"",
                                          HistoryItem = hi
                                      }
                              };

            //set end event
            target.JobFinished = (x, y) =>
                {
                    errorFound = y;
                    if (!errorFound && x)
                        target.StartNewJob(0, hi);
                    else
                        waitHandle.Set(); // signal that the finished event was raised
                };
            target.LogUpdate = x => log.AppendLine(x);
            target.ErrorForward = x => log.AppendLine(x);

            // call the async method
            target.StartNewJob(0, hi);

            // Wait until the event handler is invoked);
            if (!waitHandle.WaitOne(60000, false))
            {
                var logstring1 = log.ToString();
                if (logstring1 != "Test Case")
                    Assert.Fail("Test timed out." + Environment.NewLine + logstring1);
            }
            var logstring = log.ToString();
            Assert.IsFalse(errorFound, "Not as many output files as input" + Environment.NewLine + logstring);
        }

        #region Myri Tests

        [TestMethod()]
        public void Myri_mzXMLTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mzXMLTest.mzXML");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Myrimatch);

            if (File.Exists(Path.Combine(_outputDirectory, "mzXMLTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mzXMLTest.pepXML"));
            else
                Assert.Fail("mzMLTest.pepXML not found");
        }

        [TestMethod()]
        public void Myri_mzMLTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mzMLTest.mzML");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Myrimatch);

            if (File.Exists(Path.Combine(_outputDirectory, "mzMLTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mzMLTest.pepXML"));
            else
                Assert.Fail("mzMLTest.pepXML not found");
        }

        [TestMethod()]
        public void Myri_mz5Test()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mz5Test.mz5");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Myrimatch);

            if (File.Exists(Path.Combine(_outputDirectory, "mz5Test.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mz5Test.pepXML"));
            else
                Assert.Fail("mz5Test.pepXML not found");
        }

        [TestMethod()]
        public void Myri_ThermoTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\ThermoTest.raw");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Myrimatch);

            if (File.Exists(Path.Combine(_outputDirectory, "ThermoTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "ThermoTest.pepXML"));
            else
                Assert.Fail("ThermoTest.pepXML not found");
        }

        [TestMethod()]
        public void Myri_AgilentTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\AgilentTest.d");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Myrimatch);

            if (File.Exists(Path.Combine(_outputDirectory, "AgilentTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "AgilentTest.pepXML"));
            else
                Assert.Fail("AgilentTest.pepXML not found");
        }

        [TestMethod()]
        public void Myri_BrukerTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\BrukerTest.d");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Myrimatch);

            if (File.Exists(Path.Combine(_outputDirectory, "BrukerTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "BrukerTest.pepXML"));
            else
                Assert.Fail("BrukerTest.pepXML not found");
        }

        [TestMethod()]
        public void Myri_WatersTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\WatersTest.raw");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Myrimatch);

            if (File.Exists(Path.Combine(_outputDirectory, "WatersTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "WatersTest.pepXML"));
            else
                Assert.Fail("WatersTest.pepXML not found");
        }

        #endregion

        #region Tag Tests

        [TestMethod()]
        public void Tag_mzXMLTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mzXMLTest.mzXML");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Tag);

            if (File.Exists(Path.Combine(_outputDirectory, "mzXMLTest.tags")))
                File.Delete(Path.Combine(_outputDirectory, "mzXMLTest.tags"));
            else
                Assert.Fail("mzXMLTest.tags not found");

            if (File.Exists(Path.Combine(_outputDirectory, "mzXMLTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mzXMLTest.pepXML"));
            else
                Assert.Fail("mzXMLTest.pepXML not found");
        }

        [TestMethod()]
        public void Tag_mzMLTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mzMLTest.mzML");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Tag);

            if (File.Exists(Path.Combine(_outputDirectory, "mzMLTest.tags")))
                File.Delete(Path.Combine(_outputDirectory, "mzMLTest.tags"));
            else
                Assert.Fail("mzMLTest.tags not found");

            if (File.Exists(Path.Combine(_outputDirectory, "mzMLTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mzMLTest.pepXML"));
            else
                Assert.Fail("mzMLTest.pepXML not found");
        }

        [TestMethod()]
        public void Tag_mz5Test()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mz5Test.mz5");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Tag);

            if (File.Exists(Path.Combine(_outputDirectory, "mz5Test.tags")))
                File.Delete(Path.Combine(_outputDirectory, "mz5Test.tags"));
            else
                Assert.Fail("mz5Test.tags not found");

            if (File.Exists(Path.Combine(_outputDirectory, "mz5Test.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mz5Test.pepXML"));
            else
                Assert.Fail("mz5Test.pepXML not found");
        }

        [TestMethod()]
        public void Tag_ThermoTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\ThermoTest.raw");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Tag);

            if (File.Exists(Path.Combine(_outputDirectory, "ThermoTest.tags")))
                File.Delete(Path.Combine(_outputDirectory, "ThermoTest.tags"));
            else
                Assert.Fail("ThermoTest.tags not found");

            if (File.Exists(Path.Combine(_outputDirectory, "ThermoTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "ThermoTest.pepXML"));
            else
                Assert.Fail("ThermoTest.pepXML not found");
        }

        [TestMethod()]
        public void Tag_AgilentTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\AgilentTest.d");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Tag);

            if (File.Exists(Path.Combine(_outputDirectory, "AgilentTest.tags")))
                File.Delete(Path.Combine(_outputDirectory, "AgilentTest.tags"));
            else
                Assert.Fail("AgilentTest.tags not found");

            if (File.Exists(Path.Combine(_outputDirectory, "AgilentTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "AgilentTest.pepXML"));
            else
                Assert.Fail("AgilentTest.pepXML not found");
        }

        [TestMethod()]
        public void Tag_BrukerTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\BrukerTest.d");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Tag);

            if (File.Exists(Path.Combine(_outputDirectory, "BrukerTest.tags")))
                File.Delete(Path.Combine(_outputDirectory, "BrukerTest.tags"));
            else
                Assert.Fail("BrukerTest.tags not found");

            if (File.Exists(Path.Combine(_outputDirectory, "BrukerTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "BrukerTest.pepXML"));
            else
                Assert.Fail("BrukerTest.pepXML not found");
        }

        [TestMethod()]
        public void Tag_WatersTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\WatersTest.raw");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");

            RunFile(neededFile, neededDB, JobType.Tag);

            if (File.Exists(Path.Combine(_outputDirectory, "WatersTest.tags")))
                File.Delete(Path.Combine(_outputDirectory, "WatersTest.tags"));
            else
                Assert.Fail("WatersTest.tags not found");

            if (File.Exists(Path.Combine(_outputDirectory, "WatersTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "WatersTest.pepXML"));
            else
                Assert.Fail("WatersTest.pepXML not found");
        }

        #endregion

        #region Pepitome Tests

        [TestMethod()]
        public void Pep_mzXMLTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mzXMLTest.mzXML");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            var neededLib = Path.Combine(_workingDirectory, "Data\\tinyLib.sptxt");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");
            Assert.IsTrue(File.Exists(neededLib), "Test library not found");

            RunFile(neededFile, neededDB, neededLib, JobType.Library);

            if (File.Exists(Path.Combine(_outputDirectory, "mzXMLTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mzXMLTest.pepXML"));
            else
                Assert.Fail("mzMLTest.pepXML not found");
        }

        [TestMethod()]
        public void Pep_mzMLTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mzMLTest.mzML");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            var neededLib = Path.Combine(_workingDirectory, "Data\\tinyLib.sptxt");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");
            Assert.IsTrue(File.Exists(neededLib), "Test library not found");

            RunFile(neededFile, neededDB, neededLib, JobType.Library);

            if (File.Exists(Path.Combine(_outputDirectory, "mzMLTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mzMLTest.pepXML"));
            else
                Assert.Fail("mzMLTest.pepXML not found");
        }

        [TestMethod()]
        public void Pep_mz5Test()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\mz5Test.mz5");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            var neededLib = Path.Combine(_workingDirectory, "Data\\tinyLib.sptxt");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");
            Assert.IsTrue(File.Exists(neededLib), "Test library not found");

            RunFile(neededFile, neededDB, neededLib, JobType.Library);

            if (File.Exists(Path.Combine(_outputDirectory, "mz5Test.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "mz5Test.pepXML"));
            else
                Assert.Fail("mz5Test.pepXML not found");
        }

        [TestMethod()]
        public void Pep_ThermoTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\ThermoTest.raw");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            var neededLib = Path.Combine(_workingDirectory, "Data\\tinyLib.sptxt");
            Assert.IsTrue(File.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");
            Assert.IsTrue(File.Exists(neededLib), "Test library not found");

            RunFile(neededFile, neededDB, neededLib, JobType.Library);

            if (File.Exists(Path.Combine(_outputDirectory, "ThermoTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "ThermoTest.pepXML"));
            else
                Assert.Fail("ThermoTest.pepXML not found");
        }

        [TestMethod()]
        public void Pep_AgilentTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\AgilentTest.d");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            var neededLib = Path.Combine(_workingDirectory, "Data\\tinyLib.sptxt");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");
            Assert.IsTrue(File.Exists(neededLib), "Test library not found");

            RunFile(neededFile, neededDB, neededLib, JobType.Library);

            if (File.Exists(Path.Combine(_outputDirectory, "AgilentTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "AgilentTest.pepXML"));
            else
                Assert.Fail("AgilentTest.pepXML not found");
        }

        [TestMethod()]
        public void Pep_BrukerTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\BrukerTest.d");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            var neededLib = Path.Combine(_workingDirectory, "Data\\tinyLib.sptxt");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");
            Assert.IsTrue(File.Exists(neededLib), "Test library not found");

            RunFile(neededFile, neededDB, neededLib, JobType.Library);

            if (File.Exists(Path.Combine(_outputDirectory, "BrukerTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "BrukerTest.pepXML"));
            else
                Assert.Fail("BrukerTest.pepXML not found");
        }

        [TestMethod()]
        public void Pep_WatersTest()
        {
            var neededFile = Path.Combine(_workingDirectory, "Data\\WatersTest.raw");
            var neededDB = Path.Combine(_workingDirectory, "Data\\tinyDB.fasta");
            var neededLib = Path.Combine(_workingDirectory, "Data\\tinyLib.sptxt");
            Assert.IsTrue(Directory.Exists(neededFile), "Test file not found");
            Assert.IsTrue(File.Exists(neededDB), "Test database not found");
            Assert.IsTrue(File.Exists(neededLib), "Test library not found");

            RunFile(neededFile, neededDB, neededLib, JobType.Library);

            if (File.Exists(Path.Combine(_outputDirectory, "WatersTest.pepXML")))
                File.Delete(Path.Combine(_outputDirectory, "WatersTest.pepXML"));
            else
                Assert.Fail("WatersTest.pepXML not found");
        }

        #endregion
    }
}
