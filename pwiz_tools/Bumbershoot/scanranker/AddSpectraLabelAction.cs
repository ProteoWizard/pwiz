using System;
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
        private string metricsFile;
        public string MetricsFile
        {
            get { return metricsFile; }
            set { metricsFile = value; }
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
        private string outFilename;
        public string OutFilename
        {
            get { return outFilename; }
            set { outFilename = value; }
        }


        private void runIdpQonvert(IDPickerInfo idpCfg, string outDir)
        {
            // run idpQonvert and create idpXML file
            string EXE = @"idpqonvert.exe";
            string BIN_DIR = Path.GetDirectoryName(Application.ExecutablePath);
            string pathAndExeFile = BIN_DIR + "\\IDPicker\\" + EXE;
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
                throw new Exception(exc.Message);
            }

            //if (!File.Exists(outputFilename))
            //{
            //    throw new Exception("Error in running idpQonvert, no idpXML file generated");
            //}

        }

        // code copied from IDPicker project
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

        public void AddSpectraLable()
        {
            Workspace.SetText("\r\nStart adding spectra labels to a metrics file ...\r\n\r\n");

            runIdpQonvert(idpCfg, outDir);

            // read idpxml, extract spectra id.charge, save to a dictionary
            Dictionary<string, int> idtScanDict = new Dictionary<string, int>();
            string idpXMLFilename = Path.GetFileNameWithoutExtension(idpCfg.PepXMLFile) + ".idpXML";
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
                throw new Exception(exc.Message);
            }

            // open metrics file, check existence of scan id in dictionary, add label, write to a new file
            try
            {
                if (File.Exists(outFilename))
                {
                    File.Delete(outFilename);
                }

                int count = 0;
                using (TextReader r = File.OpenText(metricsFile))
                {
                    using (TextWriter w = File.CreateText(outFilename))
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
                throw new Exception(exc.Message);
            }

            Workspace.SetText("\r\nFinished adding spectra labels\r\n\r\n");
            Workspace.ChangeButton();
        }

    }
}
