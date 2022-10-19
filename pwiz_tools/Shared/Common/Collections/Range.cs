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

namespace pwiz.Common.Collections
{
    public struct Range
    {
        public Range(int start, int end) : this()
        {
            Start = start;
            End = end;
        }
        public int Start { get; private set; }
        public int End { get; private set; }
        public int Length { get { return End - Start; } }
    }

    public struct Interval<T> where T:IComparable
    {
        public delegate T MetricDelegate(T item1, T item2);

        public Interval(T start, T end, MetricDelegate metric)
        {
            Start =start;
            End =end;
            Metric =metric;
        }
        public T Start { get; private set; }
        public T End { get; private set; }
        public MetricDelegate Metric { get; private set; }
        public T Length
        {
            get { return Metric(Start, End); }
        }

        public bool InclusiveIn(T val)
        {
            return Start.CompareTo(val) <= 0 && End.CompareTo(val) >= 0;
        }
        public bool ExclusiveIn(T val)
        {
            return Start.CompareTo(val) < 0 && End.CompareTo(val) > 0;
        }
    }
}
