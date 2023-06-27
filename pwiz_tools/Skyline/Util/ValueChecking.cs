/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Util
{
    public static class ValueChecking
    {
        public static ushort CheckUShort(int value, bool allowNegativeOne = false)
        {
            return (ushort)CheckValue(value, ushort.MinValue, ushort.MaxValue, allowNegativeOne);
        }

        public static byte CheckByte(int value, int maxValue = byte.MaxValue)
        {
            return (byte)CheckValue(value, byte.MinValue, maxValue);
        }

        private static int CheckValue(int value, int min, int max, bool allowNegativeOne = false)
        {
            if (min > value || value > max)
            {
                if (!allowNegativeOne || value != -1)
                    throw new ArgumentOutOfRangeException(string.Format(@"The value {0} must be between {1} and {2}.", value, min, max)); // CONSIDER: localize?  Does user see this?
            }
            return value;
        }
    }
}
