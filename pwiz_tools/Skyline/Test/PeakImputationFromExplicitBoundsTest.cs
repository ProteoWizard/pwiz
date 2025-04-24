using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using MathNet.Numerics.Statistics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Model.RetentionTimes.PeakImputation;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PeakImputationFromExplicitBoundsTest : AbstractUnitTest
    {
        private const string ZIP_PATH = @"Test\PeakImputationFromExplicitBoundsTest.zip";
        [TestMethod]
        public void TestPeakImputationFromExplicitBounds()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_PATH);
            var dictionaries = ReadData(TestFilesDir.GetTestPath("plate4withScores.sql"));
            Assert.IsNotNull(dictionaries);
            var dataSet = GetPoints(dictionaries.Values.First(), dictionaries.Values.Skip(1).First());
            var bestAlignment = PerformAlignment(dataSet);
            foreach (int reducedPointCount in new[] { 5000, 1000, 400, 300, 200, 100, 50, 25, 10 })
            {
                var bestAlignmentReduced = bestAlignment.ReducePointCount(reducedPointCount);
                Console.Out.WriteLine("Reduced point count {0} difference: {1}", reducedPointCount, MaxDifference(bestAlignment, bestAlignmentReduced));
                var bestAlignmentFloatReduced = PiecewiseLinearMap.FromValues(
                    bestAlignmentReduced.XValues.Select(v => (float)v).Select(v => (double)v),
                    bestAlignmentReduced.YValues.Select(v => (float)v).Select(v => (double)v));
                Console.Out.WriteLine("Reduced point count {0} and float difference: {1}", reducedPointCount, MaxDifference(bestAlignment, bestAlignmentFloatReduced));

            }
            Assert.IsNotNull(bestAlignment);
            foreach (int pointCount in new[]{10000, 5000, 2000, 1000, 500, 100})
            {
                var downSampleWeightedPoints = AlignmentTarget.DownsamplePoints(dataSet, pointCount);
                var downsampledWeighted = PerformAlignment(downSampleWeightedPoints);
                var difference = MaxDifference(bestAlignment, downsampledWeighted);
                Console.Out.WriteLine("Downsample weighted {0} difference: {1}", pointCount, difference);
                var downSampledUnweighted =
                    PerformAlignment(downSampleWeightedPoints.Select(pt => new WeightedPoint(pt.X, pt.Y)).ToList());
                var differenceUnweighted = MaxDifference(bestAlignment, downSampledUnweighted);
                Console.Out.WriteLine("Downsample unweighted {0} difference: {1}", pointCount, differenceUnweighted);

            }
        }

        [TestMethod]
        public void CompareDownsampledAlignments()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_PATH);
            var dictionaries = ReadData(TestFilesDir.GetTestPath("plate4withScores.sql"));
            var alignmentTarget = GetMedianRetentionTimes(dictionaries.Values);
            foreach (var dictionary in dictionaries.Values.Take(2))
            {
                var completePointSet = MakePoints(GetRetentionTimes(dictionary), alignmentTarget).ToList();
                var downSampledPoints = AlignmentTarget.DownsamplePoints(completePointSet, 2000);
                var bestAlignment = PerformAlignment(completePointSet);
                var downSampledAlignment = PerformAlignment(downSampledPoints);
                var maxDifference = MaxDifference(bestAlignment, downSampledAlignment);
                Assert.AreEqual(maxDifference, MaxDifference(downSampledAlignment, bestAlignment));
            }
        }

        [TestMethod]
        public void TestDownSampledPeakImputation()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_PATH);
            var dictionaries = ReadData(TestFilesDir.GetTestPath("plate4withScores.sql"));
            var targets = dictionaries.Values.SelectMany(dictionary => dictionary.Keys).Distinct().ToList();
            var medianRetentionTimes = GetMedianRetentionTimes(dictionaries.Values);
            var fullAlignments = new Dictionary<string, PiecewiseLinearMap>();
            var downSampledAlignments = new Dictionary<string, PiecewiseLinearMap>();
            foreach (var entry in dictionaries)
            {
                var allPoints = MakePoints(medianRetentionTimes, GetRetentionTimes(entry.Value)).ToList();
                var downSampledPoints = AlignmentTarget.DownsamplePoints(allPoints, 2000);
                var downSampledAlignment = PerformAlignment(downSampledPoints);
                downSampledAlignments.Add(entry.Key, downSampledAlignment);
                fullAlignments.Add(entry.Key, PerformAlignment(allPoints));
            }

            var unalignedExemplaryPeaks = new Dictionary<Target, PeakBounds>();
            var fullExemplaryPeaks = new Dictionary<Target, PeakBounds>();
            var downSampledExemplaryPeaks = new Dictionary<Target, PeakBounds>();
            foreach (var target in targets)
            {
                var exemplaryPeak = GetExemplaryPeak(target, dictionaries);
                unalignedExemplaryPeaks.Add(target, exemplaryPeak.PeakBounds);
                var fullAlignment = fullAlignments[exemplaryPeak.SpectrumSourceFile];
                fullExemplaryPeaks.Add(target, PeakBoundaryImputer.MakeImputedPeak(fullAlignment.ToAlignmentFunction(true), exemplaryPeak, AlignmentFunction.IDENTITY).PeakBounds);
                var downSampledAlignment = downSampledAlignments[exemplaryPeak.SpectrumSourceFile];
                downSampledExemplaryPeaks.Add(target, PeakBoundaryImputer.MakeImputedPeak(downSampledAlignment.ToAlignmentFunction(true), exemplaryPeak, AlignmentFunction.IDENTITY).PeakBounds);
            }

            foreach (var entry in dictionaries)
            {
                var unalignedDistance =
                    GetAverageDistance(unalignedExemplaryPeaks, entry.Value, AlignmentFunction.IDENTITY);
                var fullAlignmentDistance = GetAverageDistance(fullExemplaryPeaks, entry.Value,
                    fullAlignments[entry.Key].ToAlignmentFunction(true));
                var downSampleAlignmentDistance = GetAverageDistance(downSampledExemplaryPeaks, entry.Value,
                    downSampledAlignments[entry.Key].ToAlignmentFunction(true));
                Assert.IsFalse(downSampleAlignmentDistance < fullAlignmentDistance, "{0} should not be less than {1} for {2}", downSampleAlignmentDistance, fullAlignments, entry.Key);
                Assert.IsFalse(unalignedDistance < downSampleAlignmentDistance, "{0} should not be less than {1} for {2}", unalignedDistance, downSampleAlignmentDistance, entry.Key);
                Console.Out.WriteLine("Distances for {0}: Unaligned:{1} Full Alignment:{2} Down-Sampled Alignment:{3}", entry.Key, unalignedDistance, fullAlignmentDistance, downSampleAlignmentDistance);
            }
        }

        private double GetAverageDistance(Dictionary<Target, PeakBounds> exemplaryPeaks,
            Dictionary<Target, RetentionTimeData> datas, AlignmentFunction alignmentFunction)
        {
            double totalDistance = 0;
            int count = 0;
            foreach (var entry in datas)
            {
                var exemplaryPeak = exemplaryPeaks[entry.Key];
                var imputedPeak = PeakBoundaryImputer.MakeImputedPeak(AlignmentFunction.IDENTITY,
                    new ExemplaryPeak(null, null, exemplaryPeak), alignmentFunction);
                var difference = Math.Abs(imputedPeak.PeakBounds.StartTime - entry.Value.StartTime) +
                                 Math.Abs(imputedPeak.PeakBounds.EndTime - entry.Value.EndTime);
                totalDistance += difference;
                count++;
            }
            return totalDistance / count;
        }

        private ExemplaryPeak GetExemplaryPeak(Target target,
            IDictionary<string, Dictionary<Target, RetentionTimeData>> dictionaries)
        {
            double? bestScore = null;
            ExemplaryPeak exemplaryPeak = null;
            foreach (var entry in dictionaries)
            {
                if (entry.Value.TryGetValue(target, out var data))
                {
                    if (bestScore == null || data.Score < bestScore)
                    {
                        exemplaryPeak =
                            new ExemplaryPeak(null, entry.Key, new PeakBounds(data.StartTime, data.EndTime));
                        bestScore = data.Score;
                    }
                }
            }
            return exemplaryPeak;
        }

        private Dictionary<Target, double> GetMedianRetentionTimes(
            ICollection<Dictionary<Target, RetentionTimeData>> retentionTimeDictionaries)
        {
            var result = new Dictionary<Target, double>();
            foreach (var target in retentionTimeDictionaries.SelectMany(dict => dict.Keys).Distinct())
            {
                var times = new List<double>();
                foreach (var dictionary in retentionTimeDictionaries)
                {
                    if (dictionary.TryGetValue(target, out var data))
                    {
                        times.Add(data.RetentionTime);
                    }
                }
                result.Add(target, times.Median());
            }

            return result;
        }

        private Dictionary<string, Dictionary<Target, RetentionTimeData>> ReadData(string path)
        {
            AssertEx.FileExists(path);
            using var connection = SQLiteFactory.Instance.CreateConnection();
            Assert.IsNotNull(connection);
            connection.ConnectionString = SessionFactoryFactory.SQLiteConnectionStringBuilderFromFilePath(path).ToString();
            connection.Open();
            var targets = ReadTargets(connection);
            var fileNames = ReadFileNames(connection);

            using var cmd = connection.CreateCommand();
            var result = fileNames.Values.ToDictionary(fileName => fileName,
                fileName => new Dictionary<Target, RetentionTimeData>());
            cmd.CommandText =
                @"Select RefSpectraId, SpectrumSourceId, retentionTime, startTime, endTime, score FROM ExplicitPeakBounds";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var target = targets[reader.GetInt32(0)];
                var fileName = fileNames[reader.GetInt32(1)];
                var data = new RetentionTimeData(reader.GetDouble(2), reader.GetDouble(3), reader.GetDouble(4),
                    reader.GetDouble(5));
                var dict = result[fileName];
                dict.Add(target, data);
            }

            return result;
        }

        private Dictionary<int, Target> ReadTargets(DbConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"Select Id, peptideModSeq from RefSpectra";
            using var reader = cmd.ExecuteReader();
            var result = new Dictionary<int, Target>();
            while (reader.Read())
            {
                result.Add(reader.GetInt32(0), new Target(reader.GetString(1)));
            }

            return result;
        }

        private Dictionary<int, string> ReadFileNames(DbConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, fileName from SpectrumSourceFiles";
            using var reader = cmd.ExecuteReader();
            var result = new Dictionary<int, string>();
            while (reader.Read())
            {
                result.Add(reader.GetInt32(0), reader.GetString(1));
            }

            return result;
        }

        private PiecewiseLinearMap PerformAlignment(IEnumerable<WeightedPoint> pointsEnumerable)
        {
            var stopWatch = new Stopwatch();
            var points = pointsEnumerable.OrderBy(tuple => tuple.X).ToList();
            stopWatch.Start();
            var bandwidth = Math.Max(LoessInterpolator.DEFAULT_BANDWIDTH, 2.0 / points.Count);
            var loessInterpolator = new LoessInterpolator(bandwidth, LoessInterpolator.DEFAULT_ROBUSTNESS_ITERS);
            var xArray = points.Select(tuple => tuple.X).ToArray();
            
            var smoothedValues = loessInterpolator.Smooth(xArray,
                points.Select(tuple => tuple.Y).ToArray(), points.Select(tuple => tuple.Weight).ToArray(),
                CancellationToken.None);
            Console.Out.WriteLine("Aligned {0} points in {1}", points.Count, stopWatch.Elapsed);
            return PiecewiseLinearMap.FromValues(xArray, smoothedValues);
        }

        private IList<WeightedPoint> GetPoints(Dictionary<Target, RetentionTimeData> x,
            Dictionary<Target, RetentionTimeData> y)
        {
            var points = new List<WeightedPoint>();
            
            foreach (var entry in x)
            {
                if (y.TryGetValue(entry.Key, out var yValue))
                {
                    points.Add(new WeightedPoint(entry.Value.RetentionTime, yValue.RetentionTime));
                }
            }

            return points;
        }

        private Dictionary<Target, double> GetRetentionTimes(Dictionary<Target, RetentionTimeData> dictionary)
        {
            return dictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.RetentionTime);
        }

        private IEnumerable<WeightedPoint> MakePoints(Dictionary<Target, double> xDictionary,
            Dictionary<Target, double> yDictionary)
        {
            return xDictionary.Keys.Intersect(yDictionary.Keys)
                .Select(key => new WeightedPoint(xDictionary[key], yDictionary[key]));
        }

        private class RetentionTimeData
        {
            public RetentionTimeData(double retentionTime, double startTime, double endTime, double score)
            {
                RetentionTime = retentionTime;
                StartTime = startTime;
                EndTime = endTime;
                Score = score;
            }

            public double RetentionTime { get;}

            public double StartTime { get; }
            public double EndTime { get; }
            public double Score { get; }
        }

        private double MaxDifference(PiecewiseLinearMap a, PiecewiseLinearMap b)
        {
            var maxXDifference = a.XValues.Concat(b.XValues).Max(x => Math.Abs(a.GetY(x) - b.GetY(x)));
            var maxYDifference = a.YValues.Concat(b.YValues).Max(y => Math.Abs(a.GetX(y) - b.GetX(y)));
            return Math.Max(maxXDifference, maxYDifference);
        }
    }
}
