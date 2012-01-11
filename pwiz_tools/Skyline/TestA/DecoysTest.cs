/*
 * Original author: Lucia Espona <espona .at. imsb.biol.ethz.ch>,
 *                  IMSB, ETHZ
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class DecoysTest
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

        [TestMethod]
        public void GenerateDecoysTest()
        {
            TestFilesDir testFilesDir = new TestFilesDir(TestContext, @"TestA\SimpleDecoys.zip");

            var document = InitGenerateDecoys(testFilesDir);

            // First check right number of decoy peptide groups and transtions are generated
            var refineSettings = new RefinementSettings();
            int numDecoys = document.TransitionCount/3;
            var decoysDoc = refineSettings.GenerateDecoys(document, numDecoys, IsotopeLabelType.light, DecoyGeneration.ADD_RANDOM);

            AssertEx.IsDocumentState(decoysDoc, 1, document.PeptideGroupCount + 1, document.PeptideCount + numDecoys, 
                                     document.TransitionGroupCount + numDecoys, document.TransitionCount*2);

            // Check for the existence of the Decoys peptide group and that everything under it is marked as a decoy. 
            var nodePeptideGroups = decoysDoc.PeptideGroups.Where(nodePeptideGroup => nodePeptideGroup.IsDecoy).ToArray();
            Assert.AreEqual(1, nodePeptideGroups.Length);

            PeptideGroupDocNode nodePeptideGroupDecoy = nodePeptideGroups.First();
            foreach (var nodePep in nodePeptideGroupDecoy.GetPeptideNodes(decoysDoc.Settings, false))
            {
                Assert.AreEqual(true, nodePep.IsDecoy);
                foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                {
                    Assert.AreEqual(true, nodeGroup.IsDecoy);
                    Assert.AreEqual(0, nodeGroup.Transitions.Count(nodeTr => !nodeTr.IsDecoy));
                }
            }

            // Check that the resulting document persists correctly by passing the SrmDocument to AssertEx.IsSerializable().
            //AssertEx.Serializable(decoysDoc);

            //second call to generate decoys to make sure that it removes the original Decoys group and generates a completely new one.
            var newDecoysDoc = refineSettings.GenerateDecoys(decoysDoc, numDecoys, IsotopeLabelType.light, DecoyGeneration.ADD_RANDOM);
            Assert.AreNotEqual(decoysDoc.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy),
                newDecoysDoc.PeptideGroups.First(nodePeptideGroup => nodePeptideGroup.IsDecoy));
        }

        private static SrmDocument InitGenerateDecoys(TestFilesDir testFilesDir)
        {
            string docPath = testFilesDir.GetTestPath("SimpleDecoys.sky");
            SrmDocument simpleDecoysDoc = ResultsUtil.DeserializeDocument(docPath);
            AssertEx.IsDocumentState(simpleDecoysDoc, 0, 1, 18, 18, 56);
            return simpleDecoysDoc;
        }
    }
}