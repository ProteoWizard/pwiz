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
    public class MzKey : IComparable<MzKey>
    {
        public MzKey(int charge, int massIndex)
        {
            Charge = charge;
            MassIndex = massIndex;
        }
        public int Charge { get; private set; }
        public int MassIndex { get; private set; }
        public override int GetHashCode()
        {
            return Charge.GetHashCode()*31 + MassIndex.GetHashCode();
        }
        public override bool Equals(Object o)
        {
            if (o == this)
            {
                return true;
            }
            var that = o as MzKey;
            if (that == null)
            {
                return false;
            }
            return Charge == that.Charge && MassIndex == that.MassIndex;
        }

        public int CompareTo(MzKey other)
        {
            int result = Charge.CompareTo(other.Charge);
            if (0 == result)
            {
                result = MassIndex.CompareTo(other.MassIndex);
            }
            return result;
        }
    }
}
