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
using System.Text;
using System.IO;
using System.Windows.Forms;

using pwiz.CLI;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;

namespace ScanRanker
{
    public class WriteSpectraAction
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
        private float cutoff;
        public float Cutoff
        {
            get { return cutoff; }
            set { cutoff = value; }
        }
        private string outFormat;
        public string OutFormat
        {
            get { return outFormat; }
            set { outFormat = value; }
        }
        private string outFileSuffix;
        public string OutFileSuffix
        {
            get { return outFileSuffix; }
            set { outFileSuffix = value; }
        }

        private class SpectrumList_FilterPredicate_IndexSet
        {
            public List<int> indexSet;
            public bool accept(Spectrum s) { return indexSet.Contains(s.index); }
            public SpectrumList_FilterPredicate_IndexSet()
            {
                indexSet = new List<int>();
            }

        }

        /// <summary>
        /// write out a subset of high quality spectra based on ScanRanker metrics file
        /// using ProteoWizard library
        /// </summary>
        public void Write()
        {
            foreach (FileInfo file in inFileList)
            {
                string fileBaseName = Path.GetFileNameWithoutExtension(file.FullName);
                string metricsFileName = fileBaseName + metricsFileSuffix + ".txt";
                string outFileName = fileBaseName + outFileSuffix + "." + outFormat;

                Workspace.SetText("\r\nStart writing high quality spectra for file: " + file.Name + " ...\r\n\r\n");
                if (File.Exists(outFileName))
                {
                    File.Delete(outFileName);
                }

                if (!File.Exists(metricsFileName))
                {
                    Workspace.SetText("\r\nError: Cannot find quality metrics file: " + metricsFileName + " in output directory!");
                    Workspace.ChangeButtonTo("Close");
                    return;
                }


                List<int> allIndices = new List<int>();
                List<int> highQualIndices = new List<int>();

                // read metrics file, split and get high quality spectra indecies from the second column
                try
                {
                    Workspace.SetText("\r\nExtracting scan index from metrics file: " + metricsFileName);
                    using (TextReader tr = File.OpenText(metricsFileName))
                    {
                        tr.ReadLine();  // read the header line but do nothing, first three lines are header
                        tr.ReadLine();  // read the header line but do nothing
                        tr.ReadLine();  // read the header line but do nothing
                        string line = string.Empty;
                        while ((line = tr.ReadLine()) != null)
                        {
                            string[] items = line.Split('\t');
                            int index = Convert.ToInt32(items[1]);  //index
                            if (!allIndices.Exists(element => element == index))   // remove duplicate index
                            {
                                allIndices.Add(index);
                            }
                        }
                    }
                }
                catch (Exception exc)
                {
                    //throw new Exception("Error in reading metrics file for spectra removal\r\n",exc);
                    Workspace.SetText("\r\nError in reading metrics file for spectra removal\r\n");
                    Workspace.ChangeButtonTo("Close");
                    throw new Exception(exc.Message);
                }

                // get indices for high quality spectra
                Workspace.SetText("\r\nGenerating indices of high quality spectra");
                int numOutputSpectra = Convert.ToInt32(allIndices.Count * cutoff);
                highQualIndices = allIndices.GetRange(0, numOutputSpectra);
                //highQualIndices.Sort();

                var predicate = new SpectrumList_FilterPredicate_IndexSet();
                foreach (int i in highQualIndices)
                {
                    predicate.indexSet.Add(i);
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
                    Workspace.SetText("\r\nWriting high quality spectra to file: " + outFileName);
                    using (MSDataFile msFile = new MSDataFile(file.FullName))
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

                Workspace.SetText("\r\nFinished writing high quality spectra for file: " + file.Name + " \r\n\r\n");
                
            }// end of foreach file

        Workspace.SetText("\r\nCompleted!");
        Workspace.ChangeButtonTo("Close");
        } // end of write()
    }
}
