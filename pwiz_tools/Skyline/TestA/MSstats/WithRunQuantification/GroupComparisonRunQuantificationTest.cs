using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis.FoldChange;
using pwiz.Common.DataAnalysis.Matrices;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA.MSstats.WithRunQuantification
{
    /// <summary>
    /// Tests of the MSstats algorithm that uses two steps of linear regressions, where the first step
    /// assigns an abundance to each run, and the second step takes those run quantification numbers
    /// to calculate fold changes.
    /// </summary>
    [TestClass]
    public class GroupComparisonRunQuantificationTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestRunQuantification()
        {
            var cache = new QrFactorizationCache();
            var csvReader = new DsvFileReader(GetTextReader("quant.csv"), ',');
            var dataRowsByProtein = ToDataRows(ReadCsvFile(csvReader));
            var expectedResultsByProtein =  ReadCsvFile(new DsvFileReader(GetTextReader("runquantdata.csv"), ',')).ToLookup(row => row["Protein"]);
            foreach (var entry in dataRowsByProtein)
            {
                var expectedResultsByRun = expectedResultsByProtein[entry.Key].ToLookup(row => row["RUN"]);
                FoldChangeDataSet dataSet = FoldChangeCalculator.MakeDataSet(entry.Value);
                var designMatrix = DesignMatrix.GetRunQuantificationDesignMatrix(dataSet);
                var runNames = FoldChangeCalculator.GetUniqueList(entry.Value.Select(row => row.Run));
                var results = designMatrix.PerformLinearFit(cache);
                for (int i = 0; i < dataSet.RunCount; i++)
                {
                    string message = string.Format("Protein:{0} Run:{1}", entry.Key, runNames[i]);
                    var expectedRow = expectedResultsByRun[runNames[i]].FirstOrDefault();
                    Assert.IsNotNull(expectedRow);
                    Assert.AreEqual(double.Parse(expectedRow["LogIntensities"], CultureInfo.InvariantCulture), results[i].EstimatedValue, .000001, message);
                    Assert.AreEqual(int.Parse(expectedRow["NumFeature"], CultureInfo.InvariantCulture), dataSet.FeatureCount, message);
                    Assert.AreEqual(int.Parse(expectedRow["NumPeaks"], CultureInfo.InvariantCulture), dataSet.GetFeatureCountForRun(i), message);
                }
            }
        }

        [TestMethod]
        public void TestGroupComparisonWithRunQuantification()
        {
            var csvReader = new DsvFileReader(GetTextReader("quant.csv"), ',');
            var dataRowsByProtein = ToDataRows(ReadCsvFile(csvReader));
            var expectedResultsByProtein = ReadCsvFile(new DsvFileReader(GetTextReader("result_newtesting_v2.csv"), ','))
                .ToDictionary(row => row["Protein"]);
            var cache = new QrFactorizationCache();
            foreach (var entry in dataRowsByProtein)
            {
                FoldChangeDataSet dataSet = FoldChangeCalculator.MakeDataSet(entry.Value);
                var quantifiedRuns = DesignMatrix.GetRunQuantificationDesignMatrix(dataSet).PerformLinearFit(cache);
                var subjects = new List<int>();

                for (int run = 0; run < quantifiedRuns.Count; run++)
                {
                    int iRow = dataSet.Runs.IndexOf(run);
                    subjects.Add(dataSet.Subjects[iRow]);
                }
                var abundances = quantifiedRuns.Select(result => result.EstimatedValue).ToArray();
                var quantifiedDataSet = new FoldChangeDataSet(
                    abundances, 
                    Enumerable.Repeat(0, quantifiedRuns.Count).ToArray(),
                    Enumerable.Range(0, quantifiedRuns.Count).ToArray(), 
                    subjects, 
                    dataSet.SubjectControls);
                var foldChangeResult = DesignMatrix.GetDesignMatrix(quantifiedDataSet, false).PerformLinearFit(cache).First();
                var expectedResult = expectedResultsByProtein[entry.Key];
                string message = entry.Key;
                Assert.AreEqual(double.Parse(expectedResult["logFC"], CultureInfo.InvariantCulture), foldChangeResult.EstimatedValue, 1E-6, message);
                Assert.AreEqual(double.Parse(expectedResult["SE"], CultureInfo.InvariantCulture), foldChangeResult.StandardError, 1E-6, message);
                Assert.AreEqual(int.Parse(expectedResult["DF"], CultureInfo.InvariantCulture), foldChangeResult.DegreesOfFreedom, message);
                if (Math.Abs(foldChangeResult.EstimatedValue) > 1E-8)
                {
                    Assert.AreEqual(double.Parse(expectedResult["pvalue"], CultureInfo.InvariantCulture), foldChangeResult.PValue, 1E-6, message);
                    Assert.AreEqual(double.Parse(expectedResult["Tvalue"], CultureInfo.InvariantCulture), foldChangeResult.TValue, 1E-6, message);
                }
            }
        }


        private TextReader GetTextReader(string filename)
        {
            var stream = typeof(GroupComparisonRunQuantificationTest).Assembly.GetManifestResourceStream(typeof(GroupComparisonRunQuantificationTest), filename);
            Assert.IsNotNull(stream);
            return new StreamReader(stream);
        }
        private IList<Dictionary<string, string>> ReadCsvFile(DsvFileReader fileReader)
        {
            var result = new List<Dictionary<string, string>>();
            while (null != fileReader.ReadLine())
            {
                Dictionary<string, string> row = new Dictionary<string, string>();
                for (int i = 0; i < fileReader.NumberOfFields; i++)
                {
                    var value = fileReader.GetFieldByIndex(i);
                    if (null != value)
                    {
                        row.Add(fileReader.FieldNames[i], value);
                    }
                }
                result.Add(row);
            }
            return result;
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
                        Run = row["RUN"],
                        Subject = row["SUBJECT"],
                        Feature = row["FEATURE"],
                    };
                    dataRows.Add(dataRow);
                }
                result.Add(rowsByProtein.Key, dataRows);
            }
            return result;
        }
        private abstract class FoldChangeCalculator : FoldChangeCalculator<string, string, string>
        {
        }

    }
}
