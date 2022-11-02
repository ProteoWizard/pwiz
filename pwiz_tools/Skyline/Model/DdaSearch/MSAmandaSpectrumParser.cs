/*
 * Original author: Viktoria Dorfer <viktoria.dorfer .at. fh-hagenberg.at>,
 *                  Bioinformatics Research Group, University of Applied Sciences Upper Austria
 *
 * Copyright 2020 University of Applied Sciences Upper Austria
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
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using MSAmanda.InOutput.Input;
using MSAmanda.Utils;
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class MSAmandaSpectrumParser : IParserInput
    {
        public class MSDataRunPath
        {
            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((MSDataRunPath) obj);
            }

            public MSDataRunPath(string filepathPossiblyWithRunIndexSuffix)
            {
                var match = Regex.Match(filepathPossiblyWithRunIndexSuffix, @"(.+):(\d+)");
                if (match.Success)
                {
                    Filepath = match.Groups[1].Value;
                    RunIndex = int.Parse(match.Groups[2].Value);
                }
                else
                {
                    Filepath = filepathPossiblyWithRunIndexSuffix;
                    RunIndex = 0;
                }
            }

            public MSDataRunPath(string filepath, int runIndex)
            {
                Filepath = filepath;
                RunIndex = runIndex;
            }

            public string Filepath { get; }
            public int RunIndex { get; }

            public bool Equals(MSDataRunPath other)
            {
                return string.Equals(Filepath, other.Filepath) && RunIndex == other.RunIndex;
            }

            public static bool operator ==(MSDataRunPath lhs, MSDataRunPath rhs)
            {
                return lhs?.Equals(rhs) ?? rhs is null;
            }

            public static bool operator !=(MSDataRunPath lhs, MSDataRunPath rhs)
            {
                return !(lhs == rhs);
            }

            public override int GetHashCode()
            {
                return Filepath.GetHashCode() ^ RunIndex.GetHashCode();
            }

            public override string ToString()
            {
                return $@"{Filepath}:{RunIndex}";
            }
        }

        private MsDataFileImpl spectrumFileReader;
        private int specId;
        private int amandaId;
        private List<int> consideredCharges;
        private bool useMonoIsotopicMass;
        private MSDataRunPath msdataRunPath;
        private readonly string _spectrumIdFormatAccession;
        private readonly string _fileFormatAccession;
        public Dictionary<int, string> SpectTitleMap { get; }

        public string SpectrumIdFormatName { get; }
        public string FileFormatName { get; }

        public string SpectrumIdFormatAccession => _spectrumIdFormatAccession;
        public string FileFormatAccession => _fileFormatAccession;

        public int CurrentSpectrum { get; private set; }
        public int TotalSpectra { get; private set; }

        public MSAmandaSpectrumParser(string file, List<int> charges, bool mono)
        {
            consideredCharges = charges;
            spectrumFileReader = new MsDataFileImpl(file,
                requireVendorCentroidedMS2: MsDataFileImpl.SupportsVendorPeakPicking(file),
                acceptZeroLengthSpectra: false, ignoreZeroIntensityPoints: true, trimNativeId: false);
            useMonoIsotopicMass = mono;

            msdataRunPath = new MSDataRunPath(file);
            SpectTitleMap = new Dictionary<int, string>();
            CurrentSpectrum = 0;

            spectrumFileReader.GetNativeIdAndFileFormat(out _spectrumIdFormatAccession, out _fileFormatAccession);
            SpectrumIdFormatName = MsDataFileImpl.GetCvParamName(_spectrumIdFormatAccession);
            FileFormatName = MsDataFileImpl.GetCvParamName(_fileFormatAccession);
        }
        public void Dispose()
        {
            spectrumFileReader.Dispose();
        }

        public bool ReaderIsActiveAndNotEOF()
        {
            return specId < spectrumFileReader.SpectrumCount;
        }

        public List<Spectrum> ParseNextSpectra(int numberOfSpectraToRead, out int nrOfParsed, CancellationToken cancellationToken = new CancellationToken())
        {
            List<Spectrum> spectra = new List<Spectrum>();
            nrOfParsed = 0;
            while (nrOfParsed < numberOfSpectraToRead && specId < TotalSpectra) { 
                MsDataSpectrum spectrum = spectrumFileReader.GetSpectrum(specId);
                cancellationToken.ThrowIfCancellationRequested();
                ++specId;
                if (spectrum.Level != 2)
                    continue;
                Spectrum amandaSpectrum = GenerateMSAmandaSpectrum(spectrum, amandaId);
                if (amandaSpectrum.Precursor.Charge == 0)
                {
                    foreach (int charge in consideredCharges)
                    {
                        Spectrum newSpect = GenerateSpectrum(amandaSpectrum, amandaId,
                            spectrum.Precursors[0].PrecursorMz.Value, charge);
                        SpectTitleMap.Add(amandaId, spectrum.Id);
                        ++amandaId;
                        spectra.Add(newSpect);
                    }
                }
                else
                {
                    SpectTitleMap.Add(amandaId, spectrum.Id);
                    ++amandaId;
                    spectra.Add(amandaSpectrum);
                }

                ++nrOfParsed;
                
            }

            CurrentSpectrum += nrOfParsed;

            return spectra;
        }

        private Spectrum GenerateSpectrum(Spectrum spec, int id, double mOverZ, int charge)
        {
            Spectrum s = new Spectrum
            {
                //clone peaks
                FragmentsPeaks = new List<AMassCentroid>(spec.FragmentsPeaks.ToArray()),
                SpectrumId = id,
                ScanNumber = spec.ScanNumber,
                RT = spec.RT,
                immuneMasses = new SortedSet<double>(),
                immunePeaks = new Dictionary<int, double>()
            };
            s.Precursor.SetMassCharge(mOverZ, charge, useMonoIsotopicMass);
            return s;
        }

        private Spectrum GenerateMSAmandaSpectrum(MsDataSpectrum spectrum, int index)
        {
            Spectrum amandaSpectrum = new Spectrum() { RT = spectrum.RetentionTime.Value, SpectrumId = index };
            amandaSpectrum.FragmentsPeaks = GetFragmentPeaks(spectrum.Mzs, spectrum.Intensities);
            amandaSpectrum.ScanNumber = spectrum.Index;
            if (spectrum.Precursors[0].ChargeState.HasValue && spectrum.Precursors[0].PrecursorMz.HasValue)
                amandaSpectrum.Precursor.SetMassCharge(spectrum.Precursors[0].PrecursorMz.Value, spectrum.Precursors[0].ChargeState.Value, true);
            //SpectTitleMap.Add(index, spectrum.Id);
            return amandaSpectrum;
        }

        private List<AMassCentroid> GetFragmentPeaks(double[] spectrumMzs, double[] spectrumIntensities)
        {
            List<AMassCentroid> peaks = new List<AMassCentroid>();
            for (int i = 0; i < spectrumMzs.Length; ++i)
            {
                peaks.Add(new AMassCentroid() { Charge = 1, Intensity = spectrumIntensities[i], Position = spectrumMzs[i] });
            }

            return peaks;
        }

        public int GetTotalNumberOfSpectra(string spectraFile)
        {
            if (new MSDataRunPath(spectraFile) != msdataRunPath)
                return 0;
            using (var filereader = new MsDataFileImpl(msdataRunPath.Filepath, msdataRunPath.RunIndex, acceptZeroLengthSpectra: false, preferOnlyMsLevel: 2))
            {
                TotalSpectra = filereader.SpectrumCount;
            }

            return TotalSpectra;
        }
    }
}
