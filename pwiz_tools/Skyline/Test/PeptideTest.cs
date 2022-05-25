/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PeptideTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestPeptidePosition()
        {
            var srmSettings = SrmSettingsList.GetDefault();
            var fastaSequence = new FastaSequence("protein", null, null, "ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            var peptide1 = fastaSequence.CreateFullPeptideDocNode(srmSettings, new Target("ABCDE"));
            var peptide2 = fastaSequence.CreateFullPeptideDocNode(srmSettings, new Target("MNOP"));
            var peptide3 = fastaSequence.CreateFullPeptideDocNode(srmSettings, new Target("UVWXYZ"));

            Assert.AreEqual("-.ABCDE.F [1, 5]", peptide1.Peptide.ToString());
            Assert.AreEqual("L.MNOP.Q [13, 16]", peptide2.Peptide.ToString());
            Assert.AreEqual("T.UVWXYZ.- [21, 26]", peptide3.Peptide.ToString());
            var peptideGroupDocNode = new PeptideGroupDocNode(fastaSequence, Annotations.EMPTY,
                ProteinMetadata.EMPTY, new[] { peptide1, peptide2, peptide3}, false);
            var document = (SrmDocument)new SrmDocument(srmSettings).ChangeChildren(new[] { peptideGroupDocNode });
            var skylineDataSchema = SkylineDataSchema.MemoryDataSchema(document, DataSchemaLocalizer.INVARIANT);
            var protein =
                new Skyline.Model.Databinding.Entities.Protein(skylineDataSchema, new IdentityPath(fastaSequence));
            var peptideEntity1 = protein.Peptides[0];
            Assert.AreEqual(1, peptideEntity1.FirstPosition);
            Assert.AreEqual(5, peptideEntity1.LastPosition);
            var peptideEntity2 = protein.Peptides[1];
            Assert.AreEqual(13, peptideEntity2.FirstPosition);
            Assert.AreEqual(16, peptideEntity2.LastPosition);
            var peptideEntity3 = protein.Peptides[2];
            Assert.AreEqual(21, peptideEntity3.FirstPosition);
            Assert.AreEqual(26, peptideEntity3.LastPosition);
        }
    }
}
