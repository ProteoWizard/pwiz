//
// $Id: $
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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Xml;
using System.Text;
using System.Data.SQLite;
using System.Linq;

using IDPicker;
using NHibernate;
using NHibernate.Linq;
using pwiz.CLI.chemistry;
using pwiz.CLI.proteome;

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
        }
        #endregion

        public enum FileType
        {
            Unknown,
            PepXML,
            IdpXML
        }

        public void IdentifyXml(StreamReader xmlStream, out FileType fileType, out string proteinDatabasePath)
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
                        }
                    }

                    if (fileType == FileType.IdpXML && !foundDatabase)
                    {
                        // old idpXML, look for database in spectraSources
                        while (!foundDatabase && reader.Read())
                        {
                            if (reader.Name == "processingParam" &&
                                getAttribute(reader, "name") == "ProteinDatabase")
                            {
                                proteinDatabasePath = Path.GetFileName(getAttribute(reader, "value"));
                                foundDatabase = true;
                            }
                        }
                    }
                }
            }
        }

        public void ReadXml (IEnumerable<string> xmlFilepaths, string sqlitePath, string rootInputDirectory)
        {
            #region Manually curated sets of model entities
            IList<DataModel.SpectrumSourceGroup> dbGroups;
            IList<DataModel.SpectrumSource> dbSources;
            IList<DataModel.SpectrumSourceGroupLink> dbSourceGroupLinks;
            IList<DataModel.Analysis> dbAnalyses;

            var dbSpectra = new Dictionary<object[], DataModel.Spectrum>();
            var dbProteins = new Dictionary<string, DataModel.Protein>();
            var dbPeptides = new Dictionary<string, DataModel.Peptide>();
            var dbPeptideInstances = new Dictionary<DataModel.PeptideInstance, bool>(new PeptideInstanceComparer());
            var dbModifications = new Dictionary<double, DataModel.Modification>();

            int apCount = 0;
            int psmCount = 0;
            int pmCount = 0;
            #endregion

            long currentMaxProteinLength = 0;

            #region Create a new IDPicker database or load data from an existing database
            using (var sessionFactory = DataModel.SessionFactoryFactory.CreateSessionFactory(sqlitePath, !File.Exists(sqlitePath), false))
            using (var session = sessionFactory.OpenSession())
            {
                try { session.CreateSQLQuery("CREATE TABLE ProteinClusters (ProteinId INTEGER PRIMARY KEY, ClusterId INT)").ExecuteUpdate(); } catch { }
                try { session.CreateSQLQuery("CREATE TABLE ProteinGroups (ProteinId INTEGER PRIMARY KEY, ProteinGroup TEXT)").ExecuteUpdate(); } catch { }

                dbGroups = session.QueryOver<DataModel.SpectrumSourceGroup>().List();
                dbSources = session.QueryOver<DataModel.SpectrumSource>().List();
                dbSourceGroupLinks = session.QueryOver<DataModel.SpectrumSourceGroupLink>().List();
                dbAnalyses = session.QueryOver<DataModel.Analysis>().List();

                foreach (var protein in session.QueryOver<DataModel.Protein>().List())
                    dbProteins.Add(protein.Accession, protein);

                foreach (var peptide in session.QueryOver<DataModel.Peptide>().List())
                    dbPeptides.Add(peptide.Sequence, peptide);

                foreach (var peptideInstance in session.QueryOver<DataModel.PeptideInstance>().List())
                    dbPeptideInstances.Add(peptideInstance, true);

                foreach (var spectrum in session.QueryOver<DataModel.Spectrum>().List())
                    dbSpectra[new object[] { spectrum.Source, spectrum.NativeID }] = spectrum;

                foreach (var modification in session.QueryOver<DataModel.Modification>().List())
                    dbModifications[modification.MonoMassDelta] = modification;

                apCount = session.QueryOver<DataModel.AnalysisParameter>().RowCount();
                psmCount = session.QueryOver<DataModel.PeptideSpectrumMatch>().RowCount();
                pmCount = session.QueryOver<DataModel.PeptideModification>().RowCount();

                currentMaxProteinLength = session.CreateQuery("SELECT MAX(Length) FROM Protein").UniqueResult<long>();
            }
            #endregion

            long parsedBytes = 0;
            long totalBytes = xmlFilepaths.Sum(o => new FileInfo(o).Length);

            string proteinDatabasePath;
            string lastProteinDatabasePathLocation = Directory.GetCurrentDirectory();
            string lastSourcePathLocation = Directory.GetCurrentDirectory();

            foreach (string xmlFilepath in xmlFilepaths)
            {
                var sourceXml = new StreamReader(xmlFilepath);

                #region Determine input file type and protein database used; also locate database and open it
                FileType fileType;
                IdentifyXml(sourceXml, out fileType, out proteinDatabasePath);
                string databaseFilepath = null;
                try
                {
                    databaseFilepath = Util.FindDatabaseInSearchPath(proteinDatabasePath, rootInputDirectory);
                }
                catch
                {
                    try
                    {
                        databaseFilepath = Util.FindDatabaseInSearchPath(proteinDatabasePath, lastProteinDatabasePathLocation);
                    }
                    catch
                    {
                        if (DatabaseNotFound != null)
                        {
                            var eventArgs = new DatabaseNotFoundEventArgs() { DatabasePath = proteinDatabasePath };
                            DatabaseNotFound(this, eventArgs);
                            if (File.Exists(eventArgs.DatabasePath))
                            {
                                lastProteinDatabasePathLocation = Path.GetDirectoryName(eventArgs.DatabasePath);
                                databaseFilepath = eventArgs.DatabasePath;
                            }
                        }
                    }

                    if(databaseFilepath == null)
                        throw;
                }

                var pd = new pwiz.CLI.proteome.ProteomeDataFile(databaseFilepath);
                var proteinList = pd.proteinList;
                #endregion

                #region Initialize the XmlReader
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.None;
                settings.ValidationFlags = System.Xml.Schema.XmlSchemaValidationFlags.None;
                settings.IgnoreProcessingInstructions = true;
                settings.ProhibitDtd = false;
                settings.XmlResolver = null;

                sourceXml.BaseStream.Seek(0, SeekOrigin.Begin);
                sourceXml.DiscardBufferedData();
                XmlReader reader = XmlTextReader.Create(sourceXml, settings);
                #endregion

                #region Declare model data rows to be inserted into the database
                var proteinRows = new List<object[]>();
                var peptideRows = new List<object[]>();
                var peptideInstanceRows = new List<object[]>();
                var spectrumSourceGroupRows = new List<object[]>();
                var spectrumSourceRows = new List<object[]>();
                var spectrumSourceGroupLinkRows = new List<object[]>();
                var spectrumRows = new List<object[]>();
                var analysisRows = new List<object[]>();
                var apRows = new List<object[]>();
                var modificationRows = new List<object[]>();
                var psmRows = new List<object[]>();
                var psmScoreRows = new List<object[]>();
                var pmRows = new List<object[]>();
                #endregion

                long maxProteinLength = currentMaxProteinLength;

                #region Current object references, used to share information within the XML hierarchy
                int curId = 0;
                DataModel.SpectrumSourceGroup curGroup = null;
                DataModel.SpectrumSource curSource = null;
                DataModel.Spectrum curSpectrum = null;
                DataModel.Peptide curPeptide = null;
                DataModel.PeptideSpectrumMatch curPSM = null;
                DataModel.Analysis curAnalysis = null;
                string curProcessingEventType = null;
                Map<int, double> curMods = null; // map mod positions to masses
                Map<string, int> curProteins = null; // map protein accessions to offsets for the current peptide
                #endregion

                pwiz.CLI.msdata.MSData curSourceData = null;
                pwiz.CLI.msdata.SpectrumList curSpectrumList = null;
                Set<int> curSpectraIndexSubset = null;

                string tag;
                long lastStatusUpdatePosition = 0;
                long baseStreamLength = sourceXml.BaseStream.Length;

                if (fileType == FileType.IdpXML)
                {
                    #region idpXML reading

                    string[] proteinIndex = null;
                    string[] peptideIndex = null;

                    while (reader.Read())
                    {
                        #region Send ParsingProgress event (but read at least 100kb between updates)
                        if (ParsingProgress != null)
                        {
                            long position = sourceXml.BaseStream.Position;
                            if (position > lastStatusUpdatePosition + 100000 || position == baseStreamLength)
                            {
                                parsedBytes += position - lastStatusUpdatePosition;
                                lastStatusUpdatePosition = position;
                                var eventArgs = new ParsingProgressEventArgs() { ParsedBytes = parsedBytes, TotalBytes = totalBytes };
                                ParsingProgress(this, eventArgs);
                                if (eventArgs.Cancel)
                                    return;
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
                                    double neutralPrecursorMass = curPSM.Spectrum.PrecursorMZ * curPSM.Charge - (curPSM.Charge * pwiz.CLI.chemistry.Proton.Mass);

                                    curPSM.Peptide = dbPeptides[pep];

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
                                            curPSM.Id = ++psmCount;
                                            curPSM.MonoisotopicMass = curPSM.Peptide.MonoisotopicMass;
                                            curPSM.MolecularWeight = curPSM.Peptide.MolecularWeight;

                                            string[] modInfoStrs = ambiguousLocations[i].Split(' ');

                                            #region Add mods from this interpretation
                                            foreach (string modInfoStr in modInfoStrs)
                                            {
                                                string[] modPosMassPair = modInfoStr.Split(":".ToCharArray());
                                                string modPosStr = modPosMassPair[0];

                                                double modMass = Convert.ToDouble(modPosMassPair[1]);
                                                curPSM.MonoisotopicMass += modMass;
                                                curPSM.MolecularWeight += modMass;
                                                curPSM.Modifications = new List<DataModel.PeptideModification>();

                                                DataModel.Modification mod;
                                                if (!dbModifications.TryGetValue(modMass, out mod))
                                                {
                                                    mod = new DataModel.Modification()
                                                    {
                                                        Id = dbModifications.Count + 1,
                                                        MonoMassDelta = modMass,
                                                        AvgMassDelta = modMass,
                                                    };
                                                    dbModifications[modMass] = mod;

                                                    modificationRows.Add(new object[]
                                                    {
                                                        mod.Id,
                                                        mod.MonoMassDelta,
                                                        mod.AvgMassDelta,
                                                        mod.Formula,
                                                        mod.Name
                                                    });
                                                }

                                                var peptideModification = new DataModel.PeptideModification()
                                                {
                                                    Id = ++pmCount,
                                                    PeptideSpectrumMatch = curPSM,
                                                    Modification = mod
                                                };

                                                curPSM.Modifications.Add(peptideModification);

                                                int offset;
                                                char site;
                                                if (modPosStr == "n")
                                                {
                                                    offset = int.MinValue;
                                                    site = '(';
                                                }
                                                else if (modPosStr == "c")
                                                {
                                                    offset = int.MaxValue;
                                                    site = ')';
                                                }
                                                else
                                                {
                                                    offset = Convert.ToInt32(modPosStr);
                                                    site = pep[offset - 1];
                                                }

                                                pmRows.Add(new object[]
                                                {
                                                    peptideModification.Id,
                                                    peptideModification.PeptideSpectrumMatch.Id,
                                                    peptideModification.Modification.Id,
                                                    offset,
                                                    site.ToString()
                                                });
                                            }
                                            #endregion

                                            curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                            curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                            psmRows.Add(new object[]
                                            {
                                                curPSM.Id,
                                                curPSM.Spectrum.Id,
                                                curPSM.Analysis.Id,
                                                curPSM.Peptide.Id,
                                                curPSM.QValue,
                                                curPSM.MonoisotopicMass,
                                                curPSM.MolecularWeight,
                                                curPSM.MonoisotopicMassError,
                                                curPSM.MolecularWeightError,
                                                curPSM.Rank,
                                                curPSM.Charge
                                            });
                                        }
                                        #endregion
                                    }
                                    else
                                    {
                                        // add the unmodified PSM
                                        curPSM.Id = ++psmCount;
                                        curPSM.MonoisotopicMass = curPSM.Peptide.MonoisotopicMass;
                                        curPSM.MolecularWeight = curPSM.Peptide.MolecularWeight;
                                        curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                        curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                        psmRows.Add(new object[]
                                        {
                                            curPSM.Id,
                                            curPSM.Spectrum.Id,
                                            curPSM.Analysis.Id,
                                            curPSM.Peptide.Id,
                                            curPSM.QValue,
                                            curPSM.MonoisotopicMass,
                                            curPSM.MolecularWeight,
                                            curPSM.MonoisotopicMassError,
                                            curPSM.MolecularWeightError,
                                            curPSM.Rank,
                                            curPSM.Charge
                                        });
                                    }
                                }
                                #endregion
                                #region <spectrum id="614" nativeID="614" index="196" z="1" mass="569.32" time="16.7" targets="82" decoys="0" results="1">
                                else if (tag == "spectrum")
                                {
                                    int index = getAttributeAs<int>(reader, "index", true);
                                    string nativeID = getAttribute(reader, "id");
                                    int z = getAttributeAs<int>(reader, "z", true);

                                    object[] sourceIdPair = new object[] { curSource, nativeID };

                                    if (!dbSpectra.TryGetValue(sourceIdPair, out curSpectrum))
                                    {
                                        double neutralPrecursorMass = getAttributeAs<double>(reader, "mass", true);

                                        curSpectrum = dbSpectra[sourceIdPair] = new DataModel.Spectrum()
                                        {
                                            Id = dbSpectra.Count + 1,
                                            Index = index,
                                            NativeID = nativeID,
                                            PrecursorMZ = (neutralPrecursorMass + pwiz.CLI.chemistry.Proton.Mass * z) / z
                                        };

                                        //byte[] peakListBytes = null;

                                        if (curSourceData != null)
                                        {
                                            int realIndex = curSpectrumList.find(nativeID);
                                            if (realIndex != curSpectrumList.size())
                                                curSpectraIndexSubset.Add(realIndex);

                                            /*pwiz.CLI.msdata.Spectrum s = curSpectrumList.spectrum(realIndex, true);
                                            var mzArray = s.getMZArray().data;
                                            var intensityArray = s.getIntensityArray().data;

                                            var stream = new System.IO.MemoryStream();
                                            var writer = new System.IO.BinaryWriter(stream);
                                            for (int i = 0; i < mzArray.Count; ++i)
                                            {
                                                writer.Write(mzArray[i]);
                                                writer.Write(intensityArray[i]);
                                            }
                                            writer.Close();
                                            peakListBytes = stream.ToArray();*/
                                        }

                                        spectrumRows.Add(new object[]
                                    {
                                        dbSpectra.Count,
                                        curSource.Id,
                                        curSpectrum.Index,
                                        curSpectrum.NativeID,
                                        curSpectrum.PrecursorMZ,
                                        //peakListBytes
                                    });
                                    }

                                    curPSM = new DataModel.PeptideSpectrumMatch()
                                    {
                                        Spectrum = curSpectrum,
                                        Analysis = curAnalysis,
                                        Charge = z
                                    };

                                    //curSpectrum.retentionTime = getAttributeAs<float>(reader, "time");

                                    /*curSpectrum.numComparisons = getAttributeAs<int>(reader, "comparisons");
                                    curSpectrum.numComparisons += getAttributeAs<int>(reader, "targets");
                                    curSpectrum.numComparisons += getAttributeAs<int>(reader, "decoys");*/
                                }
                                #endregion
                                #region <result rank="1" FDR="0.1">
                                else if (tag == "result")
                                {
                                    curPSM.Rank = getAttributeAs<int>(reader, "rank", true);
                                    curPSM.QValue = getAttributeAs<float>(reader, "FDR", true);
                                    //getAttribute(reader, "scores", true);
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
                                        int index = proteinList.find(locus);
                                        pwiz.CLI.proteome.Protein pro = proteinList.protein(index);

                                        dbProteins[locus] = new IDPicker.DataModel.Protein()
                                        {
                                            Id = dbProteins.Count + 1,
                                            Accession = pro.id,
                                            Description = pro.description,
                                            Sequence = pro.sequence
                                        };

                                        maxProteinLength = Math.Max(pro.sequence.Length, maxProteinLength);

                                        proteinRows.Add(new object[]
                                    {
                                        dbProteins.Count,
                                        pro.id,
                                        pro.description.Replace("'", "''"),
                                        pro.sequence
                                    });
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
                                        pwiz.CLI.proteome.Peptide pep = new pwiz.CLI.proteome.Peptide(sequence);

                                        dbPeptides[sequence] = new DataModel.Peptide(sequence)
                                        {
                                            Id = dbPeptides.Count + 1,
                                            Instances = new List<DataModel.PeptideInstance>(),
                                            MonoisotopicMass = pep.monoisotopicMass(),
                                            MolecularWeight = pep.molecularWeight()
                                        };

                                        //pep.unique = Convert.ToBoolean(getAttributeAs<int>(reader, "unique"));
                                        bool NTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "NTerminusIsSpecific", true));
                                        bool CTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "CTerminusIsSpecific", true));

                                        peptideRows.Add(new object[]
                                    {
                                        dbPeptides.Count,
                                        pep.monoisotopicMass(),
                                        pep.molecularWeight()
                                    });
                                    }

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

                                    int offset = getAttributeAs<int>(reader, "offset", true);
                                    var peptideInstance = new IDPicker.DataModel.PeptideInstance()
                                                            {
                                                                Peptide = dbPeptides[pep],
                                                                Protein = dbProteins[pro],
                                                                Offset = offset,
                                                                Length = pep.Length,
                                                                NTerminusIsSpecific = true,
                                                                CTerminusIsSpecific = true,
                                                                MissedCleavages = 0
                                                            };

                                    if (!dbPeptideInstances.ContainsKey(peptideInstance))
                                    {
                                        dbPeptideInstances.Add(peptideInstance, true);
                                        peptideInstanceRows.Add(new object[]
                                    {
                                        dbPeptideInstances.Count,
                                        peptideInstance.Protein.Id,
                                        peptideInstance.Peptide.Id,
                                        offset,
                                        pep.Length,
                                        1, 1, 0 // TODO: GET REAL VALUES!!
                                    });
                                    }
                                }
                                #endregion
                                #region Initialize protein/peptide indexes
                                else if (tag == "proteinIndex")
                                {
                                    // Read protein index tag
                                    proteinIndex = new string[getAttributeAs<int>(reader, "count", true) + 1];
                                    proteinIndex.Initialize();

                                    string database = getAttribute(reader, "database");
                                    if (proteinDatabasePath != null && !String.IsNullOrEmpty(database) && database != proteinDatabasePath)
                                        Console.Error.WriteLine("warning: protein database should be the same in all input files");
                                    else if (!String.IsNullOrEmpty(database))
                                        proteinDatabasePath = database;

                                }
                                else if (tag == "peptideIndex")
                                {
                                    // Read peptide index tag
                                    peptideIndex = new string[getAttributeAs<int>(reader, "count", true) + 1];
                                    peptideIndex.Initialize();
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
                                        dbGroups.Add(new DataModel.SpectrumSourceGroup()
                                        {
                                            Id = dbGroups.Count + 1,
                                            Name = groupName
                                        });

                                        curGroup = dbGroups.Last();

                                        spectrumSourceGroupRows.Add(new object[]
                                {
                                    curGroup.Id,
                                    curGroup.Name
                                });
                                    }
                                    else
                                        curGroup = groupQuery.First();

                                    string sourceName = getAttribute(reader, "name", true);

                                    var sourceQuery = from s in dbSources
                                                      where s.Name == sourceName
                                                      select s;

                                    if (sourceQuery.Count() == 0)
                                    {
                                        dbSources.Add(new DataModel.SpectrumSource()
                                        {
                                            Id = dbSources.Count + 1,
                                            Name = sourceName,
                                            //URL = source.filepath,
                                            //Spectra = new List<DataModel.Spectrum>(),
                                            Group = curGroup
                                        });

                                        curSource = dbSources.Last();

                                        spectrumSourceRows.Add(new object[]
                                    {
                                        dbSources.Count,
                                        curSource.Name,
                                        curSource.URL,
                                        curGroup.Id,
                                        null // placeholder for gzipped mzML
                                    });

                                        dbSourceGroupLinks.Add(new DataModel.SpectrumSourceGroupLink()
                                        {
                                            Id = dbSourceGroupLinks.Count + 1,
                                            Group = curGroup,
                                            Source = curSource
                                        });

                                        spectrumSourceGroupLinkRows.Add(new object[]
                                    {
                                        dbSourceGroupLinks.Count,
                                        curSource.Id,
                                        curGroup.Id
                                    });

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
                                                    dbGroups.Add(new DataModel.SpectrumSourceGroup()
                                                    {
                                                        Id = dbGroups.Count + 1,
                                                        Name = parentGroupName
                                                    });

                                                    spectrumSourceGroupRows.Add(new object[]
                                                {
                                                    dbGroups.Last().Id,
                                                    dbGroups.Last().Name
                                                });
                                                }

                                                dbSourceGroupLinks.Add(new DataModel.SpectrumSourceGroupLink()
                                                {
                                                    Id = dbSourceGroupLinks.Count + 1,
                                                    Group = dbGroups.First(o => o.Name == parentGroupName),
                                                    Source = curSource
                                                });

                                                spectrumSourceGroupLinkRows.Add(new object[]
                                            {
                                                dbSourceGroupLinks.Count,
                                                curSource.Id,
                                                dbSourceGroupLinks.Last().Group.Id
                                            });

                                                if (parentGroupName == "/")
                                                    break;
                                                parentGroupName = parentGroupName.Substring(0, parentGroupName.LastIndexOf("/"));
                                            }
                                        }
                                    }
                                    else
                                        curSource = sourceQuery.First();

                                    curSourceData = null;
                                    curSpectrumList = null;
                                    curSpectraIndexSubset = null;

                                    #region Create subset spectrum list
                                    try
                                    {
                                        string sourcePath = Util.FindSourceInSearchPath(curSource.Name, rootInputDirectory);
                                        curSourceData = new pwiz.CLI.msdata.MSDataFile(sourcePath);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            string sourcePath = Util.FindSourceInSearchPath(curSource.Name, lastSourcePathLocation);
                                            curSourceData = new pwiz.CLI.msdata.MSDataFile(sourcePath);
                                        }
                                        catch
                                        {
                                            if (SourceNotFound != null)
                                            {
                                                var eventArgs = new SourceNotFoundEventArgs() { SourcePath = curSource.Name };
                                                SourceNotFound(this, eventArgs);
                                                if (File.Exists(eventArgs.SourcePath) || Directory.Exists(eventArgs.SourcePath))
                                                {
                                                    lastSourcePathLocation = Path.GetDirectoryName(eventArgs.SourcePath);
                                                    curSourceData = new pwiz.CLI.msdata.MSDataFile(eventArgs.SourcePath);
                                                }
                                            }
                                        }
                                    }

                                    if (curSourceData != null)
                                    {
                                        curSpectrumList = curSourceData.run.spectrumList;
                                        curSpectrumList = new pwiz.CLI.analysis.SpectrumList_PeakPicker(curSpectrumList,
                                            new pwiz.CLI.analysis.LocalMaximumPeakDetector(5), true, new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                                        curSpectrumList = new pwiz.CLI.analysis.SpectrumList_PeakFilter(curSpectrumList,
                                            new pwiz.CLI.analysis.ThresholdFilter(pwiz.CLI.analysis.ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_Count,
                                                                                  50,
                                                                                  pwiz.CLI.analysis.ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense));
                                        curSpectraIndexSubset = new Set<int>();
                                    }
                                    #endregion
                                }
                                #endregion
                                #region Processing events
                                else if (tag == "processingEvent")
                                {
                                    curProcessingEventType = getAttribute(reader, "type", true);

                                    if (curProcessingEventType == "identification")
                                    {
                                        curAnalysis = new DataModel.Analysis();
                                        curAnalysis.Parameters = new Iesi.Collections.Generic.SortedSet<DataModel.AnalysisParameter>();

                                        try
                                        {
                                            string LegacyTimeFormat = "MM/dd/yyyy@HH:mm:ss";
                                            curAnalysis.StartTime = DateTime.ParseExact(getAttribute(reader, "start"), LegacyTimeFormat, null);
                                            //DateTime.ParseExact(getAttribute(reader, "end"), TimeFormat, null);
                                        }
                                        catch { }
                                    }
                                }
                                else if (curProcessingEventType == "identification" && tag == "processingParam")
                                {
                                    string paramName = getAttribute(reader, "name", true);
                                    string paramValue = getAttribute(reader, "value", true);

                                    if (paramName == "software name")
                                    {
                                        curAnalysis.Software = new IDPicker.DataModel.AnalysisSoftware();
                                        curAnalysis.Software.Name = paramValue;
                                    }
                                    else if (paramName == "software version")
                                    {
                                        curAnalysis.Software.Version = paramValue;
                                        curAnalysis.Name = curAnalysis.Software.Name + " " + curAnalysis.Software.Version;
                                    }
                                    else if (!paramName.Contains(": "))
                                    {
                                        curAnalysis.Parameters.Add(new DataModel.AnalysisParameter()
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
                                    #region Create subset source mzML
                                    if (spectrumSourceRows.Count > 0 &&
                                        spectrumSourceRows.Last()[1] as string == curSource.Name &&
                                        curSourceData != null &&
                                        curSpectraIndexSubset.Count > 0)
                                    {
                                        curSourceData.run.spectrumList = new pwiz.CLI.analysis.SpectrumList_Filter(curSpectrumList,
                                            delegate(pwiz.CLI.msdata.Spectrum s)
                                            {
                                                return curSpectraIndexSubset.Contains(s.index);
                                            });

                                        string tempFilepath = Path.GetTempFileName() + ".mzML.gz";
                                        var writeConfig = new pwiz.CLI.msdata.MSDataFile.WriteConfig()
                                        {
                                            format = pwiz.CLI.msdata.MSDataFile.Format.Format_mzML,
                                            gzipped = true
                                        };

                                        pwiz.CLI.msdata.MSDataFile.write(curSourceData, tempFilepath, writeConfig);
                                        spectrumSourceRows.Last()[4] = File.ReadAllBytes(tempFilepath);
                                        File.Delete(tempFilepath);
                                    }
                                    #endregion

                                    // insert and commit changes on a per-source basis
                                    string connectionString = new SQLiteConnectionStringBuilder() { DataSource = sqlitePath }.ToString();
                                    using (var db = new System.Data.SQLite.SQLiteConnection(connectionString))
                                    {
                                        db.Open();
                                        var transaction = db.BeginTransaction();

                                        var spectrumSourceGroupInsert = createSQLiteInsertCommand(db, "SpectrumSourceGroups", spectrumSourceGroupRows.Count > 0 ? spectrumSourceGroupRows.First().Length : 0);
                                        var spectrumSourceInsert = createSQLiteInsertCommand(db, "SpectrumSources", spectrumSourceRows.Count > 0 ? spectrumSourceRows.First().Length : 0);
                                        var spectrumSourceGroupLinkInsert = createSQLiteInsertCommand(db, "SpectrumSourceGroupLinks", spectrumSourceGroupLinkRows.Count > 0 ? spectrumSourceGroupLinkRows.First().Length : 0);
                                        var spectrumInsert = createSQLiteInsertCommand(db, "Spectra", spectrumRows.Count > 0 ? spectrumRows.First().Length : 0);
                                        var analysisInsert = createSQLiteInsertCommand(db, "Analyses", analysisRows.Count > 0 ? analysisRows.First().Length : 0);
                                        var proteinInsert = createSQLiteInsertCommand(db, "Proteins", proteinRows.Count > 0 ? proteinRows.First().Length : 0);
                                        var peptideInsert = createSQLiteInsertCommand(db, "Peptides", peptideRows.Count > 0 ? peptideRows.First().Length : 0);
                                        var peptideInstanceInsert = createSQLiteInsertCommand(db, "PeptideInstances", peptideInstanceRows.Count > 0 ? peptideInstanceRows.First().Length : 0);
                                        var modificationInsert = createSQLiteInsertCommand(db, "Modifications", modificationRows.Count > 0 ? modificationRows.First().Length : 0);
                                        var apInsert = createSQLiteInsertCommand(db, "AnalysisParameters", apRows.Count > 0 ? apRows.First().Length : 0);
                                        var psmInsert = createSQLiteInsertCommand(db, "PeptideSpectrumMatches", psmRows.Count > 0 ? psmRows.First().Length : 0);
                                        var pmInsert = createSQLiteInsertCommand(db, "PeptideModifications", pmRows.Count > 0 ? pmRows.First().Length : 0);

                                        executeSQLiteInsertCommand(spectrumSourceGroupInsert, spectrumSourceGroupRows);
                                        executeSQLiteInsertCommand(spectrumSourceInsert, spectrumSourceRows);
                                        executeSQLiteInsertCommand(spectrumSourceGroupLinkInsert, spectrumSourceGroupLinkRows);
                                        executeSQLiteInsertCommand(spectrumInsert, spectrumRows);
                                        executeSQLiteInsertCommand(analysisInsert, analysisRows);
                                        executeSQLiteInsertCommand(proteinInsert, proteinRows);
                                        executeSQLiteInsertCommand(peptideInsert, peptideRows);
                                        executeSQLiteInsertCommand(peptideInstanceInsert, peptideInstanceRows);
                                        executeSQLiteInsertCommand(modificationInsert, modificationRows);
                                        executeSQLiteInsertCommand(apInsert, apRows);
                                        executeSQLiteInsertCommand(psmInsert, psmRows);
                                        executeSQLiteInsertCommand(pmInsert, pmRows);

                                        spectrumSourceGroupRows.Clear();
                                        spectrumSourceRows.Clear();
                                        spectrumSourceGroupLinkRows.Clear();
                                        spectrumRows.Clear();
                                        analysisRows.Clear();
                                        proteinRows.Clear();
                                        peptideRows.Clear();
                                        peptideInstanceRows.Clear();
                                        modificationRows.Clear();
                                        apRows.Clear();
                                        psmRows.Clear();
                                        pmRows.Clear();

                                        #region Add an integer set from [0, maxProteinLength)
                                        var createIntegerSetTable = new SQLiteCommand("CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY)", db);
                                        try { createIntegerSetTable.ExecuteNonQuery(); }
                                        catch { }

                                        var integerInsert = createSQLiteInsertCommand(db, "IntegerSet", 1);
                                        var integerRows = new List<object[]>();
                                        for (long i = currentMaxProteinLength; i < maxProteinLength; ++i)
                                            integerRows.Add(new object[] { i });

                                        executeSQLiteInsertCommand(integerInsert, integerRows);
                                        currentMaxProteinLength = maxProteinLength;
                                        #endregion

                                        transaction.Commit();
                                    }
                                }
                                #endregion
                                #region Determine if the current analysis is already in the database
                                else if (curProcessingEventType == "identification" && tag == "processingEvent")
                                {
                                    // an analysis is unique if its name is unique and its parameter set has some
                                    // difference with other analyses
                                    var analysisQuery = from a in dbAnalyses
                                                        where a.Name == curAnalysis.Name &&
                                                              a.Parameters.ExclusiveOr(curAnalysis.Parameters).Count == 0
                                                        select a;

                                    if (analysisQuery.Count() == 0)
                                    {
                                        dbAnalyses.Add(curAnalysis);
                                        curAnalysis.Id = dbAnalyses.Count;
                                        curAnalysis.Type = DataModel.AnalysisType.DatabaseSearch;

                                        analysisRows.Add(new object[]
                                                    {
                                                        curAnalysis.Id,
                                                        curAnalysis.Name,
                                                        curAnalysis.Software.Name,
                                                        curAnalysis.Software.Version,
                                                        curAnalysis.Type,
                                                        curAnalysis.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                                                    });

                                        foreach (var analysisParameter in curAnalysis.Parameters)
                                        {
                                            apRows.Add(new object[]
                                                    {
                                                        ++apCount,
                                                        curAnalysis.Id,
                                                        analysisParameter.Name,
                                                        analysisParameter.Value
                                                    });
                                        }
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

                    while (reader.Read())
                    {
                        #region Send ParsingProgress event (but read at least 100kb between updates)
                        if (ParsingProgress != null)
                        {
                            long position = sourceXml.BaseStream.Position;
                            if (position > lastStatusUpdatePosition + 100000 || position == baseStreamLength)
                            {
                                parsedBytes += position - lastStatusUpdatePosition;
                                lastStatusUpdatePosition = position;
                                var eventArgs = new ParsingProgressEventArgs() { ParsedBytes = parsedBytes, TotalBytes = totalBytes };
                                ParsingProgress(this, eventArgs);
                                if (eventArgs.Cancel)
                                    return;
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
                                    try
                                    {
                                        string name = getAttribute(reader, "name");
                                        double value = getAttributeAs<double>(reader, "value");
                                        psmScoreRows.Add(new object[]
                                                        {
                                                            curPSM.Id,
                                                            value,
                                                            name,
                                                        });
                                    }
                                    catch
                                    {
                                        // ignore non-numeric values
                                    }
                                }
                                #endregion
                                #region <search_hit hit_rank="1" peptide="QTSSM" peptide_prev_aa="R" peptide_next_aa="-" protein="rev_RPA0405" peptide_offset="476" num_tot_proteins="1" num_matched_ions="0" tot_num_ions="6" calc_neutral_pep_mass="123" massdiff="123" num_tol_term="2" num_missed_cleavages="0">
                                else if (tag == "search_hit")
                                {
                                    #region Get current peptide (from dbPeptides map if possible)
                                    string sequence = getAttribute(reader, "peptide");
                                    if (!dbPeptides.TryGetValue(sequence, out curPeptide))
                                    {
                                        pwiz.CLI.proteome.Peptide pep = new pwiz.CLI.proteome.Peptide(sequence);

                                        curPeptide = dbPeptides[sequence] = new DataModel.Peptide(sequence)
                                        {
                                            Id = dbPeptides.Count + 1,
                                            Instances = new List<DataModel.PeptideInstance>(),
                                            MonoisotopicMass = pep.monoisotopicMass(),
                                            MolecularWeight = pep.molecularWeight()
                                        };

                                        //pep.unique = Convert.ToBoolean(getAttributeAs<int>(reader, "unique"));
                                        //bool NTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "NTerminusIsSpecific", true));
                                        //bool CTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "CTerminusIsSpecific", true));

                                        peptideRows.Add(new object[]
                                                        {
                                                            curPeptide.Id,
                                                            curPeptide.MonoisotopicMass,
                                                            curPeptide.MolecularWeight
                                                        });
                                    }
                                    #endregion

                                    curPSM.Id = ++pmCount;
                                    curPSM.Peptide = curPeptide;
                                    curPSM.Rank = getAttributeAs<int>(reader, "hit_rank");
                                    curPSM.MonoisotopicMass = curPSM.Peptide.MonoisotopicMass;
                                    curPSM.MolecularWeight = curPSM.Peptide.MolecularWeight;

                                    curProteins = new Map<string, int>();

                                    string locus = getAttribute(reader, "protein");

                                    if (locus.Contains("IPI:IPI"))
                                        locus = locus.Split('|')[0];

                                    string offsetString = getAttribute(reader, "peptide_offset");
                                    int offset;
                                    if (int.TryParse(offsetString, out offset))
                                        curProteins[locus] = offset;
                                    else
                                        curProteins[locus] = -1;
                                }
                                #endregion
                                #region <alternative_protein protein="ABCD" />
                                else if (tag == "alternative_protein")
                                {
                                    string locus = getAttribute(reader, "protein");

                                    if (locus.Contains("IPI:IPI"))
                                        locus = locus.Split('|')[0];

                                    string offsetString = getAttribute(reader, "peptide_offset");
                                    int offset;
                                    if (int.TryParse(offsetString, out offset))
                                        curProteins[locus] = offset;
                                    else
                                        curProteins[locus] = -1;
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

                                    int z = getAttributeAs<int>(reader, "assumed_charge", true);

                                    object[] sourceIdPair = new object[] { curSource, nativeID };

                                    if (!dbSpectra.TryGetValue(sourceIdPair, out curSpectrum))
                                    {
                                        double neutralPrecursorMass = getAttributeAs<double>(reader, "precursor_neutral_mass", true);

                                        curSpectrum = dbSpectra[sourceIdPair] = new DataModel.Spectrum()
                                        {
                                            Id = dbSpectra.Count + 1,
                                            Index = index,
                                            NativeID = nativeID,
                                            PrecursorMZ = (neutralPrecursorMass + pwiz.CLI.chemistry.Proton.Mass * z) / z
                                        };

                                        if (curSourceData != null)
                                        {
                                            int realIndex = curSpectrumList.find(nativeID);
                                            if (realIndex != curSpectrumList.size())
                                                curSpectraIndexSubset.Add(realIndex);
                                        }

                                        spectrumRows.Add(new object[]
                                                        {
                                                            dbSpectra.Count,
                                                            curSource.Id,
                                                            curSpectrum.Index,
                                                            curSpectrum.NativeID,
                                                            curSpectrum.PrecursorMZ
                                                        });
                                    }

                                    curPSM = new DataModel.PeptideSpectrumMatch()
                                    {
                                        Spectrum = curSpectrum,
                                        Analysis = curAnalysis,
                                        Charge = z
                                    };

                                    //curSpectrum.retentionTime = getAttributeAs<float>(reader, "time");

                                    /*curSpectrum.numComparisons = getAttributeAs<int>(reader, "comparisons");
                                    curSpectrum.numComparisons += getAttributeAs<int>(reader, "targets");
                                    curSpectrum.numComparisons += getAttributeAs<int>(reader, "decoys");*/
                                }
                                #endregion
                                #region <search_summary base_name="abc" search_engine="MyriMatch" precursor_mass_type="monoisotopic" fragment_mass_type="monoisotopic" out_data_type="n/a" out_data="n/a" search_id="1">
                                else if (tag == "search_summary")
                                {
                                    curAnalysis = new DataModel.Analysis()
                                    {
                                        Software = new DataModel.AnalysisSoftware() 
                                        {
                                            Name = getAttribute(reader, "search_engine")
                                        },
                                        Parameters = new Iesi.Collections.Generic.SortedSet<DataModel.AnalysisParameter>()
                                    };

                                    #region Get root group (from dbGroups if possible)
                                    string groupName = "/";
                                    var groupQuery = from g in dbGroups
                                                     where g.Name == groupName
                                                     select g;

                                    if (groupQuery.Count() == 0)
                                    {
                                        dbGroups.Add(new DataModel.SpectrumSourceGroup()
                                        {
                                            Id = dbGroups.Count + 1,
                                            Name = groupName
                                        });

                                        curGroup = dbGroups.Last();

                                        spectrumSourceGroupRows.Add(new object[]
                                                                    {
                                                                        curGroup.Id,
                                                                        curGroup.Name
                                                                    });
                                    }
                                    else
                                        curGroup = groupQuery.Single();
                                    #endregion

                                    #region Get current spectrum source (from dbSources if possible)
                                    string sourceName = getAttribute(reader, "base_name", true);
                                    var sourceQuery = from s in dbSources
                                                      where s.Name == sourceName
                                                      select s;

                                    if (sourceQuery.Count() == 0)
                                    {
                                        dbSources.Add(new DataModel.SpectrumSource()
                                        {
                                            Id = dbSources.Count + 1,
                                            Name = sourceName,
                                            //URL = source.filepath,
                                            //Spectra = new List<DataModel.Spectrum>(),
                                            Group = curGroup
                                        });

                                        curSource = dbSources.Last();

                                        spectrumSourceRows.Add(new object[]
                                                                {
                                                                    dbSources.Count,
                                                                    curSource.Name,
                                                                    curSource.URL,
                                                                    curGroup.Id,
                                                                    null // placeholder for gzipped mzML
                                                                });

                                        dbSourceGroupLinks.Add(new DataModel.SpectrumSourceGroupLink()
                                                                {
                                                                    Id = dbSourceGroupLinks.Count + 1,
                                                                    Group = curGroup,
                                                                    Source = curSource
                                                                });

                                        spectrumSourceGroupLinkRows.Add(new object[]
                                                                        {
                                                                            dbSourceGroupLinks.Count,
                                                                            curSource.Id,
                                                                            curGroup.Id
                                                                        });
                                    }
                                    else
                                        curSource = sourceQuery.Single();
                                    #endregion

                                    curSourceData = null;
                                    curSpectrumList = null;
                                    curSpectraIndexSubset = null;

                                    #region Create subset spectrum list
                                    try
                                    {
                                        string sourcePath = Util.FindSourceInSearchPath(curSource.Name, rootInputDirectory);
                                        curSourceData = new pwiz.CLI.msdata.MSDataFile(sourcePath);
                                    }
                                    catch
                                    {
                                        try
                                        {
                                            string sourcePath = Util.FindSourceInSearchPath(curSource.Name, lastSourcePathLocation);
                                            curSourceData = new pwiz.CLI.msdata.MSDataFile(sourcePath);
                                        }
                                        catch
                                        {
                                            if (SourceNotFound != null)
                                            {
                                                var eventArgs = new SourceNotFoundEventArgs() { SourcePath = curSource.Name };
                                                SourceNotFound(this, eventArgs);
                                                if (File.Exists(eventArgs.SourcePath) || Directory.Exists(eventArgs.SourcePath))
                                                {
                                                    lastSourcePathLocation = Path.GetDirectoryName(eventArgs.SourcePath);
                                                    curSourceData = new pwiz.CLI.msdata.MSDataFile(eventArgs.SourcePath);
                                                }
                                            }
                                        }
                                    }

                                    if (curSourceData != null)
                                    {
                                        curSpectrumList = curSourceData.run.spectrumList;
                                        curSpectrumList = new pwiz.CLI.analysis.SpectrumList_PeakPicker(curSpectrumList,
                                            new pwiz.CLI.analysis.LocalMaximumPeakDetector(5), true, new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                                        curSpectrumList = new pwiz.CLI.analysis.SpectrumList_PeakFilter(curSpectrumList,
                                            new pwiz.CLI.analysis.ThresholdFilter(pwiz.CLI.analysis.ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_Count,
                                                                                  50,
                                                                                  pwiz.CLI.analysis.ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense));
                                        curSpectraIndexSubset = new Set<int>();
                                    }
                                    #endregion
                                }
                                else if (tag == "parameter")
                                {
                                    string paramName = getAttribute(reader, "name", true);
                                    string paramValue = getAttribute(reader, "value", true);

                                    if (paramName == "SearchEngine: Version")
                                    {
                                        curAnalysis.Software.Version = paramValue;
                                        curAnalysis.Name = curAnalysis.Software.Name + " " + curAnalysis.Software.Version;
                                    }
                                    else if(paramName == "SearchTime: Started")
                                    {
                                        curAnalysis.StartTime = DateTime.ParseExact(paramValue, "HH:mm:ss 'on' dd/MM/yyyy", System.Globalization.DateTimeFormatInfo.CurrentInfo);
                                    }
                                    else if(paramName.Contains("Config: "))
                                    {
                                        curAnalysis.Parameters.Add(new DataModel.AnalysisParameter()
                                                                    {
                                                                        Name = paramName.Substring(8),
                                                                        Value = paramValue
                                                                    });
                                    }
                                    else if (!paramName.Contains(": "))
                                    {
                                        curAnalysis.Parameters.Add(new DataModel.AnalysisParameter()
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

                                #region </search_hit>: add current PSM
                                if (tag == "search_hit")
                                {
                                    #region Add proteins and peptide instances

                                    foreach (var itr in curProteins)
                                    {
                                        string locus = itr.Key;
                                        int offset = itr.Value;

                                        DataModel.Protein curProtein;
                                        if (!dbProteins.TryGetValue(locus, out curProtein))
                                        {
                                            //pro.isDecoy = Convert.ToBoolean(getAttributeAs<int>(reader, "decoy"));
                                            int index = proteinList.find(locus);
                                            pwiz.CLI.proteome.Protein pro = proteinList.protein(index);

                                            curProtein = dbProteins[locus] = new DataModel.Protein()
                                            {
                                                Id = dbProteins.Count + 1,
                                                Accession = pro.id,
                                                Description = pro.description,
                                                Sequence = pro.sequence
                                            };

                                            maxProteinLength = Math.Max(pro.sequence.Length, maxProteinLength);

                                            proteinRows.Add(new object[]
                                                            {
                                                                dbProteins.Count,
                                                                pro.id,
                                                                pro.description.Replace("'", "''"),
                                                                pro.sequence
                                                            });
                                        }

                                        var peptideOffets = new List<int>();

                                        // if necessary, look up the real offset(s) of the peptide
                                        if (offset < 0 ||
                                            (offset + curPeptide.Sequence.Length - 1) >= curProtein.Sequence.Length ||
                                            curProtein.Sequence.Substring(offset, curPeptide.Sequence.Length) != curPeptide.Sequence)
                                        {
                                            int start = curProtein.Sequence.IndexOf(curPeptide.Sequence);
                                            do
                                            {
                                                peptideOffets.Add(start);
                                                start = curProtein.Sequence.IndexOf(curPeptide.Sequence, start + 1);
                                            }
                                            while (start >= 0);
                                        }
                                        else
                                            peptideOffets.Add(offset);

                                        foreach (int peptideOffset in peptideOffets)
                                        {
                                            var peptideInstance = new DataModel.PeptideInstance()
                                            {
                                                Peptide = curPeptide,
                                                Protein = curProtein,
                                                Offset = peptideOffset,
                                                Length = curPeptide.Sequence.Length,
                                                // TODO: GET REAL VALUES!!
                                                NTerminusIsSpecific = true,
                                                CTerminusIsSpecific = true,
                                                MissedCleavages = 0
                                            };

                                            if (!dbPeptideInstances.ContainsKey(peptideInstance))
                                            {
                                                dbPeptideInstances.Add(peptideInstance, true);
                                                peptideInstanceRows.Add(new object[]
                                                                        {
                                                                            dbPeptideInstances.Count,
                                                                            peptideInstance.Protein.Id,
                                                                            peptideInstance.Peptide.Id,
                                                                            peptideInstance.Offset,
                                                                            peptideInstance.Length,
                                                                            peptideInstance.NTerminusIsSpecific ? 1 : 0,
                                                                            peptideInstance.CTerminusIsSpecific ? 1 : 0,
                                                                            peptideInstance.MissedCleavages
                                                                        });
                                            }
                                        }
                                    }
                                    #endregion

                                    double neutralPrecursorMass = curPSM.Spectrum.PrecursorMZ * curPSM.Charge - (curPSM.Charge * pwiz.CLI.chemistry.Proton.Mass);

                                    curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                    curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                    psmRows.Add(new object[]
                                                {
                                                    curPSM.Id,
                                                    curPSM.Spectrum.Id,
                                                    curPSM.Analysis.Id,
                                                    curPSM.Peptide.Id,
                                                    curPSM.QValue,
                                                    curPSM.MonoisotopicMass,
                                                    curPSM.MolecularWeight,
                                                    curPSM.MonoisotopicMassError,
                                                    curPSM.MolecularWeightError,
                                                    curPSM.Rank,
                                                    curPSM.Charge
                                                });
                                }
                                #endregion
                                #region </modification_info>: add current modifications
                                else if (tag == "modification_info")
                                {
                                    foreach (var itr in curMods)
                                    {
                                        int position = itr.Key;
                                        double mass = itr.Value;

                                        DataModel.Modification mod;
                                        if (!dbModifications.TryGetValue(mass, out mod))
                                        {
                                            mod = dbModifications[mass] = new DataModel.Modification()
                                            {
                                                Id = dbModifications.Count + 1,
                                                MonoMassDelta = mass,
                                                AvgMassDelta = mass,
                                            };

                                            modificationRows.Add(new object[]
                                                                {
                                                                    mod.Id,
                                                                    mod.MonoMassDelta,
                                                                    mod.AvgMassDelta,
                                                                    mod.Formula,
                                                                    mod.Name
                                                                });
                                        }

                                        curPSM.MonoisotopicMass += mass;
                                        curPSM.MolecularWeight += mass;

                                        char site;
                                        if (position == int.MinValue)
                                            site = '(';
                                        else if (position == int.MaxValue)
                                            site = ')';
                                        else
                                            site = curPSM.Peptide.Sequence[position - 1];

                                        pmRows.Add(new object[]
                                                    {
                                                        ++pmCount,
                                                        curPSM.Id,
                                                        mod.Id,
                                                        position,
                                                        site.ToString()
                                                    });
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
                                                              a.Parameters.ExclusiveOr(curAnalysis.Parameters).Count == 0
                                                        select a;

                                    if (analysisQuery.Count() == 0)
                                    {
                                        dbAnalyses.Add(curAnalysis);
                                        curAnalysis.Id = dbAnalyses.Count;
                                        curAnalysis.Type = DataModel.AnalysisType.DatabaseSearch;

                                        analysisRows.Add(new object[]
                                                    {
                                                        curAnalysis.Id,
                                                        curAnalysis.Name,
                                                        curAnalysis.Software.Name,
                                                        curAnalysis.Software.Version,
                                                        curAnalysis.Type,
                                                        curAnalysis.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
                                                    });

                                        foreach (var analysisParameter in curAnalysis.Parameters)
                                        {
                                            apRows.Add(new object[]
                                                    {
                                                        ++apCount,
                                                        curAnalysis.Id,
                                                        analysisParameter.Name,
                                                        analysisParameter.Value
                                                    });
                                        }
                                    }
                                    else
                                        curAnalysis = analysisQuery.Single();
                                }
                                #endregion
                                #region </msms_run_summary>: create subset source mzML and add the current rows to the database
                                else if (tag == "msms_run_summary")
                                {
                                    #region Create subset source mzML
                                    if (spectrumSourceRows.Count > 0 &&
                                        spectrumSourceRows.Last()[1] as string == curSource.Name &&
                                        curSourceData != null &&
                                        curSpectraIndexSubset.Count > 0)
                                    {
                                        curSourceData.run.spectrumList = new pwiz.CLI.analysis.SpectrumList_Filter(curSpectrumList,
                                            delegate(pwiz.CLI.msdata.Spectrum s)
                                            {
                                                return curSpectraIndexSubset.Contains(s.index);
                                            });

                                        string tempFilepath = Path.GetTempFileName() + ".mzML.gz";
                                        var writeConfig = new pwiz.CLI.msdata.MSDataFile.WriteConfig()
                                        {
                                            format = pwiz.CLI.msdata.MSDataFile.Format.Format_mzML,
                                            gzipped = true
                                        };

                                        pwiz.CLI.msdata.MSDataFile.write(curSourceData, tempFilepath, writeConfig);
                                        spectrumSourceRows.Last()[4] = File.ReadAllBytes(tempFilepath);
                                        File.Delete(tempFilepath);
                                    }
                                    #endregion

                                    // insert and commit changes on a per-source basis
                                    string connectionString = new SQLiteConnectionStringBuilder() { DataSource = sqlitePath }.ToString();
                                    using (var db = new System.Data.SQLite.SQLiteConnection(connectionString))
                                    {
                                        db.Open();
                                        var transaction = db.BeginTransaction();

                                        var spectrumSourceGroupInsert = createSQLiteInsertCommand(db, "SpectrumSourceGroups", spectrumSourceGroupRows.Count > 0 ? spectrumSourceGroupRows.First().Length : 0);
                                        var spectrumSourceInsert = createSQLiteInsertCommand(db, "SpectrumSources", spectrumSourceRows.Count > 0 ? spectrumSourceRows.First().Length : 0);
                                        var spectrumSourceGroupLinkInsert = createSQLiteInsertCommand(db, "SpectrumSourceGroupLinks", spectrumSourceGroupLinkRows.Count > 0 ? spectrumSourceGroupLinkRows.First().Length : 0);
                                        var spectrumInsert = createSQLiteInsertCommand(db, "Spectra", spectrumRows.Count > 0 ? spectrumRows.First().Length : 0);
                                        var analysisInsert = createSQLiteInsertCommand(db, "Analyses", analysisRows.Count > 0 ? analysisRows.First().Length : 0);
                                        var proteinInsert = createSQLiteInsertCommand(db, "Proteins", proteinRows.Count > 0 ? proteinRows.First().Length : 0);
                                        var peptideInsert = createSQLiteInsertCommand(db, "Peptides", peptideRows.Count > 0 ? peptideRows.First().Length : 0);
                                        var peptideInstanceInsert = createSQLiteInsertCommand(db, "PeptideInstances", peptideInstanceRows.Count > 0 ? peptideInstanceRows.First().Length : 0);
                                        var modificationInsert = createSQLiteInsertCommand(db, "Modifications", modificationRows.Count > 0 ? modificationRows.First().Length : 0);
                                        var apInsert = createSQLiteInsertCommand(db, "AnalysisParameters", apRows.Count > 0 ? apRows.First().Length : 0);
                                        var psmInsert = createSQLiteInsertCommand(db, "PeptideSpectrumMatches", psmRows.Count > 0 ? psmRows.First().Length : 0);
                                        var psmScoresInsert = createSQLiteInsertCommand(db, "PeptideSpectrumMatchScores", psmScoreRows.Count > 0 ? psmScoreRows.First().Length : 0);
                                        var pmInsert = createSQLiteInsertCommand(db, "PeptideModifications", pmRows.Count > 0 ? pmRows.First().Length : 0);

                                        executeSQLiteInsertCommand(spectrumSourceGroupInsert, spectrumSourceGroupRows);
                                        executeSQLiteInsertCommand(spectrumSourceInsert, spectrumSourceRows);
                                        executeSQLiteInsertCommand(spectrumSourceGroupLinkInsert, spectrumSourceGroupLinkRows);
                                        executeSQLiteInsertCommand(spectrumInsert, spectrumRows);
                                        executeSQLiteInsertCommand(analysisInsert, analysisRows);
                                        executeSQLiteInsertCommand(proteinInsert, proteinRows);
                                        executeSQLiteInsertCommand(peptideInsert, peptideRows);
                                        executeSQLiteInsertCommand(peptideInstanceInsert, peptideInstanceRows);
                                        executeSQLiteInsertCommand(modificationInsert, modificationRows);
                                        executeSQLiteInsertCommand(apInsert, apRows);
                                        executeSQLiteInsertCommand(psmInsert, psmRows);
                                        executeSQLiteInsertCommand(psmScoresInsert, psmScoreRows);
                                        executeSQLiteInsertCommand(pmInsert, pmRows);

                                        spectrumSourceGroupRows.Clear();
                                        spectrumSourceRows.Clear();
                                        spectrumSourceGroupLinkRows.Clear();
                                        spectrumRows.Clear();
                                        analysisRows.Clear();
                                        proteinRows.Clear();
                                        peptideRows.Clear();
                                        peptideInstanceRows.Clear();
                                        modificationRows.Clear();
                                        apRows.Clear();
                                        psmRows.Clear();
                                        psmScoreRows.Clear();
                                        pmRows.Clear();

                                        #region Add an integer set from [0, maxProteinLength)
                                        var createIntegerSetTable = new SQLiteCommand("CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY)", db);
                                        try { createIntegerSetTable.ExecuteNonQuery(); }
                                        catch { }

                                        var integerInsert = createSQLiteInsertCommand(db, "IntegerSet", 1);
                                        var integerRows = new List<object[]>();
                                        for (long i = currentMaxProteinLength; i < maxProteinLength; ++i)
                                            integerRows.Add(new object[] { i });

                                        executeSQLiteInsertCommand(integerInsert, integerRows);
                                        currentMaxProteinLength = maxProteinLength;
                                        #endregion

                                        transaction.Commit();
                                    }
                                }
                                #endregion
                                break;
                        } // switch
                    } // while
                    #endregion
                }
            }
        }

        private class PeptideInstanceComparer : EqualityComparer<DataModel.PeptideInstance>
        {
            public override bool Equals (DataModel.PeptideInstance x, DataModel.PeptideInstance y)
            {
                return x.Offset == y.Offset &&
                       x.Length == y.Length &&
                       x.Protein.Accession == y.Protein.Accession;
            }

            public override int GetHashCode (DataModel.PeptideInstance obj)
            {
                return obj.Offset.GetHashCode() ^
                       obj.Length.GetHashCode() ^
                       obj.Protein.Accession.GetHashCode();
            }
        }

        #region getAttribute convenience functions
        private string getAttribute (XmlReader reader, string attribute)
        {
            return getAttributeAs<string>(reader, attribute);
        }

        private string getAttribute (XmlReader reader, string attribute, bool throwIfAbsent)
        {
            return getAttributeAs<string>(reader, attribute, throwIfAbsent);
        }

        private T getAttributeAs<T> (XmlReader reader, string attribute)
        {
            return getAttributeAs<T>(reader, attribute, false);
        }

        private T getAttributeAs<T> (XmlReader reader, string attribute, bool throwIfAbsent)
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
        public SQLiteCommand createSQLiteInsertCommand (SQLiteConnection conn, string table, int parameterCount)
        {
            var parameterPlaceholders = new List<string>();
            for (int i = 0; i < parameterCount; ++i) parameterPlaceholders.Add("?");
            var parameterPlaceholdersStr = String.Join(",", parameterPlaceholders.ToArray());
            var insertCommand = new SQLiteCommand(String.Format("INSERT INTO {0} VALUES({1})", table, parameterPlaceholdersStr), conn);
            for (int i = 0; i < parameterCount; ++i)
                insertCommand.Parameters.Add(new SQLiteParameter());
            return insertCommand;
        }

        public void executeSQLiteInsertCommand (SQLiteCommand cmd, IList<object[]> rows)
        {
            foreach (object[] row in rows)
                executeSQLiteInsertCommand(cmd, row);
        }

        public void executeSQLiteInsertCommand (SQLiteCommand cmd, object[] row)
        {
            for (int i = 0; i < row.Length; ++i)
                cmd.Parameters[i].Value = row[i];
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