using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            var colorScheme = new DiscreteColorScheme(0);
            Assert.IsNotNull(colorScheme);
        }
    }
}
