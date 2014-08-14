/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteomeDatabase.Fasta;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class IpiToUniprotMapperTest : AbstractUnitTest 
    {
        [TestMethod]
        public void TestIpiToUniprot()
        {
            var mapper = new IpiToUniprotMap();
            Assert.AreEqual("O95793", mapper.MapToUniprot("IPI0000001")); // The 0th one
            Assert.AreEqual("Q28062", mapper.MapToUniprot("IPI1028485")); // The nth one
            Assert.AreEqual("IPI1028486", mapper.MapToUniprot("IPI1028486")); // The n+1th nonexistent one
            Assert.AreEqual("IPI1027205", mapper.MapToUniprot("IPI1027205")); // A midrange nonexistent one
            Assert.AreEqual("Q2V4A8", mapper.MapToUniprot("IPI0657347")); // Somewhere in the middle
            Assert.AreEqual("IPI0000000", mapper.MapToUniprot("IPI0000000")); // The -1th nonexistent one
            Assert.AreEqual("nonsense", mapper.MapToUniprot("nonsense")); // The really nonexistent one
            Assert.AreEqual("O14686", mapper.MapToUniprot("IPI0297859")); // Somewhere in the middle
            Assert.AreEqual("P42025", mapper.MapToUniprot("IPI029469")); // Somewhere toward the front
        }
    }
}
