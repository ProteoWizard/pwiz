using pwiz.SkylineTestUtil;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Filtering;
using pwiz.Skyline.Model.Results.Spectra;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class SpectrumClassFilterParserTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestParseSpectrumClassFilter()
        {
            var filter1 = SpectrumClassFilter.ParseFilterString(string.Format("DissociationMethod = 'CID' and CollisionEnergy = {0:F1} and MsLevel = 2", 20));
            Assert.IsNotNull(filter1);
            var filter2 = SpectrumClassFilter.ParseFilterString("MsLevel = 1");
            Assert.IsNotNull(filter2);
            var filter3 = SpectrumClassFilter.ParseFilterString(string.Format("(DissociationMethod = 'CID' and CollisionEnergy = {0:F1} and MsLevel = 2) or (MsLevel = 1)", 20));
            Assert.IsNotNull(filter3);
            var filter4 = SpectrumClassFilter.ParseFilterString(string.Format("(DissociationMethod = 'CID' and CollisionEnergy = {0:F1} and MsLevel = 2) or MsLevel = 1", 20));
            Assert.IsNotNull(filter4);
        }

        [TestMethod]
        public void TestSpectrumClassFilterToFilterString()
        {
            var filter = new SpectrumClassFilter(
                new FilterClause(new[]
                {
                    new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClass.DissociationMethod)),
                        FilterOperations.OP_EQUALS, "CID"),
                    new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.CollisionEnergy)), FilterOperations.OP_EQUALS, "2.00e1"),
                    new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)), FilterOperations.OP_EQUALS, "2")
                }),
                new FilterClause(new[]
                {
                    new FilterSpec(PropertyPath.Root.Property(nameof(SpectrumClassColumn.MsLevel)), FilterOperations.OP_EQUALS, "1")
                })
            );
            var text = filter.ToFilterString();
            Assert.IsNotNull(text);
            var roundTrip = SpectrumClassFilter.ParseFilterString(text);
            Assert.IsNotNull(roundTrip);
        }
    }
}
