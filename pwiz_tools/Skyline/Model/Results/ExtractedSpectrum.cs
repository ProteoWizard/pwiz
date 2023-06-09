/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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

using System.Drawing;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    public sealed class ExtractedSpectrum
    {
        public ExtractedSpectrum(ChromatogramGroupId chromatogramGroupId,
                                 Color peptideColor,
                                 SignedMz precursorMz,
                                 IonMobilityFilter ionMobility,
                                 ChromExtractor chromExtractor,
                                 int filterIndex,
                                 SpectrumProductFilter[] productFilters,
                                 float[] intensities,
                                 float[] massErrors)
        {
            ChromatogramGroupId = chromatogramGroupId;
            PeptideColor = peptideColor;
            PrecursorMz = precursorMz;
            IonMobility = ionMobility;
            Extractor = chromExtractor;
            FilterIndex = filterIndex;
            ProductFilters = productFilters;
            Intensities = intensities;
            MassErrors = massErrors;
        }

        public ChromatogramGroupId ChromatogramGroupId { get; private set; }
        public Color PeptideColor { get; private set; }
        public SignedMz PrecursorMz { get; private set; }
        public IonMobilityFilter IonMobility { get; private set; }
        public int FilterIndex { get; private set; }
        public SpectrumProductFilter[] ProductFilters { get; private set; }
        public float[] Intensities { get; private set; }
        public float[] MassErrors { get; private set; }
        public ChromExtractor Extractor { get; private set; }
    }
}