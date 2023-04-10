/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Prosit
{
    public class PrositMS2Spectra
    {
        public PrositMS2Spectra(SrmSettings settings, IList<PrositIntensityModel.PeptidePrecursorNCE> peptidePrecursorPairs, PrositIntensityModel.PrositIntensityOutput prositIntensityOutput)
        {
            Spectra = new PrositMS2Spectrum[peptidePrecursorPairs.Count];
            for (int i = 0; i < peptidePrecursorPairs.Count; ++i)
                Spectra[i] = new PrositMS2Spectrum(settings, peptidePrecursorPairs[i], i, prositIntensityOutput);
        }

        public PrositMS2Spectrum GetSpectrum(TransitionGroupDocNode precursor)
        {
            return Spectra.FirstOrDefault(s => s.PeptidePrecursorNCE.NodeGroup.EqualsId(precursor));
        }

        public PrositMS2Spectrum[] Spectra { get; private set; }
    }

    public class PrositMS2Spectrum : IEquatable<PrositMS2Spectrum>
    {
        public PrositMS2Spectrum(SrmSettings settings, PrositIntensityModel.PeptidePrecursorNCE peptidePrecursorNCE,
            int precursorIndex, PrositIntensityModel.PrositIntensityOutput prositIntensityOutput)
        {
            PeptidePrecursorNCE = peptidePrecursorNCE;
            Settings = settings;
            var peptide = peptidePrecursorNCE.NodePep;

            var calc = settings.GetFragmentCalc(peptidePrecursorNCE.LabelType, peptide.ExplicitMods);
            var ionTable = calc.GetFragmentIonMasses(peptide.Target); // TODO: get mods and pass them as explicit mods above?
            var ions = ionTable.GetLength(1);

            var mis = new List<SpectrumPeaksInfo.MI>(ions * PrositConstants.IONS_PER_RESIDUE);

            for (int i = 0; i < ions; ++i)
            {
                var intensities = prositIntensityOutput.OutputRows[precursorIndex].Intensities[i].Intensities
                    .Select(ReLu).ToArray();
                var yMIs = CalcMIs(ionTable[IonType.y, ions - i - 1], intensities, 0);
                var bMIs = CalcMIs(ionTable[IonType.b, i], intensities, PrositConstants.IONS_PER_RESIDUE / 2);
                mis.AddRange(yMIs);
                mis.AddRange(bMIs);
            }

            var maxIntensity = mis.Max(mi => mi.Intensity);

            // Max Norm
            for (int i = 0; i < mis.Count; ++i)
                mis[i] = new SpectrumPeaksInfo.MI {Mz = mis[i].Mz, Intensity = mis[i].Intensity / maxIntensity };

            SpectrumPeaks = new SpectrumPeaksInfo(mis.ToArray());
        }

        public bool Equals(PrositMS2Spectrum other)
        {
            return other != null && PeptidePrecursorNCE.Equals(other.PeptidePrecursorNCE) &&
                   ArrayUtil.EqualsDeep(SpectrumPeaks.Peaks, other.SpectrumPeaks.Peaks);
        }

        private static float ReLu(float f)
        {
            return Math.Max(f, 0.0f);
        }

        private List<SpectrumPeaksInfo.MI> CalcMIs(TypedMass mass, float[] intensities, int offset)
        {
            var result = new List<SpectrumPeaksInfo.MI>(PrositConstants.IONS_PER_RESIDUE / 2);
            for (var c = 0; c < PrositConstants.IONS_PER_RESIDUE / 2; ++c)
            {
                // Not a possible charge
                if (PeptidePrecursorNCE.NodeGroup.PrecursorCharge <= c)
                    break;

                result.Add(new SpectrumPeaksInfo.MI
                {
                    Mz = SequenceMassCalc.GetMZ(mass, c + 1),
                    Intensity = intensities[c + offset]
                });
            }

            return result;
        }

        public PrositIntensityModel.PeptidePrecursorNCE PeptidePrecursorNCE { get; private set; }
        public SpectrumPeaksInfo SpectrumPeaks { get; private set; }

        private SrmSettings Settings { get; }

        public SpectrumMzInfo SpecMzInfo
        {
            get
            {
                var peptide = PeptidePrecursorNCE.NodePep;
                var precursor = PeptidePrecursorNCE.NodeGroup;
                SpectrumMzInfo info = new SpectrumMzInfo();
                info.SpectrumPeaks = SpectrumPeaks;
                info.IonMobility = IonMobilityAndCCS.EMPTY;
                info.Key = new LibKey(
                    Settings.GetModifiedSequence(peptide.Target, PeptidePrecursorNCE.LabelType, peptide.ExplicitMods, SequenceModFormatType.lib_precision),
                    precursor.PrecursorCharge);
                info.Label = precursor.LabelType;
                info.PrecursorMz = precursor.PrecursorMz;
                info.RetentionTime = null;
                info.RetentionTimes = null;
                info.SourceFile = string.Format(@"Prosit{0}_{1}", PrositIntensityModel.Instance.Model, PrositIntensityModel.SIGNATURE); // TODO: maybe put URL here?
                return info;
            }
        }
    }
}
