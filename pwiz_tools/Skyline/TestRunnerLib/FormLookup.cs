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
using System.Reflection;
using System.Text;

namespace TestRunnerLib
{
    public class FormLookup
    {
        private const string CacheFile = "TestRunnerFormLookup.csv";

        private readonly Dictionary<string, List<string>> _formLookup =
            new Dictionary<string, List<string>>();
 
        public FormLookup()
        {
            Load();
        }

        private void Load()
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var cacheFile = Path.Combine(exeDir ?? "", CacheFile);
            if (!File.Exists(cacheFile))
                return;

            foreach (var line in File.ReadAllLines(cacheFile))
            {
                var parts = line.Split(',').ToList();
                if (parts.Count > 0)
                {
                    var form = parts[0];
                    parts.RemoveAt(0);
                    _formLookup[form] = parts;
                }
            }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            foreach (var pair in _formLookup.OrderBy(p => p.Key))
            {
                sb.Append(pair.Key);
                foreach (var form in pair.Value)
                {
                    sb.Append(",");
                    sb.Append(form);
                }
                sb.AppendLine();
            }
            File.WriteAllText(CacheFile, sb.ToString());
        }

        public void AddForms(string testName, int testDuration, List<string> forms)
        {
            var testPlusDuration = testName + ":" + testDuration;
            foreach (var form in forms)
            {
                if (!_formLookup.ContainsKey(form))
                    _formLookup[form] = new List<string>();
                _formLookup[form].Add(testPlusDuration);
            }
            Save();
        }

        public bool HasTest(string form)
        {
            return _formLookup.ContainsKey(form) && _formLookup[form].Count > 0;
        }

        public List<string> FindTests(List<string> forms, out List<string> uncoveredForms)
        {
            var tests = new List<string>();
            uncoveredForms = new List<string>();

            foreach (var form in forms)
            {
                if (!_formLookup.ContainsKey(form))
                {
                    uncoveredForms.Add(form);
                    continue;
                }

                string bestTest = null;
                int minDuration = int.MaxValue;
                foreach (var testPlusDuration in _formLookup[form])
                {
                    var parts = testPlusDuration.Split(':');
                    var test = parts[0];
                    var duration = int.Parse(parts[1]);
                    if (tests.Contains(test))
                    {
                        bestTest = null;
                        break;
                    }
                    if (minDuration > duration)
                    {
                        minDuration = duration;
                        bestTest = test;
                    }
                }

                if (bestTest != null)
                    tests.Add(bestTest);
            }

            return tests;
        }

        public bool IsEmpty {get { return _formLookup.Count == 0; }}

        public static void ClearCache()
        {
            if (File.Exists(CacheFile))
                File.Delete(CacheFile);
        }

        public static void CopyCacheFile(string directory)
        {
            File.Copy(CacheFile, Path.Combine(directory, CacheFile), true);
        }
    }
}
