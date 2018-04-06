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
// The Initial Developer of the DirecTag peptide sequence tagger is Matt Chambers.
// Contributor(s): Surendra Dasaris
//
// The Initial Developer of the ScanRanker GUI is Zeqiang Ma.
// Contributor(s): 
//
// Copyright 2009 Vanderbilt University
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
//using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.Windows.Forms;
//using IDPicker;
using pwiz.CLI;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;
using System.Data.SQLite;

namespace ScanRanker
{
    public class AddSpectraLabelAction
    {
        private ArrayList inFileList;
        public ArrayList InFileList
        {
            get { return inFileList; }
            set { inFileList = value; }
        }

        private string metricsFileSuffix;
        public string MetricsFileSuffix
        {
            get { return metricsFileSuffix; }
            set { metricsFileSuffix = value; }
        }

        //private string spectraFileName;
        //public string SpectraFileName
        //{
        //    get { return spectraFileName; }
        //    set { spectraFileName = value; }
        //}

        //private string metricsFileName;
        //public string MetricsFileName
        //{
        //    get { return metricsFileName; }
        //    set { metricsFileName = value; }
        //}

        private bool writeOutUnidentifiedSpectra;
        public bool WriteOutUnidentifiedSpectra
        {
            get { return writeOutUnidentifiedSpectra; }
            set { writeOutUnidentifiedSpectra = value; }
        }

        private float recoveryCutoff;
        public float RecoveryCutoff
        {
            get { return recoveryCutoff; }
            set { recoveryCutoff = value; }
        }
        private string recoveryOutFormat;
        public string RecoveryOutFormat
        {
            get { return recoveryOutFormat; }
            set { recoveryOutFormat = value; }
        }

        private IDPickerInfo idpCfg;
        public IDPickerInfo IdpCfg
        {
            get { return idpCfg; }
            set { idpCfg = value; }
        }
        private string outDir;
        public string OutDir
        {
            get { return outDir; }
            set { outDir = value; }
        }

        private string outFileSuffix;
        public string OutFileSuffix
        {
            get { return outFileSuffix; }
            set { outFileSuffix = value; }
        }

        /// <summary>
        /// run idpQonvert to create idpXML file to determine identified spectra
        /// </summary>
        /// <param name="idpCfg"></param>
        /// <param name="outDir"></param>
        private void runIdpQonvert(IDPickerInfo idpCfg, string outDir)
        {
            // run idpQonvert and create idpXML file
            string EXE = @"idpQonvert.exe";
            string BIN_DIR = Path.GetDirectoryName(Application.ExecutablePath);
            string pathAndExeFile = BIN_DIR + "\\" + EXE;
            // string outputFilename = Path.GetFileNameWithoutExtension(idpCfg.PepXMLFile) + ".idpXML";
            //// IDPicker 2 args
            //string args =
            //      " -MaxFDR " + idpCfg.MaxFDR
            //    + " -NormalizeSearchScores " + idpCfg.NormalizeSearchScores
            //    + " -OptimizeScoreWeights " + idpCfg.OptimizeScoreWeights
            //    + " -SearchScoreWeights " + "\"" + idpCfg.ScoreInfo + "\""
            //    + " -DecoyPrefix " + "\"" + idpCfg.DecoyPrefix + "\""
            //    + " -WriteQonversionDetails  0 "
            //    + " -ProteinDatabase " + "\"" + idpCfg.DBFile + "\"" + " "
            //    + idpCfg.PepXMLFiles; // each file enclosed by "..." and separated by a space
            //// IDPicker 3 args
            // MaxImportFDR equal to MaxFDR to reduce idpDB size
            string args =
                  " -MaxImportFDR " + idpCfg.MaxFDR
                + " -MaxFDR " + idpCfg.MaxFDR
                + " -QonverterMethod " + "\"" + idpCfg.QonverterMethod + "\"" 
                + " -ScoreInfo " + "\"" + idpCfg.ScoreInfo + "\""
                + " -DecoyPrefix " + "\"" + idpCfg.DecoyPrefix + "\""
                + " -WriteQonversionDetails  0 "
                + " -ProteinDatabase " + "\"" + idpCfg.DBFile + "\"" + " "
                + idpCfg.PepXMLFiles; // each file enclosed by "..." and separated by a space

           // Workspace.SetText("\r\nidpQonvert cmd\r\n" + pathAndExeFile + " " + args + "\r\n\r\n");
            try
            {
                //if (File.Exists(outputFilename))
                //{
                //    File.Delete(outputFilename);
                //}
                Workspace.SetText("\r\nidpQonvert command:\r\n" + pathAndExeFile + args + "\r\n");
                Workspace.RunProcess(pathAndExeFile, args, outDir);
            }
            catch (Exception exc)
            {
                //throw new Exception("Error running idpQonvert\r\n", exc);
                Workspace.SetText("\r\nError in running idpQonvert\r\n");
                Workspace.ChangeButtonTo("Close");
                throw new Exception(exc.Message);
            }

            //if (!File.Exists(outputFilename))
            //{
            //    throw new Exception("Error in running idpQonvert, no idpXML file generated");
            //}

        }

