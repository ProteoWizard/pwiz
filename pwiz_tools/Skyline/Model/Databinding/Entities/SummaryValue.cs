/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class SummaryValue
    {
        public SummaryValue(Statistics statistics)
        {
            Mean = statistics.Mean();
            if (statistics.Length > 1)
            {
                Stdev = statistics.StdDev();
                Cv = Stdev/Mean;
            }
        }

        public virtual double Mean { get; private set; }
        public virtual double? Stdev { get; private set; }
        [Format(Formats.CV, NullValue = TextUtil.EXCEL_NA)]
        public virtual double? Cv { get; private set; }

        protected string ToString(string format)
        {
            if (Stdev.HasValue)
            {
                return Mean.ToString(format) + "+/-" + Stdev.Value.ToString(format); // Not L10N
            }
            return Mean.ToString(format);
        }
    }
    // ReSharper disable LocalizableElement
    public class RetentionTimeSummary : SummaryValue
    {
        public RetentionTimeSummary(Statistics statistics) : base(statistics)
        {
            Min = statistics.Min();
            Max = statistics.Max();
        }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double Min { get; private set; }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double Max { get; private set; }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double Range { get { return Max - Min; } }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public override double Mean { get { return base.Mean; } }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public override double? Stdev { get { return base.Stdev; } }
        public override string ToString()
        {
            return ToString(Formats.RETENTION_TIME);
        }
    }

    public class FwhmSummary : SummaryValue
    {
        public FwhmSummary(Statistics statistics) : base(statistics)
        {
        }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public override double Mean { get { return base.Mean; } }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public override double? Stdev { get { return base.Stdev; } }

        public override string ToString()
        {
            return ToString(Formats.RETENTION_TIME);
        }
    }

    public class AreaSummary : SummaryValue
    {
        public AreaSummary(Statistics statistics) : base(statistics)
        {
        }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public override double Mean { get { return base.Mean; } }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public override double? Stdev { get { return base.Stdev; } }

        public override string ToString()
        {
            return ToString(Formats.PEAK_AREA);
        }
    }

    public class AreaRatioSummary : SummaryValue
    {
        public AreaRatioSummary(Statistics statistics) : base(statistics)
        {
        }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public override double Mean { get { return base.Mean; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public override double? Stdev { get { return base.Stdev; } }

        public override string ToString()
        {
            return ToString(Formats.STANDARD_RATIO);
        }
    }

    public class AreaNormalizedSummary : SummaryValue
    {
        public AreaNormalizedSummary(Statistics statistics) : base(statistics)
        {
        }
        [Format(Formats.PEAK_AREA_NORMALIZED, NullValue = TextUtil.EXCEL_NA)]
        public override double Mean { get { return base.Mean; } }
        [Format(Formats.PEAK_AREA_NORMALIZED, NullValue = TextUtil.EXCEL_NA)]
        public override double? Stdev { get { return base.Stdev; } }

        public override string ToString()
        {
            return ToString(Formats.PEAK_AREA_NORMALIZED);
        }
    }
}