// Hints to ReSharper for quiet compile (mostly about this code not worrying about localization)
// ReSharper disable LocalizableElement
// ReSharper disable StringCompareToIsCultureSpecific
// ReSharper disable UseCollectionCountProperty
// ReSharper disable SpecifyACultureInStringConversionExplicitly
// ReSharper disable PossibleLossOfFraction
// ReSharper disable RedundantUsingDirective
// ReSharper disable CheckNamespace

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.Common.Params;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Instruments;
using Pwiz.Data.MsData.Readers;
using Pwiz.Data.MsData.Spectra;
using Pwiz.Vendor.Thermo;

namespace Pwiz.Tools.BullseyeSharp
{
    struct sPrecursor
    {
        public int charge;
        public double monoMz;
    }

    class sScanInfo
    {
        public int msLevel;
        public int scanNumber;
        public double mz;
        public float rTime;
        public sPrecursor[] precursor;

        public sScanInfo()
        {
            precursor = new sPrecursor[100];
        }
    }

    class sBullseyeMatch
    {
        public int index;
        public int scanNumber;
    }

    class Program
    {
        //double mean, stD;
        static double ppmTolerance;
        static double rtTolerance;
        static bool bMatchPrecursorOnly;
        static string sHKFile;
        static string sDataFile;
        static string sPosFile;
        static string sNegFile;
        static string sSumFile;
        static string sID = "v1.33"; // 1.33 has some tweaks for Skyline use - timer, performance improvements
        static string sDate = "July 2 2024";
        static string sCopyright = "Copyright 2008-2024 Mike Hoopmann, Ed Hsieh, Mike MacCoss";
        static string sTimerArg = "--timer";


        static void Main(string[] args)
        {
            Stopwatch timer = null;

            CKronik2 p1 = new CKronik2();

            if (args.Contains(sTimerArg))
            {
                // Skyline use, put copyright and version on a single line
                Console.WriteLine($"{sID}, {sDate} {sCopyright} University of Washington");
            }
            else
            {
                Console.WriteLine("BullseyeSharp " + sID + ", " + sDate);
                Console.WriteLine(sCopyright);
                Console.WriteLine("University of Washington");
            }

            //Set global variables
            ppmTolerance = 10.0;
            rtTolerance = 0.5;
            bMatchPrecursorOnly = false;
            sSumFile = "";

            //Set default parameters for some options
            double contam = 2.0;
            double maxMass = 8000.0;
            double minMass = 600.0;

            if (args.Count() < 4)
            {
                usage();
                return;
            }

            int fcount = 0;
            for (int i = 0; i < args.Count(); i++)
            {
                if (args[i].CompareTo("-c") == 0)
                {
                    contam = Convert.ToDouble(args[++i]);
                }
                else if (args[i].CompareTo("-e") == 0)
                {
                    bMatchPrecursorOnly = true;
                }
                else if (args[i].CompareTo("-g") == 0)
                {
                    p1.setGapTol(Convert.ToInt32(args[++i]));
                }
                else if (args[i].CompareTo("-m") == 0)
                {
                    maxMass = Convert.ToDouble(args[++i]);
                }
                else if (args[i].CompareTo("-n") == 0)
                {
                    minMass = Convert.ToDouble(args[++i]);
                }
                else if (args[i].CompareTo("-o") == 0)
                {
                    sSumFile = args[++i];
                }
                else if (args[i].CompareTo("-p") == 0)
                {
                    ppmTolerance = Convert.ToDouble(args[++i]);
                }
                else if (args[i].CompareTo("-r") == 0)
                {
                    p1.setPPMTol(Convert.ToDouble(args[++i]));
                }
                else if (args[i].CompareTo("-s") == 0)
                {
                    p1.setMatchTol(Convert.ToInt32(args[++i]));
                }
                else if (args[i].CompareTo("-t") == 0)
                {
                    rtTolerance = Convert.ToDouble(args[++i]);
                }
                else if (args[i].CompareTo(sTimerArg) == 0)
                {
                    timer = new Stopwatch();
                    timer.Start();
                }
                else if (args[i][0] == '-')
                {
                    Console.WriteLine("\nInvalid flag: " + args[i] + "\n\n");
                    usage();
                    return;
                }
                else
                {
                    if (fcount == 0) sHKFile = args[i];
                    else if (fcount == 1) sDataFile = args[i];
                    else if (fcount == 2) sPosFile = args[i];
                    else if (fcount == 3) sNegFile = args[i];
                    else
                    {
                        Console.WriteLine("\nInvalid parameter: " + args[i] + "\n\n");
                        usage();
                        return;
                    }
                    fcount++;
                }
            }

            p1.processHK(sHKFile, sHKFile + ".bs.kro");

            if (!string.IsNullOrEmpty(sPosFile)) // This won't be specified for MS1-only use
            {
                //Remove contaminants
                p1.removeContam(contam);
                Console.WriteLine("Persistent peptides after removing contaminants: " + p1.size());

                //Filter based on size
                p1.removeMass(minMass, maxMass);
                Console.WriteLine("Persistent peptides from " + minMass + " to " + maxMass + ": " + p1.size());

                matchMS2(ref p1, sDataFile, sPosFile, sNegFile);
            }
            if (timer != null)
                Console.WriteLine($"Completed in {timer.Elapsed}");
        }

