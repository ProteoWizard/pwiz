//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Data;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Xml;
using System.Text;
using System.Data.SQLite;
using System.Linq;

using NHibernate;
using NHibernate.Cfg;
using NHibernate.Linq;

using pwiz.CLI.cv;
using pwiz.CLI.chemistry;
using pwiz.CLI.proteome;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using proteome = pwiz.CLI.proteome;
using msdata = pwiz.CLI.msdata;
using analysis = pwiz.CLI.analysis;
using Environment = System.Environment;
using IndexList = pwiz.CLI.msdata.IndexList;

namespace IDPicker.DataModel
{
    public class Parser : IDisposable
    {
        #region Events
        public event EventHandler<DatabaseNotFoundEventArgs> DatabaseNotFound;
        public event EventHandler<SourceNotFoundEventArgs> SourceNotFound;
        public event EventHandler<ParsingProgressEventArgs> ParsingProgress;
        #endregion

        #region Event arguments
        public class DatabaseNotFoundEventArgs : EventArgs
        {
            public string DatabasePath { get; set; }
        }

        public class SourceNotFoundEventArgs : EventArgs
        {
            public string SourcePath { get; set; }
        }

        public class ParsingProgressEventArgs : CancelEventArgs
        {
            public long ParsedBytes { get; set; }
            public long TotalBytes { get; set; }

            public string ParsingStage { get; set; }
            public Exception ParsingException { get; set; }
        }
        #endregion

        public enum FileType
        {
            Unknown,
            PepXML,
            IdpXML
        }

        public readonly static string DefaultDecoyPrefix = "r-";

        public string IdpDbFilepath { get; protected set; }
        public string DecoyPrefix { get; set; }

        string rootInputDirectory;
        long parsedBytes, totalBytes;
        string parsingStage = "Initializing parser...";

        Dictionary<string, ProteomeData> proteomeDataFiles = new Dictionary<string, ProteomeData>();
        int proteinCount = 0;

        Dictionary<string, List<string>> sourceNamesByXmlFilepath = new Dictionary<string, List<string>>();
        Dictionary<string, string> rawDataFilepathByName = new Dictionary<string, string>();

        List<string> xmlFilepathList;

        object dbGroupsMutex = new object();
        object dbSourcesMutex = new object();
        object dbSourceGroupLinksMutex = new object();
        object dbAnalysesMutex = new object();
        object dbProteinsMutex = new object();
        object dbPeptidesMutex = new object();
        object dbModificationsMutex = new object();
        object currentMaxProteinLengthMutex = new object();
        object queueMutex = new object();
        object progressMutex = new object();
        object iomutex = new object();

        public Parser (string idpDbFilepath, string rootInputDirectory, params string[] xmlFilepaths)
        {
            IdpDbFilepath = idpDbFilepath;
            DecoyPrefix = DefaultDecoyPrefix;

            this.rootInputDirectory = rootInputDirectory;
            this.xmlFilepathList = new List<string>(xmlFilepaths);

            //var sessionFactory = SessionFactoryFactory.CreateSessionFactory(IdpDbFilepath, !File.Exists(IdpDbFilepath), false);
            //session = sessionFactory.OpenSession();
        }

        Queue<string> xmlFilepathQueue;

        public void Start ()
        {
            string proteinDatabasePath = "";
            //string lastProteinDatabasePathLocation = Directory.GetCurrentDirectory();
            //string lastSourcePathLocation = Directory.GetCurrentDirectory();

            parsedBytes = 0;
            totalBytes = 0;

            foreach (string xmlFilepath in xmlFilepathList)
            {
                try
                {
                    using (var sourceXml = new StreamReader(xmlFilepath))
                    {
                        FileType fileType;
                        long fileSize = 0;
                        IList<string> sourceNames = sourceNamesByXmlFilepath[xmlFilepath] = new List<string>();
                        IdentifyXml(sourceXml, out fileType, out fileSize, out proteinDatabasePath, sourceNames);

                        string databaseFilepath = locateProteinDatabase(proteinDatabasePath);

                        // don't open the database if it's already open
                        if (!proteomeDataFiles.ContainsKey(proteinDatabasePath))
                            proteomeDataFiles[proteinDatabasePath] = new ProteomeDataFile(databaseFilepath, true);

                        foreach (string sourceName in sourceNames)
                        {
                            try
                            {
                                string sourceFilepath = locateSpectrumSource(sourceName);
                                if (!File.Exists(sourceFilepath) && !Directory.Exists(sourceFilepath))
                                    throw new ArgumentException("source filepath " + rawDataFilepathByName[sourceName] + " does not exist");
                                rawDataFilepathByName[sourceName] = sourceFilepath;
                            }
                            catch
                            {
                                rawDataFilepathByName[sourceName] = String.Empty;
                            }
                        }

                        parsedBytes = totalBytes;
                        totalBytes += fileSize;
                        if(OnInitializingParserProgress(null))
                            return;
                        System.Windows.Forms.Application.DoEvents();
                    }
                }
                catch (Exception ex)
                {
                    OnInitializingParserProgress(ex);
                    return;
                }
            }

            parsedBytes = 0;
            parsingStage = "Parsing identifications...";

            xmlFilepathQueue = new Queue<string>(xmlFilepathList);

            var workerThreads = new Queue<Thread>();
            for (int i = 0; i < Environment.ProcessorCount; ++i)
            {
                workerThreads.Enqueue(new Thread(readXml));
                workerThreads.Last().Name = workerThreads.Count.ToString();
                workerThreads.Last().Start();
            }

            while (workerThreads.Count > 0)
            {
                var thread = workerThreads.Dequeue();
                while (thread.IsAlive)
                {
                    thread.Join(100);

                    if (OnParsingProgress(null))
                    {
                        while (workerThreads.Count > 0)
                            workerThreads.Dequeue().Abort();
                        return;
                    }
                }
            }

            addProteinAndPeptideInstances();

            totalBytes = rawDataFilepathByName.Values.Where(o => !String.IsNullOrEmpty(o)).Sum(o => new FileInfo(o).Length);
            parsedBytes = 0;
            parsingStage = "Embedding subset mzMLs...";

            var workerThread = new Thread(embedSubsetMzML);
            workerThread.Start();

            while (workerThread.IsAlive)
            {
                workerThread.Join(100);

                if (OnParsingProgress(null))
                    return;
            }
        }

        ~Parser ()
        {
            //session.Close();
        }

        public void embedSubsetMzML ()
        {
            foreach (var xmlFilepath in xmlFilepathList)
            {
                try
                {
                    EmbedSourceMzML(Path.ChangeExtension(xmlFilepath, ".idpDB"));

                    foreach (var sourceName in sourceNamesByXmlFilepath[xmlFilepath])
                        if (!String.IsNullOrEmpty(rawDataFilepathByName[sourceName]))
                            parsedBytes += new FileInfo(rawDataFilepathByName[sourceName]).Length;

                    if (OnParsingProgress(null))
                        return;
                }
                catch (Exception ex)
                {
                    if (OnParsingProgress(ex))
                        return;
                }
            }
        }

        public static void IdentifyXml (StreamReader xmlStream,
                                        out FileType fileType,
                                        out long fileSize,
                                        out string proteinDatabasePath,
                                        IList<string> sourceNames)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.ValidationType = ValidationType.None;
            settings.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None;
            settings.IgnoreProcessingInstructions = true;
            settings.ProhibitDtd = false;
            settings.XmlResolver = null;
            settings.CloseInput = false;

            int numTagsRead = 0;
            bool foundXMLTag = false;
            bool foundDatabase = false;
            fileType = FileType.Unknown;
            fileSize = 0;
            proteinDatabasePath = null;

