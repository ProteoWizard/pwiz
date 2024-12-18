using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using pwiz.Skyline.Model.AlphaPeptDeep;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AlphapeptdeepLibraryBuilderTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestModificationInfo()
        {
            var groupedByAccession = LibraryHelper.AlphapeptdeepModificationName.GroupBy(item => item.Accession);
            foreach (var group in groupedByAccession)
            {
                Assert.AreEqual(1, group.Count(), "Duplicate accession {0}", group.Key);
            }
        }
    }
}
