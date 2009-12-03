using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace ScanRanker
{

    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private string fileBaseName;
        IDPickerInfo idpickerCfg = new IDPickerInfo(); // set to null when load form
  
        # region functions to enable or disable controls
        /// <summary>
        /// functions to enable or disable controls
        /// </summary>
        private void enableAssessmentControls()
        {
            foreach (Control c in gbAssessment.Controls)
            {
                c.Enabled = true;
            }
            tbMetricsFileForRemoval.Enabled = false;
            tbMetricsFileForRemoval.Text = string.Empty;
            tbMetricsFileForRecovery.Enabled = false;
            tbMetricsFileForRecovery.Text = string.Empty;
            btnMetricsFileBrowseForRemoval.Enabled = false;
            btnMetricsFileBrowseForRecovery.Enabled = false;
        }
        private void enableRemovalControls()
        {
            foreach (Control c in gbRemoval.Controls)
            {
                c.Enabled = true;
            }
            if (cbAssessement.Checked)
            {
                tbMetricsFileForRemoval.Enabled = false;
                tbMetricsFileForRemoval.Text = string.Empty;
                btnMetricsFileBrowseForRemoval.Enabled = false;
            }
        }
        private void enableRecoveryControls()
        {
            foreach (Control c in gbRecovery.Controls)
            {
                c.Enabled = true;
            }
            if (cbAssessement.Checked)
            {
                tbMetricsFileForRecovery.Enabled = false;
                tbMetricsFileForRecovery.Text = string.Empty;
                btnMetricsFileBrowseForRecovery.Enabled = false;
            }
        }
        private void disableAssessmentControls()
        {
            foreach (Control c in gbAssessment.Controls)
            {
                c.Enabled = false;
            }
            cbAssessement.Enabled = true;
            tbMetricsFileForRemoval.Enabled = (cbRemoval.Checked) ? true : false;
            tbMetricsFileForRemoval.Text = string.Empty;
            btnMetricsFileBrowseForRemoval.Enabled = (cbRemoval.Checked) ? true : false; ;
            tbMetricsFileForRecovery.Enabled = (cbRecovery.Checked) ? true : false;
            tbMetricsFileForRecovery.Text = string.Empty;
            btnMetricsFileBrowseForRecovery.Enabled = (cbRecovery.Checked) ? true : false;
        }
        private void disableRemovalControls()
        {
            foreach (Control c in gbRemoval.Controls)
            {
                c.Enabled = false;
            }
            cbRemoval.Enabled = true;

        }
        private void disableRecoveryControls()
        {
            foreach (Control c in gbRecovery.Controls)
            {
                c.Enabled = false;
            }
            cbRecovery.Enabled = true;
        }
        private void cbAssessement_CheckedChanged(object sender, EventArgs e)
        {
            if (cbAssessement.Checked)
            {
                enableAssessmentControls();
            }
            else
            {
                disableAssessmentControls();
            }

        }
        private void cbRemoval_CheckedChanged(object sender, EventArgs e)
        {
            if (cbRemoval.Checked)
            {
                enableRemovalControls();
            }
            else
            {
                disableRemovalControls();
            }
        }
        private void cbRecovery_CheckedChanged(object sender, EventArgs e)
        {
            if (cbRecovery.Checked)
            {
                enableRecoveryControls();
            }
            else
            {
                disableRecoveryControls();
            }
        }

        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {
            /// <summary>
            /// default for running quality assessment and removal
            /// </summary>
            cbAssessement.Checked = true;
            cbRemoval.Checked = true;
            cbRecovery.Checked = false;
            tbMetricsFileForRemoval.Enabled = false;
            btnMetricsFileBrowseForRemoval.Enabled = false;
            tbMetricsFileForRecovery.Enabled = false;
            disableRecoveryControls();

            idpickerCfg = null;
        }

        private void btnSrcFileBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenFileBrowseDialog(tbInputFile.Text);
                if (!selFile.Equals(string.Empty))
                {
                    tbInputFile.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
                //HandleExceptions(exc);
            }
            if (!tbInputFile.Text.Equals(string.Empty))
            {
                fileBaseName = Path.GetFileNameWithoutExtension(tbInputFile.Text);
                tbOutputDir.Text = Path.GetDirectoryName(tbInputFile.Text);
            }
            else
            {
                MessageBox.Show("Please select input file!");
                return;
            }
            string outputMetricFileName = fileBaseName + "-ScanRanker-metrics.txt";
            string outputRemovalFileName = fileBaseName + "-HighQualSpectra" + tbRemovalCutoff.Text + "Perc." + cmbOutputFileFormat.Text;
            string outputRecoveryFileName = fileBaseName + "-ScanRanker-labels.txt";
            tbOutputMetrics.Text = outputMetricFileName;
            tbOutFileNameForRemoval.Text = outputRemovalFileName;
            tbOutFileNameForRecovery.Text = outputRecoveryFileName;
            tbMetricsFileForRemoval.Text = string.Empty;
            tbMetricsFileForRecovery.Text = string.Empty;
            
        }

        private void btnOutputDirBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenDirBrowseDialog(tbOutputDir.Text, true);
                if (!selFile.Equals(string.Empty))
                {
                    tbOutputDir.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening direcoty dialog\r\n", exc);
                //HandleExceptions(exc);
            }
        }

        private void btnMetricsFileBrowseForRemoval_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenFileBrowseDialog(tbMetricsFileForRemoval.Text);
                if (!selFile.Equals(string.Empty))
                {
                    tbMetricsFileForRemoval.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
                //HandleExceptions(exc);
            }
        }
        
        private void btnMetricsFileBrowseForRecovery_Click(object sender, EventArgs e)
        {
            try
            {
                string selFile = Workspace.OpenFileBrowseDialog(tbMetricsFileForRecovery.Text);
                if (!selFile.Equals(string.Empty))
                {
                    tbMetricsFileForRecovery.Text = selFile;
                }
            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
                //HandleExceptions(exc);
            }
            fileBaseName = Path.GetFileNameWithoutExtension(tbMetricsFileForRecovery.Text);
            string outputRecoveryFileName = fileBaseName + "-labels.txt";
            tbOutFileNameForRecovery.Text = outputRecoveryFileName;
        }

        private void btnSetIDPicker_Click(object sender, EventArgs e)
        {
            IDPickerConfigForm idpCfgForm = new IDPickerConfigForm();
            idpCfgForm.ShowDialog();
            idpickerCfg = idpCfgForm.IdpCfg;
        }

        private void cmbOutputFileFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            string outputRemovalFileName = fileBaseName + "-HighQualSpectra" + tbRemovalCutoff.Text + "Perc." + cmbOutputFileFormat.Text;
            tbOutFileNameForRemoval.Text = outputRemovalFileName;
        }

        private void tbRemovalCutoff_TextChanged(object sender, EventArgs e)
        {
            string outputRemovalFileName = fileBaseName + "-HighQualSpectra" + tbRemovalCutoff.Text + "Perc." + cmbOutputFileFormat.Text;
            tbOutFileNameForRemoval.Text = outputRemovalFileName;
        }
       
        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
        
        /// <summary>
        ///  write cfg file for DirecTag
        /// </summary>
        private void bulidDirectagInfo(DirecTagInfo directagInfo)
        {
            directagInfo.NumChargeStates = Convert.ToInt32(tbNumChargeStates.Text);
            directagInfo.FragmentMzTolerance = Convert.ToSingle(tbFragmentTolerance.Text);
            directagInfo.IsotopeMzTolerance = Convert.ToSingle(tbIsotopeTolerance.Text);
            directagInfo.PrecursorMzTolerance = Convert.ToSingle(tbPrecursorTolerance.Text);
            directagInfo.UseAvgMassOfSequences = (rbAverage.Checked) ? 1 : 0;
            directagInfo.UseChargeStateFromMS = (cbUseChargeStateFromMS.Checked) ? 1 : 0;
            directagInfo.UseMultipleProcessors = (cbUseMultipleProcessors.Checked) ? 1 : 0;
            directagInfo.StaticMods = tbStaticMods.Text;

            directagInfo.WriteOutTags = (cbWriteOutTags.Checked) ? 1 : 0;
            directagInfo.WriteScanRankerMetrics = (cbAssessement.Checked) ? 1 : 0;
            directagInfo.ScanRankerMetricsFileName = tbOutputMetrics.Text;
            directagInfo.WriteHighQualSpectra = (cbRemoval.Checked) ? 1 : 0;
            directagInfo.HighQualSpecFileName = tbOutFileNameForRemoval.Text;
            directagInfo.OutputFormat = cmbOutputFileFormat.Text;
            directagInfo.HighQualSpecCutoff = Convert.ToSingle(tbRemovalCutoff.Text) / 100.0f;
            
            directagInfo.WriteDirectagCfg();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            if (cbAssessement.Checked)
            {
                # region  Error checking
                if (tbInputFile.Text.Equals(string.Empty) || !File.Exists(tbInputFile.Text))
                {
                    MessageBox.Show("Error: Please select input file!");
                    return;
                }
                if (tbOutputDir.Text.Equals(string.Empty) || !Directory.Exists(tbOutputDir.Text))
                {
                    MessageBox.Show("Error: Please set up the correct output directory!");
                    return;
                }
                if (cbRemoval.Checked)
                {
                    List<string> allowedFormat = new List<string>(new string[] { "mzXML", "mzxml", "mzML", "mzml", "mgf", "MGF" });
                    if (!allowedFormat.Exists(element => element.Equals(cmbOutputFileFormat.Text)))
                    {
                        MessageBox.Show("Please select proper output format");
                        return;
                    }
                    if (Convert.ToInt32(tbRemovalCutoff.Text) <= 0 || Convert.ToInt32(tbRemovalCutoff.Text) > 100)
                    {
                        MessageBox.Show("Please specify proper cutoff between 0 and 100!");
                        return;
                    }
                    if (tbOutFileNameForRemoval.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Please specify metrics file and output file name!");
                        return;
                    }
                }
                if (cbRecovery.Checked)
                {
                    if (tbOutFileNameForRecovery.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify output file!");
                        return;
                    }
                    if (idpickerCfg == null)
                    {
                        MessageBox.Show("Please configurate IDPicker settings");
                        return;
                    }
                }

                # endregion

                string outputDir = tbOutputDir.Text;
                RunDirecTagAction directagAction = new RunDirecTagAction();
                directagAction.InFile = tbInputFile.Text;
                directagAction.OutMetricsFile = tbOutputMetrics.Text;
                directagAction.OutputDir = tbOutputDir.Text;
                if (cbRecovery.Checked)
                {
                    directagAction.AddLabel = true;
                    directagAction.IdpickerCfg = idpickerCfg;
                    directagAction.OutputFilenameForRecovery = tbOutFileNameForRecovery.Text;
                }
                Directory.SetCurrentDirectory(outputDir);

                if (Workspace.statusForm == null || Workspace.statusForm.IsDisposed)
                {
                    Workspace.statusForm = new TextBoxForm(this);
                    Workspace.statusForm.Show();
                    Application.DoEvents();
                }

                // run directag 

                bgDirectagRun.WorkerSupportsCancellation = true;
                bgDirectagRun.RunWorkerCompleted += bgDirectagRun_RunWorkerCompleted;

                DirecTagInfo directagInfo = new DirecTagInfo();
                bulidDirectagInfo(directagInfo);
                  
                //Thread t = new Thread(delegate() { directagAction.RunDirectag(); });
                //t.Start();
                //t.Join();
                //bgDirectagRun.ProgressChanged += bgDirectagRun_ProgressChanged;
                bgDirectagRun.RunWorkerAsync(directagAction);
            }
            else
            {
                #region error checking
                if (cbRemoval.Checked)
                {
                    if (tbInputFile.Text.Equals(string.Empty) || !File.Exists(tbInputFile.Text))
                    {
                        MessageBox.Show("Error: Please select input file!");
                        return;
                    }
                    if (tbOutputDir.Text.Equals(string.Empty) || !Directory.Exists(tbOutputDir.Text))
                    {
                        MessageBox.Show("Error: Please set up the correct output directory!");
                        return;
                    }
                    List<string> allowedFormat = new List<string>(new string[] { "mzXML", "mzxml", "mzML", "mzml", "mgf", "MGF" });
                    if (!allowedFormat.Exists(element => element.Equals(cmbOutputFileFormat.Text)))
                    {
                        MessageBox.Show("Please select proper output format");
                        return;
                    }
                    if (Convert.ToInt32(tbRemovalCutoff.Text) <= 0 || Convert.ToInt32(tbRemovalCutoff.Text) > 100)
                    {
                        MessageBox.Show("Please specify proper cutoff between 0 and 100!");
                        return;
                    }
                    if (tbMetricsFileForRemoval.Text.Equals(string.Empty) || tbOutFileNameForRemoval.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Please specify metrics file and output file name!");
                        return;
                    }
                }

                if (cbRecovery.Checked)
                {
                    if (tbMetricsFileForRecovery.Text.Equals(string.Empty) || !File.Exists(tbMetricsFileForRecovery.Text))
                    {
                        MessageBox.Show("Error: Please select metrics file!");
                        return;
                    }
                    if (tbOutputDir.Text.Equals(string.Empty) || !Directory.Exists(tbOutputDir.Text))
                    {
                        MessageBox.Show("Error: Please set up the correct output directory!");
                        return;
                    }
                    if (tbOutFileNameForRecovery.Text.Equals(string.Empty))
                    {
                        MessageBox.Show("Error: Please specify output file!");
                        return;
                    }
                    if (idpickerCfg == null)
                    {
                        MessageBox.Show("Please configurate IDPicker settings");
                        return;
                    }
                }
                #endregion

                // spectra removal based on metrics file, without running directag
                if (cbRemoval.Checked)
                {
                    string outputDir = tbOutputDir.Text;
                    WriteSpectraAction writeHighQualSpectra = new WriteSpectraAction();
                    writeHighQualSpectra.InFile = tbInputFile.Text;
                    writeHighQualSpectra.MetricsFile = tbMetricsFileForRemoval.Text;
                    writeHighQualSpectra.Cutoff = Convert.ToSingle(tbRemovalCutoff.Text) / 100.0f;
                    writeHighQualSpectra.OutFormat = cmbOutputFileFormat.Text;
                    writeHighQualSpectra.OutFilename = tbOutFileNameForRemoval.Text;
                    Directory.SetCurrentDirectory(outputDir);

                    if (Workspace.statusForm == null || Workspace.statusForm.IsDisposed)
                    {
                        Workspace.statusForm = new TextBoxForm(this);
                        Workspace.statusForm.Show();
                        Application.DoEvents();
                    }

                    bgWriteSpectra.WorkerSupportsCancellation = true;
                    bgWriteSpectra.RunWorkerCompleted += bgWriteSpectra_RunWorkerCompleted;
                    bgWriteSpectra.RunWorkerAsync(writeHighQualSpectra);
                }

                // add spectra label based on metrics file, without running directag
                if (cbRecovery.Checked)
                {
                    string outputDir = tbOutputDir.Text;
                    AddSpectraLabelAction addSpectraLabelAction = new AddSpectraLabelAction();
                    addSpectraLabelAction.MetricsFile = tbMetricsFileForRecovery.Text;
                    addSpectraLabelAction.IdpCfg = idpickerCfg;
                    addSpectraLabelAction.OutDir = outputDir;
                    addSpectraLabelAction.OutFilename = tbOutFileNameForRecovery.Text;
                    Directory.SetCurrentDirectory(outputDir);

                    if (Workspace.statusForm == null || Workspace.statusForm.IsDisposed)
                    {
                        Workspace.statusForm = new TextBoxForm(this);
                        Workspace.statusForm.Show();
                        Application.DoEvents();
                    }

                    bgAddLabels.WorkerSupportsCancellation = true;
                    bgAddLabels.RunWorkerCompleted += bgAddLabels_RunWorkerCompleted;
                    bgAddLabels.RunWorkerAsync(addSpectraLabelAction);

                }
            }



        }

        private void bgDirectagRun_DoWork(object sender, DoWorkEventArgs e)
        {
            // Do not access the form's BackgroundWorker reference directly.
            // Instead, use the reference provided by the sender parameter.
            BackgroundWorker bw = sender as BackgroundWorker;
            // Extract the argument.
            RunDirecTagAction arg = e.Argument as RunDirecTagAction;
            // If the operation was canceled by the user, 
            // set the DoWorkEventArgs.Cancel property to true.
            //if (bw.CancellationPending)
            //{
            //    e.Cancel = true;
            //    return;
            //}
            // Start the time-consuming operation.
            //e.Result = TimeConsumingOperation(bw, arg);
            while (!bw.CancellationPending)
            {
                arg.RunDirectag();
                return;
            }

        }

        private void bgWriteSpectra_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            WriteSpectraAction arg = e.Argument as WriteSpectraAction;
            while (!bw.CancellationPending)
            {
                arg.Write();
                return;
            }

        }

        private void bgAddLabels_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            AddSpectraLabelAction arg = e.Argument as AddSpectraLabelAction;
            while (!bw.CancellationPending)
            {
                arg.AddSpectraLable();
                return;
            }
        }

        // This event handler demonstrates how to interpret 
        // the outcome of the asynchronous operation implemented
        // in the DoWork event handler.
        private void bgDirectagRun_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //if (e.Cancelled)
            //{
            //    MessageBox.Show("Operation was canceled");
            //}
            //else
            if (e.Error != null)
            {
                string msg = String.Format("An error occurred: {0}", e.Error.Message);
                MessageBox.Show(msg);
            }
            //else
            //{
            //    string msg = "Operation completed";
            //    MessageBox.Show(msg);
            //}
        }

        private void bgWriteSpectra_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                string msg = String.Format("An error occurred: {0}", e.Error.Message);
                MessageBox.Show(msg);
            }
        }

        private void bgAddLabels_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                string msg = String.Format("An error occurred: {0}", e.Error.Message);
                MessageBox.Show(msg);
            }
        }

        public void CancelBgWorker()
        {
            if (bgDirectagRun.IsBusy)
            {
                bgDirectagRun.CancelAsync();
            }
            if (bgWriteSpectra.IsBusy)
            {
                bgWriteSpectra.CancelAsync();
            }
            if (bgAddLabels.IsBusy)
            {
                bgAddLabels.CancelAsync();
            }
        }

    }
}
