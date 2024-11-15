using System;
using System.Collections.Generic;
using System.Globalization;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    public class PeakIonMetrics : IFormattable
    {
        public PeakIonMetrics(double? totalIonCurrentArea, double? apexSpectrumIonCount, double? totalSpectrumIonCount, double? apexIonCount, double? totalIonCount)
        {
            TotalIonCurrentArea = totalIonCurrentArea;
            ApexSpectrumIonCount = apexSpectrumIonCount;
            TotalSpectrumIonCount = totalSpectrumIonCount;
            ApexIonCount = apexIonCount;
            TotalIonCount = totalIonCount;
        }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalIonCurrentArea
        {
            get; private set;
        }

        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? ApexSpectrumIonCount { get; }
        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalSpectrumIonCount { get; }


        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? ApexIonCount { get; private set; }

        [Format(Formats.PEAK_AREA, NullValue = TextUtil.EXCEL_NA)]
        public double? TotalIonCount { get; private set; }

        public override string ToString()
        {
            return ToString(Formats.PEAK_AREA, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            var parts = new List<string>();
            if (TotalIonCurrentArea != null)
            {
                parts.Add("TIC Area:" + TotalIonCurrentArea.Value.ToString(format, formatProvider));
            }

            if (ApexSpectrumIonCount.HasValue)
            {
                parts.Add("Apex Spectrum Ion Count:" + ApexSpectrumIonCount.Value.ToString(format, formatProvider));
            }

            if (TotalSpectrumIonCount.HasValue)
            {
                parts.Add("Total Spectrum Ion Count:" + TotalSpectrumIonCount.Value.ToString(format, formatProvider));
            }

            if (ApexIonCount != null)
            {
                parts.Add("Apex Ion Count:" + ApexIonCount.Value.ToString(format, formatProvider));
            }

            if (TotalIonCount != null)
            {
                parts.Add("Total Ion Count:" + TotalIonCount.Value.ToString(format, formatProvider));
            }

            return TextUtil.SpaceSeparate(parts);
        }
    }
}
