/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Controls.GroupComparison
{
    public struct GroupIdentifier : IComparable
    {
        private readonly object _value;

        public GroupIdentifier(bool boolValue)
        {
            _value = boolValue;
        }

        public GroupIdentifier(double doubleValue)
        {
            _value = doubleValue;
        }

        public GroupIdentifier(string stringValue)
        {
            _value = stringValue;
        }

        public double? Number
        {
            get
            {
                if (_value is double)
                {
                    return (double) _value;
                }
                if (_value is bool)
                {
                    return (bool) _value ? 1 : 0;
                }
                return null;
            }
        }

        public override string ToString()
        {
            if (_value == null)
            {
                return string.Empty;
            }
            return _value.ToString();
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }
            var that = (GroupIdentifier) obj;
            if (Number.HasValue)
            {
                if (that.Number.HasValue)
                {
                    return Number.Value.CompareTo(that.Number.Value);
                }
                return 1;
            }
            if (that.Number.HasValue)
            {
                return -1;
            } 
            return StringComparer.CurrentCultureIgnoreCase.Compare(ToString(), that.ToString());
        }

        public static GroupIdentifier MakeGroupIdentifier(object value)
        {
            if (value == null)
            {
                return default(GroupIdentifier);
            }
            if (value is bool)
            {
                return new GroupIdentifier((bool) value);
            }
            if (value is double)
            {
                return new GroupIdentifier((double) value);
            }
            return new GroupIdentifier(value as string ?? value.ToString());
        }
    }
}
