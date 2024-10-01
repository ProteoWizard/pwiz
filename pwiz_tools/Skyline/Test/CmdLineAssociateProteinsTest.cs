/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests various combinations of "--associate-proteins-fasta" and "--save-settings" using SkylineCmd.exe
    /// </summary>
    [TestClass]
    public class CmdLineAssociateProteinsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCmdLineAssociateProteins()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"Test\CmdLineAssociateProteinsTest.data");
            var skylineExeFolder = Path.GetDirectoryName(typeof(SkylineWindow).Assembly.Location);
            Assert.IsNotNull(skylineExeFolder);
            var skylineCmdExePath = Path.Combine(skylineExeFolder, "SkylineCmd.exe");
            AssertFileExists(skylineCmdExePath);

            // Run the batch file "AssociateProteinsScript.bat". Its first argument is the
            // path to SkylineCmd.exe
            var batchFileName = TestFilesDir.GetTestPath("AssociateProteinsScript.bat");
            AssertFileExists(batchFileName);
            var workingDirectory = Path.GetDirectoryName(batchFileName);
            Assert.IsNotNull(workingDirectory);
            var processStartInfo = new ProcessStartInfo(batchFileName, skylineCmdExePath)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            };
            var outputWriter = new StringWriter();
            var processRunner = new ProcessRunner();
            IProgressStatus status = new ProgressStatus(string.Empty);
            processRunner.Run(processStartInfo, null, null, ref status, outputWriter);

            try
            {
                // The batch file is expected to have produced four documents with the following
                // expected numbers of proteins
                Assert.AreEqual(2, CountProteins(TestFilesDir.GetTestPath("Document1.sky")));
                Assert.AreEqual(2, CountProteins(TestFilesDir.GetTestPath("Document2.sky")));
                Assert.AreEqual(3, CountProteins(TestFilesDir.GetTestPath("Document3.sky")));
                Assert.AreEqual(2, CountProteins(TestFilesDir.GetTestPath("Document4.sky")));
            }
            catch (Exception)
            {
                // If the test fails, dump the output from running the .bat file
                Console.Out.WriteLine("Test has failed. Output from executing {0} {1}: {2}", processStartInfo.FileName, processStartInfo.Arguments, outputWriter);
                throw;
            }
        }

        private int CountProteins(string skyDocPath)
        {
            AssertFileExists(skyDocPath);
            var deserializer = new XmlSerializer(typeof(SrmDocument));
            using var stream = File.OpenRead(skyDocPath);
            var document = (SrmDocument) deserializer.Deserialize(stream);
            return document.MoleculeGroupCount;
        }

        private void AssertFileExists(string path)
        {
            Assert.IsTrue(File.Exists(path), "{0} does not exist", path);
        }
    }
}
