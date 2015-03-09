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

namespace TestRunnerLib
{
    public class FindLanguages
    {
        private readonly List<string> _languages;

        public FindLanguages(string executingDirectory, params string[] addLanguages)
        {
            _languages = new List<string>(addLanguages);
            if (!Directory.Exists(executingDirectory))
                return;

            foreach (var resourcesDll in Directory.EnumerateFiles(executingDirectory, "*.resources.dll", SearchOption.AllDirectories))
            {
                var file = Path.GetFileName(resourcesDll);
                if (file == null)
                    continue;
                if (file.ToLowerInvariant().StartsWith("skyline"))
                {
                    var language = Path.GetFileName(Path.GetDirectoryName(resourcesDll));
                    if (!_languages.Contains(language))
                        _languages.Add(language);
                }
            }

        }

        public IEnumerable<string> Enumerate()
        {
            return _languages;
        }
    }
}
