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
using System;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// A list of integers starting from one number, 
    /// and going up to but not including a higher number.
    /// </summary>
    public class RangeList : AbstractReadOnlyList<int>
    {
        public RangeList(int start, int end)
        {
            Start = start;
            End = end;
        }

        public RangeList(Range range) : this(range.Start, range.End)
        {
            
        }

        public int Start { get; private set; }
        public int End { get; private set; }
        public override int Count { get { return End - Start; } }
        public Range Range { get { return new Range(Start, End);} }

        public override int this[int index]
        {
            get 
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }
                return Start + index;
            }
        }

        public override bool Contains(int item)
        {
            return item >= Start && item < End;
        }

        public override int IndexOf(int item)
        {
            if (item >= Start && item < End)
            {
                return item - Start;
            }
            return -1;
        }
    }
}
