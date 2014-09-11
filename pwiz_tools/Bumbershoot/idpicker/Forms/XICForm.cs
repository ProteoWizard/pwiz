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
    public partial class XICForm : Form
    {
        private NHibernate.ISession session;
        private string _rFilepath;
        private Embedder.XICConfiguration _config;
        private bool _editMode = false;
        private double _maxQValue = 0.05;

        public XICForm(Embedder.XICConfiguration oldConfig, double maxQValue)
        {
            this.session = null;
            _config = oldConfig;
            _editMode = true;
            _maxQValue = maxQValue;
            InitializeComponent();
        }

        public XICForm(NHibernate.ISession session, double maxQValue)
        {
            this.session = session.SessionFactory.OpenSession();
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
            {
                _rFilepath = Directory.GetFiles(rPath, "Rscript.exe", SearchOption.AllDirectories).LastOrDefault();
                if (_rFilepath == null)
                    RTAlignBox.Enabled = false;
            }

            //load config into form
            if (_editMode)
            {
                StartButton.Text = "OK";
                SourceLocationLabel.Visible = false;
                SourceLocationBox.Visible = false;
                SourceLocationBrowse.Visible = false;

                MonoisotopicAdjustmentMinBox.Value = _config.MonoisotopicAdjustmentMin;
                MonoisotopicAdjustmentMaxBox.Value = _config.MonoisotopicAdjustmentMax;
                RTTolLowerBox.Value = _config.RetentionTimeLowerTolerance;
                RTTolUpperBox.Value = _config.RetentionTimeUpperTolerance;
                ChromatogramMzLowerOffsetValueBox.Value = (decimal)_config.ChromatogramMzLowerOffset.value;
                ChromatogramMzUpperOffsetValueBox.Value = (decimal)_config.ChromatogramMzUpperOffset.value;
                ChromatogramMzLowerOffsetUnitsBox.SelectedIndex = (int)_config.ChromatogramMzLowerOffset.units;
                ChromatogramMzUpperOffsetUnitsBox.SelectedIndex = (int)_config.ChromatogramMzUpperOffset.units;
            }
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
            _config = new Embedder.XICConfiguration
            {
                ChromatogramMzLowerOffset = new MZTolerance(cloValue, cloUnits),
                ChromatogramMzUpperOffset = new MZTolerance(cuoValue, cuoUnits),
                RetentionTimeLowerTolerance = rtMin,
                RetentionTimeUpperTolerance = rtMax,
                MonoisotopicAdjustmentMax = maMax,
                MonoisotopicAdjustmentMin = maMin,
                MaxQValue = _maxQValue,
                AlignRetentionTime = false
            };
            if (_editMode || session == null)
            {
                this.DialogResult = DialogResult.OK;
                return;
            }
            new Thread(() =>
                {
                    string idpDbFilepath = session.Connection.GetDataSource();
                    var searchPath =
                        new StringBuilder(String.Join(";",
                                                      Util.StringCollectionToStringArray(
                                                          Properties.Settings.Default.SourcePaths)));
                    if (Directory.Exists(SourceLocationBox.Text))
                        searchPath.AppendFormat(";{0}", SourceLocationBox.Text);
                    try
                    {
                        // add location of original idpDBs to the search path
                        var mergedFilepaths =
                            session.CreateSQLQuery("SELECT DISTINCT Filepath FROM MergedFiles").List<string>();
                        foreach (var filepath in mergedFilepaths)
                            searchPath.AppendFormat(";{0}", System.IO.Path.GetDirectoryName(filepath));
                    }
                    catch
                    {
                        // ignore if MergedFiles does not exist
                    }

                    //get quanititation method
                    var quantitationMethodBySource = new Dictionary<int, Embedder.QuantitationConfiguration>();
                    var xicConfigMethodBySource = new Dictionary<int, Embedder.XICConfiguration>();
                    var rows = session.CreateSQLQuery(
                        "SELECT ss.Id, Name, COUNT(s.Id), IFNULL((SELECT LENGTH(MsDataBytes) FROM SpectrumSourceMetadata WHERE Id=ss.Id), 0), MAX(s.ScanTimeInSeconds), QuantitationMethod " +
                        "FROM SpectrumSource ss " +
                        "JOIN UnfilteredSpectrum s ON ss.Id=Source " +
                        "GROUP BY ss.Id")
                                      .List<object[]>()
                                      .Select(o => new
                                          {
                                              Id = Convert.ToInt32(o[0]),
                                              Name = (string) o[1],
                                              Spectra = Convert.ToInt32(o[2]),
                                              EmbeddedSize = Convert.ToInt32(o[3]),
                                              MaxScanTime = Convert.ToDouble(o[4]),
                                              QuantitationMethodIndex = Convert.ToInt32(o[5])
                                          });
                    foreach (var row in rows)
                    {
                        quantitationMethodBySource[row.Id] = new Embedder.QuantitationConfiguration
                            {
                                QuantitationMethod = (QuantitationMethod) row.QuantitationMethodIndex,
                                ReporterIonMzTolerance = new MZTolerance(0.015, MZTolerance.Units.MZ)
                            };
                        xicConfigMethodBySource[row.Id] = _config;
                    }
                    var ilr = new IterationListenerRegistry();
                    ilr.addListener(new XICIterationListener(this), 1);

                    Embedder.EmbedMS1Metrics(idpDbFilepath, searchPath.ToString(),
                                             Properties.Settings.Default.SourceExtensions,
                                             quantitationMethodBySource, xicConfigMethodBySource, ilr);
                    MessageBox.Show("Finished embedding MS1 information");
                    this.DialogResult = DialogResult.OK;
                }).Start();
            ContentPanel.Enabled = false;
        }

        public Embedder.XICConfiguration GetConfig()
        {
            return _config;
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

        private void SourceLocationBrowse_Click(object sender, EventArgs e)
        {
            var origin = string.Empty;
            if (SourceLocationBox.Text == "<Default>")
                origin = Path.GetDirectoryName(session.Connection.GetDataSource());
            else if (Directory.Exists(SourceLocationBox.Text))
                origin = SourceLocationBox.Text;
            var fbd = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(origin) && Directory.Exists(origin))
                fbd.SelectedPath = origin;
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                ValidateSourceLocation();
                SourceLocationBox.Text = fbd.SelectedPath;
            }
        }

        private void ValidateSourceLocation()
        {
            //TODO: implement method to validate source location
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
