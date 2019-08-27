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
    public class PrositMSMSSpectra
    {
        public PrositMSMSSpectra(SrmSettings settings, IList<PeptidePrecursorPair> peptidePrecursorPairs, PrositIntensityModel.PrositIntensityOutput prositIntensityOutput)
        {
            Spectra = new PrositMSMSSpectrum[peptidePrecursorPairs.Count];
            for (int i = 0; i < peptidePrecursorPairs.Count; ++i)
                Spectra[i] = new PrositMSMSSpectrum(settings, peptidePrecursorPairs[i], i, prositIntensityOutput);
        }

        public PrositMSMSSpectrum GetSpectrum(TransitionGroupDocNode precursor)
        {
            return Spectra.FirstOrDefault(s => s.PeptidePrecursorPair.NodeGroup.EqualsId(precursor));
        }

        public PrositMSMSSpectrum[] Spectra { get; private set; }
    }

    public class PrositMSMSSpectrum
    {
        public PrositMSMSSpectrum(SrmSettings settings, PeptidePrecursorPair peptidePrecursorPair,
            int precursorIndex, PrositIntensityModel.PrositIntensityOutput prositIntensityOutput)
        {
            PeptidePrecursorPair = peptidePrecursorPair;
            var precursor = peptidePrecursorPair.NodeGroup;
            var peptide = peptidePrecursorPair.NodePep;

            var calc = settings.GetFragmentCalc(precursor.TransitionGroup.LabelType, peptide.ExplicitMods);
            var ionTable = calc.GetFragmentIonMasses(precursor.TransitionGroup.Peptide.Target);
            var ions = ionTable.GetLength(1);

            var mis = new List<SpectrumPeaksInfo.MI>(ions * Constants.PRECURSOR_CHARGES);
            var max = float.MinValue;

            for (int i = 0; i < ions; ++i)
            {
                var intensities = prositIntensityOutput.OutputRows[precursorIndex].Intensities[i].Intensities
                    .Select(ReLu).ToArray();
                max = Math.Max(max, intensities.Max());
                var yMIs = CalcMIs(ionTable[IonType.y, i], intensities, 0);
                var bMIs = CalcMIs(ionTable[IonType.b, i], intensities, Constants.PRECURSOR_CHARGES / 2);
                mis.AddRange(yMIs);
                mis.AddRange(bMIs);
            }

            // Max Norm
            for (int i = 0; i < mis.Count; ++i)
                mis[i] = new SpectrumPeaksInfo.MI {Mz = mis[i].Mz, Intensity = mis[i].Intensity / max}; // Yikes

            SpectrumPeaks = new SpectrumPeaksInfo(mis.ToArray());
        }

        private static float ReLu(float f)
        {
            return Math.Max(f, 0.0f);
        }

        private List<SpectrumPeaksInfo.MI> CalcMIs(TypedMass mass, float[] intensities, int offset)
        {
            var result = new List<SpectrumPeaksInfo.MI>(Constants.PRECURSOR_CHARGES / 2);
            for (var c = 0; c < Constants.PRECURSOR_CHARGES / 2; ++c)
            {
                // Not a possible charge
                if (PeptidePrecursorPair.NodeGroup.PrecursorCharge <= c)
                    break;

                result.Add(new SpectrumPeaksInfo.MI
                {
                    Mz = SequenceMassCalc.GetMZ(mass, c + 1),
                    Intensity = intensities[c + offset]
                });
            }

            return result;
        }

        public PeptidePrecursorPair PeptidePrecursorPair { get; private set; }
        public SpectrumPeaksInfo SpectrumPeaks { get; private set; }

        public SpectrumMzInfo SpecMzInfo
        {
            get
            {
                var peptide = PeptidePrecursorPair.NodePep;
                var precursor = PeptidePrecursorPair.NodeGroup;
                SpectrumMzInfo info = new SpectrumMzInfo();
                info.SpectrumPeaks = SpectrumPeaks;
                info.IonMobility = IonMobilityAndCCS.EMPTY;
                info.Key = new LibKey(peptide.ModifiedTarget, precursor.PrecursorCharge);
                info.Label = precursor.LabelType;
                info.PrecursorMz = precursor.PrecursorMz;
                info.RetentionTime = null;
                info.RetentionTimes = null;
                info.SourceFile = "Prosit" + PrositIntensityModel.Instance.Model + "_" + PrositIntensityModel.SIGNATURE; // TODO: maybe put URL here?
                return info;
            }
        }
    }
}
