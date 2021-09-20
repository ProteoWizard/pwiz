using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class FeatureTooltipTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestFeatureTooltips()
        {
            var resourceManager = FeatureTooltips.ResourceManager;
            var missingTooltips = new List<string>();
            foreach (var calculator in PeakFeatureCalculator.Calculators.OrderBy(c => c.HeaderName,
                StringComparer.InvariantCultureIgnoreCase))
            {
                var tooltip = resourceManager.GetString(calculator.HeaderName);
                if (tooltip == null)
                {
                    Console.Out.WriteLine("<data name=\"{0}\" xml:space=\"preserve\"><value></value></data>", calculator.HeaderName);
                    missingTooltips.Add(calculator.HeaderName);
                }
            }
            Assert.AreEqual(0, missingTooltips.Count, "Missing tooltips for {0}", string.Join(",", missingTooltips));
        }
    }
}
