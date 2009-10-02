using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.CLI;
using pwiz.CLI.msdata;
using turnover;
using turnover.Data;
using turnover.Enrichment;

namespace topog
{
    class MpeCalculator
    {
        private List<PeptideCalculator> peptides = new List<PeptideCalculator>();
        public void AddPeptideCalculator(PeptideCalculator peptideCalculator)
        {
            peptides.Add(peptideCalculator);
        }
        public void ProcessFile(MSDataFile msDataFile)
        {
            var spectrumList = msDataFile.run.spectrumList;
            var timeIntensityPairs = new TimeIntensityPairList();
            using (var chromatogram = msDataFile.run.chromatogramList.chromatogram(0, true))
            {
                chromatogram.getTimeIntensityPairs(ref timeIntensityPairs);
            }
            int minScan = int.MaxValue;
            int maxScan = 0;
            foreach (var peptide in peptides)
            {
                minScan = Math.Min(minScan, peptide.GetMinScan());
                maxScan = Math.Max(maxScan, peptide.GetMaxScan());
            }
            minScan = Math.Max(minScan, 0);
            maxScan = Math.Min(maxScan, spectrumList.size());
            for (int iScan = minScan; iScan < maxScan; iScan++)
            {
                using (var spectrum = spectrumList.spectrum(iScan))
                {
                    if (spectrum.cvParam(CVID.MS_ms_level).value != 1)
                    {
                        continue;
                    }
                }
                var time = timeIntensityPairs[iScan].time;
                using (var spectrum = spectrumList.spectrum(iScan, true))
                {
                    var mzs = ToArray(spectrum.getMZArray().data);
                    var intensities = ToArray(spectrum.getIntensityArray().data);
                    foreach (var peptide in peptides)
                    {
                        peptide.ProcessScan(iScan, time, mzs, intensities);
                    }
                }
            }
            foreach (var peptide in peptides)
            {
                peptide.Finish();
            }
            peptides.Clear();
        }
        private double[] ToArray(IList<double> list)
        {
            double[] result = new double[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }
            return result;
        }
    }
    public class PeptideCalculator
    {
        private EnrichmentDef enrichment;
        private ChargedPeptide peptide;
        private int firstScan;
        private int lastScan;
        private List<double> masses;
        private List<List<double>> intensitiesList;
        private DbPeptideSearchResult dbPeptideSearchResult;

        private List<double> times = new List<double>();
        private List<int> scanIndexes = new List<int>();
        private bool finished;
        private const double resolution = 50000;
        private TextWriter writer;

        private int firstScanIndex = int.MaxValue;
        private int lastScanIndex = int.MinValue;
        
        public PeptideCalculator(EnrichmentDef enrichment, DbPeptideSearchResult dbPeptideSearchResult, TextWriter writer)
        {
            this.enrichment = enrichment;
            this.dbPeptideSearchResult = dbPeptideSearchResult;
            this.writer = writer;
            firstScan = dbPeptideSearchResult.FirstDetectedScan;
            lastScan = dbPeptideSearchResult.LastDetectedScan;
        }

