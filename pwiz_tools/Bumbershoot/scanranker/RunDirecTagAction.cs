using System;
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
        private string inFile;
        public string InFile
        {
            get { return inFile; }
            set { inFile = value; }
        }
        private string outMetricsFile;
        public string OutMetricsFile
        {
            get { return outMetricsFile; }
            set { outMetricsFile = value; }
        }
        private string outputDir;
        public string OutputDir
        {
            get { return outputDir; }
            set { outputDir = value; }
        }

        private bool addLabel;
        public bool AddLabel
        {
            get { return addLabel;}
            set { addLabel = value; }
        }

        private IDPickerInfo idpickerCfg;
        public IDPickerInfo IdpickerCfg
        {
            get { return idpickerCfg;}
            set { idpickerCfg = value; }
        }

        private string outputFilenameForRecovery;
        public string OutputFilenameForRecovery
        {
            get { return outputFilenameForRecovery; }
            set { outputFilenameForRecovery = value; }
        }

        public RunDirecTagAction()
        {
            addLabel = false;
            idpickerCfg = null;
            outputFilenameForRecovery = string.Empty;
        }

        public void RunDirectag()
        {
            string EXE = @"directag.exe";
            string BIN_DIR = Path.GetDirectoryName(Application.ExecutablePath);
            string pathAndExeFile = BIN_DIR + "\\DirecTag\\" + EXE;
            string args = " -cfg directag.cfg " + "\"" + inFile + "\"";

            Workspace.SetText("Start assessing spectral quality ...\r\n\r\n");

            try
            {
                if (File.Exists(outMetricsFile))
                {
                    File.Delete(outMetricsFile);
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

            if (File.Exists("directag_intensity_ranksum_bins.cache"))
            {
                File.Delete("directag_intensity_ranksum_bins.cache");
            }
            Workspace.SetText("\r\nFinished spectral quality assessment\r\n\r\n");

            if (addLabel)
            {
                //Workspace.SetText("\r\nStart adding spectra labels ...\r\n\r\n");
                AddSpectraLabelAction addSpectraLabelAction = new AddSpectraLabelAction();
                addSpectraLabelAction.MetricsFile = outMetricsFile;
                addSpectraLabelAction.IdpCfg = idpickerCfg;
                addSpectraLabelAction.OutDir = outputDir;
                addSpectraLabelAction.OutFilename = outputFilenameForRecovery;
                addSpectraLabelAction.AddSpectraLable();
                //Workspace.SetText("\r\nFinished adding spectra labels\r\n\r\n");
            }

            Workspace.ChangeButton();
            //Workspace.statusForm.btnStop.Visible = false;
            //Workspace.statusForm.btnClose.Visible = true;
        }
    }
}
