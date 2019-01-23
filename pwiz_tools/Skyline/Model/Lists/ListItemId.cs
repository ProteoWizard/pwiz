/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.Lists
{
    /// <summary>
    /// Identifies a row in a <see cref="ListData"/>, and enables following the row when other rows are added and removed.
    /// </summary>
    public struct ListItemId : IComparable<ListItemId>, IComparable
    {
        public ListItemId(int intValue) : this()
        {
            IntValue = intValue;
        }
        public int IntValue { get; private set; }
        public int CompareTo(ListItemId other)
        {
            return IntValue.CompareTo(other.IntValue);
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }
            return CompareTo((ListItemId) obj);
        }

        public override string ToString()
        {
            return @"#Row " + IntValue + @"#";
        }
    }
}