        static string averagine(double mass)
        {
            string st = "";
            int aa = (int)(mass / 111.2137);
            int c = (int)(aa * 4.9558 + 0.5);
            int h = (int)(aa * 7.8241 + 0.5);
            int n = (int)(aa * 1.3571 + 0.5);
            int o = (int)(aa * 1.4716 + 0.5);
            int s = (int)(aa * 0.0390 + 0.5);

            double tot = c * 12 + h * 1.007825 + n * 14.003074 + o * 15.9949141 + s * 31.97207;

            h += (int)((mass - tot) / 1.007285 + 0.5);
            st = "C" + c.ToString() + "H" + h.ToString() + "N" + n.ToString() + "O" + o.ToString();
            if (s > 0) st += "S" + s.ToString();

            return st;
        }

        // Sentinel for "extension not recognized as an acceptable output format". cpp's
        // MSDataFile.Format.Format_Text served the same role; sharp's WriteFormat.Text is
        // also unimplemented as an output writer, so we use it as the rejection signal.
        private const WriteFormat UnrecognizedFormat = WriteFormat.Text;

        // Build the vendor-aware ReaderList used for reading sDataFile. Mirrors MsConvert's
        // configuration (the only difference is that BullseyeSharp doesn't need to plumb
        // --combineIonMobilitySpectra etc. through to the readers — it operates on standard
        // DDA MS/MS data).
        static ReaderList BuildReaderList()
        {
            var readers = ThermoReaderRegistration.CreateDefaultWithThermo();
            readers.Add(new Pwiz.Vendor.Bruker.Reader_Bruker());
            readers.Add(new Pwiz.Vendor.Waters.Reader_Waters());
            readers.Add(new Pwiz.Vendor.Agilent.Reader_Agilent());
            readers.Add(new Pwiz.Vendor.Sciex.Reader_Sciex());
            readers.Add(new Pwiz.Vendor.Shimadzu.Reader_Shimadzu());
            readers.Add(new Pwiz.Vendor.UNIFI.Reader_UNIFI());
            readers.Add(new Pwiz.Vendor.UIMF.Reader_UIMF());
            readers.Add(new Pwiz.Vendor.Mobilion.Reader_Mobilion());
            return readers;
        }