        /// <summary>
        /// get attribute from xml file, copied from IDPicker project 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reader"></param>
        /// <param name="attribute"></param>
        /// <param name="throwIfAbsent"></param>
        /// <returns></returns>
        private T getAttributeAs<T>(XmlTextReader reader, string attribute, bool throwIfAbsent)
        {
            if (reader.MoveToAttribute(attribute))
            {
                TypeConverter c = TypeDescriptor.GetConverter(typeof(T));
                if (c == null || !c.CanConvertFrom(typeof(string)))
                    throw new Exception("unable to convert from string to " + typeof(T).Name);
                T value = (T)c.ConvertFromString(reader.Value);
                reader.MoveToElement();
                return value;
            }
            else if (throwIfAbsent)
                throw new Exception("missing required attribute \"" + attribute + "\"");
            else if (typeof(T) == typeof(string))
                return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(String.Empty);
            else
                return default(T);
        }
        
        //private class SpectrumList_FilterPredicate_IndexSet
        //{
        //    public List<int> indexSet;
        //    public bool? accept(Spectrum s) { return indexSet.Contains(s.index); }
        //    public SpectrumList_FilterPredicate_IndexSet()
        //    {
        //        indexSet = new List<int>();
        //    }

        //}

        private class SpectrumList_FilterPredicate_NativeIDSet
        {
            public List<string> nativeIDSet;
            public bool? accept(Spectrum s)
            {
                if (String.IsNullOrEmpty(s.id)) return null;
                return nativeIDSet.Contains(s.id);
            }
            public SpectrumList_FilterPredicate_NativeIDSet()
            {
                nativeIDSet = new List<string>();
            }

        }

        private void writeUnidentifiedSpectra(string spectraFilename,
                                               string outFileSuffix,
                                               List<string> unidentifiedSpectra,
                                               float cutoff,
                                               string outFormat)
        {
            int numOutputSpectra = Convert.ToInt32(unidentifiedSpectra.Count * cutoff);
            //List<int> highQualIndices;
            List<string> highQualIndices;
            highQualIndices = unidentifiedSpectra.GetRange(0, numOutputSpectra);
  
            //var predicate = new SpectrumList_FilterPredicate_IndexSet();
            //foreach (int i in highQualIndices)
            //{
            //    predicate.indexSet.Add(i);
            //}

            var predicate = new SpectrumList_FilterPredicate_NativeIDSet();
            foreach (string i in highQualIndices)
            {
                predicate.nativeIDSet.Add(i);
            }

            //MSDataFile.WriteConfig writeConfig = new MSDataFile.WriteConfig(MSDataFile.Format.Format_mzXML);
            MSDataFile.WriteConfig writeConfig = new MSDataFile.WriteConfig();
            if (outFormat.Equals("mzXML") || outFormat.Equals("mzxml"))
            {
                writeConfig.format = MSDataFile.Format.Format_mzXML;
            }
            else if (outFormat.Equals("mzML") || outFormat.Equals("mzml"))
            {
                writeConfig.format = MSDataFile.Format.Format_mzML;
            }
            else if (outFormat.Equals("MGF") || outFormat.Equals("mgf"))
            {
                writeConfig.format = MSDataFile.Format.Format_MGF;
            }
            else if (outFormat.Equals("MS2") || outFormat.Equals("ms2"))
            {
                writeConfig.format = MSDataFile.Format.Format_MS2;
            }
            else
            {
                MessageBox.Show("Plese select output format");
            }

            writeConfig.precision = MSDataFile.Precision.Precision_32;

            try
            {
                string outFileName = Path.GetFileNameWithoutExtension(spectraFilename) + outFileSuffix + "-Top" + (recoveryCutoff*100).ToString()
                                        + "PercUnidentSpec." + recoveryOutFormat;
                Workspace.SetText("\r\nWriting unidentified high quality spectra to file: " + outFileName);
                using (MSDataFile msFile = new MSDataFile(spectraFilename))
                {
                    msFile.run.spectrumList = new SpectrumList_Filter(msFile.run.spectrumList, new SpectrumList_FilterAcceptSpectrum(predicate.accept));
                    msFile.write(outFileName, writeConfig);
                }
            }
            catch (Exception exc)
            {
                //throw new Exception("Error in writiing new spectra file", exc);
                Workspace.SetText("\r\nError in writing new spectra file\r\n");
                Workspace.ChangeButtonTo("Close");
                throw new Exception(exc.Message);
            }
        }


