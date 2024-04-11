/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.Spectra.Alignment;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class SpectrumSummaryTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestGenerateData()
        {
            var relativePathZip = @"TestData\bin\SpectrumSummaryTestFullData.zip";
            var fullPath = TestContext.GetProjectDirectory(relativePathZip);
            if (!File.Exists(fullPath))
            {
                return;
            }
            TestFilesDir = new TestFilesDir(TestContext, relativePathZip);
            var outputFolder = TestFilesDir.GetTestPath("SpectrumSummaryTest");
            Directory.CreateDirectory(outputFolder);
            ParallelEx.ForEach(new[]
            {
                Tuple.Create("S_1.raw", 32), Tuple.Create("S_12.raw", 32), Tuple.Create("2021_01_20_coraleggs_10B_23.raw", 8), Tuple.Create("2021_01_20_coraleggs_10NB_13.raw", 8)
            }, tuple =>
            {
                var rawFileName = tuple.Item1;
                var summaryLength = tuple.Item2;
                var summaryList = GetSpectrumSummaryList(TestFilesDir.GetTestPath(rawFileName), summaryLength);
                summaryList = summaryList.RemoveRareSpectra();
                var outputPath = Path.Combine(outputFolder,
                    Path.GetFileNameWithoutExtension(rawFileName) + ".spectrumsummaries.tsv");
                File.WriteAllText(outputPath, ToTsv(summaryList));
            });
        }


        [TestMethod]
        public void TestGetBestPath()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\SpectrumSummaryTest.zip");
            var spectrumSummaryList = ReadSpectrumSummaryList(TestFilesDir.GetTestPath("S_1.spectrumSummaries.tsv"));
            var evenSpectra = new SpectrumSummaryList(Enumerable.Range(0, spectrumSummaryList.Count / 2)
                .Select(i =>
                {
                    var spectrum = spectrumSummaryList[i * 2];
                    return ChangeRetentionTime(spectrum, spectrum.RetentionTime * 3);
                }));
            var similarityMatrix = spectrumSummaryList.GetSimilarityMatrix(null, null, evenSpectra);
            var bestPath = similarityMatrix.FindBestPath();
            AssertEx.IsNotNull(bestPath);
            foreach (var point in bestPath)
            {
                AssertEx.AreEqual(point.Value, point.Key * 3, .01);
            }

            var kdeAlignment = new KdeAligner();
            kdeAlignment.Train(bestPath.Select(kvp=>kvp.Key).ToArray(), bestPath.Select(kvp=>kvp.Value).ToArray(), CancellationToken.None);
            AssertEx.AreEqual(30, kdeAlignment.GetValue(10), .1);
        }

        [TestMethod]
        public void TestBigAlignment()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\SpectrumSummaryTest.zip");
            var spectrumSummaryList1 = ReadSpectrumSummaryList(TestFilesDir.GetTestPath("2021_01_20_coraleggs_10B_23.spectrumsummaries.tsv"));
            var spectrumSummaryList2 = ReadSpectrumSummaryList(TestFilesDir.GetTestPath("2021_01_20_coraleggs_10NB_13.spectrumsummaries.tsv"));
            var similarityMatrix = spectrumSummaryList1.GetSimilarityMatrix(null, null, spectrumSummaryList2);
            var bestPath = similarityMatrix.FindBestPath();
            AssertEx.IsNotNull(bestPath);
        }

        public static string ToTsv(IEnumerable<SpectrumSummary> spectrumSummaryList)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("RetentionTime\tScanWindowLower\tScanWindowUpper\tDigestValue");
            foreach (var spectrum in spectrumSummaryList)
            {
                sb.AppendLine(string.Join("\t", spectrum.RetentionTime.ToString("R", CultureInfo.InvariantCulture),
                    FloatToString((float)spectrum.SpectrumMetadata.ScanWindowLowerLimit.Value),
                    FloatToString((float)spectrum.SpectrumMetadata.ScanWindowUpperLimit.Value),
                    new FormattableList<float>(spectrum.SummaryValueFloats.ToList()).ToString("R",
                        CultureInfo.InvariantCulture)));
            }

            return sb.ToString();
        }

        private static string FloatToString(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private SpectrumSummaryList GetSpectrumSummaryList(string dataFilePath, int summaryLength)
        {
            using var msDataFile = new MsDataFileImpl(TestFilesDir.GetTestPath(dataFilePath));
            var spectrumSummaries = new List<SpectrumSummary>();
            for (int spectrumIndex = 0; spectrumIndex < msDataFile.SpectrumCount; spectrumIndex++)
            {
                var spectrum = msDataFile.GetSpectrum(spectrumIndex);
                var spectrumSummary = SpectrumSummary.FromSpectrum(spectrum, summaryLength);
                if (spectrumSummary.SpectrumMetadata.ScanWindowLowerLimit.HasValue &&
                    spectrumSummary.SpectrumMetadata.ScanWindowUpperLimit.HasValue &&
                    spectrumSummary.SummaryValueLength > 0)
                {
                    spectrumSummaries.Add(spectrumSummary);
                }
            }

            return new SpectrumSummaryList(spectrumSummaries);
        }

        private SpectrumSummaryList ReadSpectrumSummaryList(string tsvPath)
        {
            var streamReader = new StreamReader(tsvPath);
            var tsvReader = new DsvFileReader(streamReader, '\t');
            var spectrumSummaries = new List<SpectrumSummary>();
            while (null != tsvReader.ReadLine())
            {
                var spectrumMetadata = new SpectrumMetadata(spectrumSummaries.Count.ToString(),
                        double.Parse(tsvReader.GetFieldByIndex(0), CultureInfo.InvariantCulture))
                    .ChangeScanWindow(double.Parse(tsvReader.GetFieldByIndex(1), CultureInfo.InvariantCulture),
                        double.Parse(tsvReader.GetFieldByIndex(2), CultureInfo.InvariantCulture));
                var summaryValue = tsvReader.GetFieldByIndex(3).Split(',')
                    .Select(v => float.Parse(v, CultureInfo.InvariantCulture));
                spectrumSummaries.Add(new SpectrumSummary(spectrumMetadata, summaryValue));
            }
            return new SpectrumSummaryList(spectrumSummaries);
        }

        private SpectrumSummary ChangeRetentionTime(SpectrumSummary spectrumSummary, double retentionTime)
        {
            var spectrumMetadata = new SpectrumMetadata(spectrumSummary.SpectrumMetadata.Id, retentionTime)
                .ChangeScanWindow(spectrumSummary.SpectrumMetadata.ScanWindowLowerLimit.Value,
                    spectrumSummary.SpectrumMetadata.ScanWindowUpperLimit.Value);
            return new SpectrumSummary(spectrumMetadata, spectrumSummary.SummaryValue);
        }
    }
}
