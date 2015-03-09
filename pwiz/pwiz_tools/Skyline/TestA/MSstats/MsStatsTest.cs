using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis;
using pwiz.Common.DataAnalysis.FoldChange;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.MSstats
{
    /// <summary>
    /// Summary description for MsStatsTest
    /// </summary>
    [TestClass]
    public class MsStatsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestRatPlasmaWithInteraction()
        {
            var expectedResults = ReadExpectedResults("RatPlasmaResultsWithInteraction.csv");
            TestGroupComparison(GetTextReader("RatPlasmaProcessedData.csv"), true, expectedResults);
        }

        [TestMethod]
        public void TestRatPlasmaWithoutIteraction()
        {
            var expectedResults = ReadExpectedResults("RatPlasmaResultsWithoutInteraction.csv");
            TestGroupComparison(GetTextReader("RatPlasmaProcessedData.csv"), false, expectedResults);
        }

        private TextReader GetTextReader(string filename)
        {
            var stream = typeof (MsStatsTest).Assembly.GetManifestResourceStream(typeof (MsStatsTest), filename);
            Assert.IsNotNull(stream);
            return new StreamReader(stream);
        }
        
        private void TestGroupComparison(TextReader textReader, bool includeInteraction, IDictionary<string, LinearFitResult> expectedResults)
        {
            var csvReader = new DsvFileReader(textReader, ',');
            var dataRowsByProtein = ToDataRows(ReadCsvFile(csvReader));
            Assert.AreNotEqual(0, dataRowsByProtein.Count);
            var cache = new QrFactorizationCache();
            foreach (var entry in dataRowsByProtein)
            {
                FoldChangeDataSet dataSet = FoldChangeCalculator.MakeDataSet(entry.Value);
                var designMatrix = DesignMatrix.GetDesignMatrix(dataSet, includeInteraction);
                var foldChange = designMatrix.PerformLinearFit(cache).First();
                LinearFitResult expectedResult = null;
                if (null != expectedResults)
                {
                    Assert.IsTrue(expectedResults.TryGetValue(entry.Key, out expectedResult));
                }
                if (null != expectedResult)
                {
                    Assert.AreEqual(expectedResult.EstimatedValue, foldChange.EstimatedValue, 1E-6);
                    Assert.AreEqual(expectedResult.DegreesOfFreedom, foldChange.DegreesOfFreedom);
                    Assert.AreEqual(expectedResult.StandardError, foldChange.StandardError, 1E-6);
                    Assert.AreEqual(expectedResult.TValue, foldChange.TValue, 1E-6);
                    Assert.AreEqual(expectedResult.PValue, foldChange.PValue, 1E-6);
                }
            }
        }

        private IList<Dictionary<string, string>> ReadCsvFile(DsvFileReader fileReader)
        {
            return MsStatsTestUtil.ReadCsvFile(fileReader);
        }

        private IDictionary<string, IList<FoldChangeCalculator.DataRow>> ToDataRows(
            IList<Dictionary<string, string>> rows)
        {
            var result = new Dictionary<string, IList<FoldChangeCalculator.DataRow>>();
            foreach (var rowsByProtein in rows.ToLookup(row => row["PROTEIN"]))
            {
                var dataRows = new List<FoldChangeCalculator.DataRow>();
                foreach (var row in rowsByProtein)
                {
                    if ("NA" == row["ABUNDANCE"])
                    {
                        continue;
                    }
                    var dataRow = new FoldChangeCalculator.DataRow
                    {
                        Abundance = Convert.ToDouble(row["ABUNDANCE"], CultureInfo.InvariantCulture),
                        Control = "2" == row["GROUP"],
                        Run = 0,
                        Subject = row["SUBJECT"],
                        Feature = row["FEATURE"],
                    };
                    dataRows.Add(dataRow);
                }
                result.Add(rowsByProtein.Key, dataRows);
            }
            return result;
        }

        public IDictionary<string, LinearFitResult> ReadExpectedResults(string filename)
        {
            return MsStatsTestUtil.ReadExpectedResults(typeof (MsStatsTest), filename);
        }

        private abstract class FoldChangeCalculator : FoldChangeCalculator<int, string, string>
        {
            
        }
    }
}
