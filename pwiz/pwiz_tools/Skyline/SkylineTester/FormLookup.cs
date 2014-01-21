using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SkylineTester
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
            if (!File.Exists(CacheFile))
                return;

            foreach (var line in File.ReadAllLines(CacheFile))
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
