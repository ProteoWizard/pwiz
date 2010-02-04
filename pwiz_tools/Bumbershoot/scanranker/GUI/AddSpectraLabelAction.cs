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


namespace ScanRanker
{
    public class AddSpectraLabelAction
    {
        //private ArrayList inFileList;
        //public ArrayList InFileList
        //{
        //    get { return inFileList; }
        //    set { inFileList = value; }
        //}

        //private string metricsFileSuffix;
        //public string MetricsFileSuffix
        //{
        //    get { return metricsFileSuffix; }
        //    set { metricsFileSuffix = value; }
        //}

        private string spectraFileName;
        public string SpectraFileName
        {
            get { return spectraFileName; }
            set { spectraFileName = value; }
        }

        private string metricsFileName;
        public string MetricsFileName
        {
            get { return metricsFileName; }
            set { metricsFileName = value; }
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
        //private string outFileSuffix;
        //public string OutFileSuffix
        //{
        //    get { return outFileSuffix; }
        //    set { outFileSuffix = value; }
        //}

        private string outFileName;
        public string OutFileName
        {
            get { return outFileName; }
            set { outFileName = value; }
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
            string outputFilename = Path.GetFileNameWithoutExtension(idpCfg.PepXMLFile) + ".idpXML";
            string args =
                  " -MaxFDR " + idpCfg.MaxFDR
                + " -NormalizeSearchScores " + idpCfg.NormalizeSearchScores
                + " -OptimizeScoreWeights " + idpCfg.OptimizeScoreWeights
                + " -SearchScoreWeights " + "\"" + idpCfg.ScoreWeights + "\""
                + " -DecoyPrefix " + "\"" + idpCfg.DecoyPrefix + "\""
                + " -WriteQonversionDetails  0 "
                + " -ProteinDatabase " + "\"" + idpCfg.DBFile + "\""
                + " \"" + idpCfg.PepXMLFile + "\"";

            try
            {
                if (File.Exists(outputFilename))
                {
                    File.Delete(outputFilename);
                }

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

        /// <summary>
        /// add spectra labels to ScanRanker metrics file
        /// identified spectra ids stored in idpXML file by idqQonvert 
        /// </summary>
        public void AddSpectraLabel()
        {
            string fileBaseName = Path.GetFileNameWithoutExtension(spectraFileName);
            idpCfg.PepXMLFile = idpCfg.PepXMLFileDir + "\\" + fileBaseName + ".pepXML";  // pepXML file has to be the same basename

            Workspace.SetText("\r\nStart adding spectra labels to a metrics file: " + metricsFileName + " ...\r\n\r\n");

            if (!File.Exists(idpCfg.PepXMLFile))
            {
                Workspace.SetText("\r\nError: Cannot find pepXML file: " + idpCfg.PepXMLFile );
                Workspace.SetText("\r\nPlease check IDPicker configurations and make sure pepXML files have the same basenames as spectra files");
                Workspace.ChangeButtonTo("Close");
                return;
            }
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

            // read idpxml, extract spectra id.charge, save to a dictionary
            Dictionary<string, int> idtScanDict = new Dictionary<string, int>();
            string idpXMLFilename = Path.GetFileNameWithoutExtension(idpCfg.PepXMLFile) + ".idpXML";
            if (!File.Exists(idpXMLFilename))
            {
                Workspace.SetText("\r\nError: Cannot create idpXML file: " + idpXMLFilename + " in output directory!");
                Workspace.SetText("\r\nPlease check IDPicker configurations and the database file");
                Workspace.ChangeButtonTo("Close");
                return;
            }
            try
            {
                using (XmlTextReader reader = new XmlTextReader(idpXMLFilename))
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType.Equals(XmlNodeType.Element) && reader.Name.Equals("spectrum"))
                        {
                            // Read the spectrum tag
                            //  <spectrum id="614" nativeID="614" index="196" z="1" mass="569.32" 
                            //   time="16.7" targets="82" decoys="0" results="1">
                            string nativeID = getAttributeAs<string>(reader, "id", false);
                            Match m = Regex.Match(nativeID, @"scan=(\d+)");
                            if (m.Success)
                            {
                                nativeID = m.Groups[1].Value;
                            }
                            int z = getAttributeAs<int>(reader, "z", true);
                            //int index = getAttributeAs<int>(reader, "index", true);
                            string idtScan = nativeID + "." + Convert.ToString(z);
                            idtScanDict.Add(idtScan, 1);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                Workspace.SetText("\r\nError in reading idpXML file, please check IDPicker configuration and try again\r\n");
                Workspace.ChangeButtonTo("Close");
                throw new Exception(exc.Message);
            }

            // open metrics file, check existence of scan id in dictionary, add label, write to a new file
            try
            {
                if (File.Exists(outFileName))
                {
                    File.Delete(outFileName);
                }

                int count = 0;
                using (TextReader r = File.OpenText(metricsFileName))
                {
                    using (TextWriter w = File.CreateText(outFileName))
                    {
                        string header = r.ReadLine();  // read the header line but do nothing
                        w.WriteLine(header + "\tLabel" + "\tCumsumLabel");

                        string line = string.Empty;
                        while ((line = r.ReadLine()) != null)
                        {
                            string[] items = line.Split('\t');
                            string scanNativeID = items[0];   // items[0]: nativeID
                            Match m = Regex.Match(scanNativeID, @"scan=(\d+)");  // extract scan number in nativeID
                            if (m.Success)
                            {
                                scanNativeID = m.Groups[1].Value;
                            }
                            scanNativeID = scanNativeID + "." + items[2]; // use nativeID scanNumber.charge as scanID

                            if (idtScanDict.ContainsKey(scanNativeID))
                            {
                                count += 1;
                                w.WriteLine(line + "\t1\t" + count);

                            }
                            else
                            {
                                w.WriteLine(line + "\t0\t" + count);
                            }
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

            Workspace.SetText("\r\nFinished adding spectra labels for file: " + metricsFileName + " \r\n\r\n");
            Workspace.ChangeButtonTo("Close");
        }
    }
}
