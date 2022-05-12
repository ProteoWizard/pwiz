using System;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results.Spectra
{
    public class FileSpectrumInfo : SkylineObject, ILinkValue
    {
        public FileSpectrumInfo(SkylineDataSchema dataSchema, int spectrumCount) : base(dataSchema)
        {
            SpectrumCount = spectrumCount;
        }

        public int SpectrumCount { get; private set; }

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
    }
}
