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
using System.Text.RegularExpressions;

namespace SkylineTester
{
    public class TabRunStats : TabBase
    {
        public override void Enter()
        {
            MainWindow.DataGridRunStats.Columns[0].Width = 300;
            MainWindow.InitLogSelector(MainWindow.ComboRunStats);
            if (File.Exists(MainWindow.DefaultLogFile) && MainWindow.LastRunName != null)
                MainWindow.ComboRunStats.Items.Insert(0, MainWindow.LastRunName + " output");
            MainWindow.ComboRunStats.SelectedIndex =
                (MainWindow.ComboRunStats.Items.Count > 0 ? 0 : -1);
        }

        private class TestData
        {
            public int Iterations;
            public int Duration;
        }

        public void Process(string logFile)
        {
            var log = File.ReadAllText(logFile);

            var testDictionary = new Dictionary<string, TestData>();
            var startTest = new Regex(@"\r\n\[\d\d:\d\d\] +(\d+).(\d+) +(\S+) +\((\w\w)\) ", RegexOptions.Compiled);
            var endTest = new Regex(@" \d+ failures, ([\.\d]+)/([\.\d]+) MB, (\d+) sec\.\r\n", RegexOptions.Compiled);

            for (var startMatch = startTest.Match(log); startMatch.Success; startMatch = startMatch.NextMatch())
            {
                var name = startMatch.Groups[3].Value;
                var endMatch = endTest.Match(log, startMatch.Index);
                var duration = endMatch.Groups[3].Value;

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

            MainWindow.DataGridRunStats.Rows.Clear();
            foreach (var pair in testDictionary)
            {
                MainWindow.DataGridRunStats.Rows.Add(pair.Key, pair.Value.Iterations, pair.Value.Duration, pair.Value.Duration / pair.Value.Iterations);
            }
        }
    }
}
