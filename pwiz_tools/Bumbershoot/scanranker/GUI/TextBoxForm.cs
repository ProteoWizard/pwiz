using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace ScanRanker
{
    public partial class TextBoxForm : Form
    {
        private MainForm frmMain;
        public TextBoxForm(MainForm frmParent)
        {
            InitializeComponent();
            frmMain = frmParent;
        }

        private void TextBoxForm_Load(object sender, EventArgs e)
        {
            btnClose.Visible = false;
            btnStop.Visible = true;
        }

        private void FindAndKillProcess(string name)
        {
            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.StartsWith(name))
                {
                    //since we found the proccess we now need to use the
                    //Kill Method to kill the process. Remember, if you have
                    //the process running more than once, say IE open 4
                    //times the loop thr way it is now will close all 4,
                    //if you want it to just close the first one it finds
                    //then add a return; after the Kill
                    clsProcess.Kill();
                    //return;
                }
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Really quit the current session?", "Confirm Quit", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                //if (frmMain.bgDirectagRun.IsBusy)
                //{
                //    MessageBox.Show("directag bgw busy");
                //    frmMain.bgDirectagRun.WorkerSupportsCancellation = true;
                //    frmMain.bgDirectagRun.CancelAsync();
                //}
                //if (frmMain.bgWriteSpectra.IsBusy)
                //{
                //    frmMain.bgWriteSpectra.WorkerSupportsCancellation = true;
                //    frmMain.bgWriteSpectra.CancelAsync();
                //}
                //if (frmMain.bgAddLabels.IsBusy)
                //{
                //    frmMain.bgAddLabels.WorkerSupportsCancellation = true;
                //    frmMain.bgAddLabels.CancelAsync();
                //}

                frmMain.CancelBgWorker();

                FindAndKillProcess("directag");
                FindAndKillProcess("idpqonvert");
               // Close();   // closing current form won't release bg worker
               Application.Exit();
            }
            else
            {
                return;
            }
            
        }


       
    }
}
