using System.Collections.Generic;
using EnvDTE;
using JetBrains.Annotations;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClass : SkylineObject
    {
        public SpectrumClass(SkylineDataSchema dataSchema, SpectrumClassKey classKey) : base(dataSchema)
        {
            Files = new Dictionary<string, FileSpectrumInfo>();
            for (int i = 0; i < classKey.Columns.Count; i++)
            {
                var value = classKey.Values[i];
                if (value != null)
                {
                    classKey.Columns[i].SetValue(this, value);
                }
            }
        }

        [UsedImplicitly]
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

        public double? ScanWindowWidth { get; private set; }

        public Dictionary<string, FileSpectrumInfo> Files { get; }
    }
}
