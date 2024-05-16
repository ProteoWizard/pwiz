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
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Koina
{
    public class KoinaMS2Spectra
    {
        public KoinaMS2Spectra(SrmSettings settings, IList<KoinaIntensityModel.PeptidePrecursorNCE> peptidePrecursorPairs, KoinaIntensityModel.KoinaIntensityOutput koinaIntensityOutput)
        {
            Spectra = new KoinaMS2Spectrum[peptidePrecursorPairs.Count];
            for (int i = 0; i < peptidePrecursorPairs.Count; ++i)
                Spectra[i] = new KoinaMS2Spectrum(settings, peptidePrecursorPairs[i], i, koinaIntensityOutput);
        }

        public KoinaMS2Spectrum GetSpectrum(TransitionGroupDocNode precursor)
        {
            return Spectra.FirstOrDefault(s => s.PeptidePrecursorNCE.NodeGroup.EqualsId(precursor));
        }

        public KoinaMS2Spectrum[] Spectra { get; private set; }
    }

    public class KoinaMS2Spectrum : IEquatable<KoinaMS2Spectrum>
    {
        public KoinaMS2Spectrum(SrmSettings settings, KoinaIntensityModel.PeptidePrecursorNCE peptidePrecursorNCE,
            int precursorIndex, KoinaIntensityModel.KoinaIntensityOutput koinaIntensityOutput)
        {
            PeptidePrecursorNCE = peptidePrecursorNCE;
            Settings = settings;
            var explicitMods = peptidePrecursorNCE.NodePep?.ExplicitMods ?? peptidePrecursorNCE.ExplicitMods;
            var target = peptidePrecursorNCE.NodePep?.Target ?? new Target(peptidePrecursorNCE.Sequence);

            var calc = settings.GetFragmentCalc(peptidePrecursorNCE.LabelType, explicitMods);
            var ionMzTable = calc.GetFragmentIonMasses(target); // TODO: get mods and pass them as explicit mods above?
            var ionIntensityTable = koinaIntensityOutput.OutputRows[precursorIndex].Intensities;
            var ions = Math.Min(ionMzTable.GetLength(1), ionIntensityTable.GetLength(1));

            var mis = new List<SpectrumPeaksInfo.MI>(ions * KoinaConstants.IONS_PER_RESIDUE);

            for (int i = 0; i < ions; ++i)
            {
                AddMIs(mis, ionMzTable[IonType.y, i], ionIntensityTable[IonType.y, i]);
                AddMIs(mis, ionMzTable[IonType.b, i], ionIntensityTable[IonType.b, i]);
            }

            if (ions > 0)
            {
                var maxIntensity = mis.Max(mi => mi.Intensity);

                // Max Norm
                for (int i = 0; i < mis.Count; ++i)
                    mis[i] = new SpectrumPeaksInfo.MI { Mz = mis[i].Mz, Intensity = mis[i].Intensity / maxIntensity };
            }

            SpectrumPeaks = new SpectrumPeaksInfo(mis.ToArray());
        }

        public bool Equals(KoinaMS2Spectrum other)
        {
            return other != null && PeptidePrecursorNCE.Equals(other.PeptidePrecursorNCE) &&
                   ArrayUtil.EqualsDeep(SpectrumPeaks.Peaks, other.SpectrumPeaks.Peaks);
        }

        private static float ReLu(float f)
        {
            return Math.Max(f, 0.0f);
        }

        private void AddMIs(List<SpectrumPeaksInfo.MI> mis, TypedMass mass, float[] intensities)
        {
            // will be null if all predicted intensities were <= 0
            if (intensities == null)
                return;

            for (var c = 0; c < intensities.Length; ++c)
            {
                // Not a possible charge
                if (PeptidePrecursorNCE.PrecursorCharge <= c)
                    break;

                mis.Add(new SpectrumPeaksInfo.MI
                {
                    Mz = SequenceMassCalc.GetMZ(mass, c + 1),
                    Intensity = intensities[c]
                });
            }
        }

        public KoinaIntensityModel.PeptidePrecursorNCE PeptidePrecursorNCE { get; private set; }
        public SpectrumPeaksInfo SpectrumPeaks { get; private set; }

        private SrmSettings Settings { get; }

        public SpectrumMzInfo _specMzInfo;
        public SpectrumMzInfo SpecMzInfo
        {
            get
            {
                if (_specMzInfo != null)
                    return _specMzInfo;

                var peptide = PeptidePrecursorNCE.Sequence;
                var precursor = PeptidePrecursorNCE;
                var info = _specMzInfo = new SpectrumMzInfo();
                info.SpectrumPeaks = SpectrumPeaks;
                info.IonMobility = IonMobilityAndCCS.EMPTY;
                info.Key = new LibKey(
                    Settings.GetModifiedSequence(new Target(peptide), PeptidePrecursorNCE.LabelType, PeptidePrecursorNCE.ExplicitMods, SequenceModFormatType.lib_precision),
                    precursor.PrecursorCharge);
                info.Label = precursor.LabelType;
                info.PrecursorMz = precursor.PrecursorMz;
                info.RetentionTime = null;
                info.RetentionTimes = null;
                info.SourceFile = string.Format(@"Koina-{0}-{1}", KoinaIntensityModel.Instance.Model, KoinaRetentionTimeModel.Instance.Model); // TODO: maybe put URL here?
                return info;
            }
        }
    }
}