            using (XmlReader reader = XmlTextReader.Create(xmlStream, settings))
            {
                reader.Read();
                numTagsRead++;

                if (reader.Name.Equals("xml"))
                {
                    foundXMLTag = true;
                }

                if (foundXMLTag)
                {
                    // assuming msms tag appears in file before database tag
                    while (!foundDatabase && reader.Read() && numTagsRead < 20)
                    {
                        numTagsRead++;

                        if (reader.Name == "msms_pipeline_analysis")
                        {
                            fileType = FileType.PepXML;
                        }
                        else if (reader.Name == "idPickerPeptides")
                        {
                            fileType = FileType.IdpXML;
                        }
                        else if (reader.Name == "msms_run_summary")
                        {
                            sourceNames.Add(Path.GetFileNameWithoutExtension(getAttribute(reader, "base_name")));
                        }
                        else if (reader.Name == "search_database")
                        {
                            proteinDatabasePath = Path.GetFileName(getAttribute(reader, "local_path").Replace(".pro", ""));
                            foundDatabase = true;
                        }
                        else if (reader.Name == "proteinIndex")
                        {
                            proteinDatabasePath = Path.GetFileName(getAttribute(reader, "database").Replace(".pro", ""));
                            if (!String.IsNullOrEmpty(proteinDatabasePath))
                                foundDatabase = true;
                            break;
                        }
                    }

                    if (fileType == FileType.IdpXML && !foundDatabase)
                    {
                        // old idpXML, look for database in spectraSources
                        int expectedSourceCount = int.MaxValue;
                        while (reader.Read() && (!foundDatabase || sourceNames.Count < expectedSourceCount))
                        {
                            if (reader.Name == "spectraSources")
                            {
                                expectedSourceCount = getAttributeAs<int>(reader, "count");
                            }
                            if (reader.Name == "processingParam" &&
                                getAttribute(reader, "name") == "ProteinDatabase")
                            {
                                proteinDatabasePath = Path.GetFileName(getAttribute(reader, "value"));
                                foundDatabase = true;
                            }
                            else if (reader.Name == "spectraSource")
                            {
                                sourceNames.Add(getAttribute(reader, "name"));
                            }
                        }
                    }
                }
            }

            xmlStream.BaseStream.Seek(0, SeekOrigin.End);
            fileSize = xmlStream.BaseStream.Position;
        }

        void readXml ()
        {
            try
            {
                doReadXml();
            }
            catch (Exception ex)
            {
                OnParsingProgress(ex);
            }
        }

