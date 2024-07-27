/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Results.Spectra
{
    [InvariantDisplayName("Info")]
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
                        Name = _dataFileUri.GetFileName(),
                        TimeIntensities = timeIntensities,
                        Source = chromSource,
                        ExtractionWidth = 0,
                    };
                    var msDataFileScanIds = new ResultFileMetaData(_spectra).ToMsDataFileScanIds();
                    IScanProvider scanProvider = new ScanProvider(DataSchema.SkylineWindow.DocumentFilePath,
                        _dataFileUri, chromSource, timeIntensities.Times,
                        new[] {transitionFullScanInfo}, null, msDataFileScanIds);
                    DataSchema.SkylineWindow.ShowGraphFullScan(scanProvider, 0, 0, 0);
                };
            }
        }

        public object Value
        {
            get { return this; }
        }

        public override string ToString()
        {
            return string.Format(SpectraResources.FileSpectrumInfo_ToString__0__Spectra, SpectrumCount);
        }

        public ImmutableList<SpectrumMetadata> GetSpectra()
        {
            return _spectra;
        }
    }
}
