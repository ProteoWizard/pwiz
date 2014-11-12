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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s): 
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using IDPicker;
using Microsoft.WindowsAPICodePack.Taskbar;
using NHibernate.Linq;
using IDPicker.DataModel;
using pwiz.CLI.chemistry;
using pwiz.CLI.util;

namespace IDPicker.Forms
{
    public partial class EmbedderForm : Form
    {
        private NHibernate.ISession session;
        private bool hasEmbeddedSources, hasNonEmbeddedSources;
        private bool embeddedChanges; // true if the embedded data has changed
        private Dictionary<int, object[]> _savedRowSettings;

        public bool CancelRequested { get; private set; }
        public bool EmbedInProgress { get; private set; }

        public EmbedderForm (NHibernate.ISession session)
        {
            this.session = session.SessionFactory.OpenSession();

            InitializeComponent();
        }

        protected override void OnLoad (EventArgs e)
        {
            searchPathTextBox.Text = String.Join(";", Util.StringCollectionToStringArray(Properties.Settings.Default.SourcePaths));
            extensionsTextBox.Text = Properties.Settings.Default.SourceExtensions;
            embeddedChanges = false;
            _savedRowSettings = new Dictionary<int, object[]>();

            CancelRequested = false;
            EmbedInProgress = false;

            if (Owner != null && StartPosition == FormStartPosition.CenterParent)
                Location = new Point(Owner.Location.X + Owner.Width / 2 - Width / 2,
                                     Owner.Location.Y + Owner.Height / 2 - Height / 2);

            base.OnLoad(e);

            Refresh();
        }

        public override void Refresh ()
        {
            Text = "Embed Subset Spectra";
            Application.UseWaitCursor = false;

            dataGridView.SuspendLayout();
            dataGridView.Rows.Clear();

            var rows = session.CreateSQLQuery(
                "SELECT ss.Id, Name, COUNT(s.Id), IFNULL((SELECT LENGTH(MsDataBytes) FROM SpectrumSourceMetadata WHERE Id=ss.Id), 0), MAX(s.ScanTimeInSeconds), QuantitationMethod, " +
                //"IFNULL((SELECT count() FROM XICMetrics x JOIN PeptideSpectrumMatch psm on x.PsmId=psm.Id JOIN Spectrum s on psm.Spectrum = s.Id where s.Source =ss.Id), 0) " +
                "IFNULL((SELECT TotalSpectra FROM XICMetricsSettings WHERE SourceId=ss.Id), 0), IFNULL((SELECT Settings FROM XICMetricsSettings WHERE SourceId=ss.Id), '') " +
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
                                      QuantitationMethodIndex = Convert.ToInt32(o[5]),
                                      XICTotal = Convert.ToInt32(o[6]),
                                      XICSettings = (string) o[7]
                                  });

            hasEmbeddedSources = hasNonEmbeddedSources = false;

            foreach (var row in rows)
            {
                string status;
                if (row.EmbeddedSize > 0)
                {
                    status = String.Format("{0} spectra embedded ({1} bytes)", row.Spectra, row.EmbeddedSize);
                    hasEmbeddedSources = true;
                }
                else if (row.QuantitationMethodIndex != 0)
                {
                    status = String.Format("{0} spectra with quantitation", row.Spectra);
                    hasEmbeddedSources = true;
                    hasNonEmbeddedSources = true;
                }
                else if (row.MaxScanTime > 0)
                {
                    status = String.Format("{0} spectra with scan times", row.Spectra);
                    hasNonEmbeddedSources = true;
                }
                else
                {
                    status = "not embedded";
                    hasNonEmbeddedSources = true;
                }
                var quantitationMethod = ((string)quantitationMethodColumn.Items[row.QuantitationMethodIndex]);
                if (_savedRowSettings.ContainsKey(row.Id))
                    quantitationMethod = _savedRowSettings[row.Id][0].ToString();
                if (quantitationMethod == "Label free")
                {
                    var newXIC = (_savedRowSettings.ContainsKey(row.Id))
                                     ? _savedRowSettings[row.Id][1]
                                     : new Embedder.XICConfiguration(row.XICSettings);
                    dataGridView.Rows.Add(row.Id, row.Name, status, quantitationMethod, newXIC);
                }
                else
                    dataGridView.Rows.Add(row.Id, row.Name, status, quantitationMethod, "n/a");
            }
            _savedRowSettings = new Dictionary<int, object[]>();

