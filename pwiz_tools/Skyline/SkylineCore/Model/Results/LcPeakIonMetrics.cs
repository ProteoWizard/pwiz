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
    public class LcPeakIonMetrics : Immutable, IFormattable
    {
        public LcPeakIonMetrics(string apexSpectrumId)
        {
            ApexSpectrumId = apexSpectrumId;
        }

        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? LcPeakTotalIonCurrentArea
        {
            get; private set;
        }

        public LcPeakIonMetrics ChangeTotalIonCurrentArea(double value)
        {
            return ChangeProp(ImClone(this), im => im.LcPeakTotalIonCurrentArea = value);
        }

        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? ApexTotalIonCount { get; private set; }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? LcPeakTotalIonCount { get; private set; }

        public LcPeakIonMetrics ChangeTotalIonCount(double apexTotalIonCount, double lcPeakTotalIonCount)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ApexTotalIonCount = apexTotalIonCount;
                im.LcPeakTotalIonCount = lcPeakTotalIonCount;
            });
        }


        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? ApexAnalyteIonCount { get; private set; }

        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? LcPeakAnalyteIonCount { get; private set; }

        public LcPeakIonMetrics ChangeAnalyteIonCount(double apexAnalyteIonCount, double lcPeakAnalyteIonCount)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.ApexAnalyteIonCount = apexAnalyteIonCount;
                im.LcPeakAnalyteIonCount = lcPeakAnalyteIonCount;
            });
        }

        public string ApexSpectrumId { get; private set; }

        public LcPeakIonMetrics ChangeApexSpectrumId(string value)
        {
            return ChangeProp(ImClone(this), im => im.ApexSpectrumId = value);
        }

        public override string ToString()
        {
            return ToString(Formats.PEAK_AREA, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            var parts = new List<string>();
            if (LcPeakTotalIonCurrentArea != null)
            {
                parts.Add(TextUtil.ColonSeparate(ColumnCaptions.LcPeakTotalIonCurrentArea,
                    LcPeakTotalIonCurrentArea.Value.ToString(format, formatProvider)));
            }

            if (ApexTotalIonCount.HasValue)
            {
                parts.Add(TextUtil.ColonSeparate(ColumnCaptions.ApexTotalIonCount,
                    ApexTotalIonCount.Value.ToString(format, formatProvider)));
            }

            if (LcPeakTotalIonCount.HasValue)
            {
                parts.Add(TextUtil.ColonSeparate(ColumnCaptions.LcPeakTotalIonCount,
                    LcPeakTotalIonCount.Value.ToString(format, formatProvider)));
            }

            if (ApexAnalyteIonCount != null)
            {
                parts.Add(TextUtil.ColonSeparate(ColumnCaptions.ApexAnalyteIonCount,
                    ApexAnalyteIonCount.Value.ToString(format, formatProvider)));
            }

            if (LcPeakAnalyteIonCount != null)
            {
                parts.Add(TextUtil.ColonSeparate(ColumnCaptions.LcPeakAnalyteIonCount,
                    LcPeakAnalyteIonCount.Value.ToString(format, formatProvider)));
            }

            return TextUtil.SpaceSeparate(parts);
        }
    }
}
