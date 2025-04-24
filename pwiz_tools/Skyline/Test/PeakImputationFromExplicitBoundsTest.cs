using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis;
using pwiz.Common.Database.NHibernate;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class PeakImputationFromExplicitBoundsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestPeakImputationFromExplicitBounds()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"Test\PeakImputationFromExplicitBoundsTest.zip");
            var dictionaries = ReadData(TestFilesDir.GetTestPath("plate4withScores.sql"));
            Assert.IsNotNull(dictionaries);
            var dataSet = GetCoordinates(dictionaries.Values.First(), dictionaries.Values.Skip(1).First());
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
                var downSampleWeightedPoints = DownSample(dataSet, pointCount);
                var downsampledWeighted = PerformAlignment(downSampleWeightedPoints);
                var difference = MaxDifference(bestAlignment, downsampledWeighted);
                Console.Out.WriteLine("Downsample weighted {0} difference: {1}", pointCount, difference);
                var downSampledUnweighted =
                    PerformAlignment(downSampleWeightedPoints.Select(pt => Tuple.Create(pt.Item1, pt.Item2)));
                var differenceUnweighted = MaxDifference(bestAlignment, downSampledUnweighted);
                Console.Out.WriteLine("Downsample unweighted {0} difference: {1}", pointCount, differenceUnweighted);

            }
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

        private PiecewiseLinearMap PerformAlignment(IEnumerable<Tuple<double, double>> tuples)
        {
            return PerformAlignment(tuples.Select(tuple => Tuple.Create(tuple.Item1, tuple.Item2, 0.0)).ToList());
        }

        private PiecewiseLinearMap PerformAlignment(IList<Tuple<double, double, double>> tuples)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            tuples = tuples.OrderBy(tuple => tuple.Item1).ToList();
            var loessAligner = new LoessAligner();
            var loessInterpolator = new LoessInterpolator();
            var xArray = tuples.Select(tuple => tuple.Item1).ToArray();
            var smoothedValues = loessInterpolator.Smooth(xArray,
                tuples.Select(tuple => tuple.Item2).ToArray(), tuples.Select(tuple => tuple.Item3).ToArray(),
                CancellationToken.None);
            return PiecewiseLinearMap.FromValues(xArray, smoothedValues);
            loessAligner.Train(tuples.Select(tuple=>tuple.Item1).ToArray(), tuples.Select(tuple=>tuple.Item2).ToArray(), CancellationToken.None);
            loessAligner.GetSmoothedValues(out var xSmoothed, out var ySmoothed);
            Console.Out.WriteLine("Aligned {0} points in {1}", tuples.Count, stopWatch.Elapsed);
            return PiecewiseLinearMap.FromValues(xSmoothed, ySmoothed);
        }

        private IList<Tuple<double, double>> GetCoordinates(Dictionary<Target, RetentionTimeData> x,
            Dictionary<Target, RetentionTimeData> y)
        {
            var tuples = new List<Tuple<double, double>>();
            
            foreach (var entry in x)
            {
                if (y.TryGetValue(entry.Key, out var yValue))
                {
                    tuples.Add(Tuple.Create(entry.Value.RetentionTime, yValue.RetentionTime));
                }
            }

            return tuples;
        }

        private IList<Tuple<double, double, double>> DownSample(IList<Tuple<double, double>> tuples, int targetCount)
        {
            if (targetCount >= tuples.Count)
            {
                return tuples.Select(tuple=>Tuple.Create(tuple.Item1, tuple.Item2, 1.0)).ToList();
            }

            var xMin = tuples.Min(tuple=>tuple.Item1);
            var xMax = tuples.Max(tuple=>tuple.Item1);
            var yMin = tuples.Min(tuple=>tuple.Item2);
            var yMax = tuples.Max(tuple=>tuple.Item2);
            if (xMin == xMax || yMin == yMax)
            {
                return new[]{ Tuple.Create(tuples.Average(tuple=>tuple.Item1), tuples.Average(tuple=>tuple.Item2), 1.0) };
            }
            var result = new List<Tuple<double, double, double>>();
            foreach (var bin in tuples.GroupBy(tuple=>Tuple.Create(Math.Round((tuple.Item1 - xMin) * targetCount / (xMax - xMin)),
                         Math.Round((tuple.Item2 - yMin) * targetCount / (yMax - yMin)))))
            {
                result.Add(Tuple.Create(bin.Average(t=>t.Item1), bin.Average(t=>t.Item2), (double) bin.Count()));
            }

            return result;
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
            
            return AverageDifference(a, b) + AverageDifference(PiecewiseLinearMap.FromValues(a.YValues, a.XValues),
                PiecewiseLinearMap.FromValues(b.YValues, b.XValues));
        }
        
        private double AverageDifference(PiecewiseLinearMap a, PiecewiseLinearMap b)
        {
            var xValues = new List<double>();
            var differences = new List<double>();
            foreach (var x in a.XValues.Concat(b.XValues).Distinct().OrderBy(x => x))
            {
                xValues.Add(x);
                differences.Add(a.GetY(x) - b.GetY(x));
            }

            double total = 0;
            for (int i = 0; i < xValues.Count - 1; i++)
            {
                total += Math.Abs((differences[i] + differences[i + 1]) * (xValues[i + 1] - xValues[i]) / 2);
            }

            return total / (xValues[0] + xValues[xValues.Count - 1]);
        }
    }
}
