/*
 * Original author: Clark Brace <clarkbrace@gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using System.Runtime.InteropServices;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Added by Clark Brace (cbrace3)
    /// Natural Sort class. Sorts strings into natural sort order rather than lexicographically.
    /// Designed for sorting file/folders into a more human readable format. In this implementation,
    /// numbers are prioritized over letters and the full value of the number is taken into account rather than
    /// just the independent values of each number in series (2, 22, 3 --> 2, 3, 22)
    /// e.g. (Skyline-64_22c, Skyline-64_3a, Skyline-64_4c --> Skyline-64_3a, Skyline-64_4c, Skyline-64_22c)
    /// e.g. (1AC6, 1AC66, 1AC7, 4C47 --> 1AC6, 1AC7, 1AC66, 4C47)
    /// https://www.pinvoke.net/default.aspx/shlwapi.strcmplogicalw
    /// </summary>
    public class NaturalComparer
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string x, string y);

        //Compare strings with natural sort
        public static int Compare(string x, string y)
        {
            return StrCmpLogicalW(x, y);
        }
    }
}