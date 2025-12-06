/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Globalization;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    public class TransitionIonMetrics : Immutable, IFormattable
    {
        public override string ToString()
        {
            return ToString(Formats.PEAK_AREA, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            var parts = new List<string>();
            if (LcPeakTransitionIonCount != null)
            {
                parts.Add(TextUtil.ColonSeparate(ColumnCaptions.LcPeakTransitionIonCount,
                    LcPeakTransitionIonCount.Value.ToString(format, formatProvider)));
            }

            if (ApexTransitionIonCount.HasValue)
            {
                parts.Add(TextUtil.ColonSeparate(ColumnCaptions.ApexTransitionIonCount,
                    ApexTransitionIonCount.Value.ToString(format, formatProvider)));
            }
            return TextUtil.SpaceSeparate(parts);
        }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? LcPeakTransitionIonCount { get; private set; }

        public TransitionIonMetrics ChangeLcPeakTransitionIonCount(double? value)
        {
            return ChangeProp(ImClone(this), im => im.LcPeakTransitionIonCount = value);
        }

        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? ApexTransitionIonCount { get; private set; }

        public TransitionIonMetrics ChangeApexTransitionIonCount(double? value)
        {
            return ChangeProp(ImClone(this), im => im.ApexTransitionIonCount = value);
        }
    }
}
