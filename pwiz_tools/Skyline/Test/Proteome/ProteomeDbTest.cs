/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Proteome
{
    /// <summary>
    /// Summary description for ProteomeDbTest
    /// </summary>
    [TestClass]
    public class ProteomeDbTest
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        private const string ZIP_FILE = @"Test\Proteome\high_ipi.Human.20060111.zip";

        /// <summary>
        /// Tests creating a proteome database, adding a FASTA file to it, and digesting it with trypsin.
        /// </summary>
        [TestMethod]
        public void TestProteomeDb()
        {
            var testFilesDir = new TestFilesDir(TestContext, ZIP_FILE);

            string fastaPath = testFilesDir.GetTestPath("high_ipi.Human.20060111.fasta");
            string protDbPath = testFilesDir.GetTestPath("test.protdb");

            ProteomeDb proteomeDb = ProteomeDb.CreateProteomeDb(protDbPath);
            Enzyme trypsin = EnzymeList.GetDefault();
            using (var reader = new StreamReader(fastaPath))
            {
                proteomeDb.AddFastaFile(reader, (msg, progress) => true);
            }
            proteomeDb.Digest(new ProteaseImpl(trypsin), (msg, progress) => true);
            Digestion digestion = proteomeDb.GetDigestion(trypsin.Name);
            var digestedProteins0 = digestion.GetProteinsWithSequencePrefix("EDGWVK", 100);
            Assert.IsTrue(digestedProteins0.Count >= 1);

            testFilesDir.Dispose();
        }
    }
}
