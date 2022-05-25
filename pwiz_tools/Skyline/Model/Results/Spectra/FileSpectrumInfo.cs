using System;
using System.Collections.Generic;
using System.ComponentModel;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results.Spectra
{
    [DisplayName("Info")]
    public class FileSpectrumInfo : SkylineObject, ILinkValue
    {
        private ImmutableList<SpectrumMetadata> _spectra;
        public FileSpectrumInfo(SkylineDataSchema dataSchema, IEnumerable<SpectrumMetadata> spectra) : base(dataSchema)
        {
            _spectra = ImmutableList.ValueOf(spectra);
        }

        public int SpectrumCount
        {
            get { return _spectra.Count; }
        }

        public EventHandler ClickEventHandler
        {
            get
            {
                return (sender, args) => MessageDlg.Show(null, "Not yet implemented");
            }
        }

        public object Value
        {
            get { return this; }
        }

        public override string ToString()
        {
            return string.Format("{0} Spectra", SpectrumCount);
        }

        public ImmutableList<SpectrumMetadata> GetSpectra()
        {
            return _spectra;
        }
    }
}
