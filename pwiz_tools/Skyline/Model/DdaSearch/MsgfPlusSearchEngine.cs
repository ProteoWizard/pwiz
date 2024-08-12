/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Util;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.BiblioSpec;
using pwiz.Skyline.Model.AuditLog;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class MsgfPlusSearchEngine : AbstractDdaSearchEngine, IProgressMonitor
    {
        /*
         *  -s SpectrumFile (*.mzML, *.mzXML, *.mgf, *.ms2, *.pkl or *_dta.txt)
         -d DatabaseFile (*.fasta or *.fa or *.faa)
         [-o OutputFile (*.mzid)] (Default: [SpectrumFileName].mzid)
         [-t PrecursorMassTolerance] (e.g. 2.5Da, 20ppm or 0.5Da,2.5Da, Default: 20ppm)
            Use a comma to set asymmetric values. E.g. "-t 0.5Da,2.5Da" will set 0.5Da to the left (ObsMass < TheoMass) and 2.5Da to the right (ObsMass > TheoMass)
         [-ti IsotopeErrorRange] (Range of allowed isotope peak errors, Default:0,1)
            Takes into account the error introduced by choosing a non-monoisotopic peak for fragmentation.
            The combination of -t and -ti determines the precursor mass tolerance.
            E.g. "-t 20ppm -ti -1,2" tests abs(ObservedPepMass - TheoreticalPepMass - n * 1.00335Da) < 20ppm for n = -1, 0, 1, 2.
         [-thread NumThreads] (Number of concurrent threads to be executed, Default: Number of available cores)
         [-tasks NumTasks] (Override the number of tasks to use on the threads, Default: (internally calculated based on inputs))
            More tasks than threads will reduce the memory requirements of the search, but will be slower (how much depends on the inputs).
            1 <= tasks <= numThreads: will create one task per thread, which is the original behavior.
            tasks = 0: use default calculation - minimum of: (threads*3) and (numSpectra/250).
            tasks < 0: multiply number of threads by abs(tasks) to determine number of tasks (i.e., -2 means "2 * numThreads" tasks).
            One task per thread will use the most memory, but will usually finish the fastest.
            2-3 tasks per thread will use comparably less memory, but may cause the search to take 1.5 to 2 times as long.
         [-verbose 0/1] (0: Report total progress only (Default), 1: Report total and per-thread progress/status)
         [-tda 0/1] (0: Don't search decoy database (Default), 1: Search decoy database)
         [-m FragmentMethodID] (0: As written in the spectrum or CID if no info (Default), 1: CID, 2: ETD, 3: HCD, 4: UVPD)
         [-inst MS2DetectorID] (0: Low-res LCQ/LTQ (Default), 1: Orbitrap/FTICR/Lumos, 2: TOF, 3: Q-Exactive)
         [-e EnzymeID] (0: unspecific cleavage, 1: Trypsin (Default), 2: Chymotrypsin, 3: Lys-C, 4: Lys-N, 5: glutamyl endopeptidase, 6: Arg-C, 7: Asp-N, 8: alphaLP, 9: no cleavage)
         [-protocol ProtocolID] (0: Automatic (Default), 1: Phosphorylation, 2: iTRAQ, 3: iTRAQPhospho, 4: TMT, 5: Standard)
         [-ntt 0/1/2] (Number of Tolerable Termini, Default: 2)
            E.g. For trypsin, 0: non-tryptic, 1: semi-tryptic, 2: fully-tryptic peptides only.
         [-mod ModificationFileName] (Modification file, Default: standard amino acids with fixed C+57; only if -mod is not specified)
         [-minLength MinPepLength] (Minimum peptide length to consider, Default: 6)
         [-maxLength MaxPepLength] (Maximum peptide length to consider, Default: 40)
         [-minCharge MinCharge] (Minimum precursor charge to consider if charges are not specified in the spectrum file, Default: 2)
         [-maxCharge MaxCharge] (Maximum precursor charge to consider if charges are not specified in the spectrum file, Default: 3)
         [-n NumMatchesPerSpec] (Number of matches per spectrum to be reported, Default: 1)
         [-addFeatures 0/1] (0: Output basic scores only (Default), 1: Output additional features)
         [-ccm ChargeCarrierMass] (Mass of charge carrier, Default: mass of proton (1.00727649))
         [-maxMissedCleavages Count] (Exclude peptides with more than this number of missed cleavages from the search, Default: -1 (no limit))
         */
        private static readonly string[] FRAGMENTATION_METHODS = new[]
        {
            @"As written in spectrum or CID if no info",
            @"CID",
            @"ETD",
            @"HCD",
            @"UVPD"
        };

        private static readonly string[] INSTRUMENT_TYPES = new[]
        {
            @"Low-res LCQ/LTQ",
            @"Orbitrap/FTICR/Lumos",
            @"TOF",
            @"Q-Exactive"
        };

        private static readonly string[] ENZYMES = new[]
        {
            @"Unspecific cleavage",
            @"Trypsin",
            @"Chymotrypsin",
            @"Lys-C",
            @"Lys-N",
            @"Glutamyl endopeptidase",
            @"Arg-C",
            @"Asp-N",
            @"alphaLP",
            @"No cleavage"
        };

        static string MSGFPLUS_FILENAME = @"MSGFPlus_v20210906";
        static Uri MSGFPLUS_URL = new Uri($@"https://github.com/MSGFPlus/msgfplus/releases/download/v2021.09.06/{MSGFPLUS_FILENAME}.zip");
        public static string MsgfPlusDirectory => Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), MSGFPLUS_FILENAME);
        public static string MsgfPlusBinary => Path.Combine(MsgfPlusDirectory, @"MSGFPlus.jar");

        public static FileDownloadInfo[] FilesToDownload => JavaDownloadInfo.FilesToDownload.Concat(new []
            { new FileDownloadInfo { Filename = MSGFPLUS_FILENAME, DownloadUrl = MSGFPLUS_URL, InstallPath = MsgfPlusDirectory, OverwriteExisting = true, Unzip = true}}).ToArray();

        private MzTolerance precursorMzTolerance;
        private Tuple<double, double> isotopeErrorRange = new Tuple<double, double>(0, 0);
        private int fragmentationMethod;
        private int instrumentType;
        private int enzyme;
        //private int protocol;
        private int ntt, maxMissedCleavages;
        //private int minPeptideLength, maxPeptideLength, minCharge, maxCharge;
        //private double chargeCarrierMass;
        private int maxVariableMods = 2;
        private StringBuilder modsText;
        private const string _cutoffScoreName = ScoreType.PERCOLATOR_QVALUE;
        private CancellationTokenSource _cancelToken;
        private IProgressStatus _progressStatus;
        private bool _success;

        public override string[] FragmentIons => FRAGMENTATION_METHODS;
        public override string[] Ms2Analyzers => INSTRUMENT_TYPES;
        public override string EngineName => @"MS-GF+";
        public override string CutoffScoreName => _cutoffScoreName;
        public override string CutoffScoreLabel => PropertyNames.CutoffScore_PERCOLATOR_QVALUE;
        public override double DefaultCutoffScore { get; } = new ScoreType(_cutoffScoreName, ScoreType.PROBABILITY_INCORRECT).DefaultValue;
        public override Bitmap SearchEngineLogo => null;
        public override string SearchEngineBlurb => string.Empty;
        public override event NotificationEventHandler SearchProgressChanged;

        public override bool Run(CancellationTokenSource cancelToken, IProgressStatus status)
        {
            _cancelToken = cancelToken;
            _progressStatus = status;
            _success = true;

            foreach (var spectrumFilename in SpectrumFileNames)
            {
                string modsFile = PathEx.GetTempFileNameWithExtension(@"mods");
                File.WriteAllText(modsFile, modsText.ToString());

                try
                {
                    long javaMaxHeapMB = Math.Min(16 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;

                    var pr = new ProcessRunner();
                    var psi = new ProcessStartInfo(JavaDownloadInfo.JavaBinary,
                        $@"-Xmx{javaMaxHeapMB}M -XX:-UsePerfData -jar """ + MsgfPlusBinary + @""" -tasks -2 " +
                        $@"-s ""{spectrumFilename}"" -d ""{FastaFileNames[0]}"" -tda 1 " +
                        $@"-t {precursorMzTolerance.Value}{precursorMzTolerance.UnitName} -ti {isotopeErrorRange.Item1},{isotopeErrorRange.Item2} " +
                        $@"-m {fragmentationMethod} -inst {instrumentType} -e {enzyme} -ntt {ntt} -maxMissedCleavages {maxMissedCleavages} " +
                        $@"-mod ""{modsFile}""")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = false
                    };

                    pr.Run(psi, string.Empty, this, ref _progressStatus, ProcessPriorityClass.BelowNormal, true);
                    _progressStatus = _progressStatus.NextSegment();
                }
                catch (Exception ex)
                {
                    _progressStatus = _progressStatus.ChangeErrorException(ex).ChangeMessage(string.Format(DdaSearchResources.DdaSearch_Search_failed__0, ex.Message));
                    _success = false;
                }
                finally
                {
                    FileEx.SafeDelete(modsFile);
                }

                if (IsCanceled && !_progressStatus.IsCanceled)
                {
                    _progressStatus = _progressStatus.Cancel().ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_canceled);
                    _success = false;
                }

                if (!_success)
                {
                    _cancelToken.Cancel();
                    break;
                }
            }

            if (_success)
                _progressStatus = _progressStatus.Complete().ChangeMessage(Resources.DDASearchControl_SearchProgress_Search_done);
            UpdateProgress(_progressStatus);

            return _success;
        }

        public override void SetModifications(IEnumerable<StaticMod> fixedAndVariableModifs, int maxVariableMods_)
        {
            /*  # Mass or CompositionStr, Residues, ModType, Position, Name (all the five fields are required).
                # CompositionStr (C[Num]H[Num]N[Num]O[Num]S[Num]P[Num]Br[Num]Cl[Num]Fe[Num])
                #   - C (Carbon), H (Hydrogen), N (Nitrogen), O (Oxygen), S (Sulfur), P (Phosphorus), Br (Bromine), Cl (Chlorine), Fe (Iron), and Se (Selenium) are allowed.
                #   - Negative numbers are allowed.
                #   - E.g. C2H2O1 (valid), H2C1O1 (invalid) 
                # Mass can be used instead of CompositionStr. It is important to specify accurate masses (integer masses are insufficient).
                #   - E.g. 15.994915 
                # Residues: affected amino acids (must be upper letters)
                #   - Must be upper letters or *
                #   - Use * if this modification is applicable to any residue. 
                #   - * should not be "anywhere" modification (e.g. "15.994915, *, opt, any, Oxidation" is not allowed.) 
                #   - E.g. NQ, *
                # ModType: "fix" for fixed modifications, "opt" for variable modifications, "custom" for custom amino acids (case insensitive)
                # Position: position in the peptide where the modification can be attached. 
                #   - One of the following five values should be used:
                #   - any (anywhere), N-term (peptide N-term), C-term (peptide C-term), Prot-N-term (protein N-term), Prot-C-term (protein C-term) 
                #   - Case insensitive
                #   - "-" can be omitted
                #   - E.g. any, Any, Prot-n-Term, ProtNTerm => all valid
                # Name: name of the modification (Unimod PSI-MS name)
                #   - For proper mzIdentML output, this name should be the same as the Unimod PSI-MS name
                #   - E.g. Phospho, Acetyl
                #   - Visit http://www.unimod.org to get PSI-MS names.
             */
            modsText = new StringBuilder();
            using (var modsFileStream = new StringWriter(modsText))
            {
                int modCounter = 0;
                modsFileStream.WriteLine($@"NumMods={maxVariableMods}");
                modsFileStream.WriteLine(@"C3H5NO,   U, custom, U, Selenocysteine   # Custom amino acids can only have C, H, N, O, and S");
                modsFileStream.WriteLine(@"79.9166,  U, fix, any, Se80              # Use a static mod to add Se");

                foreach (var mod in fixedAndVariableModifs)
                {
                    // these custom AAs not supported 
                    if (mod.AAs?.IndexOfAny(@"BJOZ".ToCharArray()) > 0)
                        continue;

                    // can't use mod with no formula or mass; CONSIDER throwing exception
                    if (mod.LabelAtoms == LabelAtoms.None && ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule) && mod.MonoisotopicMass == null ||
                        mod.LabelAtoms != LabelAtoms.None && mod.AAs.IsNullOrEmpty())
                        continue;

                    Action<string, string> addMod = (composition, residues) =>
                    {
                        string modType = mod.IsVariable ? @"opt" : @"fix";
                        string position = @"any";
                        switch (mod.Terminus)
                        {
                            case ModTerminus.N:
                                position = @"N-term";
                                break;
                            case ModTerminus.C:
                                position = @"C-term";
                                break;
                        }


                        string name = mod.ShortName;
                        if (name.IsNullOrEmpty())
                            name = $@"Mod{++modCounter}";

                        modsFileStream.WriteLine($@"{composition}, {residues}, {modType}, {position}, {name}");
                    };

                    if (mod.LabelAtoms != LabelAtoms.None)
                    {
                        foreach (var aa in mod.AminoAcids)
                        {
                            double mass = SequenceMassCalc.FormulaMass(BioMassCalc.MONOISOTOPIC, SequenceMassCalc.GetHeavyFormula(aa, mod.LabelAtoms), SequenceMassCalc.MassPrecision).Value;
                            addMod(mass.ToString(CultureInfo.InvariantCulture), aa.ToString());
                        }
                    }
                    else
                    {
                        // ReSharper disable once PossibleNullReferenceException
                        var modString = ParsedMolecule.IsNullOrEmpty(mod.ParsedMolecule) ? null : mod.ParsedMolecule.ToString();
                        addMod(modString ?? mod.MonoisotopicMass.ToString(), mod.AAs ?? @"*");
                    }
                }
            }
        }

        public override void SetEnzyme(Enzyme enz, int mmc)
        {
            enzyme = ENZYMES.IndexOf(o => string.Equals(o, enz.Name, StringComparison.InvariantCultureIgnoreCase));
            ntt = enz.IsSemiCleaving ? 1 : 2;
            maxMissedCleavages = mmc;
        }

        public override void SetFragmentIonMassTolerance(MzTolerance mzTolerance)
        {
            // not used by MS-GF+
        }

        public override void SetFragmentIons(string ions)
        {
            fragmentationMethod = FRAGMENTATION_METHODS.IndexOf(m => m == ions);
        }

        public override void SetMs2Analyzer(string ms2Analyzer)
        {
            instrumentType = INSTRUMENT_TYPES.IndexOf(m => m == ms2Analyzer);
        }

        public override void SetPrecursorMassTolerance(MzTolerance mzTolerance)
        {
            precursorMzTolerance = mzTolerance;
        }

        public override void SetCutoffScore(double cutoffScore)
        {
            // Do nothing. MSGF+ does not run percolator or take a cutoff value
        }

        private string[] SupportedExtensions = { @".mzml", @".mzxml", @".mgf", @".ms2" };

        public override bool GetSearchFileNeedsConversion(MsDataFileUri searchFilepath, out AbstractDdaConverter.MsdataFileFormat requiredFormat)
        {
            requiredFormat = AbstractDdaConverter.MsdataFileFormat.mzML;
            if (!SupportedExtensions.Contains(e => e == searchFilepath.GetExtension().ToLowerInvariant()))
                return true;
            return false;
        }

        public bool IsCanceled => _cancelToken.IsCancellationRequested;
        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            SearchProgressChanged?.Invoke(this, status);
            return _cancelToken.IsCancellationRequested ? UpdateProgressResponse.cancel : UpdateProgressResponse.normal;
        }

        public bool HasUI => false;

        public override void Dispose()
        {
        }
    }
}
