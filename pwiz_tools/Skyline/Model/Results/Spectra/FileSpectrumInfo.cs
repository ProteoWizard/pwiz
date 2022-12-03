using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results.Spectra
{
    [DisplayName("Info")]
    public class FileSpectrumInfo : RootSkylineObject, ILinkValue
    {
        private ImmutableList<SpectrumMetadata> _spectra;
        private MsDataFileUri _dataFileUri;
        public FileSpectrumInfo(SkylineDataSchema dataSchema, MsDataFileUri dataFileUri, IEnumerable<SpectrumMetadata> spectra) : base(dataSchema)
        {
            _dataFileUri = dataFileUri;
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
                return (sender, args) =>
                {
                    if (null == DataSchema.SkylineWindow)
                    {
                        return;
                    }

                    var chromSource = ChromSource.unknown;
                    var timeIntensities = new TimeIntensities(_spectra.Select(s => (float) s.RetentionTime),
                        new float[_spectra.Count], null, Enumerable.Range(0, _spectra.Count).ToList());
                    var transitionFullScanInfo = new TransitionFullScanInfo()
                    {
                        Color = Color.Blue,
                        Id = new ChromatogramSetId(),
                        Name = "Test",
                        TimeIntensities = timeIntensities,
                        Source = chromSource,
                        ExtractionWidth = 0,
                    };
                    var msDataFileScanIds = new ResultFileMetaData(_spectra).ToMsDataFileScanIds();
                    IScanProvider scanProvider = new ScanProvider(DataSchema.SkylineWindow.DocumentFilePath, _dataFileUri,
                        chromSource, timeIntensities.Times, new TransitionFullScanInfo[] {transitionFullScanInfo}, null, msDataFileScanIds); 
                    DataSchema.SkylineWindow.ShowGraphFullScan(scanProvider, 0, 0);
                };
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