        void doReadXml ()
        {
            while (true)
            {
                string xmlFilepath = null;
                lock (queueMutex)
                {
                    if (xmlFilepathQueue.Count == 0)
                        return;
                    xmlFilepath = xmlFilepathQueue.Dequeue();
                }

                File.Delete(Path.ChangeExtension(xmlFilepath, ".idpDB"));
                //var conn = SessionFactoryFactory.CreateFile(xmlFilepath + ".idpDB");

                var dbAnalyses = new List<Analysis>();
                var dbGroups = new List<SpectrumSourceGroup>();
                var dbSources = new List<SpectrumSource>();
                var dbSourceGroupLinks = new List<SpectrumSourceGroupLink>();
                var dbSpectra = new Dictionary<int, Spectrum>(); // Spectrum.Index -> Spectrum
                var dbProteins = new Dictionary<string, Protein>(); // Protein.Accession -> Protein
                var dbPeptides = new Dictionary<string, Peptide>(); // Peptide.Sequence -> Peptide
                var dbModifications = new Dictionary<double, Modification>();

                int apCount = 0;
                int ssglCount = 0;
                int spectrumCount = 0;
                int proteinCount = 0;
                int peptideCount = 0;
                int piCount = 0;
                int psmCount = 0;
                int pmCount = 0;

                string proteinDatabasePath = "";
                //string lastProteinDatabasePathLocation = Directory.GetCurrentDirectory();
                //string lastSourcePathLocation = Directory.GetCurrentDirectory();

                var sourceXml = new StreamReader(xmlFilepath);

                #region Determine input file type and protein database used; also locate database and open it

                FileType fileType;
                long fileSize;
                string oldProteinDatabasePath = proteinDatabasePath;
                IList<string> sourceNames = new List<string>();
                IdentifyXml(sourceXml, out fileType, out fileSize, out proteinDatabasePath, sourceNames);

                ProteinList proteinList = proteomeDataFiles[proteinDatabasePath].proteinList;

                #endregion

                #region Initialize the XmlReader

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.None;
                settings.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None;
                settings.IgnoreProcessingInstructions = true;
                settings.ProhibitDtd = false;
                settings.XmlResolver = null;

                sourceXml = new StreamReader(xmlFilepath);
                XmlReader reader = XmlTextReader.Create(sourceXml, settings);

                #endregion

                var conn = SessionFactoryFactory.CreateFile(":memory:");
                conn.ExecuteNonQuery("PRAGMA temp_store=MEMORY");
                var bulkInserter = new BulkInserter(conn);

                #region Current object references, used to share information within the XML hierarchy

                int curId = 0;
                SpectrumSourceGroup curGroup = null;
                SpectrumSource curSource = null;
                Spectrum curSpectrum = null;
                Peptide curPeptide = null;
                Protein curProtein = null;
                PeptideSpectrumMatch curPSM = null;
                Analysis curAnalysis = null;
                string curProcessingEventType = null;
                Map<int, double> curMods = null; // map mod positions to masses
                int curCharge = 0;
                bool curPeptideIsNew = false;

                #endregion

                string tag;
                long lastStatusUpdatePosition = 0;
                long baseStreamLength = sourceXml.BaseStream.Length;

                if (fileType == FileType.IdpXML)
                {
                    #region idpXML reading

                    var proteinIndex = new Dictionary<int, string>();
                    var peptideIndex = new Dictionary<int, string>();

                    while (reader.Read())
                    {
                        #region Send ParsingProgress event (but read at least 100kb between updates)

                        if (ParsingProgress != null)
                        {
                            long position = sourceXml.BaseStream.Position;
                            if (position > lastStatusUpdatePosition + 100000 || position == baseStreamLength)
                            {
                                Interlocked.Add(ref parsedBytes, position - lastStatusUpdatePosition);
                                lastStatusUpdatePosition = position;
                            }
                        }

                        #endregion

                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                tag = reader.Name;

                                #region <id peptide="103" mods="9:-272.143" />

                                if (tag == "id")
                                {
                                    int id = getAttributeAs<int>(reader, "peptide", true);

                                    string pep = peptideIndex[id];
                                    double neutralPrecursorMass = curPSM.Spectrum.PrecursorMZ*curPSM.Charge - (curPSM.Charge*Proton.Mass);

                                    curPeptide = dbPeptides[pep];

                                    // Get the mods for the peptide
                                    string modListStr = getAttribute(reader, "mods");
                                    if (modListStr.Length > 0)
                                    {
                                        // Parse the mod string
                                        // Example mod string: "9:-272.143;10:-273.143"
                                        // Each set of mods before the semi-colon represent an interpretation
                                        string[] ambiguousLocations = modListStr.Split(';');

                                        #region Add each modified interpretation

                                        for (int i = 0; i < ambiguousLocations.Length; ++i)
                                        {
                                            //int psmId;
                                            //lock (psmCountMutex) psmId = ++psmCount;
                                            curPSM = new PeptideSpectrumMatch(curPSM)
                                                         {
                                                             Id = ++psmCount,
                                                             Peptide = curPeptide,
                                                             MonoisotopicMass = curPeptide.MonoisotopicMass,
                                                             MolecularWeight = curPeptide.MolecularWeight,
                                                         };

                                            string[] modInfoStrs = ambiguousLocations[i].Split(' ');

                                            #region Add mods from this interpretation

                                            foreach (string modInfoStr in modInfoStrs)
                                            {
                                                string[] modPosMassPair = modInfoStr.Split(":".ToCharArray());
                                                string modPosStr = modPosMassPair[0];

                                                double modMass;
                                                proteome.Modification pwizMod;
                                                if (!Double.TryParse(modPosMassPair[1], out modMass))
                                                    pwizMod = new proteome.Modification(modPosMassPair[1]);
                                                else
                                                    pwizMod = new proteome.Modification(modMass, modMass);

                                                modMass = pwizMod.monoisotopicDeltaMass();
                                                curPSM.MonoisotopicMass += modMass;
                                                curPSM.MolecularWeight += pwizMod.averageDeltaMass();

                                                Modification mod;
                                                if (!dbModifications.TryGetValue(modMass, out mod))
                                                {
                                                    mod = new Modification()
                                                          {
                                                              Id = dbModifications.Count + 1,
                                                              Name = pwizMod.hasFormula() ? pwizMod.formula() : String.Empty,
                                                              Formula = pwizMod.hasFormula() ? pwizMod.formula() : String.Empty,
                                                              MonoMassDelta = modMass,
                                                              AvgMassDelta = pwizMod.averageDeltaMass(),
                                                          };
                                                    dbModifications[modMass] = mod;
                                                    bulkInserter.Add(mod);
                                                }

                                                int offset;
                                                if (modPosStr == "n")
                                                    offset = int.MinValue;
                                                else if (modPosStr == "c")
                                                    offset = int.MaxValue;
                                                else
                                                    offset = Convert.ToInt32(modPosStr) - 1;

                                                var pm = new PeptideModification(curPeptide.Sequence, offset)
                                                             {
                                                                 Id = ++pmCount,
                                                                 PeptideSpectrumMatch = curPSM,
                                                                 Modification = mod
                                                             };

                                                bulkInserter.Add(pm);
                                            }

                                            #endregion

                                            curPSM.MonoisotopicMassError = neutralPrecursorMass -
                                                                           curPSM.MonoisotopicMass;
                                            curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                            bulkInserter.Add(curPSM);
                                        }

                                        #endregion
                                    }
                                    else
                                    {
                                        //int psmId;
                                        //lock (psmCountMutex) psmId = ++psmCount;
                                        // add the unmodified PSM
                                        curPSM = new PeptideSpectrumMatch(curPSM)
                                                     {
                                                         Id = ++psmCount,
                                                         Peptide = curPeptide,
                                                         MonoisotopicMass = curPeptide.MonoisotopicMass,
                                                         MolecularWeight = curPeptide.MolecularWeight,
                                                     };

                                        curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                        curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                        bulkInserter.Add(curPSM);
                                    }
                                }
                                    #endregion
                                    #region <spectrum id="614" nativeID="614" index="196" z="1" mass="569.32" time="16.7" targets="82" decoys="0" results="1">

                                else if (tag == "spectrum")
                                {
                                    int index = getAttributeAs<int>(reader, "index", true);
                                    string nativeID = getAttribute(reader, "id");
                                    int z = getAttributeAs<int>(reader, "z", true);

                                    if (!dbSpectra.TryGetValue(index, out curSpectrum))
                                    {
                                        double neutralPrecursorMass = getAttributeAs<double>(reader, "mass", true);

                                        curSpectrum = dbSpectra[index] = new Spectrum()
                                                                             {
                                                                                 Id = ++spectrumCount,
                                                                                 Index = index,
                                                                                 NativeID = nativeID,
                                                                                 Source = curSource,
                                                                                 PrecursorMZ = (neutralPrecursorMass + Proton.Mass * z) / z
                                                                             };

                                        //curSource.Spectra.Add(curSpectrum);
                                        bulkInserter.Add(curSpectrum);
                                    }

                                    curPSM = new PeptideSpectrumMatch()
                                                 {
                                                     Spectrum = curSpectrum,
                                                     Analysis = curAnalysis,
                                                     Charge = z
                                                 };

                                    //curSpectrum.retentionTime = getAttributeAs<float>(reader, "time");

                                    //curSpectrum.numComparisons = getAttributeAs<int>(reader, "comparisons");
                                    //curSpectrum.numComparisons += getAttributeAs<int>(reader, "targets");
                                    //curSpectrum.numComparisons += getAttributeAs<int>(reader, "decoys");
                                }
                                    #endregion
                                    #region <result rank="1" FDR="0.1" scores="name1=value1 name2=value2">

                                else if (tag == "result")
                                {
                                    curPSM = new PeptideSpectrumMatch(curPSM)
                                                 {
                                                     Rank = getAttributeAs<int>(reader, "rank", true),
                                                     QValue = getAttributeAs<float>(reader, "FDR", true)
                                                 };

                                    string scores = getAttribute(reader, "scores");
                                    if (!String.IsNullOrEmpty(scores))
                                    {
                                        curPSM.Scores = new Dictionary<string, double>();
                                        string[] scoreTokens = scores.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                                        // scores could be like "name1=value1 name2=value2" or "value1 value2"
                                        bool hasNames = scoreTokens[0].Contains('=');
                                        if (hasNames)
                                            foreach (string token in scoreTokens)
                                            {
                                                string[] tokenTokens = token.Split('=');
                                                curPSM.Scores[tokenTokens[0]] = Convert.ToDouble(tokenTokens[1]);
                                            }
                                        else
                                            for (int i = 0; i < scoreTokens.Length; ++i)
                                                curPSM.Scores[String.Format("score{0}", (i + 1))] = Convert.ToDouble(scoreTokens[i]);
                                    }
                                }
                                    #endregion
                                    #region <protein id="17" locus="rev_P02413" decoy="1" length="144" />

                                else if (tag == "protein")
                                {
                                    // Read the protein tag
                                    int localId = getAttributeAs<int>(reader, "id", true);
                                    string locus = getAttribute(reader, "locus", true);
                                    if (locus.Contains("IPI:IPI"))
                                        locus = locus.Split('|')[0];

                                    proteinIndex[localId] = locus;

                                    if (!dbProteins.ContainsKey(locus))
                                    {
                                        //pro.isDecoy = Convert.ToBoolean(getAttributeAs<int>(reader, "decoy"));

                                        string description = getAttribute(reader, "description");

                                        int index = proteinList.find(locus);
                                        proteome.Protein pro = proteinList.protein(index);

                                        curProtein = dbProteins[locus] = new Protein(description, pro.sequence)
                                                                             {
                                                                                 Id = dbProteins.Count + 1,
                                                                                 Accession = locus
                                                                             };

                                        bulkInserter.Add(curProtein);
                                    }
                                }
                                    #endregion
                                #region <peptide id="3" sequence="AILAAAGIAEDVK" mass="1240.70" unique="1">

                                else if (tag == "peptide")
                                {
                                    curId = getAttributeAs<int>(reader, "id", true);
                                    string sequence = getAttribute(reader, "sequence", true);

                                    peptideIndex[curId] = sequence;

                                    if (!dbPeptides.ContainsKey(sequence))
                                    {
                                        using (proteome.Peptide pep = new proteome.Peptide(sequence))
                                        {
                                            curPeptide = dbPeptides[sequence] = new Peptide(sequence)
                                                                                    {
                                                                                        Id = dbPeptides.Count + 1,
                                                                                        Instances = new List<PeptideInstance>(),
                                                                                        MonoisotopicMass = pep.monoisotopicMass(),
                                                                                        MolecularWeight = pep.molecularWeight()
                                                                                    };
                                        }

                                        bulkInserter.Add(curPeptide);
                                    }
                                    else
                                        curPeptide = dbPeptides[sequence];

                                }
                                    #endregion
                                #region <locus id="10" offset="165" />

                                else if (tag == "locus")
                                {
                                    // Read the locus tag
                                    // 
                                    int localId = getAttributeAs<int>(reader, "id", true);

                                    string pro = proteinIndex[localId];
                                    string pep = peptideIndex[curId];

                                    curPeptide = dbPeptides[pep];
                                    curProtein = dbProteins[pro];

                                    if (curPeptide.Instances.Count(o => o.Protein == curProtein) == 0)
                                    {
                                        curPeptide.Instances.Add(new PeptideInstance()
                                        {
                                            Id = ++piCount,
                                            Peptide = curPeptide,
                                            Protein = curProtein,
                                            Offset = (int) curPeptide.Id.Value, // bogus, but keeps the instance unique
                                            Length = curPeptide.Sequence.Length,
                                            NTerminusIsSpecific = true,
                                            CTerminusIsSpecific = true
                                        });
                                        bulkInserter.Add(curPeptide.Instances.Last());
                                    }
                                }
                                #endregion
                                #region Initialize protein/peptide indexes

                                else if (tag == "proteinIndex")
                                {
                                    //proteinIndex = new string[getAttributeAs<int>(reader, "count", true) + 1];
                                    //proteinIndex.Initialize();
                                }
                                else if (tag == "peptideIndex")
                                {
                                    //peptideIndex = new string[getAttributeAs<int>(reader, "count", true) + 1];
                                    //peptideIndex.Initialize();
                                }
                                #endregion
                                #region <spectraSource ...>

                                else if (tag == "spectraSource")
                                {
                                    curAnalysis = null;

                                    string groupName = getAttribute(reader, "group");

                                    if (groupName.Length == 0)
                                        groupName = "/";
                                    else if (groupName[0] != '/')
                                        groupName = "/" + groupName;

                                    var groupQuery = from g in dbGroups
                                                     where g.Name == groupName
                                                     select g;

                                    if (groupQuery.Count() == 0)
                                    {
                                        dbGroups.Add(new SpectrumSourceGroup()
                                                         {
                                                             Id = dbGroups.Count + 1,
                                                             Name = groupName
                                                         });

                                        curGroup = dbGroups.Last();

                                        bulkInserter.Add(curGroup);
                                    }
                                    else
                                        curGroup = groupQuery.First();

                                    string sourceName = getAttribute(reader, "name", true);
                                    string sourceFilepath = rawDataFilepathByName[sourceName];

                                    // reset dbSpectra
                                    dbSpectra = new Dictionary<int, Spectrum>();

                                    var sourceQuery = from s in dbSources
                                                      where s.Name == sourceName
                                                      select s;

                                    if (sourceQuery.Count() == 0)
                                    {
                                        dbSources.Add(new SpectrumSource()
                                                          {
                                                              Id = dbSources.Count + 1,
                                                              Name = sourceName,
                                                              URL = sourceFilepath,
                                                              Spectra = new List<Spectrum>(),
                                                              Group = curGroup
                                                          });

                                        curSource = dbSources.Last();

                                        if (groupName != "/")
                                        {
                                            // add the group and all its parent groups to the source
                                            string groupPath = curSource.Group.Name;
                                            string parentGroupName = groupPath.Substring(0, groupPath.LastIndexOf("/"));
                                            while (true)
                                            {
                                                if (String.IsNullOrEmpty(parentGroupName))
                                                    parentGroupName = "/";

                                                // add the parent group if it doesn't exist yet
                                                if (dbGroups.Count(o => o.Name == parentGroupName) == 0)
                                                {
                                                    dbGroups.Add(new SpectrumSourceGroup()
                                                                     {
                                                                         Id = dbGroups.Count + 1,
                                                                         Name = parentGroupName
                                                                     });

                                                    bulkInserter.Add(dbGroups.Last());
                                                }

                                                dbSourceGroupLinks.Add(new SpectrumSourceGroupLink()
                                                                           {
                                                                               Id = dbSourceGroupLinks.Count + 1,
                                                                               Group = dbGroups.First(o => o.Name == parentGroupName),
                                                                               Source = curSource
                                                                           });

                                                bulkInserter.Add(dbSourceGroupLinks.Last());

                                                if (parentGroupName == "/")
                                                    break;
                                                parentGroupName = parentGroupName.Substring(0, parentGroupName.LastIndexOf("/"));
                                            }
                                        }

                                        bulkInserter.Add(curSource);

                                        dbSourceGroupLinks.Add(new SpectrumSourceGroupLink()
                                        {
                                            Id = dbSourceGroupLinks.Count + 1,
                                            Group = curGroup,
                                            Source = curSource
                                        });
                                        bulkInserter.Add(dbSourceGroupLinks.Last());
                                    }
                                    else
                                    {
                                        curSource = sourceQuery.Single();

                                        foreach (var spectrum in curSource.Spectra)
                                            dbSpectra[spectrum.Index] = spectrum;
                                    }
                                }
                                #endregion
                                #region Processing events

                                else if (tag == "processingEvent")
                                {
                                    curProcessingEventType = getAttribute(reader, "type", true);

                                    if (curProcessingEventType == "identification")
                                    {
                                        curAnalysis = new Analysis();
                                        curAnalysis.Parameters =
                                            new Iesi.Collections.Generic.SortedSet<AnalysisParameter>();

                                        try
                                        {
                                            string LegacyTimeFormat = "MM/dd/yyyy@HH:mm:ss";
                                            curAnalysis.StartTime = DateTime.ParseExact(getAttribute(reader, "start"),
                                                                                        LegacyTimeFormat, null);
                                            //DateTime.ParseExact(getAttribute(reader, "end"), TimeFormat, null);
                                        }
                                        catch
                                        {
                                        }
                                    }
                                }
                                else if (curProcessingEventType == "identification" && tag == "processingParam")
                                {
                                    string paramName = getAttribute(reader, "name", true);
                                    string paramValue = getAttribute(reader, "value", true);

                                    if (paramName == "software name")
                                    {
                                        curAnalysis.Software = new AnalysisSoftware();
                                        curAnalysis.Software.Name = paramValue;
                                    }
                                    else if (paramName == "software version")
                                    {
                                        curAnalysis.Software.Version = paramValue;
                                        curAnalysis.Name = curAnalysis.Software.Name + " " +
                                                           curAnalysis.Software.Version;
                                    }
                                    else if (!paramName.Contains(": "))
                                    {
                                        curAnalysis.Parameters.Add(new AnalysisParameter()
                                                                       {
                                                                           Name = paramName,
                                                                           Value = paramValue
                                                                       });
                                    }
                                }

                                #endregion

                                break;

                            case XmlNodeType.EndElement:
                                tag = reader.Name;

                                #region Create subset source mzML and add the current rows to the database

                                if (tag == "spectraSource")
                                {
                                    bulkInserter.Execute();
                                    //bulkInserter.Reset();
                                }
                                #endregion
                                #region Determine if the current analysis is already in the database

                                else if (curProcessingEventType == "identification" && tag == "processingEvent")
                                {
                                    // an analysis is unique if its name is unique and its parameter set has some
                                    // difference with other analyses
                                    var analysisQuery = from a in dbAnalyses
                                                        where a.Name == curAnalysis.Name &&
                                                              a.Parameters.ExclusiveOr(curAnalysis.Parameters).Count ==
                                                              0
                                                        select a;

                                    if (analysisQuery.Count() == 0)
                                    {
                                        dbAnalyses.Add(curAnalysis);
                                        curAnalysis.Id = dbAnalyses.Count;
                                        curAnalysis.Type = AnalysisType.DatabaseSearch;

                                        bulkInserter.Add(curAnalysis);
                                        // curAnalysis.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                                    }
                                    else
                                        curAnalysis = analysisQuery.Single();

                                    curProcessingEventType = null;
                                }

                                #endregion

                                break;
                        } // switch
                    } // while

                    #endregion
                }
                else
                {
                    #region pepXML reading

                    curGroup = new SpectrumSourceGroup() { Id = 1, Name = "/" };
                    bulkInserter.Add(curGroup);

                    while (reader.Read())
                    {
                        #region Send ParsingProgress event (but read at least 100kb between updates)

                        if (ParsingProgress != null)
                        {
                            long position = sourceXml.BaseStream.Position;
                            if (position > lastStatusUpdatePosition + 100000 || position == baseStreamLength)
                            {
                                Interlocked.Add(ref parsedBytes, position - lastStatusUpdatePosition);
                                lastStatusUpdatePosition = position;
                            }
                        }

                        #endregion

                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                tag = reader.Name;

                                #region <search_score name="mvh" value="42"/>

                                if (tag == "search_score")
                                {
                                    string name = getAttribute(reader, "name");
                                    string value = getAttribute(reader, "value");

                                    // ignore non-numeric values
                                    double numericCheck;
                                    if (Double.TryParse(value, out numericCheck))
                                    {
                                        curPSM.Scores[name] = numericCheck;
                                    }
                                }
                                    #endregion
                                    #region <search_hit hit_rank="1" peptide="QTSSM" peptide_prev_aa="R" peptide_next_aa="-" protein="rev_RPA0405" peptide_offset="476" num_tot_proteins="1" num_matched_ions="0" tot_num_ions="6" calc_neutral_pep_mass="123" massdiff="123" num_tol_term="2" num_missed_cleavages="0">

                                else if (tag == "search_hit")
                                {
                                    curPSM = new PeptideSpectrumMatch()
                                    {
                                        Id = ++psmCount,
                                        Spectrum = curSpectrum,
                                        Analysis = curAnalysis,
                                        Charge = curCharge,
                                        Scores = new Dictionary<string, double>(),
                                        Modifications = new List<PeptideModification>()
                                    };

                                    #region Get current peptide (from dbPeptides map if possible)

                                    string sequence = getAttribute(reader, "peptide");
                                    curPeptideIsNew = false;

                                    if (!dbPeptides.TryGetValue(sequence, out curPeptide))
                                    {
                                        using (proteome.Peptide pep = new proteome.Peptide(sequence))
                                        {
                                            curPeptide = dbPeptides[sequence] = new Peptide(sequence)
                                                                                    {
                                                                                        Id = ++peptideCount,
                                                                                        MonoisotopicMass = pep.monoisotopicMass(),
                                                                                        MolecularWeight = pep.molecularWeight(),
                                                                                        Instances = new List<PeptideInstance>()
                                                                                    };
                                        }
                                        curPeptideIsNew = true;
                                        bulkInserter.Add(curPeptide);

                                        string locus = getAttribute(reader, "protein");
                                        //string offset = getAttribute(reader, "peptide_offset");

                                        if (!dbProteins.TryGetValue(locus, out curProtein))
                                        {
                                            curProtein = dbProteins[locus] = new Protein()
                                                                                 {
                                                                                     Id = ++proteinCount,
                                                                                     Accession = locus
                                                                                 };

                                            bulkInserter.Add(curProtein);
                                        }

                                        curPeptide.Instances.Add(new PeptideInstance()
                                        {
                                            Id = ++piCount,
                                            Peptide = curPeptide,
                                            Protein = curProtein,
                                            Offset = (int) curPeptide.Id.Value, // bogus, but keeps the instance unique
                                            Length = curPeptide.Sequence.Length,
                                            NTerminusIsSpecific = true,
                                            CTerminusIsSpecific = true
                                        });
                                        bulkInserter.Add(curPeptide.Instances.Last());

                                        //addProteinAndPeptideInstances(dbPeptideInstances, proteinList, curPeptide, bulkInserter,
                                        //                              locus, offset, ref maxProteinLength);
                                    }
                                    #endregion

                                    curPSM.Peptide = curPeptide;
                                    curPSM.Rank = getAttributeAs<int>(reader, "hit_rank");
                                    curPSM.MonoisotopicMass = curPSM.Peptide.MonoisotopicMass;
                                    curPSM.MolecularWeight = curPSM.Peptide.MolecularWeight;
                                    curPSM.QValue = PeptideSpectrumMatch.DefaultQValue;
                                }
                                    #endregion
                                    #region <alternative_protein protein="ABCD" />

                                else if (tag == "alternative_protein")
                                {
                                    if (!curPeptideIsNew)
                                        continue;

                                    string locus = getAttribute(reader, "protein");
                                    //string offset = getAttribute(reader, "peptide_offset");

                                    if (!dbProteins.TryGetValue(locus, out curProtein))
                                    {
                                        curProtein = dbProteins[locus] = new Protein()
                                                                             {
                                                                                 Id = ++proteinCount,
                                                                                 Accession = locus
                                                                             };

                                        bulkInserter.Add(curProtein);
                                    }
                                    else if (curPeptide.Instances.Count(o => o.Protein == curProtein) > 0)
                                        continue;

                                    curPeptide.Instances.Add(new PeptideInstance()
                                    {
                                        Id = ++piCount,
                                        Peptide = curPeptide,
                                        Protein = curProtein,
                                        Offset = (int) curPeptide.Id.Value, // bogus, but keeps the instance unique
                                        Length = curPeptide.Sequence.Length,
                                        NTerminusIsSpecific = true,
                                        CTerminusIsSpecific = true
                                    });
                                    bulkInserter.Add(curPeptide.Instances.Last());

                                    //addProteinAndPeptideInstances(dbPeptideInstances, proteinList, curPeptide, bulkInserter,
                                    //                              locus, offset, ref maxProteinLength);
                                }
                                    #endregion
                                    #region <modification_info mod_nterm_mass="42" mod_cterm_mass="42">

                                else if (tag == "modification_info")
                                {
                                    curMods = new Map<int, double>();

                                    double nTermModMass = getAttributeAs<double>(reader, "mod_nterm_mass");
                                    if (nTermModMass > 0)
                                        curMods[int.MinValue] = nTermModMass;

                                    double cTermModMass = getAttributeAs<double>(reader, "mod_cterm_mass");
                                    if (cTermModMass > 0)
                                        curMods[int.MaxValue] = cTermModMass;
                                }
                                    #endregion
                                    #region <mod_aminoacid_mass position="7" mass="42" />

                                else if (tag == "mod_aminoacid_mass")
                                {
                                    double modMass = getAttributeAs<double>(reader, "mass");
                                    int position = getAttributeAs<int>(reader, "position");
                                    Formula aaFormula = AminoAcidInfo.record(curPeptide.Sequence[position - 1]).residueFormula;
                                    curMods.Add(position, modMass - aaFormula.monoisotopicMass());
                                }
                                    #endregion
                                    #region <spectrum_query spectrum="abc.42.42.2" start_scan="42" end_scan="42" spectrumNativeID="controllerType=0 controllerNumber=1 scan=42" spectrumIndex="42" precursor_neutral_mass="42" assumed_charge="2" index="1" retention_time_sec="42">

                                else if (tag == "spectrum_query")
                                {
                                    int index;
                                    string nativeID;
                                    try
                                    {
                                        index = getAttributeAs<int>(reader, "spectrumIndex", true);
                                        nativeID = getAttribute(reader, "spectrumNativeID", true);
                                    }
                                    catch
                                    {
                                        nativeID = getAttribute(reader, "start_scan", true);
                                        index = Convert.ToInt32(nativeID);
                                    }

                                    int z = curCharge = getAttributeAs<int>(reader, "assumed_charge", true);

                                    //lock (dbSourcesMutex)
                                        if (!dbSpectra.TryGetValue(index, out curSpectrum))
                                        {
                                            double neutralPrecursorMass = getAttributeAs<double>(reader,
                                                                                                 "precursor_neutral_mass",
                                                                                                 true);

                                            curSpectrum = dbSpectra[index] = new Spectrum()
                                                                                 {
                                                                                     Id =++spectrumCount,
                                                                                     Index = index,
                                                                                     NativeID = nativeID,
                                                                                     Source = curSource,
                                                                                     PrecursorMZ = (neutralPrecursorMass + Proton.Mass*z)/z
                                                                                 };

                                            bulkInserter.Add(curSpectrum);
                                        }

                                    //curSpectrum.retentionTime = getAttributeAs<float>(reader, "time");

                                    /*curSpectrum.numComparisons = getAttributeAs<int>(reader, "comparisons");
                                curSpectrum.numComparisons += getAttributeAs<int>(reader, "targets");
                                curSpectrum.numComparisons += getAttributeAs<int>(reader, "decoys");*/
                                }
                                    #endregion
                                    #region <search_summary base_name="abc" search_engine="MyriMatch" precursor_mass_type="monoisotopic" fragment_mass_type="monoisotopic" out_data_type="n/a" out_data="n/a" search_id="1">

                                else if (tag == "search_summary")
                                {
                                    curAnalysis = new Analysis()
                                                  {
                                                      Software = new AnalysisSoftware() { Name = getAttribute(reader, "search_engine") },
                                                      Parameters = new Iesi.Collections.Generic.SortedSet<AnalysisParameter>()
                                                  };

                                    string sourceName = getAttribute(reader, "base_name", true);
                                    string sourceFilepath = rawDataFilepathByName[sourceName];

                                    dbSources.Add(new SpectrumSource()
                                                      {
                                                          Id = dbSources.Count + 1,
                                                          Name = sourceName,
                                                          URL = sourceFilepath,
                                                          Group = curGroup
                                                      });

                                    curSource = dbSources.Last();

                                    bulkInserter.Add(curSource);

                                    dbSourceGroupLinks.Add(new SpectrumSourceGroupLink()
                                    {
                                        Id = dbSourceGroupLinks.Count + 1,
                                        Group = curGroup,
                                        Source = curSource
                                    });
                                    bulkInserter.Add(dbSourceGroupLinks.Last());
                                }
                                    #endregion
                                #region <parameter name="foo" value="bar"/>

                                else if (tag == "parameter")
                                {
                                    string paramName = getAttribute(reader, "name", true);
                                    string paramValue = getAttribute(reader, "value", true);

                                    if (paramName == "SearchEngine: Version")
                                    {
                                        curAnalysis.Software.Version = paramValue;
                                        curAnalysis.Name = curAnalysis.Software.Name + " " + curAnalysis.Software.Version;
                                    }
                                    else if (paramName == "SearchTime: Started")
                                    {
                                        curAnalysis.StartTime = DateTime.ParseExact(paramValue, "HH:mm:ss 'on' MM/dd/yyyy", System.Globalization.DateTimeFormatInfo.CurrentInfo);
                                    }
                                    else if (paramName.Contains("WorkingDirectory"))
                                    {
                                        // ignore
                                    }
                                    else if (paramName.Contains("Config: "))
                                    {
                                        curAnalysis.Parameters.Add(new AnalysisParameter()
                                                                       {
                                                                           Id = ++apCount,
                                                                           Name = paramName.Substring(8),
                                                                           Value = paramValue
                                                                       });
                                    }
                                    else if (!paramName.Contains(": "))
                                    {
                                        curAnalysis.Parameters.Add(new AnalysisParameter()
                                                                       {
                                                                           Id = ++apCount,
                                                                           Name = paramName,
                                                                           Value = paramValue
                                                                       });
                                    }
                                }

                                #endregion

                                break;

                            case XmlNodeType.EndElement:
                                tag = reader.Name;

                                #region </search_hit>: add current PSM

                                if (tag == "search_hit")
                                {
                                    double neutralPrecursorMass = curPSM.Spectrum.PrecursorMZ*curPSM.Charge - (curPSM.Charge*Proton.Mass);

                                    curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                    curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                    bulkInserter.Add(curPSM);
                                }
                                    #endregion
                                    #region </modification_info>: add current modifications

                                else if (tag == "modification_info")
                                {
                                    foreach (var itr in curMods)
                                    {
                                        int position = itr.Key;
                                        double mass = itr.Value;

                                        Modification mod;
                                        if (!dbModifications.TryGetValue(mass, out mod))
                                        {
                                            mod = dbModifications[mass] = new Modification()
                                                                              {
                                                                                  Id = dbModifications.Count + 1,
                                                                                  MonoMassDelta = mass,
                                                                                  AvgMassDelta = mass,
                                                                              };

                                            bulkInserter.Add(mod);
                                        }

                                        curPSM.MonoisotopicMass += mass;
                                        curPSM.MolecularWeight += mass;

                                        var pm = new PeptideModification(curPeptide.Sequence, position - 1)
                                                     {
                                                         Id = ++pmCount,
                                                         PeptideSpectrumMatch = curPSM,
                                                         Modification = mod
                                                     };
                                        curPSM.Modifications.Add(pm);
                                        bulkInserter.Add(pm);
                                    }
                                }
                                    #endregion
                                    #region </search_summary>: determine if the current analysis is already in the database

                                else if (tag == "search_summary")
                                {
                                    // an analysis is unique if its name is unique and its parameter set has some
                                    // difference with other analyses
                                    var analysisQuery = from a in dbAnalyses
                                                        where a.Name == curAnalysis.Name &&
                                                              a.Parameters.ExclusiveOr(curAnalysis.Parameters).Count ==
                                                              0
                                                        select a;

                                    if (analysisQuery.Count() == 0)
                                    {
                                        dbAnalyses.Add(curAnalysis);
                                        curAnalysis.Id = dbAnalyses.Count;
                                        curAnalysis.Type = AnalysisType.DatabaseSearch;

                                        bulkInserter.Add(curAnalysis);
                                    }
                                    else
                                        curAnalysis = analysisQuery.Single();
                                    //Monitor.Exit(dbAnalysesMutex);
                                }
                                    #endregion

                                break;
                        } // switch(Start/End Element)
                    } // while(Read)

                    bulkInserter.Execute();

                    // this preqonversion may have incorrect terminal specificities
                    var qonverter = new IDPicker.StaticWeightQonverter();
                    qonverter.DecoyPrefix = DecoyPrefix;
                    qonverter.ScoreWeights["mvh"] = 1;
                    //qonverter.ScoreWeights["mzFidelity"] = 1;
                    qonverter.Qonvert((conn as SQLiteConnection).GetDbPtr());

                    string postQonvertCleanup = @"
BEGIN TRANSACTION;

-- Apply a broad QValue filter on top-ranked PSMs
DELETE FROM PeptideSpectrumMatch WHERE QValue > 0.25 AND Rank = 1;

-- Delete all PSMs for a spectrum if the spectrum's top-ranked PSM was deleted above
DELETE FROM PeptideSpectrumMatch
      WHERE Rank > 1
        AND Spectrum NOT IN (
                             SELECT DISTINCT Spectrum
                             FROM PeptideSpectrumMatch
                             WHERE Rank = 1
                            );
-- Delete links to the deleted PSMs
DELETE FROM PeptideSpectrumMatchScores WHERE PsmId NOT IN (SELECT Id FROM PeptideSpectrumMatch);
DELETE FROM PeptideModification WHERE PeptideSpectrumMatch NOT IN (SELECT Id FROM PeptideSpectrumMatch);
DELETE FROM Spectrum WHERE Id NOT IN (SELECT DISTINCT Spectrum FROM PeptideSpectrumMatch);
DELETE FROM Peptide WHERE Id NOT IN (SELECT DISTINCT Peptide FROM PeptideSpectrumMatch);
DELETE FROM PeptideSequences WHERE Id NOT IN (SELECT Id FROM Peptide);

-- Delete all peptide instances and proteins since the final parsing step adds them
DELETE FROM PeptideInstance;
DELETE FROM Protein;
DELETE FROM ProteinMetadata;
DELETE FROM ProteinData;

COMMIT TRANSACTION";

                    conn.ExecuteNonQuery(postQonvertCleanup);
                    conn.ExecuteNonQuery("VACUUM");

                    #endregion // pepXML
                } // file type

                sourceXml.Close();
                (conn as SQLiteConnection).SaveToDisk(Path.ChangeExtension(xmlFilepath, ".idpDB"));
                conn.Close();
            }
        }

        public static void EmbedSourceMzML (string idpDbFilepath)
        {
            using (var conn = new SQLiteConnection("Data Source=" + idpDbFilepath))
            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE SpectrumSource SET MsDataBytes = ? WHERE Id = ?";
                var bytesParam = cmd.CreateParameter(); bytesParam.DbType = DbType.Binary;
                var idParam = cmd.CreateParameter(); idParam.DbType = DbType.Int64;
                cmd.Parameters.Add(bytesParam);
                cmd.Parameters.Add(idParam);

                foreach (var row in conn.ExecuteQuery("SELECT Id, URL FROM SpectrumSource"))
                {
                    long Id = (long) row[0];
                    string URL = (string) row[1];

                    if (!String.IsNullOrEmpty(URL))
                    {
                        /*string localPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(URL));
                        if (!File.Exists(localPath))
                            File.Copy(URL, localPath);*/

                        try
                        {
                            using (var msd = new MSDataFile(URL))
                            using (var sl = msd.run.spectrumList)
                            using (var sl2 = new SpectrumList_PeakPicker(sl,
                                                                         new LocalMaximumPeakDetector(5),
                                                                         true,
                                                                         new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }))
                            using (var sl3 = new SpectrumList_PeakFilter(sl2,
                                                                         new ThresholdFilter(ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_Count,
                                                                                             50,
                                                                                             ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense)))
                            {
                                var spectrumFileIndexes = new List<string>();
                                foreach (var nativeId in conn.ExecuteQuery("SELECT NativeID FROM Spectrum WHERE Source = " + Id).Select(o => o.GetString(0)))
                                {
                                    int realIndex = sl3.find(nativeId);
                                    if (realIndex != sl3.size())
                                        spectrumFileIndexes.Add(realIndex.ToString());
                                }

                                var predicate = new analysis.SpectrumList_FilterPredicate_IndexSet(String.Join(" ", spectrumFileIndexes.ToArray()));
                                using (var sl4 = new SpectrumList_Filter(sl3, predicate))
                                {
                                    msd.run.spectrumList = sl4;

                                    string tempFilepath = Path.GetTempFileName() + ".mzML.gz";
                                    MSDataFile.write(msd, tempFilepath, new MSDataFile.WriteConfig() { gzipped = true });
                                    byte[] msdataBytes = File.ReadAllBytes(tempFilepath);
                                    File.Delete(tempFilepath);

                                    bytesParam.Value = msdataBytes;
                                    idParam.Value = Id;
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // TODO: log a warning
                        }
                    }
                }
            }
        }

        bool OnInitializingParserProgress (Exception ex)
        {
            if (ParsingProgress != null)
            {
                var eventArgs = new ParsingProgressEventArgs()
                {
                    ParsedBytes = parsedBytes,
                    TotalBytes = totalBytes,
                    ParsingException = ex
                };
                ParsingProgress(this, eventArgs);
                if (ex != null && !eventArgs.Cancel)
                    throw ex;
            }
            else if (ex != null)
                throw ex;

            return false;
        }

        bool OnParsingProgress (Exception ex)
        {
            if (ParsingProgress != null)
            {
                var eventArgs = new ParsingProgressEventArgs()
                                    {
                                        ParsedBytes = parsedBytes,
                                        TotalBytes = totalBytes,
                                        ParsingStage = parsingStage,
                                        ParsingException = ex
                                    };
                ParsingProgress(this, eventArgs);
                return eventArgs.Cancel;
            }
            else if (ex != null)
                throw ex;

            return false;
        }

        static string lastProteinDatabasePathLocation = ".";
        string locateProteinDatabase (string proteinDatabasePath)
        {
            try
            {
                return Util.FindDatabaseInSearchPath(proteinDatabasePath, rootInputDirectory);
            }
            catch
            {
                try
                {
                    return Util.FindDatabaseInSearchPath(proteinDatabasePath, lastProteinDatabasePathLocation);
                }
                catch
                {
                    if (DatabaseNotFound != null)
                    {
                        var eventArgs = new DatabaseNotFoundEventArgs() {DatabasePath = proteinDatabasePath};
                        DatabaseNotFound(this, eventArgs);
                        if (File.Exists(eventArgs.DatabasePath))
                        {
                            lastProteinDatabasePathLocation = Path.GetDirectoryName(eventArgs.DatabasePath);
                            return eventArgs.DatabasePath;
                        }
                    }

                    throw;
                }
            }
        }

        static string lastSourcePathLocation = ".";
        string locateSpectrumSource (string spectrumSourceName)
        {
            try
            {
                return Util.FindSourceInSearchPath(spectrumSourceName, rootInputDirectory);
            }
            catch
            {
                try
                {
                    return Util.FindSourceInSearchPath(spectrumSourceName, lastSourcePathLocation);
                }
                catch
                {
                    if (SourceNotFound != null)
                    {
                        var eventArgs = new SourceNotFoundEventArgs() {SourcePath = spectrumSourceName};
                        SourceNotFound(this, eventArgs);
                        if (File.Exists(eventArgs.SourcePath) || Directory.Exists(eventArgs.SourcePath))
                        {
                            lastSourcePathLocation = Path.GetDirectoryName(eventArgs.SourcePath);
                            return eventArgs.SourcePath;
                        }
                    }

                    throw;
                }
            }
        }

        Set<string> proteinSet = new Set<string>();
        Dictionary<proteome.Protein, Digestion> pwizProteins;
        bool firstBatch = true;
        bool lastBatch = false;
        void addProteinAndPeptideInstances ()
        {
            //if (locus.Contains("IPI:IPI"))
            //    locus = locus.Split('|')[0];

            var workerThreads = new Queue<Thread>();

            try
            {
                foreach (ProteomeData proteomeData in proteomeDataFiles.Values)
                {
                    ProteinList pl = proteomeData.proteinList;

                    int batchSize = 80000;
                    int numBatches = (int) Math.Ceiling(pl.size()/(double) batchSize);

                    parsingStage = "Populating peptide instances...";
                    //totalBytes = xmlFilepathList.Sum(o => new FileInfo(Path.ChangeExtension(o, ".idpDB")).Length) * numBatches;
                    totalBytes *= numBatches;
                    parsedBytes = 0;

                    if (OnParsingProgress(null))
                        return;

                    for (int batch = 0; batch < numBatches; ++batch)
                    {
                        var proteinBatch = new Dictionary<proteome.Protein, Digestion>();

                        for (int i = batch * batchSize, end = Math.Min(i + batchSize, pl.size()); i < end; ++i)
                        {
                            var pwizProtein = pl.protein(i);

                            if ((pwizProtein.index % 1000) == 0)
                            {
                                if (OnParsingProgress(null))
                                    return;
                                System.Windows.Forms.Application.DoEvents();
                            }

                            proteinBatch[pwizProtein] = new Digestion(pwizProtein,
                                                                      CVID.MS_Trypsin_P,
                                                                      new Digestion.Config(int.MaxValue,
                                                                                           0,
                                                                                           int.MaxValue,
                                                                                           Digestion.Specificity.NonSpecific));
                        }

                        while (workerThreads.Count > 0)
                        {
                            var thread = workerThreads.Dequeue();
                            while (thread.IsAlive)
                            {
                                thread.Join(200);
                                System.Windows.Forms.Application.DoEvents();
                            }
                        }

                        pwizProteins = new Dictionary<proteome.Protein, Digestion>(proteinBatch);

                        firstBatch = proteomeData == proteomeDataFiles.Values.First() &&
                                     batch == 0;
                        lastBatch = proteomeData == proteomeDataFiles.Values.Last() &&
                                    batch + 1 >= Math.Ceiling(pl.size() / (double) batchSize);

                        if (OnParsingProgress(null))
                            return;

                        xmlFilepathQueue = new Queue<string>(xmlFilepathList);

                        //for (int t = 0; t < Environment.ProcessorCount; ++t)
                            workerThreads.Enqueue(new Thread(doAddProteinAndPeptideInstances));

                        foreach (Thread thread in workerThreads)
                            thread.Start();
                    }

                    while (workerThreads.Count > 0)
                    {
                        var thread = workerThreads.Dequeue();
                        while (thread.IsAlive)
                        {
                            thread.Join(200);
                            System.Windows.Forms.Application.DoEvents();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnParsingProgress(ex);
            }
        }

        void doAddProteinAndPeptideInstances ()
        {
            try
            {
                while (true)
                {
                    string idpDbFilepath = null;
                    lock (queueMutex)
                    {
                        if (xmlFilepathQueue.Count == 0)
                            return;
                        string xmlFilepath = xmlFilepathQueue.Dequeue();
                        idpDbFilepath = Path.ChangeExtension(xmlFilepath, ".idpDB");
                        parsedBytes += new FileInfo(xmlFilepath).Length;
                    }

                    var conn = new SQLiteConnection("Data Source=" + idpDbFilepath);
                    conn.Open();
                    conn.ExecuteNonQuery("PRAGMA journal_mode=OFF;" +
                                         "PRAGMA synchronous=OFF");

                    if(firstBatch)
                        try { conn.ExecuteNonQuery("DELETE FROM Protein; DELETE FROM ProteinMetadata; DELETE FROM ProteinData;" +
                                                   "DELETE FROM PeptideInstance");}
                        catch {}

                    var peptideBySequence = new Dictionary<string, Peptide>();
                    foreach (var queryRow in conn.ExecuteQuery("SELECT Sequence, Id FROM PeptideSequences"))
                        peptideBySequence[(string) queryRow[0]] = new Peptide { Id = (long) queryRow[1] };

                    var peptideSearchTree = new Util.StringSearch(peptideBySequence.Keys);

                    var bulkInserter = new BulkInserter(idpDbFilepath);

                    long proteinLastId = conn.ExecuteQuery("SELECT IFNULL(MAX(Id),0) FROM Protein").First().GetInt64(0);
                    long peptideLastId = conn.ExecuteQuery("SELECT IFNULL(MAX(Id),0) FROM Peptide").First().GetInt64(0);
                    long piLastId = conn.ExecuteQuery("SELECT IFNULL(MAX(Id),0) FROM PeptideInstance").First().GetInt64(0);

                    int counter = 0;
                    foreach (var proteomePair in pwizProteins)
                    {
                        var dbProtein = new Protein(proteomePair.Key.description, proteomePair.Key.sequence)
                                            {
                                                Id = ++proteinLastId,
                                                Accession = proteomePair.Key.id,
                                            };

                        var instances = peptideSearchTree.FindAll(dbProtein.Sequence);

                        if ((++counter % 1000) == 0)
                            if (OnParsingProgress(null))
                                return;

                        if (instances.Count() == 0)
                            continue;

                        bulkInserter.Add(dbProtein);

                        lock (dbProteinsMutex)
                        {
                            proteinSet.Add(dbProtein.Accession);
                            proteinCount = proteinSet.Count;
                        }

                        foreach (Util.StringSearchResult instance in instances)
                        {
                            using (proteome.Peptide wtf = new proteome.Peptide(instance.Keyword))
                            using (DigestedPeptide pwizInstance = proteomePair.Value.find_first(wtf, instance.Index))
                            {
                                var peptideInstance = new PeptideInstance()
                                                          {
                                                              Id = ++piLastId,
                                                              Peptide = peptideBySequence[instance.Keyword],
                                                              Protein = dbProtein,
                                                              Offset = pwizInstance.offset(),
                                                              Length = instance.Keyword.Length,
                                                              NTerminusIsSpecific = pwizInstance.NTerminusIsSpecific(),
                                                              CTerminusIsSpecific = pwizInstance.CTerminusIsSpecific(),
                                                              MissedCleavages = pwizInstance.missedCleavages()
                                                          };

                                bulkInserter.Add(peptideInstance);
                            }
                        }
                    }

                    bulkInserter.Execute();

                    if (lastBatch)
                    {
                        conn.ExecuteNonQuery("DROP TABLE IF EXISTS PeptideSequences");
                        //conn.ExecuteNonQuery("UPDATE PeptideSpectrumMatches SET QValue = 2");

                        // another preqonversion is run with the correct terminal specificities
                        /*var qonverter = new IDPicker.StaticWeightQonverter();
                        qonverter.DecoyPrefix = DecoyPrefix;
                        qonverter.ScoreWeights["mvh"] = 1;
                        //qonverter.ScoreWeights["mzFidelity"] = 1;
                        qonverter.Qonvert(xmlFilepath + ".idpDB");*/
                    }

                    conn.Close();
                }
            }
            catch (Exception ex)
            {
                OnParsingProgress(ex);
            }
        }

        private class PeptideInstanceComparer : EqualityComparer<PeptideInstance>
        {
            public override bool Equals (PeptideInstance x, PeptideInstance y)
            {
                return x.Offset == y.Offset &&
                       x.Length == y.Length &&
                       x.Protein.Accession == y.Protein.Accession;
            }

            public override int GetHashCode (PeptideInstance obj)
            {
                return obj.Offset.GetHashCode() ^
                       obj.Length.GetHashCode() ^
                       obj.Protein.Accession.GetHashCode();
            }
        }

        #region getAttribute convenience functions
        private static string getAttribute (XmlReader reader, string attribute)
        {
            return getAttributeAs<string>(reader, attribute);
        }

        private static string getAttribute (XmlReader reader, string attribute, bool throwIfAbsent)
        {
            return getAttributeAs<string>(reader, attribute, throwIfAbsent);
        }

        private static T getAttributeAs<T> (XmlReader reader, string attribute)
        {
            return getAttributeAs<T>(reader, attribute, false);
        }

        private static T getAttributeAs<T> (XmlReader reader, string attribute, bool throwIfAbsent)
        {
            if (reader.MoveToAttribute(attribute))
            {
                TypeConverter c = TypeDescriptor.GetConverter(typeof(T));
                if (c == null || !c.CanConvertFrom(typeof(string)))
                    throw new Exception("unable to convert from string to " + typeof(T).Name);
                T value = (T) c.ConvertFromString(reader.Value);
                reader.MoveToElement();
                return value;
            }
            else if (throwIfAbsent)
                throw new Exception("missing required attribute \"" + attribute + "\"");
            else if (typeof(T) == typeof(string))
                return (T) TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(String.Empty);
            else
                return default(T);
        }
        #endregion

        #region SQLite convenience functions
        public System.Data.IDbCommand createSQLiteInsertCommand (System.Data.IDbConnection conn, string table, int parameterCount)
        {
            var parameterPlaceholders = new List<string>();
            for (int i = 0; i < parameterCount; ++i) parameterPlaceholders.Add("?");
            var parameterPlaceholdersStr = String.Join(",", parameterPlaceholders.ToArray());
            var insertCommand = conn.CreateCommand();
            insertCommand.CommandText = String.Format("INSERT INTO {0} VALUES({1})", table, parameterPlaceholdersStr);
            for (int i = 0; i < parameterCount; ++i)
                insertCommand.Parameters.Add(new SQLiteParameter());
            return insertCommand;
        }

        public void executeSQLiteInsertCommand (System.Data.IDbCommand cmd, IList<object[]> rows)
        {
            foreach (object[] row in rows)
                executeSQLiteInsertCommand(cmd, row);
        }

        public void executeSQLiteInsertCommand (System.Data.IDbCommand cmd, object[] row)
        {
            for (int i = 0; i < row.Length; ++i)
                (cmd.Parameters[i] as System.Data.IDbDataParameter).Value = row[i];
            cmd.ExecuteScalar();
        }
        #endregion

        #region IDisposable Members

        public void Dispose ()
        {
        }

        #endregion
    }
}