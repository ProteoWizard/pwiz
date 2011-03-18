using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Test
{
    /// <summary>
    /// Summary description for PrecursorPoolTest
    /// </summary>
    [TestClass]
    public class PrecursorPoolTest : BaseTest
    {

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void Test58Percent()
        {
            var path = Path.Combine(TestContext.TestDir, Guid.NewGuid().ToString() + ".tpg");
            var workspace = CreateWorkspace(path, TracerDef.GetD3LeuEnrichment());
            var turnoverCalculator = new TurnoverCalculator(workspace, "LL");
            var dict = new Dictionary<TracerFormula, double>();
            // initialize the dictionary with 70% newly synthesized from 58% precursor pool
            dict.Add(TracerFormula.Parse("Tracer2"), Math.Pow(.58, 2) * .7);
            dict.Add(TracerFormula.Parse("Tracer1"), 2 * .58 * .42 * .7);
            dict.Add(TracerFormula.Empty, 1- dict.Values.Sum());
            double turnover;
            double turnoverScore;
            IDictionary<TracerFormula, double> bestMatch;
            var precursorEnrichment = turnoverCalculator.ComputePrecursorEnrichmentAndTurnover(dict, out turnover, out turnoverScore, out bestMatch);
            Assert.AreEqual(.7, turnover);
            Assert.AreEqual(58, precursorEnrichment["Tracer"]);
        }
    }
}
