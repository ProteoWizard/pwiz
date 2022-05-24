using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class SpectrumClassFilterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSpectrumPrecursorFilterOperand()
        {
            double precursorMz = 422.5;
            var expectedSpectrumPrecursors =
                new SpectrumPrecursors(ImmutableList.Singleton(new SpectrumPrecursor(new SignedMz(precursorMz))));
            var dataSchema = new DataSchema(new DataSchemaLocalizer(CultureInfo.CurrentCulture, CultureInfo.CurrentCulture));
            var filterPredicate = FilterPredicate.CreateFilterPredicate(dataSchema, typeof(SpectrumPrecursors),
                FilterOperations.OP_EQUALS, expectedSpectrumPrecursors.ToString(null, CultureInfo.CurrentCulture));
            var operandValue = filterPredicate.GetOperandValue(dataSchema, typeof(SpectrumPrecursors));
            Assert.IsInstanceOfType(operandValue, typeof(SpectrumPrecursors));
            var spectrumPrecursors = operandValue as SpectrumPrecursors;
            Assert.AreEqual(expectedSpectrumPrecursors, spectrumPrecursors);
        }
    }
}
