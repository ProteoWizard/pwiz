using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.AlphaPeptDeep;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AlphapeptdeepLibraryBuilderTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestModificationInfo()
        {
            var groupedByAccession = AlphapeptdeepLibraryBuilder.AlphapeptdeepModificationName.GroupBy(item => item.Accession);
            foreach (var group in groupedByAccession)
            {
                Assert.AreEqual(1, group.Count(), "Duplicate accession {0}", group.Key);
            }
        }
    }
}
