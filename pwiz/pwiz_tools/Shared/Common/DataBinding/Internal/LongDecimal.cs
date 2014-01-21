/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;

namespace pwiz.Common.DataBinding.Internal
{
    /// <summary>
    /// This class is supposed to be able to be used as the key
    /// in a Tree, allowing records to be inserted in the middle
    /// and maintain the ordering.
    /// 
    /// TODO(nicksh): In order to be able to do its job perfectly,
    /// this class needs to have an arbitrary precision decimal
    /// number in it.  For now it just has a double, which means
    /// that it is possible for <see cref="MidPoint"/> not
    /// to be able to find a value between two doubles.
    /// </summary>
    internal class LongDecimal : IComparable
    {
        private double _doubleValue;        
        public LongDecimal(long longPart)
        {
            _doubleValue = longPart;
        }
        public LongDecimal GetSuccessor()
        {
            return new LongDecimal((long) Math.Ceiling(_doubleValue) + 1);
        }
        public LongDecimal GetPredecessor()
        {
            return new LongDecimal((long) Math.Floor(_doubleValue) - 1);
        }
        public override string ToString()
        {
            return _doubleValue.ToString(LocalizationHelper.CurrentCulture);
        }
        public override int GetHashCode()
        {
            return _doubleValue.GetHashCode();
        }
        public override bool Equals(object o)
        {
            if (this == o)
            {
                return true;
            }
            var that = o as LongDecimal;
            if (null == that)
            {
                return false;
            }
            return Equals(_doubleValue, that._doubleValue);
        }
        public int CompareTo(object o)
        {
            if (o == null)
            {
                return 1;
            }
            var that = (LongDecimal) o;
            return _doubleValue.CompareTo(that._doubleValue);
        }
        public LongDecimal MidPoint(LongDecimal that)
        {
            var result = new LongDecimal(0) {_doubleValue = (_doubleValue + that._doubleValue)/2};
            if (result.Equals(this) || result.Equals(that))
            {
                return null;
            }
            return result;
        }
    }
}
