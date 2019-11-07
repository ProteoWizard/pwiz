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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class CommandLineNoJoinTest : AbstractUnitTestEx
    {
        /// <summary>
        /// Tests importing some raw files using the "--no-join" command line parameter which leaves the individual .skyd file there.
        /// Then, deletes the raw files and makes sure that those partial .skyd files are sufficient to create the final cache.
        /// </summary>
        [TestMethod]
        public void TestCommandLineNoJoin()
        {
            if (!ExtensionTestContext.CanImportWatersRaw)
            {
                return;
            }
            var testFilesDir = new TestFilesDir(TestContext, @"TestData\CommandLineNoJoinTest.zip");
            string inDocPath = testFilesDir.GetTestPath("test.sky");
            string rawFileRoot = testFilesDir.GetTestPath("RawFiles");
            string[] rawFiles = 
            {
                Path.Combine(rawFileRoot, "160109_Mix1_calcurve_071.raw"),
                Path.Combine(rawFileRoot, "160109_Mix1_calcurve_074.raw")
            };
            Assert.IsTrue(Directory.Exists(rawFileRoot));
            List<string> partialSkydFiles = new List<string>();
            // First, create the partial .skyd files for each of the replicates.
            for (int iFile = 0; iFile < rawFiles.Length; iFile++)
            {
                string rawFile = rawFiles[iFile];
                Assert.IsTrue(Directory.Exists(rawFile), rawFile);
                string outFile = testFilesDir.GetTestPath("partial" + iFile + ".sky");
                RunCommand("--in=" + inDocPath, "--import-file=" + rawFile, "--out=" + outFile, "--import-no-join");
                Assert.IsTrue(File.Exists(outFile), rawFile);
                string partialSkydFile = ChromatogramCache.PartPathForName(outFile, new MsDataFilePath(rawFile));
                Assert.IsTrue(File.Exists(partialSkydFile), rawFile);
                partialSkydFiles.Add(partialSkydFile);
                File.Delete(outFile);
            }
            // Delete the raw files since we no longer need them, since we have the .skyd files
            Directory.Delete(rawFileRoot, true);
            Assert.IsFalse(Directory.Exists(rawFileRoot));

            string completeFile = testFilesDir.GetTestPath("complete.sky");
            Assert.IsNotNull(completeFile);
            string completeSkyd = Path.ChangeExtension(completeFile, "skyd");
            Assert.IsFalse(File.Exists(completeFile));
            Assert.IsFalse(File.Exists(completeSkyd));
            List<string> args = new List<string>
            {
                "--in=" + inDocPath,
                "--out=" + completeFile
            };
            args.AddRange(rawFiles.Select(file=>"--import-file=" + file));
            RunCommand(args.ToArray());
            Assert.IsTrue(File.Exists(completeFile));
            Assert.IsTrue(File.Exists(completeSkyd));
            foreach (var partialSkydFile in partialSkydFiles)
            {
                Assert.IsFalse(File.Exists(partialSkydFile), partialSkydFile);
            }
            using (var stream = new FileStream(completeFile, FileMode.Open))
            {
                var srmDocument = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
                Assert.IsTrue(srmDocument.Settings.HasResults);
                Assert.AreEqual(rawFiles.Length, srmDocument.MeasuredResults.Chromatograms.Count);
                for (int iFile = 0; iFile < rawFiles.Length; iFile++)
                {
                    var msDataFilePath = srmDocument.MeasuredResults.Chromatograms[iFile].MSDataFilePaths.First() as MsDataFilePath;
                    Assert.IsNotNull(msDataFilePath);
                    Assert.AreEqual(rawFiles[iFile], msDataFilePath.FilePath);
                }
            }
        }
    }
}
