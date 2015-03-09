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

namespace pwiz.Skyline.Model.Results
{
    public sealed class ExtractedSpectrum
    {
        public ExtractedSpectrum(string textId,
                                 double precursorMz,
                                 double ionMobilityValue,
                                 double ionMobilityExtractionWidth,
                                 ChromExtractor chromExtractor,
                                 int filterIndex,
                                 double[] mzs,
                                 double[] extractionWidths,
                                 float[] intensities,
                                 float[] massErrors)
        {
            TextId = textId;
            PrecursorMz = precursorMz;
            IonMobilityValue = ionMobilityValue;
            IonMobilityExtractionWidth = ionMobilityExtractionWidth;
            Extractor = chromExtractor;
            FilterIndex = filterIndex;
            Mzs = mzs;
            ExtractionWidths = extractionWidths;
            Intensities = intensities;
            MassErrors = massErrors;
        }

        public string TextId { get; private set; } // Peptide modified sequence or custom ion id
        public double PrecursorMz { get; private set; }
        public double IonMobilityValue { get; private set; }
        public double IonMobilityExtractionWidth { get; private set; }
        public int FilterIndex { get; private set; }
        public double[] Mzs { get; private set; }
        public double[] ExtractionWidths { get; private set; }
        public float[] Intensities { get; private set; }
        public float[] MassErrors { get; private set; }
        public ChromExtractor Extractor { get; private set; }
    }
}