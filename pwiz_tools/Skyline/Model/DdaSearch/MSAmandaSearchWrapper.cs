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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MSAmanda.Core;
using MSAmanda.Utils;
using MSAmanda.InOutput;
using MSAmanda.InOutput.Output;
using MSAmandaSettings = MSAmanda.InOutput.Settings;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using MSAmandaEnzyme = MSAmanda.Utils.Enzyme;
using OperationCanceledException = System.OperationCanceledException;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DdaSearch
{
    public class MSAmandaSearchWrapper : AbstractDdaSearchEngine
    {
        internal MSAmandaSettings Settings { get; }
        private MSHelper helper;
        private SettingsFile AvailableSettings;
        private OutputMzid mzID;
        private MSAmandaSearch SearchEngine;
        private OutputParameters _outputParameters;
        private MSAmandaSpectrumParser amandaInputParser;
        private IProgressStatus _progressStatus;
        private bool _success;

        public int CurrentFile { get; private set; }
        public int TotalFiles => SpectrumFileNames.Length;

        public override event NotificationEventHandler SearchProgressChanged;

        private const string UNIMOD_FILENAME = "Unimod.xml";
        private const string ENZYME_FILENAME = "enzymes.xml";
        private const string INSTRUMENTS_FILENAME = "Instruments.xml";
        private const string AmandaMap = @"AmandaMap";

        private const string MAX_LOADED_PROTEINS_AT_ONCE = "MaxLoadedProteinsAtOnce";
        private const string MAX_LOADED_SPECTRA_AT_ONCE = "MaxLoadedSpectraAtOnce";
        private const string CONSIDERED_CHARGES = "ConsideredCharges";

        private readonly TemporaryDirectory _baseDir = MSAmandaTempDir;

        public static TemporaryDirectory MSAmandaTempDir = new TemporaryDirectory(tempPrefix: @"~SK_MSAmanda_");

        public MSAmandaSearchWrapper()
        {
            Settings = new MSAmandaSettings();
            helper = new MSHelper();
            helper.InitLogWriter(_baseDir.DirPath);
            helper.SearchProgressChanged += Helper_SearchProgressChanged;
            var folderForMappings = Path.Combine(_baseDir.DirPath, AmandaMap);
            // creates dir if not existing
            Directory.CreateDirectory(folderForMappings);
            mzID = new OutputMzid(folderForMappings);
            AvailableSettings = new SettingsFile(helper, Settings, mzID);
            AvailableSettings.AllEnzymes = new List<MSAmandaEnzyme>();
            AvailableSettings.AllModifications = new List<Modification>();

            using (var d = new CurrentDirectorySetter(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))
            {
                if (!AvailableSettings.ParseEnzymeFile(ENZYME_FILENAME, "", AvailableSettings.AllEnzymes))
                    throw new Exception(string.Format(Resources.DdaSearch_MSAmandaSearchWrapper_enzymes_file__0__not_found, ENZYME_FILENAME));
                if (!AvailableSettings.ParseUnimodFile(UNIMOD_FILENAME, AvailableSettings.AllModifications))
                    throw new Exception(string.Format(Resources.DdaSearch_MSAmandaSearchWrapper_unimod_file__0__not_found, UNIMOD_FILENAME));
                if (!AvailableSettings.ParseOboFiles())
                    throw new Exception(Resources.DdaSearch_MSAmandaSearchWrapper_Obo_files_not_found);
                if (!AvailableSettings.ReadInstrumentsFile(INSTRUMENTS_FILENAME))
                    throw new Exception(string.Format(Resources.DdaSearch_MSAmandaSearchWrapper_Instruments_file_not_found, INSTRUMENTS_FILENAME));
            }

            AdditionalSettings = new Dictionary<string, Setting>
            {
                {MAX_LOADED_PROTEINS_AT_ONCE, new Setting(MAX_LOADED_PROTEINS_AT_ONCE, 100000, 10)},
                {MAX_LOADED_SPECTRA_AT_ONCE, new Setting(MAX_LOADED_SPECTRA_AT_ONCE, 10000, 100)},
                {CONSIDERED_CHARGES, new Setting(CONSIDERED_CHARGES, @"2,3")}
            };

            CurrentFile = 0;
        }

        public override void Dispose()
        {
            helper.Dispose();
            mzID.Dispose();
            amandaInputParser?.Dispose();
            _baseDir.Dispose();
            //AvailableSettings = new SettingsFile(null, Settings, mzID);
        }

        private void Helper_SearchProgressChanged(string message)
        {
            if (message.Contains(@"Identifying Peptides") || message.Contains(@"decoy peptide hits"))
                return;

            if (message.Contains(@"Search failed"))
            {
                SearchProgressChanged?.Invoke(this, _progressStatus.ChangeMessage(message));
                _success = false;
            }
            else if (amandaInputParser != null && amandaInputParser.TotalSpectra > 0 && TotalFiles > 0)
            {
                int percentProgress = amandaInputParser.CurrentSpectrum * 100 / amandaInputParser.TotalSpectra;
                SearchProgressChanged?.Invoke(this, _progressStatus.ChangeMessage(message).ChangePercentComplete(percentProgress));
            }
        }

        public override void SetEnzyme(DocSettings.Enzyme enzyme, int maxMissedCleavages)
        {
            MSAmandaEnzyme e = AvailableSettings.AllEnzymes.Find(enz => enz.Name.ToUpper() == enzyme.Name.ToUpper());
            if (e != null)
            {
                Settings.MyEnzyme = e;
                Settings.MissedCleavages = maxMissedCleavages;
            }
            else
            {
                MSAmandaEnzyme enz = new MSAmandaEnzyme()
                {
                    Name = enzyme.Name,
                    CleavageSites = enzyme.IsNTerm ? enzyme.CleavageN : enzyme.CleavageC,
                    CleavageInhibitors = enzyme.IsNTerm ? enzyme.RestrictN : enzyme.RestrictC,
                    Offset = enzyme.IsNTerm ? 0 : 1,
                    Specificity = enzyme.IsSemiCleaving
                        ? MSAmandaEnzyme.CLEAVAGE_SPECIFICITY.SEMI
                        : MSAmandaEnzyme.CLEAVAGE_SPECIFICITY.FULL
                };
                Settings.MyEnzyme = enz;
                Settings.MissedCleavages = maxMissedCleavages;
            }
        }

        public override string[] FragmentIons => Settings.ChemicalData.Instruments.Keys.ToArray();
        public override string[] Ms2Analyzers => new[] { @"Default" };
        public override string EngineName => @"MS Amanda";
        public override Bitmap SearchEngineLogo => Resources.MSAmandaLogo;

        public override void SetPrecursorMassTolerance(MzTolerance tol)
        {
            Settings.Ms1Tolerance = new Tolerance(tol.Value, (MassUnit) tol.Unit);
        }

        public override void SetFragmentIonMassTolerance(MzTolerance tol)
        {
            Settings.Ms2Tolerance = new Tolerance(tol.Value, (MassUnit)tol.Unit);
        }

        public override void SetFragmentIons(string ions)
        {
            if (Settings.ChemicalData.Instruments.ContainsKey(ions))
            {
                Settings.ChemicalData.CurrentInstrumentSetting = Settings.ChemicalData.Instruments[ions];
            }
        }

        public override void SetMs2Analyzer(string analyzer)
        {
            // MS2 analyzer is not relevant in MS Amanda
        }

        private List<FastaDBFile> GetFastaFileList()
        {
            List<FastaDBFile> files = new List<FastaDBFile>();
            foreach (string f in FastaFileNames)
            {
                AFastaFile file = new AFastaFile();
                file.FullPath = f;
                file.NeatName = Path.GetFileNameWithoutExtension(f);
                files.Add(new FastaDBFile() { fastaTarged = file});
            }
            return files;
        }

        private MSAmandaSearch InitializeEngine(CancellationTokenSource token, string spectrumFileName)
        {
            _outputParameters = new OutputParameters();
            _outputParameters.FastaFiles = FastaFileNames.ToList();
            _outputParameters.DBFile = FastaFileNames[0];
            //2 == mzid
            _outputParameters.SetOutputFileFormat(2);
            _outputParameters.IsPercolatorOutput = true;
            _outputParameters.SpectraFiles = new List<string>() { spectrumFileName};
            Settings.GenerateDecoyDb = true;
            Settings.ConsideredCharges.Clear();
            foreach(var chargeStr in AdditionalSettings[CONSIDERED_CHARGES].Value.ToString().Split(','))
                Settings.ConsideredCharges.Add(Convert.ToInt32(chargeStr));
            Settings.ChemicalData.UseMonoisotopicMass = true;
            Settings.ReportBothBestHitsForTD = false;
            Settings.CombineConsideredCharges = false;
            //Settings.WriteResultsTwice = true;
            //Settings.ForceTargetDecoyMode = false;
            //Console.WriteLine("\nReportBothBestHitsForTD CombineConsideredCharges WriteResultsTwice ForceTargetDecoyMode");
            //Console.WriteLine($@"{Settings.ReportBothBestHitsForTD}       {Settings.CombineConsideredCharges}        {Settings.WriteResultsTwice}       {Settings.ForceTargetDecoyMode}");
            mzID.Settings = Settings;
            SearchEngine = new MSAmandaSearch(helper, _baseDir.DirPath, _outputParameters, Settings, token);
            SearchEngine.InitializeOutputMZ(mzID);
            Settings.LoadedProteinsAtOnce = (int) AdditionalSettings[MAX_LOADED_PROTEINS_AT_ONCE].Value;
            Settings.LoadedSpectraAtOnce = (int) AdditionalSettings[MAX_LOADED_SPECTRA_AT_ONCE].Value;
            return SearchEngine;
        }
    
        public override bool Run(CancellationTokenSource tokenSource, IProgressStatus status)
        {
            _progressStatus = status;

            _success = true;
            try
            {
                using (var c = new CurrentCultureSetter(CultureInfo.InvariantCulture))
                using (var d = new CurrentDirectorySetter(_baseDir.DirPath))
                {
                    foreach (var rawFileName in SpectrumFileNames)
                    {
                        tokenSource.Token.ThrowIfCancellationRequested();
                        try
                        {
                            // CONSIDER: move this to base.Run()?
                            string outputFilepath = GetSearchResultFilepath(rawFileName);
                            if (File.Exists(outputFilepath))
                            {
                                // CONSIDER: read the file description to see what settings were used to generate the file;
                                // if the same settings were used, we can re-use the file, else regenerate
                                /*string lastLine = File.ReadLines(outputFilepath).Last();
                                if (lastLine == @"</MzIdentML>")
                                {
                                    helper.WriteMessage($"Re-using existing mzIdentML file for {rawFileName.GetSampleOrFileName()}", true);
                                    CurrentFile++;
                                    _progressStatus = _progressStatus.NextSegment();
                                    continue;
                                }
                                else*/
                                FileEx.SafeDelete(outputFilepath);
                            }

                            SearchEngine = InitializeEngine(tokenSource, rawFileName.GetSampleLocator());   // Assignment for ReSharper
                            amandaInputParser = new MSAmandaSpectrumParser(rawFileName.GetSampleLocator(), Settings.ConsideredCharges, true);
                            SearchEngine.SetInputParser(amandaInputParser);
                            SearchEngine.PerformSearch(_outputParameters.DBFile);
                            CurrentFile++;
                            _progressStatus = _progressStatus.NextSegment();
                        }
                        finally
                        {
                            SearchEngine?.Dispose();
                            amandaInputParser?.Dispose();
                            amandaInputParser = null;
                        }
                    }
                }
            }
            catch (AggregateException e)
            {
                if (e.InnerException is TaskCanceledException)
                {
                    helper.WriteMessage(Resources.DdaSearch_Search_is_canceled, true);
                }
                else
                    Program.ReportException(e);
                _success = false;
            }
            catch (OperationCanceledException)
            {
                helper.WriteMessage(Resources.DdaSearch_Search_is_canceled, true);
                _success = false;
            }
            catch (Exception ex)
            {
                helper.WriteMessage(string.Format(Resources.DdaSearch_Search_failed__0, ex.Message), true);
                _success = false;
            }
            finally
            {
                helper.Dispose();
            }

            if (tokenSource.IsCancellationRequested)
                _success = false;
            
            return _success;
        }

        public override void SetModifications(IEnumerable<StaticMod> modifications, int maxVariableMods_)
        {
            Settings.SelectedModifications.Clear();
            foreach (var item in modifications)
            {
                string name = item.Name.Split(' ')[0];
                var elemsFromUnimod = AvailableSettings.AllModifications.FindAll(m => m.Title == name);
                if (elemsFromUnimod.Count> 0)
                {
                    foreach (char aa in item.AminoAcids)
                    {
                        var elem = elemsFromUnimod.Find(m => m.AA == aa);
                        if (elem != null)
                        {
                            Modification modClone = new Modification(elem);
                            modClone.Fixed = !item.IsVariable && item.LabelAtoms == LabelAtoms.None;
                            if (item.Terminus == ModTerminus.C)
                                modClone.CTerminal = true;
                            else if (item.Terminus == ModTerminus.N)
                                modClone.NTerminal = true;
                            Settings.SelectedModifications.Add(modClone);
                        }
                        else
                        {
                            Settings.SelectedModifications.Add(GenerateNewModification(item, aa));
                        }
                    }
                }
                else
                {
                    Settings.SelectedModifications.AddRange(GenerateNewModificationsForEveryAA(item));
                }
            }

        }

        public override string GetSearchResultFilepath(MsDataFileUri searchFilepath)
        {
            return Path.ChangeExtension(searchFilepath.GetFilePath(), @".mzid.gz");
        }

        public override bool GetSearchFileNeedsConversion(MsDataFileUri searchFilepath, out AbstractDdaConverter.MsdataFileFormat requiredFormat)
        {
            requiredFormat = AbstractDdaConverter.MsdataFileFormat.mzML;
            return false;
        }

        private List<Modification> GenerateNewModificationsForEveryAA(StaticMod mod)
        {
            List<Modification> mods = new List<Modification>();
            if (mod.AAs != null)
                foreach (var a in mod.AAs)
                mods.Add(GenerateNewModification(mod, a));
            else
                mods.Add(GenerateNewModification(mod, ' '));
            return mods;
        }

        private Modification GenerateNewModification(StaticMod mod, char a)
        {
            return new Modification(mod.ShortName ?? mod.Name, mod.Name, mod.MonoisotopicMass ?? 0.0,
                mod.AverageMass ?? 0.0, a, !mod.IsVariable, mod.Losses?.Select(l => l.MonoisotopicMass).ToArray() ?? new double[0],
                mod.Terminus.HasValue && mod.Terminus.Value == ModTerminus.N,
                mod.Terminus.HasValue && mod.Terminus.Value == ModTerminus.C,
                mod.UnimodId ?? 0, false);
        }
  }
}
