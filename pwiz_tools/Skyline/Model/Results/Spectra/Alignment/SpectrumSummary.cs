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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.Model.Results.Spectra.Alignment
{
    /// <summary>
    /// Hold a SpectrumMetadata as well as a vector of numbers
    /// which is a short proxy for the m/z's and intensities in the
    /// spectrum which can be used to compare spectra with each other
    /// and find the most similar spectrum.
    /// </summary>
    public class SpectrumSummary
    {
        /// <summary>
        /// The length of the vector which will represent the spectrum intensity data.
        /// A longer vector would be more accurate, but shorter vectors take up less
        /// space and take less time to compare with other vectors.
        /// This must be a power of 2 because of how the <see cref="HaarWaveletTransform"/>
        /// works.
        /// </summary>
        private const int DIGEST_SIZE = 128;
        public SpectrumSummary(SpectrumMetadata spectrumMetadata, IList<double> digest)
        {
            SpectrumMetadata = spectrumMetadata;
            SummaryValue = ImmutableList.ValueOf(digest);
        }

        public static SpectrumSummary FromSpectrum(MsDataSpectrum spectrum)
        {
            if (spectrum == null)
            {
                return null;
            }

            return FromSpectrum(spectrum.Metadata, spectrum.Mzs.Zip(spectrum.Intensities, (mz, intensity)=>new KeyValuePair<double, double>(mz, intensity)),DIGEST_SIZE);
        }

        public static SpectrumSummary FromSpectrum(SpectrumMetadata metadata,
            IEnumerable<KeyValuePair<double, double>> mzIntensities, int summaryLength)
        {
            IList<double> summaryValue = null;
            if (metadata.ScanWindowLowerLimit.HasValue && metadata.ScanWindowUpperLimit.HasValue)
            {
                var binnedSpectrum = BinnedSpectrum.BinSpectrum(8192, metadata.ScanWindowLowerLimit.Value,
                    metadata.ScanWindowUpperLimit.Value, mzIntensities);
                summaryValue = binnedSpectrum.Intensities;
                while (summaryValue.Count > summaryLength)
                {
                    summaryValue = HaarWaveletTransform(summaryValue);
                }
            }

            return new SpectrumSummary(metadata, summaryValue);
        }

        public static double[] HaarWaveletTransform(IList<double> vector)
        {
            int n = vector.Count / 2;
            double[] result = new double[n];

            for (int i = 0; i < n / 2; i++)
            {
                // Calculate the average and difference
                result[i] = (vector[2 * i] + vector[2 * i + 1]) / Math.Sqrt(2.0);
                result[n / 2 + i] = (vector[2 * i] - vector[2 * i + 1]) / Math.Sqrt(2.0);
            }

            return result;
        }
        public SpectrumMetadata SpectrumMetadata { get; }
        public ImmutableList<double> SummaryValue { get; }
        public double RetentionTime
        {
            get { return SpectrumMetadata.RetentionTime; }
        }
    }
}
