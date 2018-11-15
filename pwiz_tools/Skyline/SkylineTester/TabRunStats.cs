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
            public int Iterations;
            public int Duration;
        }

        public void Process(string logFile, string logFileCompare)
        {
            var testDictionary = GetStatsFromLog(logFile);
            if (testDictionary == null)
                return;
            var compareDictionary = GetStatsFromLog(logFileCompare);
            var keys = testDictionary.Keys.ToList();
            if (compareDictionary != null)
            {
                foreach (var k in compareDictionary.Keys)
                {
                    if (!keys.Contains(k))
                    {
                        keys.Add(k);
                    }
                }
            }
            // Hide columns which are for comparison only
            for (int col = 1; col <= 2; col++)
                MainWindow.DataGridRunStats.Columns[MainWindow.DataGridRunStats.Columns.Count - col].Visible = compareDictionary != null;

            MainWindow.DataGridRunStats.Rows.Clear();
            foreach (var key in keys)
            {
                if (compareDictionary == null)
                {
                    var value = testDictionary[key];
                    MainWindow.DataGridRunStats.Rows.Add(key, value.Iterations, value.Duration, value.Duration / value.Iterations, "N/A");
                }
                else
                {
                    TestData valLeft = null;
                    testDictionary.TryGetValue(key, out valLeft);
                    TestData valRight = null;
                    compareDictionary.TryGetValue(key, out valRight);
                    var durLeftI = valLeft == null ? 0 : valLeft.Duration / valLeft.Iterations;
                    var durLeftTotal = valLeft == null ? 0 : valLeft.Duration;
                    var durRightI = valRight == null ? 0 : valRight.Duration / valRight.Iterations;
                    var durRightTotal = valRight == null ? 0 : valRight.Duration;
                    var durLeftD = valLeft == null ? 0 : valLeft.Duration / (double)valLeft.Iterations;
                    var durRightD = valRight == null ? 0 : valRight.Duration / (double)valRight.Iterations;
                    MainWindow.DataGridRunStats.Rows.Add(key,
                        string.Format("{0}/{1}", valLeft == null ? 0 : valLeft.Iterations, valRight == null ? 0 : valRight.Iterations),
                        string.Format("{0}/{1}", valLeft == null ? 0 : valLeft.Duration, valRight == null ? 0 : valRight.Duration),
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
            var endTest = new Regex(@" \d+ failures, ([\.\d]+)/([\.\d]+) MB, (\d+) sec\.\r\n", RegexOptions.Compiled);
            var endTestHandles = new Regex(@" \d+ failures, ([\.\d]+)/([\.\d]+) MB, ([\d]+)/([\d]+) handles, (\d+) sec\.\r\n", RegexOptions.Compiled);
            int durationIndex = 3;

            for (var startMatch = startTest.Match(log); startMatch.Success; startMatch = startMatch.NextMatch())
            {
                var name = startMatch.Groups[3].Value;
                var endMatch = endTest.Match(log, startMatch.Index);
                if (!endMatch.Success)
                {
                    if (ReferenceEquals(endTest, endTestHandles))
                        break;
                    endTest = endTestHandles;
                    endMatch = endTest.Match(log, startMatch.Index);
                    durationIndex = 5;
                }
                var duration = endMatch.Groups[durationIndex].Value;

                TestData testData;
                if (!testDictionary.TryGetValue(name, out testData))
                {
                    testData = new TestData();
                    testDictionary.Add(name, testData);
                }
                testData.Iterations++;
                int durationSeconds;
                if (int.TryParse(duration, out durationSeconds))
                    testData.Duration += durationSeconds;
            }
            return testDictionary;
        }
    }
}
