using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var filter1 = SpectrumClassFilter.ParseFilterString("DissociationMethod = 'CID' and CollisionEnergy = 20.0 and MsLevel = 2");
            Assert.IsNotNull(filter1);
            var filter2 = SpectrumClassFilter.ParseFilterString("MsLevel = 1");
            Assert.IsNotNull(filter2);
            var filter3 = SpectrumClassFilter.ParseFilterString("(DissociationMethod = 'CID' and CollisionEnergy = 20.0 and MsLevel = 2) or (MsLevel = 1)");
            Assert.IsNotNull(filter3);
            var filter4 = SpectrumClassFilter.ParseFilterString("(DissociationMethod = 'CID' and CollisionEnergy = 20.0 and MsLevel = 2) or MsLevel = 1");
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
