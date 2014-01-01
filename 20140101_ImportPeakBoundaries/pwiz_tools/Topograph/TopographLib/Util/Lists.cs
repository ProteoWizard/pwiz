/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections;
using System.Text;

namespace pwiz.Topograph.Util
{
    public static class Lists
    {
        public static int GetHashCodeDeep(IList list)
        {
            int result = 0;
            for (int i = 0; i < list.Count; i ++)
            {
                int hashCode = list[i] == null ? 0 : list[i].GetHashCode();
                result = result*31 + hashCode;
            }
            return result;
        }
        public static bool EqualsDeep(IList list1, IList list2)
        {
            if (list1.Count != list2.Count)
            {
                return false;
            }
            for (int i = 0; i < list1.Count; i++)
            {
                if (!Equals(list1[i], list2[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public static String Join(IEnumerable values, String separator)
        {
            var result = new StringBuilder();
            var comma = "";
            foreach (var value in values)
            {
                result.Append(comma);
                comma = separator;
                result.Append(value);
            }
            return result.ToString();
        }
    }
}
