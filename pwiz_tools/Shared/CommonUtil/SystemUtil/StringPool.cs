/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Maintains a dictionary of strings in order to reduce the amount of
    /// duplicated string objects in memory.
    /// </summary>
    public class StringPool
    {
        private readonly Dictionary<string, string> _dictionary = new Dictionary<string, string>();

        public string GetString(string s)
        {
            if (s == null)
            {
                return null;
            }
            string result;
            if (_dictionary.TryGetValue(s, out result))
            {
                return result;
            }
            _dictionary.Add(s, s);
            return s;
        }
    }
}
