﻿/*
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
        public ExtractedSpectrum(Target target,
                                 Color peptideColor,
                                 SignedMz precursorMz,
                                 IonMobilityFilterSet ionMobility,
                                 ChromExtractor chromExtractor,
                                 int filterIndex,
                                 SpectrumProductFilter[] productFilters,
                                 float[] intensities,
                                 float[] massErrors)
        {
            Target = target;
            PeptideColor = peptideColor;
            PrecursorMz = precursorMz;
            IonMobility = ionMobility;
            Extractor = chromExtractor;
            FilterIndex = filterIndex;
            ProductFilters = productFilters;
            Intensities = intensities;
            MassErrors = massErrors;
        }

        public Target Target { get; private set; } // Peptide modified sequence or custom ion id
        public Color PeptideColor { get; private set; }
        public SignedMz PrecursorMz { get; private set; }
        public IonMobilityFilterSet IonMobility { get; private set; }
        public int FilterIndex { get; private set; }
        public SpectrumProductFilter[] ProductFilters { get; private set; }
        public float[] Intensities { get; private set; }
        public float[] MassErrors { get; private set; }
        public ChromExtractor Extractor { get; private set; }
    }
}