        public class ResultInstance
        {
            public string Mods;
            public string Peptide;
            public string Protein;

        }
        /// <summary>
        /// add spectra labels to ScanRanker metrics file
        /// identified spectra ids stored in idpXML file by idqQonvert 
        /// </summary>
        public void AddSpectraLabel()
        {
            // get pepXML files and run idpQonvert for all, so only read database once
            idpCfg.PepXMLFiles = "";
            foreach (FileInfo file in inFileList)
            {
                string fileBaseName = Path.GetFileNameWithoutExtension(file.Name);
                string pepXMLFile = idpCfg.PepXMLFileDir + "\\" + fileBaseName + ".pepXML";  // pepXML file has to be the same basename
                idpCfg.PepXMLFiles += "\"" + pepXMLFile + "\"" + " ";  // each file enclosed by "..." and separated by a space
                string idpDBFilename = fileBaseName + ".idpDB";  // pepXML file has to be the same basename as spectra filename
                if (File.Exists(idpDBFilename))
                {
                    File.Delete(idpDBFilename);
                    Workspace.SetText("\r\nRemoved existing idpDB file: " + idpDBFilename + "\n");
                }

            }
            //Workspace.SetText("\r\npepXMLFies\r\n" + idpCfg.PepXMLFiles + "\r\n\r\n");

            Workspace.SetText("\r\nRunning idpQonvert for peptide validation\r\n");
            try
            {
                runIdpQonvert(idpCfg, outDir);
            }
            catch (Exception exc)
            {
                Workspace.SetText("\r\nError in running idpQonvert\r\n");
                Workspace.ChangeButtonTo("Close");
                throw new Exception(exc.Message);
            }

            // use IDPickerWorkspace.dll to parse idpXML, get spectra->peptides->proteins info, work for IDP2
            // use System.Data.SQLite to read idpDB for IDP3, get spectra->peptides->proteins info
            // add labels, peptide and proteins to metrics file
            foreach (FileInfo file in inFileList)
            {
                string fileBaseName = Path.GetFileNameWithoutExtension(file.Name);
                //string idpXMLFilename = fileBaseName + ".idpXML";  // pepXML file has to be the same basename as spectra filename
                string idpDBFilename = fileBaseName + ".idpDB";  // pepXML file has to be the same basename as spectra filename

                if (!File.Exists(idpDBFilename))
                {
                    Workspace.SetText("\r\nError: Cannot find idpDB file: " + idpDBFilename + " in output directory!");
                    Workspace.SetText("\r\nPlease check IDPicker configurations and the database file");
                    Workspace.ChangeButtonTo("Close");
                    return;
                }

                // parse idpDB file
                Workspace.SetText("\r\nReading idpDB file: " + idpDBFilename);

                Map<string, ResultInstance> resultsMap = new Map<string, ResultInstance>();

                // Connect to database
                string dbConnectionString = "Data Source=" + idpDBFilename;
                using (SQLiteConnection sqliteCon = new SQLiteConnection(dbConnectionString))
                {
                    // Execute query on database
                    string querySQL = @"SELECT s.NativeID,  
                                    REPLACE(IFNULL(GROUP_CONCAT(DISTINCT pm.Offset || '=' || mod.MonoMassDelta),''),',',';') as Mods,
                                    IFNULL(IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length),
                                        (SELECT DecoySequence FROM Peptide p WHERE p.Id = pi.Peptide)),
                                        (SELECT SUBSTR(pd.Sequence, pi.Offset+1, pi.Length)
                                        FROM PeptideInstance pi 
                                        JOIN Protein pro ON pi.Protein = pro.Id AND pro.IsDecoy = 0
                                        LEFT JOIN ProteinData pd ON pi.Protein=pd.Id
                                        WHERE psm.Peptide = pi.Peptide
                                        UNION
                                        SELECT p.DecoySequence
                                        FROM Peptide p
                                        JOIN PeptideInstance pi ON p.Id = pi.Peptide
                                        JOIN Protein pro ON pi.Protein = pro.Id AND pro.IsDecoy = 1
                                        WHERE psm.Peptide = pi.Peptide AND p.DecoySequence is not null)) as Peptide,
                                    GROUP_CONCAT(pro.Accession) as Protein
                                    FROM PeptideSpectrumMatch psm 
                                    JOIN Spectrum s ON s.Id = psm.Spectrum
                                    JOIN SpectrumSource source ON s.Source = source.Id
                                    JOIN PeptideInstance pi ON psm.Peptide = pi.Peptide
                                    JOIN Protein pro ON pi.Protein = pro.Id
                                    LEFT JOIN ProteinData pd ON pi.Protein=pd.Id
                                    LEFT JOIN PeptideModification pm ON psm.Id = pm.PeptideSpectrumMatch
                                    LEFT JOIN Modification mod ON pm.Modification = mod.Id
                                    Where psm.Rank = 1 and psm.Qvalue < " + idpCfg.MaxFDR.ToString() +
                                        " GROUP BY psm.Id";

                    Workspace.SetText("\r\nExtracting identified spectra, peptides and proteins from " + idpDBFilename);
                    using (SQLiteCommand queryCommand = new SQLiteCommand(querySQL, sqliteCon))
                    {
                        sqliteCon.Open();
                        using (SQLiteDataReader dataReader = queryCommand.ExecuteReader())
                        {
                            // Iterate every record 
                            while (dataReader.Read())
                            {
                                Console.WriteLine(dataReader.GetString(0) + " " + dataReader.GetString(1) + " " + dataReader.GetString(2));
                                ResultInstance ri = new ResultInstance();
                                string nativeID = dataReader.GetString(0);
                                ri.Mods = dataReader.GetString(1);
                                ri.Peptide = dataReader.GetString(2);
                                ri.Protein = dataReader.GetString(3);
                                resultsMap.Add(nativeID, ri);

                            }
                        }
                    }
                }
                //dataReader.Close();
                //sqliteCon.Close();


