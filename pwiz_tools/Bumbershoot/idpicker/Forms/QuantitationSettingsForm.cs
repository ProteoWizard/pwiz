//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using IDPicker.DataModel;
using Microsoft.WindowsAPICodePack.Taskbar;
using NHibernate.Linq;
using pwiz.CLI.chemistry;
using pwiz.CLI.util;

namespace IDPicker.Forms
{
    public partial class QuantitationSettingsForm : Form
    {
        private string _rFilepath;
        public Embedder.XICConfiguration XicConfig { get; private set; }
        public Embedder.QuantitationConfiguration IsobaricConfig { get; private set; }
        private double _maxQValue = 0.05;

        public QuantitationSettingsForm(Embedder.XICConfiguration oldXicConfig, Embedder.QuantitationConfiguration oldIsobaricConfig, double maxQValue)
        {
            XicConfig = oldXicConfig;
            IsobaricConfig = oldIsobaricConfig;
            _maxQValue = maxQValue;
            InitializeComponent();
        }

        private void XICForm_Load(object sender, EventArgs e)
        {
            //Check for presence of R
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesRoot = Path.GetPathRoot(programFiles);
            string rPath =string.Empty;
            if (Directory.Exists(Path.Combine(programFiles, "R")))
                rPath = Path.Combine(programFiles, "R");
            else if (Directory.Exists(Path.Combine(programFilesRoot, "Program Files/R")))
                rPath = Path.Combine(programFilesRoot, "Program Files/R");
            else if (Directory.Exists(Path.Combine(programFilesRoot, "Program Files (x86)/R")))
                rPath = Path.Combine(programFilesRoot, "Program Files (x86)/R");
            else
                RTAlignBox.Enabled = false;

            if (!string.IsNullOrEmpty(rPath))
                _rFilepath = Directory.GetFiles(rPath, "Rscript.exe", SearchOption.AllDirectories).LastOrDefault();
            else
                RTAlignBox.Enabled = false;

            //load config into form
            MonoisotopicAdjustmentMinBox.Value = XicConfig.MonoisotopicAdjustmentMin;
            MonoisotopicAdjustmentMaxBox.Value = XicConfig.MonoisotopicAdjustmentMax;
            RTTolLowerBox.Value = XicConfig.RetentionTimeLowerTolerance;
            RTTolUpperBox.Value = XicConfig.RetentionTimeUpperTolerance;
            ChromatogramMzLowerOffsetValueBox.Value = (decimal)XicConfig.ChromatogramMzLowerOffset.value;
            ChromatogramMzUpperOffsetValueBox.Value = (decimal)XicConfig.ChromatogramMzUpperOffset.value;
            ChromatogramMzLowerOffsetUnitsBox.SelectedIndex = (int)XicConfig.ChromatogramMzLowerOffset.units;
            ChromatogramMzUpperOffsetUnitsBox.SelectedIndex = (int)XicConfig.ChromatogramMzUpperOffset.units;

            reporterIonToleranceUpDown.Value = (decimal)IsobaricConfig.ReporterIonMzTolerance.value;
            reporterIonToleranceUnits.SelectedIndex = (int)IsobaricConfig.ReporterIonMzTolerance.units;
            normalizeReporterIonsCheckBox.Checked = IsobaricConfig.NormalizeIntensities;
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            var cloValue = (double)ChromatogramMzLowerOffsetValueBox.Value;
            var cloUnits = (MZTolerance.Units)ChromatogramMzLowerOffsetUnitsBox.SelectedIndex;
            var cuoValue = (double)ChromatogramMzUpperOffsetValueBox.Value;
            var cuoUnits = (MZTolerance.Units)ChromatogramMzUpperOffsetUnitsBox.SelectedIndex;
            var rtMax = (int)Math.Round(RTTolUpperBox.Value);
            var rtMin = (int)Math.Round(RTTolLowerBox.Value);
            var maMax = (int)Math.Round(MonoisotopicAdjustmentMaxBox.Value);
            var maMin = (int)Math.Round(MonoisotopicAdjustmentMinBox.Value);
            XicConfig = new Embedder.XICConfiguration{
                ChromatogramMzLowerOffset = new MZTolerance(cloValue, cloUnits),
                ChromatogramMzUpperOffset = new MZTolerance(cuoValue, cuoUnits),
                RetentionTimeLowerTolerance = rtMin,
                RetentionTimeUpperTolerance = rtMax,
                MonoisotopicAdjustmentMax = maMax,
                MonoisotopicAdjustmentMin = maMin,
                MaxQValue = _maxQValue,
                AlignRetentionTime = RTAlignBox.Checked};

            IsobaricConfig = new Embedder.QuantitationConfiguration
            {
                QuantitationMethod = IsobaricConfig.QuantitationMethod,
                ReporterIonMzTolerance = new MZTolerance((double) reporterIonToleranceUpDown.Value, (MZTolerance.Units) reporterIonToleranceUnits.SelectedIndex),
                NormalizeIntensities = normalizeReporterIonsCheckBox.Checked
            };

            DialogResult = DialogResult.OK;
        }

        internal class XICIterationListener : IterationListener
        {
            Form form;
            public XICIterationListener(Form form) { this.form = form; }

            //private bool updateMutexActive = false;
            public override Status update(UpdateMessage updateMessage)
            {
                //if (updateMutexActive)
                //    return IterationListener.Status.Ok;
                //updateMutexActive = true;
                var title = new StringBuilder(updateMessage.message);
                title[0] = Char.ToUpper(title[0]);
                title.AppendFormat(" ({0}/{1})", updateMessage.iterationIndex + 1, updateMessage.iterationCount);
                form.BeginInvoke(new MethodInvoker(() =>
                    {
                        try
                        {
                            form.Text = title.ToString();
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(e.ToString());
                            throw;
                        }
                    }));

                return IterationListener.Status.Ok;
            }
        }

        private void RTAlignInfo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://www.r-project.org/");
        }

        private void NumericUpDownEnter(object sender, EventArgs e)
        {
            var control = sender as NumericUpDown;
            if (control == null)
                return;
            control.Select(0, control.Text.Length);
        }
    }
}
