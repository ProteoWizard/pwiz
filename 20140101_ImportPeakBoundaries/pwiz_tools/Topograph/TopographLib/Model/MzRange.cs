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

namespace pwiz.Topograph.Model
{
    public struct MzRange
    {
        public MzRange(double value) : this(value,value)
        {
        }
        public MzRange(double min, double max) : this()
        {
            Min = min;
            Max = max;
        }
        public MzRange Union(double value)
        {
            return new MzRange(Math.Min(Min, value), Math.Max(Max, value));
        }
        public bool Contains(double value)
        {
            return value >= Min && value <= Max;
        }
        public double Center
        {
            get { return (Min + Max) / 2; }
        }
        public bool ContainsWithMassAccuracy(double value, double massAccuracy)
        {
            if (value < Min)
            {
                return (Min - value)*massAccuracy < Center;
            }
            if (value > Max)
            {
                return (value - Max)*massAccuracy < Center;
            }
            return true;
        }
        public double MinWithMassAccuracy(double massAccuracy)
        {
            return Min - Center/massAccuracy;
        }
        public double MaxWithMassAccuracy(double massAccuracy)
        {
            return Max + Center/massAccuracy;
        }
        public double Distance(double value)
        {
            if (value < Min)
            {
                return Min - value;
            }
            if (value > Max)
            {
                return value - Max;
            }
            return 0;
        }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public override String ToString()
        {
            if (Min == Max)
            {
                return Max.ToString();
            }
            return Min + "-" + Max;
        }
        public String ToString(String format)
        {
            if (Min == Max)
            {
                return Max.ToString(format);
            }
            return Min.ToString(format) + "-" + Max.ToString(format);
        }
    }
}
