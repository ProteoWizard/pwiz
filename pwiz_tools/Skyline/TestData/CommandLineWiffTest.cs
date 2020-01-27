/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class CommandLineWiffTest : AbstractUnitTestEx
    {
        private const string ZIP_PATH = @"TestData\CommandLineWiffTest.zip";
        private const string DOC_NAME = "wiffcmdtest.sky";
        private const string WIFF_NAME = "051309_digestion.wiff";

        private static readonly ImmutableList<string> SAMPLE_NAMES =
            ImmutableList.ValueOf(new[] {"blank", "rfp9_after_h_1", "test", "rfp9_before_h_1"});

        /// <summary>
        /// Tests importing results from a Wiff file containing multiple samples
        /// </summary>
        [TestMethod]
        public void TestWiffCommandLineImport()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_PATH);
            string docPath = testFilesDir.GetTestPath(DOC_NAME);
            string rawPath = testFilesDir.GetTestPath(WIFF_NAME);
            
            RunCommand("--in=" + docPath,
                "--import-file=" + rawPath,
                "--save");
            using (var stream = File.OpenRead(docPath))
            {
                var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
                Assert.IsTrue(doc.Settings.HasResults);
                Assert.AreEqual(SAMPLE_NAMES.Count, doc.Settings.MeasuredResults.Chromatograms.Count);
                for (int i = 0; i < SAMPLE_NAMES.Count; i++)
                {
                    var chromatogramSet = doc.Settings.MeasuredResults.Chromatograms[i];
                    Assert.AreEqual(SAMPLE_NAMES[i], chromatogramSet.Name);
                    Assert.AreEqual(1, chromatogramSet.MSDataFilePaths.Count());
                    var msDataFilePath = chromatogramSet.MSDataFilePaths.First() as MsDataFilePath;
                    Assert.IsNotNull(msDataFilePath);
                    Assert.AreEqual(i, msDataFilePath.SampleIndex);
                    Assert.AreEqual(SAMPLE_NAMES[i], msDataFilePath.SampleName);
                    Assert.AreEqual(rawPath, msDataFilePath.FilePath);
                }
            }
            // Import the file a second time, and make sure it does not result in 8 replicates.
            RunCommand("--in=" + docPath,
                "--import-file=" + rawPath,
                "--save");
            using (var stream = File.OpenRead(docPath))
            {
                var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
                Assert.IsTrue(doc.Settings.HasResults);
                Assert.AreEqual(SAMPLE_NAMES.Count, doc.Settings.MeasuredResults.Chromatograms.Count);
            }

            // Test importing results from two .wiff files with different file names but same sample names.
            // Make a copy of the wiff file, rename it and import it. The document should contain 8 replicates. 
            // From the 1st file: "blank", "rfp9_after_h_1", "test", "rfp9_before_h_1"
            // From the 2nd file: "blank2", "rfp9_after_h_12", "test2", "rfp9_before_h_12"
            var wiffCopy = Path.Combine(testFilesDir.FullPath,
                Path.GetFileNameWithoutExtension(rawPath) + "_copy.wiff");
            File.Copy(PathEx.SafePath(rawPath), wiffCopy);
            Assert.IsTrue(File.Exists(wiffCopy));
            var msg = RunCommand("--in=" + docPath,
                "--import-file=" + wiffCopy,
                "--save");
            CheckDocument(docPath, new[] { rawPath, wiffCopy }, 2);
            foreach (var sample in SAMPLE_NAMES)
            {
                CheckRunCommandOutputContains(
                    string.Format(
                        Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                        sample, sample + "2"), msg);
            }
        }

        /// <summary>
        /// Tests importing a wiff file containing multiple samples into a single replicate
        /// named "MyReplicate".
        /// </summary>
        [TestMethod]
        public void TestWiffCommandLineImportSingleReplicate()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_PATH);
            string docPath = testFilesDir.GetTestPath(DOC_NAME);
            string rawPath = testFilesDir.GetTestPath(WIFF_NAME);
            const string replicateName = "MyReplicate";

            RunCommand("--in=" + docPath,
                "--import-replicate-name=" + replicateName,
                "--import-file=" + rawPath,
                "--save");
            using (var stream = File.OpenRead(docPath))
            {
                var doc = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
                Assert.IsTrue(doc.Settings.HasResults);
                Assert.AreEqual(1, doc.Settings.MeasuredResults.Chromatograms.Count);
                var chromatogramSet = doc.Settings.MeasuredResults.Chromatograms.First();
                Assert.AreEqual(replicateName, chromatogramSet.Name);
                Assert.AreEqual(SAMPLE_NAMES.Count, chromatogramSet.MSDataFilePaths.Count());
                for (int i = 0; i < SAMPLE_NAMES.Count; i++)
                {
                    var msDataFilePath = chromatogramSet.MSDataFileInfos[i].FilePath as MsDataFilePath;
                    Assert.IsNotNull(msDataFilePath);
                    Assert.AreEqual(SAMPLE_NAMES[i], msDataFilePath.SampleName);
                    Assert.AreEqual(i, msDataFilePath.SampleIndex);
                }
            }
        }

        private static void CheckDocument(string docPath, string[] rawPaths, int importCount)
        {
            var doc = ResultsUtil.DeserializeDocument(docPath);
            Assert.IsTrue(doc.Settings.HasResults);
            Assert.AreEqual(SAMPLE_NAMES.Count * importCount, doc.Settings.MeasuredResults.Chromatograms.Count);
            for (int i = 0; i < SAMPLE_NAMES.Count; i++)
            {
                for (int j = 1; j <= importCount; j++)
                {
                    var chromIndex = i + SAMPLE_NAMES.Count * (j - 1);
                    var chromatogramSet = doc.Settings.MeasuredResults.Chromatograms[chromIndex];

                    var replicateName = SAMPLE_NAMES[i] + (j > 1 ? j.ToString() : "");
                    Assert.AreEqual(replicateName, chromatogramSet.Name);

                    Assert.AreEqual(1, chromatogramSet.MSDataFilePaths.Count());
                    var msDataFilePath = chromatogramSet.MSDataFilePaths.First() as MsDataFilePath;
                    Assert.IsNotNull(msDataFilePath);
                    Assert.AreEqual(i, msDataFilePath.SampleIndex);
                    Assert.AreEqual(SAMPLE_NAMES[i], msDataFilePath.SampleName);
                    Assert.AreEqual(rawPaths[j - 1], msDataFilePath.FilePath);
                }
            }
        }

        private static void CheckRunCommandOutputContains(string expectedMessage, string actualMessage)
        {
            Assert.IsTrue(actualMessage.Contains(expectedMessage),
                string.Format("Expected RunCommand result message that contains \n\"{0}\",\ngot\n\"{1}\"\ninstead.", expectedMessage, actualMessage));
        }
    }
}