                //IDPicker.Workspace ws = new IDPicker.Workspace();
                //using (StreamReader idpXMLStream = new StreamReader(idpDBFilename))
                //{
                //    ws.readPeptidesXml(idpXMLStream, "", (float)idpCfg.MaxFDR, 1);
                //}

                  
                //foreach (SourceGroupInfo group in ws.groups.Values)
                //    foreach (SourceInfo source in group.getSources())
                //        foreach (SpectrumInfo spectrum in source.spectra.Values)
                //        {
                //            ResultInstance ri = spectrum.results[1];
                //            resultsMap.Add(spectrum.nativeID, ri);                            
                //            //Workspace.SetText("\r\n" + spectrum.nativeID);
                //        }

                // remove idpDB file
                if (File.Exists(idpDBFilename))
                {
                    File.Delete(idpDBFilename);
                }
                Workspace.SetText("\r\nRemoved idpDB file: " + idpDBFilename + "\n");

                // read idpxml, extract spectra id, save to a dictionary
                /*Dictionary<string, int> idtScanDict = new Dictionary<string, int>();
     
                try
                {
                    using (XmlTextReader reader = new XmlTextReader(idpXMLFilename))
                    {
                        while (reader.Read())
                        {
                            if (reader.NodeType.Equals(XmlNodeType.Element) && reader.Name.Equals("spectrum"))
                            {
                                // Read the spectrum tag
                               //  <spectrum id="scan=3601" index="2834" z="2" mass="1466.6829557734375" time="33.856500000000004" targets="45327" decoys="45504" results="1">
                                string nativeID = getAttributeAs<string>(reader, "id", false);   // id in idpXML = nativeID in DirecTag
                                Match m = Regex.Match(nativeID, @"scan=(\d+)");
                                if (m.Success)
                                {
                                    nativeID = m.Groups[1].Value;                                
                                }
                                //int z = getAttributeAs<int>(reader, "z", true);
                                //int index = getAttributeAs<int>(reader, "index", true);
                                //string idtScan = nativeID + "." + Convert.ToString(z);                                                   
                                if (!idtScanDict.ContainsKey(nativeID))
                                    idtScanDict.Add(nativeID, 1);   // use only scan number as identification, no charge info
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    Workspace.SetText("\r\nError in reading idpXML file, please check IDPicker configuration and try again\r\n");
                    Workspace.ChangeButtonTo("Close");
                    throw new Exception(exc.Message);
                }*/

