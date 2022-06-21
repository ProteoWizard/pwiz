using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClass
    {
        public SpectrumClass(SpectrumClassKey classKey)
        {
            for (int i = 0; i < classKey.Columns.Count; i++)
            {
                var value = classKey.Values[i];
                if (value != null)
                {
                    classKey.Columns[i].SetValue(this, value);
                }
            }
        }

        [Format(Formats.Mz)]
        public SpectrumPrecursors Ms1Precursors
        {
            get; private set;
        }

        [Format(Formats.Mz)]
        public SpectrumPrecursors Ms2Precursors
        {
            get; private set;
        }

        public string ScanDescription
        {
            get; private set;
        }

        public double? CollisionEnergy
        {
            get; private set;
        }

        public double? CompensationVoltage
        {
            get;
            private set;
        }

        public double? ScanWindowWidth { get; private set; }

        public int PresetScanConfiguration { get; private set; }

        public int MsLevel { get; private set; }
    }
}