        static void matchMS2(ref CKronik2 p, string ms2File, string outFile, string outFile2)
        {
            Spectrum s;
            WriteConfig wcPos = getFileFormat(outFile);
            WriteConfig wcNeg = getFileFormat(outFile2);
            SpectrumListSimple slPos = new SpectrumListSimple();
            SpectrumListSimple slNeg = new SpectrumListSimple();
            int i, j;
            const int LOOKUP_LEN = 8001;
            int[] lookup = new int[LOOKUP_LEN];
            double lowMass, highMass, ppm;
            int x, z;
            int a, b;
            int c = 0;
            int d = 0;
            int iPercent = 0;
            int index;
            List<int> vI = new List<int>();
            List<int> vHit = new List<int>();
            List<sBullseyeMatch> vMatch = new List<sBullseyeMatch>();

            int[] ch = new int[10];

            //Check file formats for output. Make sure the user specifies the appropriate format
            if (wcPos.Format == UnrecognizedFormat)
            {
                Console.WriteLine("Output file format (pos set) not acceptable. Choose a different format.");
                return;
            }
            if (wcNeg.Format == UnrecognizedFormat)
            {
                Console.WriteLine("Output file format (neg set) not acceptable. Choose a different format.");
                return;
            }

            var bSkyline = p.size() > 0 && !string.IsNullOrEmpty(p[0].averagineHK); // Is this the Skyline use case?

            //Hardklor results are sorted and indexed to improve speed of Bullseye
            Console.Write("Sorting Hardklor results...");
            p.sortBasePeak();
            Console.WriteLine("Done!");

            Console.Write("Building lookup table...");
            j = 0;
            if (p.size() > 0)
            {
                for (i = 0; i < LOOKUP_LEN; i++)
                {
                    while (p[j].basePeak < i)
                    {
                        if (j >= p.size() - 1) break;
                        j++;
                    }
                    lookup[i] = j;
                }
            }
            Console.WriteLine("Done!");

            //Read in the data
            Console.Write("Matching MS/MS..." + iPercent);

            b = 0;
            z = 0;
            a = 0;

            // Open sDataFile through the vendor-aware reader list (mzML, mzXML, MGF, MS2, plus
            // Thermo/Bruker/Waters/etc. when -p:IAgreeToVendorLicenses=true was set at build time).
            var msd = new MSData();
            var readers = BuildReaderList();
            readers.Read(sDataFile, msd);

            // Tag BullseyeSharp as an analysis-software entry in the output. CVParam constructor
            // with just the CVID tags the param without a value, matching cpp BullseyeSharp.
            Software sw = new Software("BullseyeSharp", new CVParam(CVID.MS_analysis_software), "1.32");
            msd.Software.Add(sw);
            ISpectrumList sl = msd.Run.SpectrumList;
            for (int sc = 0; sc < sl.Count; sc++)
            {
                s = sl.GetSpectrum(sc, true);
                sScanInfo scanInfo = processScanHeader(ref s);

                if (scanInfo.msLevel != 2) continue;
                j = (int)(scanInfo.mz + 0.5);
                x = 0;
                vHit.Clear();

                //see if we can pick it up on base peak alone
                for (i = lookup[j - 1]; i <= lookup[j + 1]; i++)
                {
                    ppm = (p[i].basePeak - scanInfo.mz) / scanInfo.mz * 1000000;
                    if (Math.Abs(ppm) < ppmTolerance &&
                        scanInfo.rTime > p[i].firstRTime - rtTolerance &&
                        scanInfo.rTime < p[i].lastRTime + rtTolerance)
                    {
                        x++;
                        index = i;
                        vHit.Add(i);
                    }
                }

                //if base peak wasn't enough, perhaps a different peak was isolated
                if (!bMatchPrecursorOnly)
                {
                    for (i = 0; i < p.size(); i++)
                    {
                        lowMass = (p[i].monoMass + p[i].charge * 1.00727649) / p[i].charge - 0.05;
                        if (p[i].charge == 1) highMass = (p[i].monoMass + p[i].charge * 1.00727649) / p[i].charge + 3.10;
                        else if (p[i].charge == 2) highMass = (p[i].monoMass + p[i].charge * 1.00727649) / p[i].charge + 2.10;
                        else highMass = (p[i].monoMass + p[i].charge * 1.00727649) / p[i].charge + 4 / p[i].charge + 0.05;

                        if (scanInfo.mz > lowMass && scanInfo.mz < highMass &&
                            scanInfo.rTime > p[i].firstRTime - rtTolerance &&
                            scanInfo.rTime < p[i].lastRTime + rtTolerance)
                        {
                            x++;
                            index = i;
                            vHit.Add(i);
                        }
                    }
                }

                vI.Add(x);

                if (x == 0)
                {
                    z++;
                    slNeg.Spectra.Add(s);
                    slNeg.Spectra.Last().Index = slNeg.Spectra.Count - 1;
                    ch[0]++;
                }
                else if (x == 1)
                {
                    a++;
                    slPos.Spectra.Add(s);
                    slPos.Spectra.Last().Index = slPos.Spectra.Count - 1;
                    slPos.Spectra.Last().Params.UserParams.Clear();
                    slPos.Spectra.Last().Params.UserParams.Add(new UserParam("ms2 file charge state", p[vHit[0]].charge.ToString() + " " + (p[vHit[0]].monoMass + 1.00727649).ToString()));
                    slPos.Spectra.Last().Precursors[0].SelectedIons.Clear();
                    SelectedIon si = new SelectedIon();
                    si.Set(CVID.MS_charge_state, p[vHit[0]].charge);
                    si.Set(CVID.MS_accurate_mass_OBSOLETE, p[vHit[0]].monoMass + 1.00727649);
                    si.Set(CVID.MS_selected_ion_m_z, p[vHit[0]].monoMass + 1.00727649);
                    slPos.Spectra.Last().Precursors[0].SelectedIons.Add(si);
                    c++;
                }
                else
                {
                    //Add spectra and erase original precursor estimates
                    slPos.Spectra.Add(s);
                    slPos.Spectra.Last().Index = slPos.Spectra.Count - 1;
                    slPos.Spectra.Last().Params.UserParams.Clear();
                    slPos.Spectra.Last().Precursors[0].SelectedIons.Clear();

                    //erase redundancies in multiple hit list
                    for (i = 0; i < vHit.Count - 1; i++)
                    {
                        for (j = i + 1; j < vHit.Count; j++)
                        {
                            if (p[vHit[i]].charge == p[vHit[j]].charge)
                            {
                                string str1 = Convert.ToString(Math.Round(p[vHit[i]].monoMass + 1.00727649, 2));
                                string str2 = Convert.ToString(Math.Round(p[vHit[j]].monoMass + 1.00727649, 2));

                                if (str1.CompareTo(str2) == 0)
                                {
                                    if (p[vHit[i]].intensity < p[vHit[j]].intensity) vHit[i] = vHit[j];
                                    vHit.RemoveAt(j--);
                                }
                            }
                        }
                    }

                    for (i = 0; i < vHit.Count; i++)
                    {
                        slPos.Spectra.Last().Params.UserParams.Add(new UserParam("ms2 file charge state", p[vHit[i]].charge.ToString() + " " + (p[vHit[i]].monoMass + 1.00727649).ToString()));
                        SelectedIon si = new SelectedIon();
                        si.Set(CVID.MS_charge_state, p[vHit[i]].charge);
                        si.Set(CVID.MS_accurate_mass_OBSOLETE, p[vHit[i]].monoMass + 1.00727649);
                        si.Set(CVID.MS_selected_ion_m_z, p[vHit[i]].monoMass + 1.00727649);
                        slPos.Spectra.Last().Precursors[0].SelectedIons.Add(si);
                    }
                    if (vHit.Count == 1)
                    {
                        a++;
                        c++;
                    }
                    else
                    {
                        b++;
                        d += vHit.Count;
                    }
                }

                for (i = 0; i < vHit.Count; i++) ch[p[vHit[i]].charge]++;

                //export matched features
                for (i = 0; i < vHit.Count; i++)
                {
                    vMatch.Add(new sBullseyeMatch());
                    vMatch.Last().index = vHit[i];
                    vMatch.Last().scanNumber = scanInfo.scanNumber;
                }

                //Update file position counter
                if (sc * 100 / sl.Count > iPercent)
                {
                    if (!bSkyline)
                    {
                        if (iPercent < 10) Console.Write("\b");
                        else Console.Write("\b\b");
                    }

                    iPercent = sc * 100 / sl.Count;
                    Console.Write(iPercent);
                    if (bSkyline) Console.WriteLine("%");
                }
            }

            Console.WriteLine(" Done!");

            Console.WriteLine(z + " scans had no visible parental distribution.");
            Console.WriteLine(a + " scans had a single parental distribution.");
            Console.WriteLine(b + " scans had multiple possible parental distributions.");

            //Some simple tables of where parental ions are found relative to scan number
            i = 0;
            a = 0;
            x = 0;
            z = 0;
            b = 1;
            Console.WriteLine("\nScan:\tNeg\tPos");
            while (i < vI.Count)
            {
                if (vI[i] == 0) x++;
                else z++;
                a++;

                if (a == 1000)
                {
                    Console.WriteLine(b * a + "\t" + x + "\t" + z);
                    x = 0;
                    z = 0;
                    b++;
                    a = 0;
                }

                i++;
            }

            Console.WriteLine(b * 1000 + a + "\t" + x + "\t" + z);

            Console.WriteLine("\nUnknown charge: " + ch[0]);
            for (i = 1; i < 10; i++) Console.WriteLine("+" + i + ": " + ch[i]);

            Console.WriteLine(c + " singles and " + d + " doubles total.");

            vMatch.Sort((v1, v2) =>
            {
                int r = v1.index.CompareTo(v2.index);
                if (r == 0) r = v1.scanNumber.CompareTo(v2.scanNumber);
                return r;
            }
            );

            if (sSumFile.Length > 0)
            {
                StreamWriter f = new StreamWriter(sSumFile);
                //Preamble
                f.Write(sID + "\nHarklor input: " + sHKFile + "\nMS/MS input file: " + sDataFile + "\nMS/MS spectra with new precursors: " + outFile + "\nRemaining spectra: " + outFile2 + "\n");
                //Heading line
                f.Write("MonoisotopicMass\tz\tCharge\tStartRT\tEndRT\tApexRT\tApexAbundance\tTotalAbundance\tAveragine\tMS2Scans\n");
                for (i = 0; i < vMatch.Count; i++)
                {
                    f.Write(p[vMatch[i].index].monoMass + "\t");
                    f.Write(p[vMatch[i].index].charge + "\t");
                    f.Write("[M+" + p[vMatch[i].index].charge + "H]" + "\t");
                    f.Write(p[vMatch[i].index].firstRTime + "\t");
                    f.Write(p[vMatch[i].index].lastRTime + "\t");
                    f.Write(p[vMatch[i].index].rTime + "\t");
                    f.Write(p[vMatch[i].index].intensity + "\t");
                    f.Write(p[vMatch[i].index].sumIntensity + "\t");
                    f.Write((string.IsNullOrEmpty(p[vMatch[i].index].averagineHK) ? averagine(p[vMatch[i].index].monoMass) : p[vMatch[i].index].averagineHK) + "\t");
                    f.Write(vMatch[i].scanNumber);
                    while (i < (vMatch.Count - 1) && vMatch[i + 1].index == vMatch[i].index)
                    {
                        i++;
                        f.Write(";" + vMatch[i].scanNumber);
                    }
                    f.Write("\n");
                }
                f.Close();
            }

            //export files
            msd.Run.SpectrumList = slNeg;
            MSDataFile.Write(msd, outFile2, wcNeg);
            msd.Run.SpectrumList = slPos;
            MSDataFile.Write(msd, outFile, wcPos);
            Console.WriteLine("Done export");
        }

