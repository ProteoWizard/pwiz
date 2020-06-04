using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MSAmanda.InOutput.Input;
using MSAmanda.Utils;
using pwiz.ProteowizardWrapper;

namespace pwiz.Skyline.Model.MSAmanda
{
    public class MSAmandaSpectrumParser : IParserInput
    {

        private MsDataFileImpl spectrumFileReader;
        private int specId = 0;
        private int amandaId = 0;
        private List<int> consideredCharges;
        private bool useMonoIsotopicMass;
        private string rawFileName; 
        public Dictionary<int, string> SpectTitleMap { get; }

        public MSAmandaSpectrumParser(string file, List<int> charges, bool mono)
        {
            consideredCharges = charges;
            if (file.EndsWith(".raw"))
            {
                spectrumFileReader = new MsDataFileImpl(file, requireVendorCentroidedMS2: true,
                    ignoreZeroIntensityPoints: true, trimNativeId: false);
            }
            else
            {
                spectrumFileReader = new MsDataFileImpl(file, requireVendorCentroidedMS2: false,
                    ignoreZeroIntensityPoints: true, trimNativeId: false);
            }

            useMonoIsotopicMass = mono;
            rawFileName = file;
            SpectTitleMap = new Dictionary<int, string>();
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
            while (nrOfParsed < numberOfSpectraToRead && specId < spectrumFileReader.SpectrumCount) { 
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

            return spectra;
        }

        private Spectrum GenerateSpectrum(Spectrum spec, int id, double mOverZ, int charge)
        {
            Spectrum s = new Spectrum
            {
                //clone peaks
                FragmentsPeaks = new List<AMassCentroid>(spec.FragmentsPeaks.ToArray()),
                SpectrumId = id,
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
            if (rawFileName != spectraFile)
                return 0;
            MsDataFileImpl filereader = new MsDataFileImpl(spectraFile, preferOnlyMsLevel:2);
            return filereader.SpectrumCount;
        }
    }
}
