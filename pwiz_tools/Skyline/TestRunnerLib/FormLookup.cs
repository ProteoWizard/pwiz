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

namespace TestRunnerLib
{
    public class FormLookup
    {
        private const string CACHE_FILE = "TestRunnerFormLookup.csv";

        private readonly Dictionary<string, string> _formLookup =
            new Dictionary<string, string>();
 
        public FormLookup()
        {
            Load();
        }

        private static string CacheFile
        {
            get
            {
                var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(exeDir ?? "", CACHE_FILE);
            }
        }

        private void Load()
        {
            if (!File.Exists(CacheFile))
                return;

            foreach (var line in File.ReadAllLines(CacheFile))
            {
                var parts = line.Split(',').ToList();
                if (parts.Count == 2)
                    _formLookup[parts[0]] = parts[1];
            }
        }

        public string GetTest(string form)
        {
            string test;
            _formLookup.TryGetValue(form, out test);
            return test;
        }

        public List<string> FindTests(List<string> forms, out List<string> uncoveredForms)
        {
            var tests = new List<string>();
            uncoveredForms = new List<string>();

            foreach (var form in forms)
            {
                string test = GetTest(form);
                if (test == null)
                    uncoveredForms.Add(form);
                else if (!tests.Contains(test))
                    tests.Add(test);
            }

            return tests;
        }
    }
}