        // Build the writer config from the output filename's extension. cpp BullseyeSharp also
        // sets compression = Zlib + indexed + gzipped flags here; the sharp WriteConfig exposes
        // these via EncoderConfig + Indexed + Gzip. We only set them on formats that honor them.
        static WriteConfig getFileFormat(string s)
        {
            WriteConfig wc = new WriteConfig
            {
                Format = UnrecognizedFormat,
                Indexed = true,
                Gzip = false,
            };
            string[] tokens = s.Split('.');
            for (int i = tokens.Length - 1; i > 0; i--)
            {
                string st = tokens[i].ToLower();
                switch (st)
                {
                    case "ms2":
                        wc.Format = WriteFormat.Ms2;
                        return wc;
                    case "cms2":
                        wc.Format = WriteFormat.Cms2;
                        return wc;
                    case "mgf":
                        wc.Format = WriteFormat.Mgf;
                        return wc;
                    case "mzxml":
                        wc.Format = WriteFormat.MzXml;
                        return wc;
                    case "mzml":
                        wc.Format = WriteFormat.Mzml;
                        return wc;
                    case "gz":
                        wc.Gzip = true;
                        continue;
                    default:
                        return wc;
                }
            }
            return wc;
        }

        static sScanInfo processScanHeader(ref Spectrum s)
        {
            sScanInfo si = new sScanInfo();
            int pIndex = 0;

            //find scan number...currently best for Thermo data.
            string[] tokens = s.Id.Split(' ', '\n', '\r');
            foreach (string st in tokens)
            {
                if (st.Contains("scan="))
                {
                    si.scanNumber = Convert.ToInt32(st.Substring(5));
                    break;
                }
            }

            // Compare by Cvid (enum) instead of comparing CVParam.Name strings — same semantics,
            // cheaper, and decoupled from CV term display-name changes.
            foreach (CVParam cv in s.Params.CVParams)
            {
                if (cv.Cvid == CVID.MS_ms_level) si.msLevel = cv.ValueAs<int>();
            }

            // Original cpp loops userParams here but does nothing with them; omit.

            foreach (Scan sc in s.ScanList.Scans)
            {
                foreach (CVParam cv in sc.CVParams)
                {
                    if (cv.Cvid == CVID.MS_scan_start_time)
                    {
                        // Per pwiz CV: scan start time may be in seconds (UO_second / MS_second_OBSOLETE)
                        // or minutes (UO_minute / MS_minute_OBSOLETE). The original (cpp) BullseyeSharp
                        // only checked the units-name string "minute". Sharp checks the units CVID
                        // directly — same intent, more robust.
                        double v = cv.ValueAs<double>();
                        if (cv.Units == CVID.UO_minute || cv.Units == CVID.MS_minute_OBSOLETE)
                            si.rTime = (float)v;
                        else
                            si.rTime = (float)(v / 60.0); //assuming if not minutes, then seconds.
                    }
                }
            }

            foreach (Precursor pr in s.Precursors)
            {
                foreach (CVParam cv in pr.IsolationWindow.CVParams)
                {
                    if (cv.Cvid == CVID.MS_isolation_window_target_m_z) si.mz = cv.ValueAs<double>();
                }
                foreach (SelectedIon sel in pr.SelectedIons)
                {
                    foreach (CVParam cv in sel.CVParams)
                    {
                        if (cv.Cvid == CVID.MS_selected_ion_m_z) si.precursor[pIndex].monoMz = cv.ValueAs<double>();
                        if (cv.Cvid == CVID.MS_charge_state) si.precursor[pIndex].charge = cv.ValueAs<int>();
                    }
                    pIndex++;
                }
            }
            return si;
        }

