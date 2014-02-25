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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace pwiz.SkylineTestUtil
{
    public class FormSeen
    {
        private const string CACHE_FILE = "TestRunnerFormSeen.csv";

        private readonly Dictionary<string, int> _formSeen =
            new Dictionary<string, int>();

        public FormSeen()
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
                    _formSeen[parts[0]] = int.Parse(parts[1]);
            }
        }

        public void Save()
        {
            var sb = new StringBuilder();
            foreach (var pair in _formSeen.OrderBy(p => p.Key))
            {
                sb.Append(pair.Key);
                sb.Append(",");
                sb.AppendLine(pair.Value.ToString("d"));
            }

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    File.WriteAllText(CacheFile, sb.ToString());
                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(50);
                }
            }
        }

        public void Saw(Type formType)
        {
            var viewTypeName = GetViewType(formType);
            if (viewTypeName != null)
                Saw(viewTypeName);
        }

        public static string GetViewType(Type formType)
        {
            string viewType = null;
            if (formType != null)
                viewType = Implements(formType, "IFormView") && formType.DeclaringType != null
                    ? formType.DeclaringType.Name + "." + formType.Name
                    : formType.Name;
            return viewType;
        }

        public void Saw(string formName)
        {
            int seen;
            _formSeen.TryGetValue(formName, out seen);
            Set(formName, seen + 1);
        }

        private static bool Implements(Type type, string interfaceName)
        {
            return type.GetInterfaces().Any(t => t.Name == interfaceName);
        }

        public void Set(string test, int seen)
        {
            _formSeen[test] = seen;
            Save();
        }

        public static void Clear()
        {
            for (int i = 0; i < 3; i++)
            {
                if (!File.Exists(CacheFile))
                    return;
                try
                {
                    File.Delete(CacheFile);
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }
            }
        }

        public int GetSeenCount(string form)
        {
            int seenCount;
            _formSeen.TryGetValue(form, out seenCount);
            return seenCount;
        }
    }
}
