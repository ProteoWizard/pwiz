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

using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace pwiz.Skyline.Util.Extensions
{
    /// <summary>
    /// Added by Clark Brace (cbrace3)
    /// Code sourced form Justin.Jones as part of (CPOL)
    /// https://www.codeproject.com/Articles/22517/Natural-Sort-Comparer
    /// </summary>
    public class NaturalComparer
    {

        public static int Compare(string x, string y)
        {
            if (x == null && y == null)
            {
                return 0;
            }

            if (x == null)
            {
                return 1;
            }

            if (y == null)
            {
                return -1;
            }


            var x1 = Regex.Split(x, @"([0-9]+)");

            var y1 = Regex.Split(y, @"([0-9]+)");


            for (var i = 0; i < x1.Length && i < y1.Length; i++)
            {
                // if both are numbers, compare as numbers
                // else if one or other is number, it wins, return that
                // else if string compare culture sensitive, case insensitive != 0, return that
                // else next
                var left = x[i];
                var right = y[i];
                if (!int.TryParse(left, out var x) || !int.TryParse(right, out var y))
                {
                    var comparison =  string.Compare(left, right, StringComparison.CurrentCulture);
                    if (comparison != 0)
                        return comparison;
                }
            }
            if (y1.Length > x1.Length)
            {
                return 1;
            }
            else if (x1.Length > y1.Length)
            {
                return -1;
            }
            else
            {
                return 0;
            }
            
        }
    }
}