                // open metrics file, if identified, add label, write to a new file
                string metricsFileName = fileBaseName + metricsFileSuffix + ".txt";
                string outFileName = fileBaseName + metricsFileSuffix + outFileSuffix + ".txt";
                
                Workspace.SetText("\r\nWriting labbled metrics file: " + outFileName);

                //List<int> unidentifiedSpectra = new List<int>();
                List<string> unidentifiedSpectra = new List<string>();
                int numSpectra = 0;
                int cumsum = 0;

                try
                {
                    if (File.Exists(outFileName))
                    {
                        File.Delete(outFileName);
                    }

                    using (TextReader r = File.OpenText(metricsFileName))
                    {
                        using (TextWriter w = File.CreateText(outFileName))
                        {
                            w.WriteLine(r.ReadLine());  // read and write the 1st header line
                            w.WriteLine(r.ReadLine());  // read and write the 1st header line
                            string header = r.ReadLine();  // read the 3rd header line
                            w.WriteLine(header + "\t" + "Label" + "\t" + "CumsumLabel" + "\t" + "Peptide" + "\t" + "Protein"); // write the 3rd header line

                            string line = string.Empty;
                            while ((line = r.ReadLine()) != null)
                            {
                                numSpectra++;
                                string[] items = line.Split('\t');
                                //string scanNativeID = items[2];   //  nativeID
                                string scanNativeID = items[1];   //  nativeID
                                //int index = Convert.ToInt32(items[1]);  //index
                                
                                //Match m = Regex.Match(scanNativeID, @"scan=(\d+)");  // extract scan number in nativeID
                                //if (m.Success)
                                //{
                                //    scanNativeID = m.Groups[1].Value;
                                //}

                                if (resultsMap.Contains(scanNativeID))
                                {
                                    cumsum += 1;
                                    ResultInstance ri = resultsMap[scanNativeID];
                                    if (ri.Mods.Equals(string.Empty))
                                    {
                                        w.WriteLine(line + "\t1\t" + cumsum + "\t" + ri.Peptide.ToString() + "\t" + ri.Protein.ToString());
                                    }
                                    else
                                    {
                                        w.WriteLine(line + "\t1\t" + cumsum + "\t" + ri.Peptide.ToString() + '{' + ri.Mods.ToString() + '}' + "\t" + ri.Protein.ToString());
                                    }
                                }
                                else
                                {
                                    w.WriteLine(line + "\t0\t" + cumsum);
                                    //unidentifiedSpectra.Add(index);
                                    unidentifiedSpectra.Add(scanNativeID);
                                }
                               
                                //scanNativeID = scanNativeID + "." + items[2]; // use nativeID scanNumber.charge as scanID
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    //throw new Exception("Error in creating spectra lable file\r\n", exc);
                    Workspace.SetText("\r\nError in creating a file with spectra labels, please check the ScanRanker metrics file\r\n");
                    Workspace.ChangeButtonTo("Close");
                    throw new Exception(exc.Message);
                }
                Workspace.SetText("\r\nFinished adding spectral labels for file: " + metricsFileName + " \r\n\r\n");

                // write out unidentfied high quality spectra
                if (unidentifiedSpectra.Count == numSpectra)
                {
                    Workspace.SetText("\r\nNo spectra were identified. This might because the spectrumNativeID attribute is missing in pepXML files");
                    Workspace.ChangeButtonTo("Close");
                    return;

                }
                if (writeOutUnidentifiedSpectra)
                {
                    writeUnidentifiedSpectra(file.Name, outFileSuffix, unidentifiedSpectra, recoveryCutoff, recoveryOutFormat);
                }

                Workspace.SetText("\r\n" + file.Name
                                    + ":\r\n\tTotal number of spectra: " + numSpectra.ToString()
                                    + "\r\n\tNumber of identified spectra: " + cumsum.ToString()
                                    + "\r\n\tNumber of unidentified spectra in output: " + (Convert.ToInt32(unidentifiedSpectra.Count * recoveryCutoff)).ToString()
                                    + "\r\n");

                //delete old metrics file                
                //if (File.Exists(metricsFileName))
                //{
                //    File.Delete(metricsFileName);
                //}
            } // end of foreach file


            Workspace.SetText("\r\nFinished adding spectral labels!");
            Workspace.ChangeButtonTo("Close");
        }// end of addSpectraLabel()
    }
}
