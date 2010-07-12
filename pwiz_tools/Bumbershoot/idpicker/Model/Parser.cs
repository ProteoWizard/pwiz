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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Xml;
using System.Text;
using System.Data.SQLite;
using System.Linq;

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

        public readonly static string DefaultDecoyPrefix = "rev_";

        public string IdpDbFilepath { get; protected set; }
        public string DecoyPrefix { get; set; }

        public Parser (string idpDbFilepath)
        {
            IdpDbFilepath = idpDbFilepath;
            DecoyPrefix = DefaultDecoyPrefix;
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

        public void ReadXml (IEnumerable<string> xmlFilepaths, string rootInputDirectory)
        {
            #region Manually curated sets of model entities
            IList<SpectrumSourceGroup> dbGroups;
            IList<SpectrumSource> dbSources;
            IList<SpectrumSourceGroupLink> dbSourceGroupLinks;
            IList<Analysis> dbAnalyses;

            var dbSpectra = new Dictionary<int, Spectrum>(); // Spectrum.Index -> Spectrum
            var dbProteins = new Dictionary<string, Protein>(); // Protein.Accession -> Protein
            var dbPeptides = new Dictionary<string, Peptide>(); // Peptide.Sequence -> Peptide
            var dbPeptideInstances = new Dictionary<PeptideInstance, bool>(new PeptideInstanceComparer());
            var dbModifications = new Dictionary<double, Modification>();

            int lastSpectrumId = 0;
            int apCount = 0;
            int psmCount = 0;
            int pmCount = 0;
            #endregion

            int currentMaxProteinLength = 0;

            #region Create a new IDPicker database or load data from an existing database
            var sessionFactory = SessionFactoryFactory.CreateSessionFactory(IdpDbFilepath, !File.Exists(IdpDbFilepath), false);
            {
                var session2 = sessionFactory.OpenSession();

                dbGroups = session2.QueryOver<SpectrumSourceGroup>().List();
                dbSources = session2.QueryOver<SpectrumSource>().List();
                dbSourceGroupLinks = session2.QueryOver<SpectrumSourceGroupLink>().List();
                dbAnalyses = session2.QueryOver<Analysis>().List();

                foreach (var protein in session2.QueryOver<Protein>().List())
                    dbProteins.Add(protein.Accession, protein);

                foreach (var peptide in session2.QueryOver<Peptide>().List())
                    dbPeptides.Add(peptide.Sequence, peptide);

                foreach (var peptideInstance in session2.QueryOver<PeptideInstance>().List())
                    dbPeptideInstances.Add(peptideInstance, true);

                foreach (var modification in session2.QueryOver<Modification>().List())
                    dbModifications[modification.MonoMassDelta] = modification;

                lastSpectrumId = (int) session2.CreateQuery("SELECT MAX(Id) FROM Spectrum").UniqueResult<long>();
                apCount = session2.QueryOver<AnalysisParameter>().RowCount();
                psmCount = session2.QueryOver<PeptideSpectrumMatch>().RowCount();
                pmCount = session2.QueryOver<PeptideModification>().RowCount();

                currentMaxProteinLength = (int) session2.CreateQuery("SELECT MAX(Length) FROM Protein").UniqueResult<long>();
                session2.Close();
            }

            //var memoryFactory = SessionFactoryFactory.CreateSessionFactory(":memory:", true, false);
            var session = sessionFactory.OpenStatelessSession();
            #endregion

            long parsedBytes = 0;
            long totalBytes = xmlFilepaths.Sum(o => new FileInfo(o).Length);

            string proteinDatabasePath = "";
            string lastProteinDatabasePathLocation = Directory.GetCurrentDirectory();
            string lastSourcePathLocation = Directory.GetCurrentDirectory();

            pwiz.CLI.proteome.ProteomeDataFile pd = null;

            int maxProteinLength = currentMaxProteinLength;

            foreach (string xmlFilepath in xmlFilepaths)
            {
                var sourceXml = new StreamReader(xmlFilepath);

                #region Determine input file type and protein database used; also locate database and open it
                FileType fileType;
                string oldProteinDatabasePath = proteinDatabasePath;
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

                    if (databaseFilepath == null)
                        throw;
                }

                // don't open the database if it's already open
                if(pd == null || oldProteinDatabasePath != proteinDatabasePath)
                    pd = new pwiz.CLI.proteome.ProteomeDataFile(databaseFilepath);
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

                var bulkInserter = new BulkInserter();

                #region Current object references, used to share information within the XML hierarchy
                int curId = 0;
                SpectrumSourceGroup curGroup = null;
                SpectrumSource curSource = null;
                Spectrum curSpectrum = null;
                Peptide curPeptide = null;
                PeptideSpectrumMatch curPSM = null;
                Analysis curAnalysis = null;
                string curProcessingEventType = null;
                Map<int, double> curMods = null; // map mod positions to masses
                int curCharge = 0;
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
                                    double neutralPrecursorMass = curPSM.Spectrum.PrecursorMZ * curPSM.Charge - (curPSM.Charge * Proton.Mass);

                                    curPeptide = dbPeptides[pep];

                                    curPSM.Peptide = curPeptide;
                                    curPSM.MonoisotopicMass = curPeptide.MonoisotopicMass;
                                    curPSM.MolecularWeight = curPeptide.MolecularWeight;

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
                                            curPSM = new PeptideSpectrumMatch(curPSM)
                                            {
                                                Id = ++psmCount,
                                            };

                                            string[] modInfoStrs = ambiguousLocations[i].Split(' ');

                                            #region Add mods from this interpretation
                                            foreach (string modInfoStr in modInfoStrs)
                                            {
                                                string[] modPosMassPair = modInfoStr.Split(":".ToCharArray());
                                                string modPosStr = modPosMassPair[0];

                                                double modMass = Convert.ToDouble(modPosMassPair[1]);
                                                curPSM.MonoisotopicMass += modMass;
                                                curPSM.MolecularWeight += modMass;

                                                Modification mod;
                                                if (!dbModifications.TryGetValue(modMass, out mod))
                                                {
                                                    mod = new Modification()
                                                    {
                                                        Id = dbModifications.Count + 1,
                                                        MonoMassDelta = modMass,
                                                        AvgMassDelta = modMass,
                                                    };
                                                    dbModifications[modMass] = mod;
                                                    bulkInserter.Modifications.Add(mod);
                                                }

                                                int offset;
                                                if (modPosStr == "n")
                                                    offset = int.MinValue;
                                                else if (modPosStr == "c")
                                                    offset = int.MaxValue;
                                                else
                                                    offset = Convert.ToInt32(modPosStr);

                                                var pm = new PeptideModification()
                                                {
                                                    Id = ++pmCount,
                                                    PeptideSpectrumMatch = curPSM,
                                                    Modification = mod,
                                                    Offset = offset
                                                };

                                                bulkInserter.PeptideModifications.Add(pm);
                                            }
                                            #endregion

                                            curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                            curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                            bulkInserter.PeptideSpectrumMatches.Add(curPSM);
                                        }
                                        #endregion
                                    }
                                    else
                                    {
                                        // add the unmodified PSM
                                        curPSM = new PeptideSpectrumMatch(curPSM)
                                        {
                                            Id = ++psmCount,
                                        };

                                        curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                        curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                        bulkInserter.PeptideSpectrumMatches.Add(curPSM);
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
                                            Id = ++lastSpectrumId,
                                            Index = index,
                                            NativeID = nativeID,
                                            Source = curSource,
                                            PrecursorMZ = (neutralPrecursorMass + Proton.Mass * z) / z
                                        };

                                        //byte[] peakListBytes = null;

                                        if (curSourceData != null)
                                        {
                                            int realIndex = curSpectrumList.find(nativeID);
                                            if (realIndex != curSpectrumList.size())
                                                curSpectraIndexSubset.Add(realIndex);
                                        }

                                        bulkInserter.Spectra.Add(curSpectrum);
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
                                #region <result rank="1" FDR="0.1">
                                else if (tag == "result")
                                {
                                    curPSM.Rank = getAttributeAs<int>(reader, "rank", true);
                                    curPSM.QValue = getAttributeAs<float>(reader, "FDR", true);
                                    // TODO: getAttribute(reader, "scores", true);
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

                                        string sequence = getAttribute(reader, "sequence");
                                        string description = getAttribute(reader, "description");

                                        if (String.IsNullOrEmpty(sequence))
                                        {
                                            int index = proteinList.find(locus);
                                            pwiz.CLI.proteome.Protein pro = proteinList.protein(index);
                                            sequence = pro.sequence;
                                        }

                                        Protein curProtein = dbProteins[locus] = new Protein()
                                        {
                                            Id = dbProteins.Count + 1,
                                            Accession = locus,
                                            Description = description,
                                            Sequence = sequence
                                        };

                                        maxProteinLength = Math.Max(sequence.Length, maxProteinLength);

                                        bulkInserter.Proteins.Add(curProtein);
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

                                        curPeptide = dbPeptides[sequence] = new Peptide(sequence)
                                        {
                                            Id = dbPeptides.Count + 1,
                                            Instances = new List<PeptideInstance>(),
                                            MonoisotopicMass = pep.monoisotopicMass(),
                                            MolecularWeight = pep.molecularWeight()
                                        };

                                        //pep.unique = Convert.ToBoolean(getAttributeAs<int>(reader, "unique"));
                                        //Convert.ToBoolean(getAttributeAs<int>(reader, "NTerminusIsSpecific", true));
                                        //Convert.ToBoolean(getAttributeAs<int>(reader, "CTerminusIsSpecific", true));

                                        bulkInserter.Peptides.Add(curPeptide);
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
                                    var peptideInstance = new PeptideInstance()
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
                                        bulkInserter.PeptideInstances.Add(peptideInstance);
                                        curPeptide.Instances.Add(peptideInstance);
                                    }
                                }
                                #endregion
                                #region Initialize protein/peptide indexes
                                else if (tag == "proteinIndex")
                                {
                                    proteinIndex = new string[getAttributeAs<int>(reader, "count", true) + 1];
                                    proteinIndex.Initialize();
                                }
                                else if (tag == "peptideIndex")
                                {
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
                                        dbGroups.Add(new SpectrumSourceGroup()
                                        {
                                            Id = dbGroups.Count + 1,
                                            Name = groupName
                                        });

                                        curGroup = dbGroups.Last();

                                        bulkInserter.SpectrumSourceGroups.Add(curGroup);
                                    }
                                    else
                                        curGroup = groupQuery.First();

                                    string sourceName = getAttribute(reader, "name", true);

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
                                            //URL = source.filepath,
                                            //Spectra = new List<Spectrum>(),
                                            Group = curGroup
                                        });

                                        curSource = dbSources.Last();

                                        bulkInserter.SpectrumSources.Add(curSource);

                                        dbSourceGroupLinks.Add(new SpectrumSourceGroupLink()
                                        {
                                            Id = dbSourceGroupLinks.Count + 1,
                                            Group = curGroup,
                                            Source = curSource
                                        });

                                        bulkInserter.SpectrumSourceGroupLinks.Add(dbSourceGroupLinks.Last());

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

                                                    bulkInserter.SpectrumSourceGroups.Add(dbGroups.Last());
                                                }

                                                dbSourceGroupLinks.Add(new SpectrumSourceGroupLink()
                                                {
                                                    Id = dbSourceGroupLinks.Count + 1,
                                                    Group = dbGroups.First(o => o.Name == parentGroupName),
                                                    Source = curSource
                                                });

                                                bulkInserter.SpectrumSourceGroupLinks.Add(dbSourceGroupLinks.Last());

                                                if (parentGroupName == "/")
                                                    break;
                                                parentGroupName = parentGroupName.Substring(0, parentGroupName.LastIndexOf("/"));
                                            }
                                        }
                                    }
                                    else
                                    {
                                        curSource = sourceQuery.Single();

                                        foreach (var spectrum in curSource.Spectra)
                                            dbSpectra[spectrum.Index] = spectrum;
                                    }

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
                                        curAnalysis = new Analysis();
                                        curAnalysis.Parameters = new Iesi.Collections.Generic.SortedSet<AnalysisParameter>();

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
                                        curAnalysis.Software = new AnalysisSoftware();
                                        curAnalysis.Software.Name = paramValue;
                                    }
                                    else if (paramName == "software version")
                                    {
                                        curAnalysis.Software.Version = paramValue;
                                        curAnalysis.Name = curAnalysis.Software.Name + " " + curAnalysis.Software.Version;
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
                                    #region Create subset source mzML
                                    if (bulkInserter.SpectrumSources.Count > 0 &&
                                        bulkInserter.SpectrumSources.Last().Name == curSource.Name &&
                                        curSourceData != null &&
                                        curSpectraIndexSubset.Count > 0)
                                    {
                                        curSourceData.run.spectrumList = new pwiz.CLI.analysis.SpectrumList_Filter(curSpectrumList,
                                            delegate(pwiz.CLI.msdata.Spectrum s)
                                            {
                                                return curSpectraIndexSubset.Contains(s.index);
                                            });

                                        // overwrite the existing curSource with a new one with the metadata
                                        curSource = bulkInserter.SpectrumSources[bulkInserter.SpectrumSources.Count - 1] =
                                            new SpectrumSource(curSourceData)
                                            {
                                                Id = curSource.Id,
                                                Name = curSource.Name,
                                                URL = curSource.URL,
                                                Group = curSource.Group,
                                            };
                                    }
                                    #endregion

                                    bulkInserter.Execute(session.Connection);
                                    bulkInserter.Reset();
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
                                        curAnalysis.Type = AnalysisType.DatabaseSearch;

                                        bulkInserter.Analyses.Add(curAnalysis);
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
                                    #region Get current peptide (from dbPeptides map if possible)
                                    string sequence = getAttribute(reader, "peptide");
                                    if (!dbPeptides.TryGetValue(sequence, out curPeptide))
                                    {
                                        using (pwiz.CLI.proteome.Peptide pep = new pwiz.CLI.proteome.Peptide(sequence))
                                        {
                                            curPeptide = dbPeptides[sequence] = new Peptide(sequence)
                                            {
                                                Id = dbPeptides.Count + 1,
                                                MonoisotopicMass = pep.monoisotopicMass(),
                                                MolecularWeight = pep.molecularWeight(),
                                                Instances = new List<PeptideInstance>()
                                            };
                                        }

                                        //pep.unique = Convert.ToBoolean(getAttributeAs<int>(reader, "unique"));
                                        //bool NTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "NTerminusIsSpecific", true));
                                        //bool CTerminusIsSpecific = Convert.ToBoolean(getAttributeAs<int>(reader, "CTerminusIsSpecific", true));

                                        bulkInserter.Peptides.Add(curPeptide);
                                    }
                                    #endregion

                                    curPSM = new PeptideSpectrumMatch()
                                    {
                                        Id = ++psmCount,
                                        Spectrum = curSpectrum,
                                        Analysis = curAnalysis,
                                        Charge = curCharge,
                                        Scores = new Dictionary<string, double>()
                                    };

                                    curPSM.Peptide = curPeptide;
                                    curPSM.Rank = getAttributeAs<int>(reader, "hit_rank");
                                    curPSM.MonoisotopicMass = curPSM.Peptide.MonoisotopicMass;
                                    curPSM.MolecularWeight = curPSM.Peptide.MolecularWeight;
                                    curPSM.QValue = double.PositiveInfinity; // default value

                                    string locus = getAttribute(reader, "protein");
                                    string offset = getAttribute(reader, "peptide_offset");

                                    addProteinAndPeptideInstances(session, dbProteins, dbPeptideInstances,
                                                                  proteinList, curPeptide, bulkInserter,
                                                                  locus, offset, ref maxProteinLength);
                                }
                                #endregion
                                #region <alternative_protein protein="ABCD" />
                                else if (tag == "alternative_protein")
                                {
                                    string locus = getAttribute(reader, "protein");
                                    string offset = getAttribute(reader, "peptide_offset");

                                    addProteinAndPeptideInstances(session, dbProteins, dbPeptideInstances,
                                                                  proteinList, curPeptide, bulkInserter,
                                                                  locus, offset, ref maxProteinLength);
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

                                    if (!dbSpectra.TryGetValue(index, out curSpectrum))
                                    {
                                        double neutralPrecursorMass = getAttributeAs<double>(reader, "precursor_neutral_mass", true);

                                        curSpectrum = dbSpectra[index] = new Spectrum()
                                        {
                                            Id = ++lastSpectrumId,
                                            Index = index,
                                            NativeID = nativeID,
                                            Source = curSource,
                                            PrecursorMZ = (neutralPrecursorMass + Proton.Mass * z) / z
                                        };

                                        if (curSourceData != null)
                                        {
                                            int realIndex = curSpectrumList.find(nativeID);
                                            if (realIndex != curSpectrumList.size())
                                                curSpectraIndexSubset.Add(realIndex);
                                        }

                                        bulkInserter.Spectra.Add(curSpectrum);
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
                                        Software = new AnalysisSoftware()
                                        {
                                            Name = getAttribute(reader, "search_engine")
                                        },
                                        Parameters = new Iesi.Collections.Generic.SortedSet<AnalysisParameter>()
                                    };

                                    #region Get root group (from dbGroups if possible)
                                    string groupName = "/";
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

                                        bulkInserter.SpectrumSourceGroups.Add(curGroup);
                                    }
                                    else
                                        curGroup = groupQuery.Single();
                                    #endregion

                                    // reset dbSpectra
                                    dbSpectra = new Dictionary<int, Spectrum>();

                                    #region Get current spectrum source (from dbSources if possible)
                                    string sourceName = getAttribute(reader, "base_name", true);
                                    var sourceQuery = from s in dbSources
                                                      where s.Name == sourceName
                                                      select s;

                                    if (sourceQuery.Count() == 0)
                                    {
                                        dbSources.Add(new SpectrumSource()
                                        {
                                            Id = dbSources.Count + 1,
                                            Name = sourceName,
                                            //URL = source.filepath,
                                            Group = curGroup
                                        });

                                        curSource = dbSources.Last();

                                        bulkInserter.SpectrumSources.Add(curSource);

                                        dbSourceGroupLinks.Add(new SpectrumSourceGroupLink()
                                                                {
                                                                    Id = dbSourceGroupLinks.Count + 1,
                                                                    Group = curGroup,
                                                                    Source = curSource
                                                                });

                                        bulkInserter.SpectrumSourceGroupLinks.Add(dbSourceGroupLinks.Last());
                                    }
                                    else
                                    {
                                        curSource = sourceQuery.Single();

                                        dbSpectra = new Dictionary<int, Spectrum>();
                                        foreach (var spectrum in curSource.Spectra)
                                            dbSpectra[spectrum.Index] = spectrum;
                                    }
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
                                    double neutralPrecursorMass = curPSM.Spectrum.PrecursorMZ * curPSM.Charge - (curPSM.Charge * Proton.Mass);

                                    curPSM.MonoisotopicMassError = neutralPrecursorMass - curPSM.MonoisotopicMass;
                                    curPSM.MolecularWeightError = neutralPrecursorMass - curPSM.MolecularWeight;

                                    bulkInserter.PeptideSpectrumMatches.Add(curPSM);
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

                                            bulkInserter.Modifications.Add(mod);
                                        }

                                        curPSM.MonoisotopicMass += mass;
                                        curPSM.MolecularWeight += mass;

                                        var pm = new PeptideModification()
                                                {
                                                    Id = ++pmCount,
                                                    PeptideSpectrumMatch = curPSM,
                                                    Modification = mod,
                                                    Offset = position
                                                };
                                        bulkInserter.PeptideModifications.Add(pm);
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
                                        curAnalysis.Type = AnalysisType.DatabaseSearch;

                                        bulkInserter.Analyses.Add(curAnalysis);
                                    }
                                    else
                                        curAnalysis = analysisQuery.Single();
                                }
                                #endregion
                                #region </msms_run_summary>: create subset source mzML and add the current rows to the database
                                else if (tag == "msms_run_summary")
                                {
                                    #region Create subset source mzML
                                    if (bulkInserter.SpectrumSources.Count > 0 &&
                                        bulkInserter.SpectrumSources.Last() == curSource &&
                                        curSourceData != null &&
                                        curSpectraIndexSubset.Count > 0)
                                    {
                                        curSourceData.run.spectrumList = new pwiz.CLI.analysis.SpectrumList_Filter(curSpectrumList,
                                            delegate(pwiz.CLI.msdata.Spectrum s)
                                            {
                                                return curSpectraIndexSubset.Contains(s.index);
                                            });

                                        // overwrite the existing curSource with a new one with the metadata
                                        curSource = bulkInserter.SpectrumSources[bulkInserter.SpectrumSources.Count - 1] =
                                            new SpectrumSource(curSourceData)
                                            {
                                                Id = curSource.Id,
                                                Name = curSource.Name,
                                                URL = curSource.URL,
                                                Group = curSource.Group,
                                            };
                                    }
                                    #endregion

                                    bulkInserter.Execute(session.Connection);
                                    bulkInserter.Reset();
                                }
                                #endregion
                                break;
                        } // switch
                    } // while
                    #endregion
                }
            }

            #region Add an integer set from [0, maxProteinLength)
            string connectionString = new SQLiteConnectionStringBuilder() { DataSource = IdpDbFilepath }.ToString();
            using (var db = new System.Data.SQLite.SQLiteConnection(connectionString))
            {
                db.Open();
                var transaction = db.BeginTransaction();

                var createIntegerSetTable = new SQLiteCommand("CREATE TABLE IntegerSet (Value INTEGER PRIMARY KEY)", db);
                try { createIntegerSetTable.ExecuteNonQuery(); }
                catch { }

                var integerInsert = createSQLiteInsertCommand(db, "IntegerSet", 1);
                var integerRows = new List<object[]>();
                for (long i = currentMaxProteinLength; i < maxProteinLength; ++i)
                    integerRows.Add(new object[] { i });

                executeSQLiteInsertCommand(integerInsert, integerRows);
                currentMaxProteinLength = maxProteinLength;

                transaction.Commit();
            }
            #endregion
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

        private void addProteinAndPeptideInstances (NHibernate.IStatelessSession session,
                                                    Dictionary<string, Protein> dbProteins,
                                                    Dictionary<PeptideInstance, bool> dbPeptideInstances,
                                                    pwiz.CLI.proteome.ProteinList proteinList,
                                                    Peptide curPeptide,
                                                    BulkInserter bulkInserter,
                                                    string locus,
                                                    string offsetString,
                                                    ref int maxProteinLength)
        {
            if (locus.Contains("IPI:IPI"))
                locus = locus.Split('|')[0];

            int offset;
            if (!int.TryParse(offsetString, out offset))
                offset = -1;

            Protein curProtein;
            if (!dbProteins.TryGetValue(locus, out curProtein))
            {
                //pro.isDecoy = Convert.ToBoolean(getAttributeAs<int>(reader, "decoy"));
                string sequence = String.Empty;
                string description = String.Empty;

                int index = proteinList.find(locus);
                using (pwiz.CLI.proteome.Protein pro = proteinList.protein(index))
                {
                    sequence = pro.sequence;
                    description = pro.description;
                }

                curProtein = dbProteins[locus] = new Protein()
                {
                    Id = dbProteins.Count,
                    Accession = locus,
                    Description = description,
                    Sequence = sequence
                };

                maxProteinLength = Math.Max(sequence.Length, maxProteinLength);

                bulkInserter.Proteins.Add(curProtein);
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
                var peptideInstance = new PeptideInstance()
                {
                    Id = dbPeptideInstances.Count + 1,
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
                    curPeptide.Instances.Add(peptideInstance);
                    bulkInserter.PeptideInstances.Add(peptideInstance);
                }
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