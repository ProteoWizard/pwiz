/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData.Spectra;

namespace pwiz.Osprey.DemuxTool
{
    /// <summary>
    /// Spectra whose intensities are ions per second, as a Thermo ion trap reports them, written as ions: each
    /// spectrum's intensities times its own injection (fill) time, in seconds. A measurement's precision is set by the
    /// ions it counted, which a rate hides (a 5 ion peak in a 0.2 ms fill reads 25,000 ions/s). The fill time stays in
    /// each spectrum's scan, so the rate can be recovered. A spectrum without a fill time is written as read.
    /// <para>Applied last, after any centroiding: Centrix expects the instrument's units (its noise estimate floors
    /// its sigma at 1) and is otherwise scale-free, so centroiding the rates and then converting them is centroiding
    /// the ions without that floor.</para>
    /// </summary>
    internal sealed class IonCountSpectrumList : SpectrumListWrapper
    {
        /// <summary>Injection times are in milliseconds.</summary>
        private const double MS_PER_SECOND = 1000;

        public IonCountSpectrumList(ISpectrumList inner)
            : base(inner)
        {
        }

        public override Spectrum GetSpectrum(int index, bool getBinaryData = false)
        {
            var spectrum = Inner.GetSpectrum(index, getBinaryData);
            if (!getBinaryData || spectrum.ScanList.Scans.Count == 0)
                return spectrum;
            double fillTimeMs = spectrum.ScanList.Scans[0].CvParamValueOrDefault(CVID.MS_ion_injection_time, double.NaN);
            var mz = spectrum.GetMZArray()?.Data;
            var intensity = spectrum.GetIntensityArray()?.Data;
            if (!(fillTimeMs > 0) || mz == null || intensity == null)
                return spectrum;
            double secondsPerFill = fillTimeMs / MS_PER_SECOND;
            var ions = new double[intensity.Count];
            for (int i = 0; i < ions.Length; i++)
                ions[i] = intensity[i] * secondsPerFill;
            // A copy of the m/z: setting the arrays clears the spectrum's own list before filling it.
            spectrum.SetMZIntensityArrays(mz.ToArray(), ions, CVID.MS_number_of_detector_counts);
            return spectrum;
        }
    }
}