        static void usage()
        {
            Console.WriteLine("Usage: bullseyeSharp [flags] <HK file> <Data file> <Pos file> <Neg file>");
            Console.WriteLine("\n  HK files are Hardklör generated results files.");
            Console.WriteLine("  http://proteome.gs.washington.edu/software/hardklor");
            Console.WriteLine("\n  Data files contain the MS/MS data to be used.");
            Console.WriteLine("  Acceptable formats:");
            Console.WriteLine("    Any ProteoWizard supported MS format (e.g. mzML, ms2, mgf, and vendor formats)");
            Console.WriteLine("  http://proteome.gs.washington.edu/software/makems2/MakeMS2.zip");
            Console.WriteLine("\n  Pos and Neg files are output files for matches and non-matches, respectively.");
            Console.WriteLine("  Acceptable formats (specify with file extension):");
            Console.WriteLine("    .ms2, .cms2, .mgf, .mzXML, .mzML");
            Console.WriteLine("\nExample: bullseyeSharp -p 5 Peptides.hk RawData.RAW Matches.ms2 NoMatch.ms2");
            Console.WriteLine("\nFlags:");
            Console.WriteLine("  -c <num>  Ignore peptides that persist for this length in time.\n" +
                 "            These peptides are considered contaminants.\n" +
                 "            The unit of time is whatever unit is used in your data file.\n" +
                 "            Default value: 2\n");
            Console.WriteLine("  -e        Use exact match to precursor ion. Rather than use wide\n" +
                 "            precursor boundaries, this flag forces Bullseye to match\n" +
                 "            precursors to the base isotope peak identified in Hardklor.\n" +
                 "            The tolerance is set with the -p flag.\n");
            Console.WriteLine("  -g <num>  Gap size tolerance when checking for peptides across consecutive\n" +
                 "            scans.\n" +
                 "            Default value: 1\n");
            Console.WriteLine("  -m <num>  Only consider peptides below this maximum mass in daltons.\n" +
                 "            Default value: 8000\n");
            Console.WriteLine("  -n <num>  Only consider peptides above this minimum mass in daltons.\n" +
                 "            Default value: 600\n");
            Console.WriteLine("  -o <file> Output tab-delimited text summary of Peptide to MS2 matches.\n");
            Console.WriteLine("  -p <num>  Sets the tolerance (+/- ppm) for exact match searches.\n" +
                 "            Default value: 10\n");
            Console.WriteLine("  -r <num>  Sets the tolerance (+/- ppm) for finding persistent peptides.\n" +
                 "            Default value: 5\n");
            Console.WriteLine("  -s <num>  Number of consecutive scans over which a peptide must be\n" +
                 "            observed to be considered real. Gaps in persistence are allowed\n" +
                 "            when setting the -g flag.\n" +
                 "            Default value: 3\n");
            Console.WriteLine("  -t <num>  Sets the tolerance (+/- minutes) around the retention\n" +
                 "            time over which a peptide can be matched to the MS/MS\n" +
                 "            spectrum.\n" +
                 "            Default value: 0.5\n");
            Console.WriteLine("\nPlease read the README.txt file for more information on Bullseye.");
        }
    }
}
