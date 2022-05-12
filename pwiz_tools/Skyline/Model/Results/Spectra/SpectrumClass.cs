using System.Collections.Generic;
using EnvDTE;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class SpectrumClass : SkylineObject
    {
        private SpectrumClassKey _classKey;
        public SpectrumClass(SkylineDataSchema dataSchema, SpectrumClassKey classKey) : base(dataSchema)
        {
            _classKey = classKey;
            Files = new Dictionary<string, FileSpectrumInfo>();
        }

        [Format(Formats.Mz)]
        public SpectrumPrecursors Ms1Precursors
        {
            get
            {
                return _classKey.Ms1Precursors;
            }
        }

        [Format(Formats.Mz)]
        public SpectrumPrecursors Ms2Precursors
        {
            get
            {
                return _classKey.Ms2Precursors;
            }
        }

        public string ScanDescription
        {
            get
            {
                return _classKey.ScanDescription;
            }
        }

        public double? CollisionEnergy
        {
            get
            {
                return _classKey.CollisionEnergy;
            }
        }

        public Dictionary<string, FileSpectrumInfo> Files { get; }
    }
}
