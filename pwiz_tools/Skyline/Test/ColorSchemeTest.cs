using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Colors;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ColorSchemeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestColorScheme()
        {
            var colorScheme = new DiscreteColorScheme();
            Assert.IsNotNull(colorScheme);
        }
    }
}
