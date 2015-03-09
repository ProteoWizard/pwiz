/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text.RegularExpressions;

namespace pwiz.Topograph.Util
{
    public static class NameComparers
    {
        private static Regex _regexpNumber = new Regex("([0-9]+)");
        /// <summary>
        /// Compares two strings which may contain sequences of numbers.
        /// This is designed so that names are sorted logically, as "replicate1", "replicate9", "replicate10"
        /// </summary>
        public static int CompareReplicateNames(string string1, string string2)
        {
            IEnumerator<string> en1 = _regexpNumber.Split(string1).AsEnumerable().GetEnumerator();
            IEnumerator<string> en2 = _regexpNumber.Split(string2).AsEnumerable().GetEnumerator();
            while (true)
            {
                if (!en1.MoveNext())
                {
                    return en2.MoveNext() ? -1 : 0;
                }
                if (!en2.MoveNext())
                {
                    return 1;
                }
                var s1 = en1.Current;
                var s2 = en2.Current;
                if (char.IsDigit(s1.FirstOrDefault()) && char.IsDigit(s2.FirstOrDefault()))
                {
                    long n1, n2;
                    if (long.TryParse(s1, out n1) && long.TryParse(s2, out n2))
                    {
                        int result = n1.CompareTo(n2);
                        if (result != 0)
                        {
                            return result;
                        }
                    }
                }
                int stringResult = StringComparer.InvariantCultureIgnoreCase.Compare(s1, s2);
                if (stringResult != 0)
                {
                    return stringResult;
                }
            }
        }
    }
}
