// Hints to ReSharper for quiet compile (mostly about this code not worrying about localization)
// ReSharper disable LocalizableElement
// ReSharper disable StringCompareToIsCultureSpecific
// ReSharper disable UseCollectionCountProperty
// ReSharper disable SpecifyACultureInStringConversionExplicitly
// ReSharper disable PossibleLossOfFraction
// ReSharper disable RedundantNameQualifier
// ReSharper disable RedundantCast
// ReSharper disable RedundantUsingDirective
// ReSharper disable CheckNamespace

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Pwiz.Tools.BullseyeSharp
{
    public class CPep
    {
        public int charge;
        public float intensity;
        public double monoMass;
        public double basePeak;
        public double xCorr;
        public string mods;
        public string averagineHK; // The averagine formula used in Hardklor (Skyline version)
    }

    public class CScan
    {
        public List<CPep> vPep;
        public int scanNum;
        public string file;
        public float rTime;

        public CScan()
        {
            scanNum = 0;
            file = "";
            rTime = 0;
            vPep = new List<CPep>();
        }
    }

    public class CProfileData
    {
        public float intensity;
        public bool interpolated;
        public double monoMass;
        public float rTime;
        public int scanNum;
        public double xCorr;
        public string averagineHK; // The averagine formula used in Hardklor (Skyline version)

        //Constructor
        public CProfileData()
        {
            interpolated = false;
            scanNum = 0;
            intensity = 0.0f;
            rTime = 0.0f;
            monoMass = 0.0;
            xCorr = 0.0;
        }
    }

    public class CPepProfile
    {
        public int lowScan;
        public int highScan;
        public int bestScan;
        public int charge;
        public int MS2Events;

        public float rTime;
        public float firstRTime;
        public float lastRTime;
        public float intensity;
        public float sumIntensity;

        public double monoMass;
        public double basePeak;
        public double xCorr;

        public string mods;
        //public string gene;
        //public string sequence;
        public string averagineHK; // The averagine formula reported by Hardklor (Skyline version)
        public string file;

        public List<CProfileData> profile;

        public CPepProfile()
        {
            profile = new List<CProfileData>();
        }

        public void setPoints(int i){
            profile.Clear();
            for (int j = 0; j < i; j++) profile.Add(new CProfileData());
        }
}

    public struct iTwo
    {
        public int scan;
        public int pep;
        public iTwo(int a, int b)
        {
            scan = a;
            pep = b;
        }
    }

    class CKronik2
    {
        private int iGapTol;
        private int iMatchTol;
        private double dPPMTol;
        private List<CPepProfile> vPeps;

        public CKronik2(){
            dPPMTol   = 10.0;
            iGapTol   = 1;
            iMatchTol = 3;
            vPeps = new List<CPepProfile>();
        }

        public CPepProfile this[int i]
        {
            get => vPeps[i];
            set => vPeps[i] = value;
        }

        private float[] _highestRemainingIntensityPerScan;

        private void UpdateIntensityMetadata(int scanIndex, int pepIndexRemoved)
        {
            if (pepIndexRemoved == 0)
            {
                // We removed the highest intensity peptide for this scan, replace with next highest for this scan
                _highestRemainingIntensityPerScan[scanIndex] = (_allScans[scanIndex].vPep.Count > 0) ? _allScans[scanIndex].vPep[0].intensity : 0;
            }
        }

        // Return the index of the scan with the currently highest intensity remaining peptide
        public int findMax()
        {
            float max = 0;
            var maxScanIndex = -1;
            for (var i = 0; i < _allScans.Count; i++)
            {
                if (_highestRemainingIntensityPerScan[i] > max)
                {
                    max = _highestRemainingIntensityPerScan[i];
                    maxScanIndex = i;
                }
            }
            return maxScanIndex;
        }

        public double interpolate(int x1, int x2, double y1, double y2, int x)
        {
            double m = (y2 - y1) / (x2 - x1);
            double b = y2 - m * x2;
            return m * x + b;
        }

        private List<CScan> _allScans;

        public bool processHK(string inFile, string outFile="")
        {
            char tag;
            int i, j, k, k1, k2;

            int pepCount = 0;
            _allScans = new List<CScan>();

            int charge;
            int gap;
            int matchCount;
            bool bMatch;

            //CPepProfile s;
            //CProfileData p;

            //for tracking which peptides
            List<iTwo> vLeft = new List<iTwo>();
            List<iTwo> vRight = new List<iTwo>();

            //clear data
            vPeps.Clear();

            //Read in the Hardklor results
            if (!System.IO.File.Exists(inFile)) return false;
            IEnumerable<string> lines = null;
            for (var retry = 0; retry < 10; retry++)
            {
                try
                {
                    lines = System.IO.File.ReadLines(inFile);
                    break;
                }
                catch (Exception)
                {
                    Thread.Sleep(1000); // Hardklor will have just written the file, antivirus may be scanning it etc
                }
            }

            CScan currentScan = null;
            var currentPeps = new List<CPep>();
            foreach (string line in lines ?? System.IO.File.ReadLines(inFile))
            {
                tag = line[0];
                if (tag == 'S')
                {
                    _allScans.Add(currentScan = new CScan());
                    var tokens = line.Split('\t','\n','\r');
                    currentScan.scanNum = Convert.ToInt32(tokens[1]);
                    currentScan.rTime = (float)Convert.ToDouble(tokens[2]);
                    currentScan.file = tokens[3];
                }
                else
                {
                    pepCount++;
                    string[] tokens = line.Split('\t', '\n', '\r');
                    var cPep = new CPep()
                    {
                        monoMass = Convert.ToDouble(tokens[1]),
                        charge = Convert.ToInt32(tokens[2]),
                        intensity = (float)Convert.ToDouble(tokens[3]),
                        basePeak = Convert.ToDouble(tokens[4]),
                        mods = tokens[7],
                        xCorr = Convert.ToDouble(tokens[8]),
                        averagineHK = (tokens.Length > 9) ? tokens[9] : string.Empty // Only found in Skyline version of Hardklor
                    };
                    currentScan.vPep.Add(cPep);
                }
            }

            Console.WriteLine("" + pepCount + " peptides from " + _allScans.Count + " scans.\n");
            for (i = 0; i < _allScans.Count; i++) _allScans[i].vPep.Sort((a, b) => b.intensity.CompareTo(a.intensity));
            //for (i = 0; i < 10; i++) Console.WriteLine("" + i + "\t" + allScans[0].vPep[i].intensity);
            Console.WriteLine("Finding persistent peptide signals:");

            // Speed up the search-for-next-most-intense with some lightweight metadata
            _highestRemainingIntensityPerScan = _allScans.Select(s => (s.vPep.Count > 0 ? s.vPep[0].intensity : 0)).ToArray(); // Note current highest intensity per scan

            int startCount = pepCount;
            int lastPercent = 0;

            var bSkyline = !string.IsNullOrEmpty(_allScans.SelectMany(s=>s.vPep).FirstOrDefault()?.averagineHK); // For Skyline, a slightly different progress indicator

            Console.Write(lastPercent);// cout << lastPercent;
            if (bSkyline) Console.WriteLine("%"); // Hint for Skyline's process handler

            //Perform the Kronik analysis
            while (pepCount > 0)
            {
                var sIndex = findMax();
                if (sIndex < 0) continue;

                var maxScan = _allScans[sIndex];

                var mass = maxScan.vPep[0].monoMass;
                var massToler = mass * dPPMTol / 1000000.0;
                charge = maxScan.vPep[0].charge;
                matchCount = 1;

                //look left
                vLeft.Clear();
                gap = 0;
                i = sIndex - 1;
                while (i > -1 && gap <= iGapTol)
                {
                    bMatch = false;
                    int iScan = i;
                    int iPep = -1;
                    var scanI = _allScans[i];
                    for (j = 0; j < scanI.vPep.Count; j++)
                    {
                        if (scanI.vPep[j].charge != charge) continue;
                        //if(allScans[i].vPep->at(j).intensity<0.0) continue;
                        var massDiff = Math.Abs(scanI.vPep[j].monoMass - mass);
                        if (massDiff < massToler)
                        {
                            iPep = j;
                            gap = 0;
                            bMatch = true;
                            matchCount++;
                            break; // CONSIDER vPep is sorted by intensity rather than mass - is this really the best match?
                        }
                    }
                    if (!bMatch) gap++;
                    vLeft.Add(new iTwo(iScan,iPep));
                    i--;
                }

                //look right
                vRight.Clear();
                gap = 0;
                i = sIndex + 1;
                while (i < _allScans.Count && gap <= iGapTol)
                {
                    bMatch = false;
                    int iScan = i;
                    int iPep = -1;
                    var scanI = _allScans[i];
                    for (j = 0; j < scanI.vPep.Count; j++)
                    {
                        if (scanI.vPep[j].charge != charge) continue;
                        //if(allScans[i].vPep->at(j).intensity<0.0) continue;
                        var massDiff = Math.Abs(scanI.vPep[j].monoMass - mass);
                        if (massDiff < massToler)
                        {
                            iPep = j;
                            gap = 0;
                            bMatch = true;
                            matchCount++;
                            break; // CONSIDER vPep is sorted by intensity rather than mass - is this really the best match?
                        }
                    }
                    if (!bMatch) gap++;
                    vRight.Add(new iTwo(iScan,iPep));
                    i++;
                }

                //Only keep sufficient matches
                if (matchCount >= iMatchTol)
                {
                    //trim gaps
                    while (vLeft.Count > 0 && vLeft[vLeft.Count - 1].pep < 0) vLeft.RemoveAt(vLeft.Count-1);
                    while (vRight.Count > 0 && vRight[vRight.Count - 1].pep < 0) vRight.RemoveAt(vRight.Count-1);

                    //apply basic information
                    var cPepProfile = new CPepProfile();
                    vPeps.Add(cPepProfile);
                    cPepProfile.file = maxScan.file;
                    cPepProfile.rTime = maxScan.rTime;
                    cPepProfile.basePeak = maxScan.vPep[0].basePeak;
                    cPepProfile.bestScan = maxScan.scanNum;
                    cPepProfile.charge = maxScan.vPep[0].charge;
                    cPepProfile.intensity = maxScan.vPep[0].intensity;
                    cPepProfile.monoMass = maxScan.vPep[0].monoMass;
                    cPepProfile.mods = maxScan.vPep[0].mods;
                    cPepProfile.xCorr = maxScan.vPep[0].xCorr;
                    cPepProfile.averagineHK = maxScan.vPep[0].averagineHK;
                    cPepProfile.setPoints(vLeft.Count + vRight.Count + 1);
                    //cPepProfile.profile = new List<CProfileData>();
                    if (vLeft.Count == 0)
                    {
                        cPepProfile.lowScan = maxScan.scanNum;
                        cPepProfile.firstRTime = maxScan.rTime;
                    }
                    else
                    {
                        cPepProfile.lowScan = _allScans[vLeft[vLeft.Count - 1].scan].scanNum;
                        cPepProfile.firstRTime = _allScans[vLeft[vLeft.Count - 1].scan].rTime;
                    }
                    if (vRight.Count == 0)
                    {
                        cPepProfile.highScan = maxScan.scanNum;
                        cPepProfile.lastRTime = maxScan.rTime;
                    }
                    else
                    {
                        cPepProfile.highScan = _allScans[vRight[vRight.Count - 1].scan].scanNum;
                        cPepProfile.lastRTime = _allScans[vRight[vRight.Count - 1].scan].rTime;
                    }

                    //apply datapoints
                    cPepProfile.profile[0].intensity = maxScan.vPep[0].intensity;
                    cPepProfile.profile[0].interpolated = false;
                    cPepProfile.profile[0].monoMass = maxScan.vPep[0].monoMass;
                    cPepProfile.profile[0].rTime = maxScan.rTime;
                    cPepProfile.profile[0].scanNum = maxScan.scanNum;
                    cPepProfile.profile[0].xCorr = maxScan.vPep[0].xCorr;
                    cPepProfile.profile[0].averagineHK = maxScan.vPep[0].averagineHK;

                    i = 1;
                    j = 0;
                    while (j < vLeft.Count)
                    {
                        //Handle interpolated data
                        if (vLeft[j].pep < 0)
                        {
                            k = 0;
                            while (vLeft[j + k].pep < 0) k++;
                            k2 = j + k;
                            if (j == 0)
                            {
                                cPepProfile.profile[i].intensity = (float)interpolate(cPepProfile.profile[0].scanNum, _allScans[vLeft[k2].scan].scanNum, (double)cPepProfile.profile[0].intensity, (double)_allScans[vLeft[k2].scan].vPep[vLeft[k2].pep].intensity, _allScans[vLeft[j].scan].scanNum);
                                cPepProfile.profile[i].monoMass = interpolate(cPepProfile.profile[0].scanNum, _allScans[vLeft[k2].scan].scanNum, cPepProfile.profile[0].monoMass, _allScans[vLeft[k2].scan].vPep[vLeft[k2].pep].monoMass, _allScans[vLeft[j].scan].scanNum);
                            }
                            else
                            {
                                k = 0;
                                while (j + k >= 0 && vLeft[j + k].pep < 0) k--;
                                k1 = j + k;
                                if (k1 < 0)
                                {
                                    cPepProfile.profile[i].intensity = (float)interpolate(cPepProfile.profile[0].scanNum, _allScans[vLeft[k2].scan].scanNum, (double)cPepProfile.profile[0].intensity, (double)_allScans[vLeft[k2].scan].vPep[vLeft[k2].pep].intensity, _allScans[vLeft[j].scan].scanNum);
                                    cPepProfile.profile[i].monoMass = interpolate(cPepProfile.profile[0].scanNum, _allScans[vLeft[k2].scan].scanNum, cPepProfile.profile[0].monoMass, _allScans[vLeft[k2].scan].vPep[vLeft[k2].pep].monoMass, _allScans[vLeft[j].scan].scanNum);
                                }
                                else
                                {
                                    cPepProfile.profile[i].intensity = (float)interpolate(_allScans[vLeft[k1].scan].scanNum, _allScans[vLeft[k2].scan].scanNum, (double)_allScans[vLeft[k1].scan].vPep[vLeft[k1].pep].intensity, (double)_allScans[vLeft[k2].scan].vPep[vLeft[k2].pep].intensity, _allScans[vLeft[j].scan].scanNum);
                                    cPepProfile.profile[i].monoMass = interpolate(_allScans[vLeft[k1].scan].scanNum, _allScans[vLeft[k2].scan].scanNum, _allScans[vLeft[k1].scan].vPep[vLeft[k1].pep].monoMass, _allScans[vLeft[k2].scan].vPep[vLeft[k2].pep].monoMass, _allScans[vLeft[j].scan].scanNum);
                                }
                            }
                            cPepProfile.profile[i].interpolated = true;
                            cPepProfile.profile[i].rTime = _allScans[vLeft[j].scan].rTime;
                            cPepProfile.profile[i].scanNum = _allScans[vLeft[j].scan].scanNum;
                            cPepProfile.profile[i].xCorr = 0.0;
                        }
                        else
                        {
                            cPepProfile.profile[i].intensity = _allScans[vLeft[j].scan].vPep[vLeft[j].pep].intensity;
                            cPepProfile.profile[i].interpolated = false;
                            cPepProfile.profile[i].monoMass = _allScans[vLeft[j].scan].vPep[vLeft[j].pep].monoMass;
                            cPepProfile.profile[i].rTime = _allScans[vLeft[j].scan].rTime;
                            cPepProfile.profile[i].scanNum = _allScans[vLeft[j].scan].scanNum;
                            cPepProfile.profile[i].xCorr = _allScans[vLeft[j].scan].vPep[vLeft[j].pep].xCorr;
                            cPepProfile.profile[i].averagineHK = _allScans[vLeft[j].scan].vPep[vLeft[j].pep].averagineHK;
                        }
                        i++;
                        j++;
                    }

                    j = 0;
                    while (j < vRight.Count)
                    {
                        //Handle interpolated data
                        if (vRight[j].pep < 0)
                        {
                            k = 0;
                            while (vRight[j + k].pep < 0) k++;
                            k2 = j + k;
                            if (j == 0)
                            {
                                cPepProfile.profile[i].intensity = (float)interpolate(cPepProfile.profile[0].scanNum, _allScans[vRight[k2].scan].scanNum, cPepProfile.profile[0].intensity, _allScans[vRight[k2].scan].vPep[vRight[k2].pep].intensity, _allScans[vRight[j].scan].scanNum);
                                cPepProfile.profile[i].monoMass = interpolate(cPepProfile.profile[0].scanNum, _allScans[vRight[k2].scan].scanNum, cPepProfile.profile[0].monoMass, _allScans[vRight[k2].scan].vPep[vRight[k2].pep].monoMass, _allScans[vRight[j].scan].scanNum);
                            }
                            else
                            {
                                k = 0;
                                while ((j + k > -1) && vRight[j + k].pep < 0) k--;
                                k1 = j + k;
                                if (k1 < 0)
                                {
                                    cPepProfile.profile[i].intensity = (float)interpolate(cPepProfile.profile[0].scanNum, _allScans[vRight[k2].scan].scanNum, cPepProfile.profile[0].intensity, _allScans[vRight[k2].scan].vPep[vRight[k2].pep].intensity, _allScans[vRight[j].scan].scanNum);
                                    cPepProfile.profile[i].monoMass = interpolate(cPepProfile.profile[0].scanNum, _allScans[vRight[k2].scan].scanNum, cPepProfile.profile[0].monoMass, _allScans[vRight[k2].scan].vPep[vRight[k2].pep].monoMass, _allScans[vRight[j].scan].scanNum);
                                }
                                else
                                {
                                    cPepProfile.profile[i].intensity = (float)interpolate(_allScans[vRight[k1].scan].scanNum, _allScans[vRight[k2].scan].scanNum, _allScans[vRight[k1].scan].vPep[vRight[k1].pep].intensity, _allScans[vRight[k2].scan].vPep[vRight[k2].pep].intensity, _allScans[vRight[j].scan].scanNum);
                                    cPepProfile.profile[i].monoMass = interpolate(_allScans[vRight[k1].scan].scanNum, _allScans[vRight[k2].scan].scanNum, _allScans[vRight[k1].scan].vPep[vRight[k1].pep].monoMass, _allScans[vRight[k2].scan].vPep[vRight[k2].pep].monoMass, _allScans[vRight[j].scan].scanNum);
                                }
                            }
                            cPepProfile.profile[i].interpolated = true;
                            cPepProfile.profile[i].rTime = _allScans[vRight[j].scan].rTime;
                            cPepProfile.profile[i].scanNum = _allScans[vRight[j].scan].scanNum;
                            cPepProfile.profile[i].xCorr = 0.0;
                        }
                        else
                        {
                            cPepProfile.profile[i].intensity = _allScans[vRight[j].scan].vPep[vRight[j].pep].intensity;
                            cPepProfile.profile[i].interpolated = false;
                            cPepProfile.profile[i].monoMass = _allScans[vRight[j].scan].vPep[vRight[j].pep].monoMass;
                            cPepProfile.profile[i].rTime = _allScans[vRight[j].scan].rTime;
                            cPepProfile.profile[i].scanNum = _allScans[vRight[j].scan].scanNum;
                            cPepProfile.profile[i].xCorr = _allScans[vRight[j].scan].vPep[vRight[j].pep].xCorr;
                            cPepProfile.profile[i].averagineHK = _allScans[vRight[j].scan].vPep[vRight[j].pep].averagineHK;
                        }
                        i++;
                        j++;
                    }

                    //sort the profile
                    cPepProfile.profile.Sort((a, b) => a.scanNum.CompareTo(b.scanNum));

                    //summed intensity (including interpolation)
                    cPepProfile.sumIntensity = 0.0f;
                    for (i = 0; i < cPepProfile.profile.Count; i++) cPepProfile.sumIntensity += cPepProfile.profile[i].intensity;

                    //Erase datapoints already used
                    for (i = 0; i < vLeft.Count; i++)
                    {
                        if (vLeft[i].pep < 0) continue;
                        _allScans[vLeft[i].scan].vPep.RemoveAt(vLeft[i].pep);
                        UpdateIntensityMetadata(vLeft[i].scan, vLeft[i].pep); // Update the search metadata as needed
                        pepCount--;
                    }
                    for (i = 0; i < vRight.Count; i++)
                    {
                        if (vRight[i].pep < 0) continue;
                        _allScans[vRight[i].scan].vPep.RemoveAt(vRight[i].pep);
                        UpdateIntensityMetadata(vRight[i].scan, vRight[i].pep); // Update the search metadata as needed
                        pepCount--;
                    }
                }

                //erase the one we're looking at
                maxScan.vPep.RemoveAt(0);
                UpdateIntensityMetadata(sIndex, 0); // Update the search metadata as needed
                pepCount--;

                //update percent
                int iPercent = 100 - (int)((float)pepCount / (float)startCount * 100.0);
                if (iPercent > lastPercent)
                {
                    if (bSkyline)
                        Console.WriteLine($"{iPercent}%"); // Hint for Skyline's process handler
                    else
                        Console.Write("\b\b\b" + iPercent); // cout << "\b\b\b" << iPercent;
                    lastPercent = iPercent;
                }

            }
            Console.WriteLine("");

            if (outFile.Length>0){
                StreamWriter f = new StreamWriter(outFile);

                //Heading line
                f.Write("File\tFirst Scan\tLast Scan\tNum of Scans\tCharge\tMonoisotopic Mass\tBase Isotope Peak\t");
                f.Write("Best Intensity\tSummed Intensity\tFirst RTime\tLast RTime\tBest RTime\tBest Correlation\tModifications");
                f.WriteLine(bSkyline?"\tBest Scan\tAveragine":string.Empty);

                for (i = 0; i < vPeps.Count; i++)
                {
                    f.Write($"{vPeps[i].file}\t");
                    f.Write(vPeps[i].lowScan + "\t");
                    f.Write(vPeps[i].highScan + "\t");
                    f.Write(vPeps[i].profile.Count + "\t");
                    f.Write(vPeps[i].charge + "\t");
                    f.Write(vPeps[i].monoMass + "\t");
                    f.Write(vPeps[i].basePeak + "\t");
                    f.Write(vPeps[i].intensity + "\t");
                    f.Write(vPeps[i].sumIntensity + "\t");
                    f.Write(vPeps[i].firstRTime + "\t");
                    f.Write(vPeps[i].lastRTime + "\t");
                    f.Write(vPeps[i].rTime + "\t");
                    f.Write(vPeps[i].xCorr + "\t");
                    f.WriteLine(bSkyline ? $"{vPeps[i].mods}\t{vPeps[i].bestScan}\t{vPeps[i].averagineHK}" : vPeps[i].mods);
                }
                f.Close();
            }

            return true;
        }

        public void removeContam(double contam)
        {
            if (contam <= 0.0) return;
            vPeps = vPeps.FindAll(a => (a.lastRTime - a.firstRTime) <= contam);
        }
        public void removeMass(double minMass, double maxMass)
        {
            vPeps = vPeps.FindAll(a => (a.monoMass >= minMass && a.monoMass <= maxMass));
        }

        public void setGapTol(int i) { iGapTol = i; }
        public void setMatchTol(int i) { iMatchTol = i; }
        public void setPPMTol(double d) { dPPMTol=d; }
        public int size() { return vPeps.Count; }
        public void sortBasePeak()
        {
            vPeps.Sort((a, b) => a.basePeak.CompareTo(b.basePeak));
        }
    }
}