        private const int MIN_ENRICHMENT_COUNT = 3;
        public bool Init(int iScan)
        {
            if (finished || iScan < GetMinScan()) 
            {
                return false;
            }
            if (iScan >= GetMaxScan())
            {
                Finish();
                return false;
            }
            if (iScan <= firstScan)
            {
                firstScanIndex = Math.Min(firstScanIndex, times.Count);
            }
            if (iScan <= lastScan)
            {
                lastScanIndex = Math.Max(lastScanIndex, times.Count);
            }
            if (peptide == null)
            {
                if ("Unknown" == dbPeptideSearchResult.Peptide.Protein)
                {
                    finished = true;
                    return false;
                }
                peptide = new ChargedPeptide(dbPeptideSearchResult.Peptide.Sequence, dbPeptideSearchResult.Charge);
            }
            if (masses == null)
            {
                masses = enrichment.GetMzs(peptide);
                times = new List<double>();
                intensitiesList = new List<List<double>>();
                foreach (var mass in masses)
                {
                    intensitiesList.Add(new List<double>());
                }
                if (masses.Count <= MIN_ENRICHMENT_COUNT * Math.Round(enrichment.DeltaMass))
                {
                    finished = true;
                }
            }
            return !finished;
        }
        public void ProcessScan(int iScan, double time, double[] mzs, double[] intensities)
        {
            if (!Init(iScan))
            {
                return;
            }
            times.Add(time);
            scanIndexes.Add(iScan);
            for (int i = 0; i < masses.Count(); i++)
            {
                intensitiesList[i].Add(MsDataFiles.GetIntensity(masses[i], mzs, intensities).Intensity);
            }
        }
        public void AddScans(int scanStart, int scanEnd)
        {
            firstScan = Math.Min(firstScan, scanStart);
            lastScan = Math.Max(lastScan, scanEnd);
        }
        public void Finish()
        {
            if (finished)
            {
                return;
            }
            int peakStart = firstScanIndex;
            int peakEnd = lastScanIndex;
            int nextPeakStart = peakStart;
            int nextPeakEnd = peakEnd;
            for (int iMass = 0; iMass < masses.Count(); iMass++)
            {
                //var peakFinder = new CrawdadPeakFinder();
                //peakFinder.SetChromatogram(times, intensitiesList[iMass]);
                //foreach (var peak in peakFinder.CalcPeaks())
                //{
                //    if (peak.EndIndex >= peakStart)
                //    {
                //        nextPeakStart = Math.Min(nextPeakStart, peak.StartIndex);
                //    }
                //    if (peak.StartIndex <= peakEnd)
                //    {
                //        nextPeakEnd = Math.Max(nextPeakEnd, peak.EndIndex);
                //    }
                //}
                //if (enrichment.IsotopesEluteEarlier)
                //{
                //    peakStart = nextPeakStart;
                //}
                //if (enrichment.IsotopesEluteLater) {
                //    peakEnd = nextPeakEnd;
                //}
            }
            peakStart = nextPeakStart;
            peakEnd = nextPeakEnd;
            List<double> observedIntensities = new List<double>();
            for (int iMass = 0; iMass < masses.Count(); iMass++)
            {
                observedIntensities.Add(SumIntensities(intensitiesList[iMass], peakStart, peakEnd));
            }
            TurnoverCalculator turnoverCalculator = new TurnoverCalculator(enrichment, peptide);
            var result = turnoverCalculator.FindBestInitialFinalApe(observedIntensities, 100);
            List<String> fields = new List<string>();
            fields.Add(Path.GetFileNameWithoutExtension(dbPeptideSearchResult.MsDataFile.Path));
            fields.Add(dbPeptideSearchResult.Peptide.Protein);
            fields.Add(peptide.Sequence + "+" + peptide.Charge);
            fields.Add(Math.Round(result.Turnover).ToString());
            fields.Add(result.InitialApe.ToString());
            fields.Add(result.FinalApe.ToString());
            fields.Add(result.Score.ToString());
            fields.Add(scanIndexes[peakStart] + "-" + scanIndexes[peakEnd]);
            foreach (var i in observedIntensities)
            {
                fields.Add(Math.Round(i).ToString());
            }
            String line = String.Join("\t", fields.ToArray());
            if (writer != null)
            {
                writer.WriteLine(line);
                writer.Flush();
            }
            Console.Out.WriteLine(line);
            finished = true;
            scanIndexes = null;
            times = null;
            intensitiesList = null;
        }

        double SumIntensities(IList<double> intensities, int startIndex, int endIndex)
        {
            double total = 0;
            for (int i = startIndex; i <= endIndex && i < intensities.Count; i++)
            {
                total += intensities[i];
            }
            return total;
        }
        public int GetMinScan()
        {
            return firstScan - 1800;
        }
        public int GetMaxScan()
        {
            return lastScan + 1800;
        }
    }
}
