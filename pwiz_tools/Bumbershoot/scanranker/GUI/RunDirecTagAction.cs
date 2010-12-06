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
//using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
//using System.Threading;

namespace ScanRanker
{
    class RunDirecTagAction
    {
        private ArrayList inFileList;
        public ArrayList InFileList
        {
            get { return inFileList; }
            set { inFileList = value; }
        }

        //private string inFile;
        //public string InFile
        //{
        //    get { return inFile; }
        //    set { inFile = value; }
        //}
        private string outMetricsSuffix;
        public string OutMetricsSuffix
        {
            get { return outMetricsSuffix; }
            set { outMetricsSuffix = value; }
        }
        private string outputDir;
        public string OutputDir
        {
            get { return outputDir; }
            set { outputDir = value; }
        }

        private string outputFormat;
        public string OutputFormat
        {
            get { return outputFormat; }
            set { outputFormat = value; }
        }

        private bool addLabel;
        public bool AddLabel
        {
            get { return addLabel;}
            set { addLabel = value; }
        }

        private bool writeOutUnidentifedSpectra;
        public bool WriteOutUnidentifedSpectra
        {
            get { return writeOutUnidentifedSpectra; }
            set { writeOutUnidentifedSpectra = value; }
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

        private bool adjustScanRankerScoreByGroup;
        public bool AdjustScanRankerScoreByGroup
        {
            get { return adjustScanRankerScoreByGroup; }
            set { adjustScanRankerScoreByGroup = value; }
        }

        private IDPickerInfo idpickerCfg;
        public IDPickerInfo IdpickerCfg
        {
            get { return idpickerCfg;}
            set { idpickerCfg = value; }
        }

        private string outputFilenameSuffixForRemoval;
        public string OutputFilenameSuffixForRemoval
        {
            get { return outputFilenameSuffixForRemoval; }
            set { outputFilenameSuffixForRemoval = value; }
        }

        private string outputFilenameSuffixForRecovery;
        public string OutputFilenameSuffixForRecovery
        {
            get { return outputFilenameSuffixForRecovery; }
            set { outputFilenameSuffixForRecovery = value; }
        }

        public RunDirecTagAction()
        {
            addLabel = false;
            idpickerCfg = null;
            outputFilenameSuffixForRecovery = string.Empty;
        }

        /// <summary>
        /// run directag.exe to assess spectrum quality, generate a subset of high quality spectra
        /// run AddSpectraLabel() to label identified spectra if configured
        /// run adjustScanRankerScoreByGroup.exe to add new scores
        /// </summary
        public void RunDirectag()
        {
            foreach (FileInfo file in inFileList)
            {
                string fileBaseName = Path.GetFileNameWithoutExtension(file.FullName);
                string outMetricsFileName = fileBaseName + outMetricsSuffix + ".txt";
                string outHighQualSpecFileName = fileBaseName + outputFilenameSuffixForRemoval + "." + outputFormat;
//                string outLabelFileName = fileBaseName + outputFilenameSuffixForRecovery + ".txt";

                string EXE = @"directag.exe";
                string BIN_DIR = Path.GetDirectoryName(Application.ExecutablePath);
                string pathAndExeFile = BIN_DIR + "\\" + EXE;
                string args = " -cfg directag.cfg " + " -ScanRankerMetricsFileName " + outMetricsFileName +
                    " -HighQualSpecFileName " + outHighQualSpecFileName + " \"" + file.FullName + "\"";

                Workspace.SetText("Start assessing spectral quality for file: " + file.Name + " ...\r\n\r\n");

                try
                {
                    if (File.Exists(outMetricsFileName))
                    {
                        File.Delete(outMetricsFileName);
                    }

                    Workspace.RunProcess(pathAndExeFile, args, outputDir);
                }
                catch (Exception exc)
                {
                    //throw new Exception("Error running DirecTag\r\n", exc);
                    Workspace.SetText("\r\nError in running DirecTag\r\n");
                    throw new Exception(exc.Message);
                }

                //if (!File.Exists(outMetricsFile))
                //{
                //    MessageBox.Show("Error in running DirecTag, no metrics file generated");
                //}

                Workspace.SetText("\r\nFinished spectral quality assessment for file: " + file.Name + "\r\n\r\n");

            } // end of foreach file to run directag

            if (File.Exists("directag_intensity_ranksum_bins.cache"))
            {
                File.Delete("directag_intensity_ranksum_bins.cache");
            }
            if (File.Exists("directag.cfg"))
            {
                File.Delete("directag.cfg");
            }

            //if adjustScanRankerScoreByGroup = true, call adjustScanRankerScoreByGroup.exe to add new scores
            if (adjustScanRankerScoreByGroup)
            {
                string groupFile = "group.txt";
                if (File.Exists(groupFile))
                {
                    File.Delete(groupFile);
                }

                // write outMetricsFileName to group.txt, separated by ",", one line
                TextWriter tw = new StreamWriter(groupFile);
                foreach (FileInfo file in inFileList)
                {
                    string fileBaseName = Path.GetFileNameWithoutExtension(file.FullName);
                    string outMetricsFileName = fileBaseName + outMetricsSuffix + ".txt";

                   if (File.Exists(outMetricsFileName))
                   {
                            tw.Write(outMetricsFileName + ",");
                   }
                   else
                   {
                        //Workspace.SetText("\r\nCannot find metrics file: " + outMetricsFileName + "\r\n");
                        throw new Exception("\r\nCannot find metrics file: " + outMetricsFileName + "\r\n");
                    }
                }
                tw.Close();

                // call adjustScanRankerScoreByGroup.exe
                string EXE = @"adjustScanRankerScoreByGroup.exe";
                string BIN_DIR = Path.GetDirectoryName(Application.ExecutablePath);
                string pathAndExeFile = BIN_DIR + "\\" + EXE;
                string args = " " + groupFile;

                try
                {
                    Workspace.RunProcess(pathAndExeFile, args, outputDir);
                }
                catch (Exception exc)
                {
                    //throw new Exception("Error running DirecTag\r\n", exc);
                    Workspace.SetText("\r\nError in running adjustScanRankerScoreByGroup.exe\r\n");
                    throw new Exception(exc.Message);
                }

                //delete old metrics file
                //new metrics file with suffix "-adjusted", hard coded in DirecTag
                foreach (FileInfo file in inFileList)
                {
                    string fileBaseName = Path.GetFileNameWithoutExtension(file.FullName);
                    string outMetricsFileName = fileBaseName + outMetricsSuffix + ".txt";
                    if (File.Exists(outMetricsFileName))
                    {
                        File.Delete(outMetricsFileName);
                    }
                }

                // remove group file
                if (File.Exists(groupFile))
                {
                    File.Delete(groupFile);
                }
            } // end of adjustScanRankerScoreByGroup

            if (addLabel)
            {
                AddSpectraLabelAction addSpectraLabelAction = new AddSpectraLabelAction();
                addSpectraLabelAction.InFileList = inFileList;
                addSpectraLabelAction.IdpCfg = idpickerCfg;
                addSpectraLabelAction.OutFileSuffix = outputFilenameSuffixForRecovery;
                addSpectraLabelAction.OutDir = outputDir;
                addSpectraLabelAction.MetricsFileSuffix = outMetricsSuffix;
                if (adjustScanRankerScoreByGroup)
                {
                    addSpectraLabelAction.MetricsFileSuffix = outMetricsSuffix + "-adjusted"; //name hard coded in directag
                }
                if (writeOutUnidentifedSpectra)
                {
                    addSpectraLabelAction.WriteOutUnidentifiedSpectra = true;
                    addSpectraLabelAction.RecoveryCutoff = recoveryCutoff;
                    addSpectraLabelAction.RecoveryOutFormat = recoveryOutFormat;
                }
                addSpectraLabelAction.AddSpectraLabel();
                

                //foreach (FileInfo file in inFileList)
                //{
                //    string fileBaseName = Path.GetFileNameWithoutExtension(file.FullName);
                //    string outMetricsFileName = fileBaseName + outMetricsSuffix + ".txt";
                //    if (adjustScanRankerScoreByGroup)
                //    {
                //        outMetricsFileName = fileBaseName + outMetricsSuffix + "-adjusted.txt"; //name hard coded in directag
                //    }
                //    string outLabelFileName = Path.GetFileNameWithoutExtension(outMetricsFileName) + outputFilenameSuffixForRecovery + ".txt";

                //    //Workspace.SetText("\r\nStart adding spectra labels ...\r\n\r\n");
                //    AddSpectraLabelAction addSpectraLabelAction = new AddSpectraLabelAction();
                //    addSpectraLabelAction.SpectraFileName = file.Name;
                //    addSpectraLabelAction.MetricsFileName = outMetricsFileName;
                //    addSpectraLabelAction.IdpCfg = idpickerCfg;
                //    addSpectraLabelAction.OutDir = outputDir;
                //    addSpectraLabelAction.OutFileName = outLabelFileName;                    
                //    if (writeOutUnidentifedSpectra)
                //    {
                //        addSpectraLabelAction.WriteOutUnidentifiedSpectra = true;
                //        addSpectraLabelAction.RecoveryCutoff = recoveryCutoff;
                //        addSpectraLabelAction.RecoveryOutFormat = recoveryOutFormat;
                //    }
                //    addSpectraLabelAction.AddSpectraLabel();
                //    //Workspace.SetText("\r\nFinished adding spectra labels\r\n\r\n");
                    
                //}

                //delete old metrics file, adjust scores by group
                //new metrics file with prefix "adjusted-", hard coded in DirecTag
                //foreach (FileInfo file in inFileList)
                //{
                //    string fileBaseName = Path.GetFileNameWithoutExtension(file.FullName);
                //    string outMetricsFileName = fileBaseName + outMetricsSuffix + ".txt";
                //    if (adjustScanRankerScoreByGroup)
                //    {
                //        outMetricsFileName = fileBaseName + outMetricsSuffix + "-adjusted.txt";
                //    }
                //    if (File.Exists(outMetricsFileName))
                //    {
                //        File.Delete(outMetricsFileName);
                //    }
                //}
            } // end of add label


            Workspace.SetText("\r\nFinished quality assessment!");
            Workspace.ChangeButtonTo("Close");
            //Workspace.statusForm.btnStop.Visible = false;
            //Workspace.statusForm.btnClose.Visible = true;


        }
    }
}
