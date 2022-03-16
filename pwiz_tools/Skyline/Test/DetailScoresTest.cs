using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class DetailScoresTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDetailScores()
        {
            Console.Out.WriteLine("Regular scores");
            foreach (var calculator in PeakFeatureCalculator.Calculators.OfType<DetailedPeakFeatureCalculator>())
            {
                Console.Out.WriteLine("{0} {1}", calculator.GetType().FullName, calculator.IsReferenceScore);
            }
        }
    }
}
