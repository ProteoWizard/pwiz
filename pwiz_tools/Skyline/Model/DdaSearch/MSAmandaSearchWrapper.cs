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

        public int CurrentFile { get; private set; }
        public int TotalFiles => SpectrumFileNames.Length;

        public override event NotificationEventHandler SearchProgressChanged;

        private const string UNIMOD_FILENAME = "Unimod.xml";
        private const string ENZYME_FILENAME = "enzymes.xml";
        private const string INSTRUMENTS_FILENAME = "Instruments.xml";
        private const string AmandaMap = @"AmandaMap";

        private const string MAX_LOADED_PROTEINS_AT_ONCE = "MaxLoadedProteinsAtOnce";
        private const string MAX_LOADED_SPECTRA_AT_ONCE = "MaxLoadedSpectraAtOnce";

        private readonly TemporaryDirectory _baseDir = new TemporaryDirectory(tempPrefix: @"~SK_MSAmanda/");

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
                {MAX_LOADED_PROTEINS_AT_ONCE, new Setting(MAX_LOADED_PROTEINS_AT_ONCE, 100000, 10, int.MaxValue)},
                {MAX_LOADED_SPECTRA_AT_ONCE, new Setting(MAX_LOADED_SPECTRA_AT_ONCE, 10000, 100, int.MaxValue)}
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
            int percentProgress = 0;
            if (amandaInputParser != null && amandaInputParser.TotalSpectra > 0 && TotalFiles > 0)
            {
                percentProgress = CurrentFile * 100 / TotalFiles;
                percentProgress += amandaInputParser.CurrentSpectrum * 100 / amandaInputParser.TotalSpectra / TotalFiles;
            }
            SearchProgressChanged?.Invoke(this, new ProgressStatus(message).ChangePercentComplete(percentProgress));
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

        public override string[] FragmentIons
        {
            get { return Settings.ChemicalData.Instruments.Keys.ToArray(); }
        }
        public override string EngineName { get { return @"MS Amanda"; } }
        public override Bitmap SearchEngineLogo
        {
            get { return Resources.MSAmandaLogo; }
        }

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

        private void InitializeEngine(CancellationTokenSource token, string spectrumFileName)
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
            Settings.ConsideredCharges.Add(2);
            Settings.ConsideredCharges.Add(3);
            Settings.ChemicalData.UseMonoisotopicMass = true;
            Settings.ReportBothBestHitsForTD = false;
            mzID.Settings = Settings;
            SearchEngine = new MSAmandaSearch(helper, _baseDir.DirPath, _outputParameters, Settings, token);
            SearchEngine.InitializeOutputMZ(mzID);
            Settings.LoadedProteinsAtOnce = (int) AdditionalSettings[MAX_LOADED_PROTEINS_AT_ONCE].Value;
            Settings.LoadedSpectraAtOnce = (int) AdditionalSettings[MAX_LOADED_SPECTRA_AT_ONCE].Value;
        }
    
        public override bool Run(CancellationTokenSource tokenSource)
        {
            bool success = true;
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
                                    continue;
                                }
                                else*/
                                FileEx.SafeDelete(outputFilepath);
                            }

                            InitializeEngine(tokenSource, rawFileName.GetSampleLocator());
                            amandaInputParser = new MSAmandaSpectrumParser(rawFileName.GetSampleLocator(), Settings.ConsideredCharges, true);
                            SearchEngine.SetInputParser(amandaInputParser);
                            SearchEngine.PerformSearch(_outputParameters.DBFile);
                            CurrentFile++;
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
                success = false;
            }
            catch (OperationCanceledException)
            {
                helper.WriteMessage(Resources.DdaSearch_Search_is_canceled, true);
                success = false;
            }
            catch (Exception ex)
            {
                helper.WriteMessage(string.Format(Resources.DdaSearch_Search_failed__0, ex.Message), true);
                success = false;
            }
            finally
            {
                helper.Dispose();
            }

            if (tokenSource.IsCancellationRequested)
                success = false;
            
            return success;
        }

        public override void SetModifications(IEnumerable<StaticMod> modifications, int maxVariableMods)
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
