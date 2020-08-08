/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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

namespace pwiz.Skyline.Model.DocSettings.AbsoluteQuantification
{
    /// <summary>
    /// Possible results of comparing an observed value to a target value, within a user-specified threshold
    /// </summary>
    public sealed class ValueStatus
    {
        /// <summary>
        /// Observed value is within the threshold of the target value
        /// </summary>
        public static readonly ValueStatus PASS = new ValueStatus(() => QuantificationStrings.ValueStatus_PASS_Pass);
        /// <summary>
        /// Observed value is outside of the threshold from the target value
        /// </summary>
        public static readonly ValueStatus FAIL = new ValueStatus(() => QuantificationStrings.ValueStatus_FAIL_Fail);
        /// <summary>
        /// Observed value is NaN or Infinity
        /// </summary>
        public static readonly ValueStatus UNDEFINED = new ValueStatus(()=>QuantificationStrings.ValueStatus_UNDEFINED_Undefined);
        /// <summary>
        /// Observed value is a valid number, and the target value unknown
        /// </summary>
        public static readonly ValueStatus PRESENT = new ValueStatus(()=>QuantificationStrings.ValueStatus_PRESENT_Present);
        /// <summary>
        /// Observed value is exactly equal to the target value and the threshold has not been specified
        /// </summary>
        public static readonly ValueStatus EQUAL = new ValueStatus(()=>QuantificationStrings.ValueStatus_EQUAL_Equal);
        /// <summary>
        /// Observed value is less than the target value and the threshold has not been specified
        /// </summary>
        public static readonly ValueStatus LOW = new ValueStatus(()=>QuantificationStrings.ValueStatus_LOW_Low);
        /// <summary>
        /// Observed value is greater than the target value and the threshold has not been specified
        /// </summary>
        public static readonly ValueStatus HIGH = new ValueStatus(()=>QuantificationStrings.ValueStatus_HIGH_High);
        private readonly Func<string> _getDisplayNameFunc;
        private ValueStatus(Func<string> getDisplayNameFunc)
        {
            _getDisplayNameFunc = getDisplayNameFunc;
        }

        public override string ToString()
        {
            return _getDisplayNameFunc();
        }

        public static ValueStatus GetStatus(double? observedValue, double? targetValue, double? targetThreshold)
        {
            if (!observedValue.HasValue)
            {
                return null;
            }

            if (double.IsNaN(observedValue.Value) || double.IsNaN(observedValue.Value))
            {
                return UNDEFINED;
            }

            if (!targetValue.HasValue)
            {
                return PRESENT;
            }

            if (!targetThreshold.HasValue)
            {
                if (observedValue == targetValue)
                {
                    return EQUAL;
                }

                if (observedValue < targetValue)
                {
                    return LOW;
                }

                if (observedValue > targetValue)
                {
                    return HIGH;
                }
            }

            if (observedValue >= targetValue - targetValue * targetThreshold &&
                observedValue <= targetValue + targetValue * targetThreshold)
            {
                return PASS;
            }

            return FAIL;
        }
    }
}
