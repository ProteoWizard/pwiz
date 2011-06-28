//
// $Id: RunDirecTagAction.cs 18 2010-12-06 19:05:26Z zeqiangma $
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

namespace IonMatcher
{
    class RunPepNovoAction
    {
        private string inFile;
        public string InFile
        {
            get { return inFile; }
            set { inFile = value; }
        }
        //private string outputDir;
        //public string OutputDir
        //{
        //    get { return outputDir; }
        //    set { outputDir = value; }
        //}
        private string workingDir;
        public string WorkingDir
        {
            get { return workingDir; }
            set { workingDir = value; }
        }
        //pepnovo parameter
        private float precursorTolerance;
        public float PrecursorTolerance
        {
            get { return precursorTolerance; }
            set { precursorTolerance = value; }
        }
        private float fragmentTolerance;
        public float FragmentTolerance
        {
            get { return fragmentTolerance; }
            set { fragmentTolerance = value; }
        }
        private string ptms;
        public string PTMs
        {
            get { return ptms; }
            set { ptms = value; }
        }
        private string useSpectrumCharge;
        public string UseSpectrumCharge
        {
            get { return useSpectrumCharge; }
            set { useSpectrumCharge = value; }
        }
        private string useSpectrumMz;
        public string UseSpectrumMz
        {
            get { return useSpectrumMz; }
            set { useSpectrumMz = value; }
        }

        private MainForm mainForm;
        public RunPepNovoAction(MainForm mainForm)
        {
            this.mainForm = mainForm;
        }

        /// <summary>
        /// run pepnovo
        /// </summary
        public void RunPepNovo()
        {
            //string fileBaseName = Path.GetFileNameWithoutExtension(file.FullName);
            //string outMetricsFileName = fileBaseName + outMetricsSuffix + ".txt";
            //string outHighQualSpecFileName = fileBaseName + outputFilenameSuffixForRemoval + "." + outputFormat;
            //                string outLabelFileName = fileBaseName + outputFilenameSuffixForRecovery + ".txt";

            if (!File.Exists(inFile))
            {
                MessageBox.Show("Error: Cannot find the spectrum file for de novo sequencing!");
                return;
            }

            string EXE = @"PepNovo.exe";
            string BIN_DIR = Path.GetDirectoryName(Application.ExecutablePath) + "\\" + "PepNovo";
            string pathAndExeFile = BIN_DIR + "\\" + EXE;
            string args = " -model CID_IT_TRYP " + " -model_dir " + "\"" + BIN_DIR + "\"" + "\\" + "Models"
                + " -pm_tolerance " + precursorTolerance + " -fragment_tolerance " + fragmentTolerance
                + " -no_quality_filter " + " -num_solutions 10 " + " -PTMs " + ptms
                + useSpectrumCharge + useSpectrumMz + " -file " + " \"" + inFile + "\"";

            MainForm.SetText(mainForm, "\r\nStart running PepNovo ...\r\n\r\n");
            //MainForm.SetText(mainForm, "PepNovo command: " + pathAndExeFile + args + "\r\n\r\n");

            try
            {
                Workspace workspace = new Workspace(mainForm);
                workspace.RunProcess(pathAndExeFile, args, workingDir);
            }
            catch (Exception exc)
            {
                //Workspace.SetText("\r\nError in running PepNovo\r\n");
                MainForm.SetText(mainForm, "\r\nError in running PepNovo\r\n");
                throw new Exception(exc.Message);
            }

            //if (!File.Exists(outMetricsFile))
            //{
            //    MessageBox.Show("Error in running DirecTag, no metrics file generated");
            //}

            MainForm.SetText(mainForm, "\r\nFinished PepNovo! \r\n");
            if (File.Exists(inFile))
            {
                File.Delete(inFile);
                MainForm.SetText(mainForm, "Deleted " + inFile + "\r\n");
            }
            
        }
    }
}