            dataGridView.ResumeLayout();

            if (dataGridView.Rows.Cast<DataGridViewRow>()
                                    .Any(x => x.Cells[quantitationMethodColumn.Index].Value.ToString() != "None"))
                embedScanTimeOnlyBox.Text = "Embed scan times and quantitation only";
            else
                embedScanTimeOnlyBox.Text = "Embed scan times only";
            deleteAllButton.Enabled = hasEmbeddedSources;
            embedAllButton.Enabled = hasNonEmbeddedSources;
            okButton.Text = "Close";
            okButton.Enabled = true;

            CancelRequested = false;
            EmbedInProgress = false;

            if (TaskbarManager.IsPlatformSupported)
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);

            base.Refresh();
        }

        protected override void OnClosing (CancelEventArgs e)
        {
            if (Application.UseWaitCursor)
                e.Cancel = true;
            else
                session.Dispose();

            base.OnClosing(e);
        }

        private class EmbedderIterationListener : IterationListener
        {
            EmbedderForm form;
            public EmbedderIterationListener(EmbedderForm form) { this.form = form; }

            public override Status update (UpdateMessage updateMessage)
            {
                var title = new StringBuilder(updateMessage.message);
                title[0] = Char.ToUpper(title[0]);
                title.AppendFormat(" ({0}/{1})", updateMessage.iterationIndex + 1, updateMessage.iterationCount);
                form.BeginInvoke(new MethodInvoker(() => form.Text = title.ToString()));

                if (TaskbarManager.IsPlatformSupported)
                {
                    TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
                    TaskbarManager.Instance.SetProgressValue(updateMessage.iterationIndex + 1, updateMessage.iterationCount);
                }

                return form.CancelRequested ? IterationListener.Status.Cancel : IterationListener.Status.Ok;
            }
        }

        private void embedAllButton_Click (object sender, EventArgs e)
        {
            var searchPath = new StringBuilder(searchPathTextBox.Text);
            string extensions = extensionsTextBox.Text;
            Application.UseWaitCursor = true;
            deleteAllButton.Enabled = embedAllButton.Enabled = false;
            embeddedChanges = true;

            try
            {
                // add location of original idpDBs to the search path
                var mergedFilepaths = session.CreateSQLQuery("SELECT DISTINCT Filepath FROM MergedFiles").List<string>();
                foreach (var filepath in mergedFilepaths)
                    searchPath.AppendFormat(";{0}", System.IO.Path.GetDirectoryName(filepath));
            }
            catch
            {
                // ignore if MergedFiles does not exist
            }

            var quantitationMethodBySource = new Dictionary<int, Embedder.QuantitationConfiguration>();
            var xicConfigBySource = new Dictionary<int, Embedder.XICConfiguration>{{0, new Embedder.XICConfiguration()}};
            _savedRowSettings = new Dictionary<int, object[]>();
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                int id = (int) row.Cells[idColumn.Index].Value;
                string methodString = (string) (row.Cells[quantitationMethodColumn.Index] as DataGridViewComboBoxCell).Value;
                int methodIndex = quantitationMethodColumn.Items.IndexOf(methodString);
                quantitationMethodBySource[id] = new Embedder.QuantitationConfiguration
                {
                    QuantitationMethod = (QuantitationMethod)methodIndex,
                    ReporterIonMzTolerance = new MZTolerance(((QuantitationMethod)methodIndex == QuantitationMethod.TMT10plex) ? 0.003 : 0.015, MZTolerance.Units.MZ)
                };
                _savedRowSettings[id] = new object[] { row.Cells[quantitationMethodColumn.Index].Value, row.Cells[quantitationSettingsColumn.Index].Value };
                if (quantitationMethodBySource[id].QuantitationMethod == QuantitationMethod.LabelFree)
                    xicConfigBySource[id] =
                        row.Cells[quantitationSettingsColumn.Index].Value as Embedder.XICConfiguration ??
                        new Embedder.XICConfiguration();
            }

            okButton.Text = "Cancel";
            EmbedInProgress = true;

            new Thread(() =>
            {
                try
                {
                    var ilr = new IterationListenerRegistry();
                    ilr.addListener(new EmbedderIterationListener(this), 1);

                    string idpDbFilepath = session.Connection.GetDataSource();
                    if (embedScanTimeOnlyBox.Checked)
                        Embedder.EmbedScanTime(idpDbFilepath, searchPath.ToString(), extensions, quantitationMethodBySource, ilr);
                    else
                        Embedder.Embed(idpDbFilepath, searchPath.ToString(), extensions, quantitationMethodBySource, ilr);
                    if (quantitationMethodBySource.Any(x=>x.Value.QuantitationMethod==QuantitationMethod.LabelFree))
                    {
                        BeginInvoke(new MethodInvoker(() => ModeandDefaultPanel.Visible = false));
                        Embedder.EmbedMS1Metrics(idpDbFilepath, searchPath.ToString(), extensions, quantitationMethodBySource, xicConfigBySource, ilr);
                        BeginInvoke(new MethodInvoker(() => ModeandDefaultPanel.Visible = true));
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("QuantitationConfiguration"))
                    {
                        string message = ex.Message.Replace("[QuantitationConfiguration] ", "");
                        message = Char.ToUpper(message[0]) + message.Substring(1);
                        MessageBox.Show(message);
                    }
                    else if (ex.Message.Contains("no filepath"))
                    {
                        bool multipleMissingFilepaths = ex.Message.Contains("\n");
                        string missingFilepaths = ex.Message.Replace("\n", "\r\n");
                        missingFilepaths = missingFilepaths.Replace("[embed] no", "No");
                        missingFilepaths = missingFilepaths.Replace("[embedScanTime] no", "No");
                        MessageBox.Show(missingFilepaths + "\r\n\r\nCheck that " +
                                        (multipleMissingFilepaths ? "these source files" : "this source file") +
                                        " can be found in the search path with one of the specified extensions.");
                    }
                    else
                        Program.HandleException(ex);
                }
                BeginInvoke(new MethodInvoker(() => Refresh()));
            }).Start();
        }

        private void deleteAllButton_Click (object sender, EventArgs e)
        {
            var message = "Are you sure you want to delete\r\nall embedded spectra and quantitation data?";
            if (MessageBox.Show(message, "Confirm",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Exclamation,
                                MessageBoxDefaultButton.Button2) == DialogResult.No)
                return;

            Text = "Deleting embedded spectra (this could take a few minutes)";
            Application.UseWaitCursor = true;
            deleteAllButton.Enabled = embedAllButton.Enabled = okButton.Enabled = false;
            embeddedChanges = true;

            if (TaskbarManager.IsPlatformSupported)
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Indeterminate);

            Refresh();

            new Thread(() =>
            {
                try
                {
                    var tls = session.SessionFactory.OpenStatelessSession();
                    tls.CreateSQLQuery(@"UPDATE SpectrumSourceMetadata SET MsDataBytes = NULL;
                                         UPDATE SpectrumSource SET QuantitationMethod = 0;
                                         DELETE FROM PeptideQuantitation;
                                         DELETE FROM DistinctMatchQuantitation;
                                         DELETE FROM ProteinQuantitation;
                                         DELETE FROM SpectrumQuantitation;
                                         DELETE FROM XICMetrics;
                                         DELETE FROM XICMetricsSettings;
                                         VACUUM
                                        ").ExecuteUpdate();
                }
                catch (Exception ex)
                {
                    Program.HandleException(ex);
                }
                BeginInvoke(new MethodInvoker(() => Refresh()));
            }).Start();
        }

        private void okButton_Click (object sender, EventArgs e)
        {
            if (EmbedInProgress)
            {
                CancelRequested = true;
                okButton.Enabled = false;
            }
            else
            {
                DialogResult = embeddedChanges ? DialogResult.OK : DialogResult.Cancel;
                Close();
            }
        }

        private void dataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;
            if (e.ColumnIndex == quantitationMethodColumn.Index)
            {
                embedAllButton.Enabled = true;

                var selectedRows =
                    dataGridView.SelectedCells.Cast<DataGridViewCell>().Select(o => o.OwningRow).ToList();
                if (selectedRows.Count > 1)
                    selectedRows.ForEach(
                        o => o.Cells[e.ColumnIndex].Value = dataGridView[e.ColumnIndex, e.RowIndex].Value);

                var rowID = (int)dataGridView[idColumn.Index, e.RowIndex].Value;
                var xicConfig = dataGridView[quantitationSettingsColumn.Index, e.RowIndex].Value;
                if (xicConfig is Embedder.XICConfiguration &&
                    dataGridView[quantitationMethodColumn.Index, e.RowIndex].Value.ToString() != "Label free")
                {
                    if (dataGridView[quantitationSettingsColumn.Index, e.RowIndex].Value is
                        Embedder.XICConfiguration)
                        _savedRowSettings[rowID] = new object[] { "Label free", dataGridView[quantitationSettingsColumn.Index, e.RowIndex].Value };
                    dataGridView[quantitationSettingsColumn.Index, e.RowIndex].Value = "n/a";
                }
                else if (_savedRowSettings.ContainsKey(rowID) &&
                            _savedRowSettings[rowID][1] is Embedder.XICConfiguration)
                    dataGridView[quantitationSettingsColumn.Index, e.RowIndex].Value = _savedRowSettings[rowID][1];
                else
                    dataGridView[quantitationSettingsColumn.Index, e.RowIndex].Value = new Embedder.XICConfiguration();
                dataGridView.InvalidateCell(quantitationSettingsColumn.Index, e.RowIndex);

                if (dataGridView.Rows.Cast<DataGridViewRow>()
                                .Any(x => x.Cells[quantitationMethodColumn.Index].Value.ToString() != "None"))
                    embedScanTimeOnlyBox.Text = "Embed scan times and quantitation only";
                else
                    embedScanTimeOnlyBox.Text = "Embed scan times only";
            }
        }

        private void defaultQuantitationMethodBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!quantitationMethodColumn.Items.Contains(defaultQuantitationMethodBox.Text))
                return;
            foreach (DataGridViewRow row in dataGridView.Rows)
                row.Cells[quantitationMethodColumn.Index].Value = defaultQuantitationMethodBox.Text;
            dataGridView.Invalidate();
        }

        private void defaultXICSettingsButton_Click(object sender, EventArgs e)
        {
            var maxQValue = 0.05;
            var mostRecentFilter = session.Query<PersistentDataFilter>().OrderByDescending(o => o.Id).FirstOrDefault();
            if (mostRecentFilter != null)
                maxQValue = mostRecentFilter.MaximumQValue;
            var xicForm = new XICForm(new Embedder.XICConfiguration(), maxQValue);
            if (xicForm.ShowDialog() == DialogResult.OK)
                foreach (DataGridViewRow row in dataGridView.Rows)
                {
                    var newXIC = xicForm.GetConfig();
                    if (row.Cells[quantitationMethodColumn.Index].Value.ToString() != "Label free")
                        row.Cells[quantitationSettingsColumn.Index].Value = "n/a";
                    else
                        row.Cells[quantitationSettingsColumn.Index].Value = newXIC;
                }
        }

        private void dataGridView_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            var maxQValue = 0.05;
            var mostRecentFilter = session.Query<PersistentDataFilter>().OrderByDescending(o => o.Id).FirstOrDefault();
            if (mostRecentFilter != null)
                maxQValue = mostRecentFilter.MaximumQValue;
            if (e.ColumnIndex == quantitationSettingsColumn.Index)
            {
                var oldSettings = dataGridView[e.ColumnIndex, e.RowIndex].Value as Embedder.XICConfiguration;
                if (oldSettings == null)
                    return;
                var xicForm = new XICForm(oldSettings, maxQValue);
                if (xicForm.ShowDialog() == DialogResult.OK)
                    dataGridView[e.ColumnIndex, e.RowIndex].Value = xicForm.GetConfig();
            }
        }
    }
}
