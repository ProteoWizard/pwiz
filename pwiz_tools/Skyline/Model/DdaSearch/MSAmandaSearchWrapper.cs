using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MSAmanda.Core;
using MSAmanda.Utils;
using MSAmanda.InOutput;
using MSAmanda.InOutput.Output;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using MSAmandaEnzyme = MSAmanda.Utils.Enzyme;
//using AmandaSettings = MSAmanda.InOutput.Settings;
using OperationCanceledException = System.OperationCanceledException;
using Thread = System.Threading.Thread;

namespace pwiz.Skyline.Model.DdaSearch
{
    [Localizable(true)]
    public class MSAmandaSearchWrapper : AbstractDdaSearchEngine
    {
        //public string[] FastaFiles { get; set; } = new string[0];
        //public string[] SpectraFiles { get; set; } =  new string[0];
        internal Settings Settings { get; private set; } = new Settings();
        private static MSHelper helper = new MSHelper();
        private SettingsFile AvailableSettings;
        private OutputMzid mzID;
        private MSAmandaSearch SearchEngine;
        private OutputParameters _outputParameters;
        private MSAmandaSpectrumParser amandaInputParser;

        public override event NotificationEventHandler SearchProgressChanged;

        private const string UNIMOD_FILENAME = "Unimod.xml";
        private const string ENZYME_FILENAME = "enzymes.xml";
        private const string INSTRUMENTS_FILENAME = "Instruments.xml";
        #region todo add as additional settings
        private const string AMANDA_DB_DIRECTORY = "C:\\ProgramData\\MSAmanda2.0\\DB";
        private const string AMANDA_SCRATCH_DIRECTORY = "C:\\ProgramData\\MSAmanda2.0\\Scratch";
        private const double CORE_USE_PERCENTAGE = 100;
        private const int MAX_NUMBER_PROTEINS = 10000;
        private const int MAX_NUMBER_SPECTRA = 1000;
        private const string AmandaResults = "AmandaResults";
        private const string AmandaDB = "AmandaDB";
        private const string AmandaMap = "AmandaMap";
        private readonly string _baseDir = "C:\\ProgramData\\MSAmanda2.0";
        #endregion

        public MSAmandaSearchWrapper()
        {
            if (!helper.IsInitialized())
            {
                helper.InitLogWriter(_baseDir);
                
            }
            helper.SearchProgressChanged += Helper_SearchProgressChanged;
            var folderForMappings = Path.Combine(_baseDir, AmandaMap);
            // creates dir if not existing
            Directory.CreateDirectory(folderForMappings);
            mzID = new OutputMzid(folderForMappings);
            AvailableSettings = new SettingsFile(helper, Settings, mzID);
            AvailableSettings.AllEnzymes = new List<MSAmandaEnzyme>();
            AvailableSettings.AllModifications = new List<Modification>();

            using (var d = new CurrentDirectorySetter(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))
            {
                if (!AvailableSettings.ParseEnzymeFile(ENZYME_FILENAME, "", AvailableSettings.AllEnzymes))
                    throw new Exception($"enzymes file '{ENZYME_FILENAME}' not found");
                if (!AvailableSettings.ParseUnimodFile(UNIMOD_FILENAME, AvailableSettings.AllModifications))
                    throw new Exception($"unimod file '{UNIMOD_FILENAME}' not found");
                if (!AvailableSettings.ParseOboFiles())
                    throw new Exception("obo files (psi-ms.obo and unimod.obo) not found");
                if (!AvailableSettings.ReadInstrumentsFile(INSTRUMENTS_FILENAME))
                    throw new Exception($"instruments file '{INSTRUMENTS_FILENAME}' not found");
            }
        }

        private void Helper_SearchProgressChanged(string message)
        {
            SearchProgressChanged?.Invoke(this, new MessageEventArgs(){Message = message});
        }

        public override void SetEnzyme(pwiz.Skyline.Model.DocSettings.Enzyme enzyme, int maxMissedCleavages)
        {
            MSAmandaEnzyme e = AvailableSettings.AllEnzymes.Find(enz => enz.Name.ToUpper() == enzyme.Name.ToUpper());
            if (e != null)
            {
                Settings.MyEnzyme = e;
                Settings.MissedCleavages = maxMissedCleavages;
            }
            else
            {
                //todo construct new enzyme
            }
        }


        public override string[] FragmentIons
        {
            get { return Settings.ChemicalData.Instruments.Keys.ToArray(); }
        }
        public override string EngineName { get { return @"MS Amanda"; } }
        public override Bitmap SearchEngineLogo
        {
            get { return Properties.Resources.MSAmandaLogo; }
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

        //private MSAmandaEngine amandaSearchEngine;

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
            SearchEngine = new MSAmandaSearch(helper, _baseDir, _outputParameters, Settings, token);
            SearchEngine.InitializeOutputMZ(mzID);
        }
    
        private bool success = true;
        public override bool Run(CancellationTokenSource tokenSource)
        {
            try
            {
                using (var c = new CurrentCultureSetter(CultureInfo.InvariantCulture))
                {
                    List<Spectrum> spectra = new List<Spectrum>();
                    foreach (var rawFileName in SpectrumFileNames)
                    {
                        tokenSource.Token.ThrowIfCancellationRequested();

                        try
                        {
                            InitializeEngine(tokenSource, rawFileName.GetSampleLocator());

                            amandaInputParser = new MSAmandaSpectrumParser(rawFileName.GetSampleLocator(), Settings.ConsideredCharges, true);
                            SearchEngine.SetInputParser(amandaInputParser);

                            SearchEngine.PerformSearch(_outputParameters.DBFile);
                        }
                        finally
                        {
                            SearchEngine.Dispose();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                helper.WriteMessage("Canceling search", true);
                success = false;
            }
            catch (Exception ex)
            {
                helper.WriteMessage($"Search failed: {ex.Message}", true);
                success = false;
            }

            if (tokenSource.IsCancellationRequested)
                success = false;
            
            return success;
        }

        

        

        public override void SaveModifications(IList<StaticMod> modifications)
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
                            modClone.Fixed = item.IsVariable;
                            Settings.SelectedModifications.Add(modClone);
                        }
                        else
                        {
                            //Settings.SelectedModifications.Add(GenerateNewModification(item.Key, item.Value));
                        }
                    }
                }
                else
                {
                    //Settings.SelectedModifications.Add(GenerateNewModification(item.Key, item.Value));
                }
            }
        }

        private Modification GenerateNewModification(StaticMod mod, bool isFixed)
        {
            //todo
            return null;
        }
    }
}
