/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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


using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SkylineTester
{
    public class TabRunStats : TabBase
    {
        public override void Enter()
        {
            MainWindow.DataGridRunStats.Columns[0].Width = 300;
            MainWindow.InitLogSelector(MainWindow.ComboRunStats);
            MainWindow.InitLogSelector(MainWindow.ComboRunStatsCompare);
            if (File.Exists(MainWindow.DefaultLogFile) && MainWindow.LastRunName != null)
            {
                MainWindow.ComboRunStats.Items.Insert(0, MainWindow.LastRunName + " output");
                MainWindow.ComboRunStatsCompare.Items.Insert(0, MainWindow.LastRunName + " output");
            }
            MainWindow.ComboRunStatsCompare.Items.Insert(0, "none");
            MainWindow.ComboRunStats.SelectedIndex =
                (MainWindow.ComboRunStats.Items.Count > 0 ? 0 : -1);
            MainWindow.ComboRunStatsCompare.SelectedIndex =
                (MainWindow.ComboRunStatsCompare.Items.Count > 0 ? 0 : -1);
        }

        private class TestData
        {
            public int Iterations => _durations.Count;
            public int TotalDuration => _durations.Sum();
            public int MinDuration => _durations.Min();
            public int MaxDuration => _durations.Max();
            public int MeanDuration => Iterations == 0 ? 0 : TotalDuration / Iterations;
            public int MedianDuration {
                get
                {
                    var count = _durations.Count;
                    switch (count)
                    {
                        case 0:
                            return 0;
                        case 1:
                            return _durations[0];
                        default:
                            _durations.Sort();
                            var mid = count / 2;
                            if (count % 2 == 0)
                            {
                                return (_durations[mid]);
                            }
                            return (_durations[mid] + _durations[mid-1])/2;
                    }
                }
            }
            public List<int> _durations;

            public TestData()
            {
                _durations = new List<int>();
            }
        }

        private Dictionary<string, TestData> TestSummaries;
        private Dictionary<string, TestData> TestSummariesCompare;
        private List<string> TestNames;
        private string TestLog;
        private string TestLogCompare;

        private void AddHeaderPair(bool paired, List<string> header, string columnName)
        {
            header.Add(columnName);
            if (paired)
            {
                header.Add(columnName);
                header.Add($@"delta {columnName}");
            }
        }

        private void AddColumnPair(int? valA, int? valB, List<string> columns)
        {
            columns.Add(valA.HasValue ? valA.ToString() : string.Empty);
            columns.Add(valB.HasValue ? valB.ToString() : string.Empty);
            columns.Add(valA.HasValue && valB.HasValue ? (valA - valB).ToString() : string.Empty);
        }

        private void AddColumns(Dictionary<string, TestData> tests, Dictionary<string, TestData> testsCompare, string testName, List<string> columns)
        {
            if (!tests.TryGetValue(testName, out var testData))
            {
                testData = null;
            }

            if (testsCompare == null)
            {
                columns.Add(testData?.Iterations.ToString()); // "Iterations"
                columns.Add(testData?.TotalDuration.ToString()); // "TotalTime"
                columns.Add(testData?.MinDuration.ToString()); // "MinTime"
                columns.Add(testData?.MaxDuration.ToString()); // "MaxTime"
                columns.Add(testData?.MeanDuration.ToString()); // "MeanTime"
                columns.Add(testData?.MedianDuration.ToString()); // "MedianTime"
            }
            else
            {
                TestData testDataCompare = null;
                if (!testsCompare.TryGetValue(testName, out testDataCompare))
                {
                    testDataCompare = null;
                }

                AddColumnPair(testData?.Iterations, testDataCompare?.Iterations, columns); // "Iterations"
                AddColumnPair(testData?.TotalDuration, testDataCompare?.TotalDuration, columns); // "TotalTime"
                AddColumnPair(testData?.MinDuration, testDataCompare?.MinDuration, columns); // "MinTime"
                AddColumnPair(testData?.MaxDuration, testDataCompare?.MaxDuration, columns); // "MaxTime"
                AddColumnPair(testData?.MeanDuration, testDataCompare?.MeanDuration, columns); // "MeanTime"
                AddColumnPair(testData?.MedianDuration, testDataCompare?.MedianDuration, columns); // "MedianTime"
            }
        }

        /// <summary>
        /// Create a CSV file with run stats for further analysis in Excel etc
        /// Works for single or side by side comparisons
        /// </summary>
        public void ExportCSV()
        {
            using (var openFileDlg = new SaveFileDialog
                   {
                       Filter = @"CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                       Title = @"Export Run Stats"
                   })
            {
                if (openFileDlg.ShowDialog() != DialogResult.OK)
                    return;
                using (var report = new StreamWriter(openFileDlg.FileName))
                {
                    var compared = (TestLogCompare == null) ? string.Empty : $@" vs {Path.GetFileNameWithoutExtension(TestLogCompare)}";
                    var header = new List<string>() { $@"Test ({Path.GetFileNameWithoutExtension(TestLog)}{compared})" };
                    var paired = !string.IsNullOrEmpty(compared);
                    AddHeaderPair(paired, header, $@"Iterations");
                    AddHeaderPair(paired, header, $@"TotalTime");
                    AddHeaderPair(paired, header, $@"MinTime");
                    AddHeaderPair(paired, header, $@"MaxTime");
                    AddHeaderPair(paired, header, $@"MeanTime");
                    AddHeaderPair(paired, header, $@"MedianTime");

                    report.WriteLine(string.Join(@",",header));
                    if (TestNames != null)
                    {
                        foreach (var test in TestNames)
                        {
                            var columns = new List<string>(){test};
                            AddColumns(TestSummaries, TestSummariesCompare, test, columns);
                            report.WriteLine(string.Join(@",", columns));
                        }
                    }
                }
            }
        }

        public void Process(string logFile, string logFileCompare)
        {
            TestLog = logFile;
            TestSummaries = GetStatsFromLog(logFile);
            if (TestSummaries == null)
                return;
            TestLogCompare = logFileCompare;
            TestSummariesCompare = GetStatsFromLog(logFileCompare);
            TestNames = TestSummaries.Keys.ToList();
            if (TestSummariesCompare != null)
            {
                foreach (var k in TestSummariesCompare.Keys)
                {
                    if (!TestNames.Contains(k))
                    {
                        TestNames.Add(k);
                    }
                }
            }
            // Hide columns which are for comparison only
            for (int col = 1; col <= 2; col++)
                MainWindow.DataGridRunStats.Columns[MainWindow.DataGridRunStats.Columns.Count - col].Visible = TestSummariesCompare != null;

            MainWindow.DataGridRunStats.Rows.Clear();
            foreach (var key in TestNames)
            {
                if (TestSummariesCompare == null)
                {
                    var value = TestSummaries[key];
                    MainWindow.DataGridRunStats.Rows.Add(key, value.Iterations, value.TotalDuration, value.TotalDuration / value.Iterations, "N/A");
                }
                else
                {
                    TestData valLeft = null;
                    TestSummaries.TryGetValue(key, out valLeft);
                    TestData valRight = null;
                    TestSummariesCompare.TryGetValue(key, out valRight);
                    var durLeftI = valLeft == null ? 0 : valLeft.TotalDuration / valLeft.Iterations;
                    var durLeftTotal = valLeft == null ? 0 : valLeft.TotalDuration;
                    var durRightI = valRight == null ? 0 : valRight.TotalDuration / valRight.Iterations;
                    var durRightTotal = valRight == null ? 0 : valRight.TotalDuration;
                    var durLeftD = valLeft == null ? 0 : valLeft.TotalDuration / (double)valLeft.Iterations;
                    var durRightD = valRight == null ? 0 : valRight.TotalDuration / (double)valRight.Iterations;
                    MainWindow.DataGridRunStats.Rows.Add(key,
                        string.Format("{0}/{1}", valLeft == null ? 0 : valLeft.Iterations, valRight == null ? 0 : valRight.Iterations),
                        string.Format("{0}/{1}", valLeft == null ? 0 : valLeft.TotalDuration, valRight == null ? 0 : valRight.TotalDuration),
                        string.Format("{0}/{1}", durLeftI, durRightI),
                        (valRight == null || valLeft == null) ? "N/A" : string.Format("{0:0.00}", (durRightD == 0 ? 1 : durLeftD / durRightD)),
                        string.Format("{0}",  durLeftTotal - durRightTotal));
                }
            }
        }

        private Dictionary<string, TestData> GetStatsFromLog(string logFile)
        {
            if (string.IsNullOrEmpty(logFile) || Equals("none", logFile))
            {
                return null;
            }
            var log = File.ReadAllText(logFile);
            var testDictionary = new Dictionary<string, TestData>();

            var startTest = new Regex(@"\r\n\[\d\d:\d\d\] +(\d+).(\d+) +(\S+) +\((\w\w)\) ", RegexOptions.Compiled);
            var endTest = new Regex(@" \d+ failures, .* (\d+) sec\.\r\n", RegexOptions.Compiled);

            for (var startMatch = startTest.Match(log); startMatch.Success; startMatch = startMatch.NextMatch())
            {
                var name = startMatch.Groups[3].Value;
                var endMatch = endTest.Match(log, startMatch.Index);
                var duration = endMatch.Groups[1].Value;

                TestData testData;
                if (!testDictionary.TryGetValue(name, out testData))
                {
                    testData = new TestData();
                    testDictionary.Add(name, testData);
                }
                int.TryParse(duration, out var durationSeconds);
                testData._durations.Add(durationSeconds);
            }
            return testDictionary;
        }
    }
}
