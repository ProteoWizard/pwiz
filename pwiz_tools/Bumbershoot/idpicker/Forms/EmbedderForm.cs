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

            var rows = session.CreateSQLQuery("SELECT ss.Id, Name, COUNT(s.Id), IFNULL((SELECT LENGTH(MsDataBytes) FROM SpectrumSourceMetadata WHERE Id=ss.Id), 0), MAX(s.ScanTimeInSeconds), QuantitationMethod " +
                                              "FROM SpectrumSource ss " +
                                              "JOIN UnfilteredSpectrum s ON ss.Id=Source " +
                                              "GROUP BY ss.Id")
                              .List<object[]>()
                              .Select(o => new { Id = Convert.ToInt32(o[0]),
                                                 Name = (string) o[1],
                                                 Spectra = Convert.ToInt32(o[2]),
                                                 EmbeddedSize = Convert.ToInt32(o[3]),
                                                 MaxScanTime = Convert.ToDouble(o[4]),
                                                 QuantitationMethodIndex = Convert.ToInt32(o[5])
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

                dataGridView.Rows.Add(row.Id, row.Name, status, quantitationMethodColumn.Items[row.QuantitationMethodIndex]);
            }

            dataGridView.ResumeLayout();

            deleteAllButton.Enabled = hasEmbeddedSources;
            embedAllButton.Enabled = hasNonEmbeddedSources;
            okButton.Enabled = true;

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
            Form form;
            public EmbedderIterationListener (Form form) { this.form = form; }

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

                return IterationListener.Status.Ok;

                // TODO: support cancel
            }
        }

        private void embedAllButton_Click (object sender, EventArgs e)
        {
            var searchPath = new StringBuilder(searchPathTextBox.Text);
            string extensions = extensionsTextBox.Text;
            Application.UseWaitCursor = true;
            deleteAllButton.Enabled = embedAllButton.Enabled = okButton.Enabled = false;
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
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                int id = (int) row.Cells[idColumn.Index].Value;
                string methodString = (string) (row.Cells[quantitationMethodColumn.Index] as DataGridViewComboBoxCell).Value;
                int methodIndex = quantitationMethodColumn.Items.IndexOf(methodString);
                quantitationMethodBySource[id] = new Embedder.QuantitationConfiguration
                {
                    QuantitationMethod = (QuantitationMethod)methodIndex,
                    ReporterIonMzTolerance = new MZTolerance(0.015, MZTolerance.Units.MZ)
                };
            }

            new Thread(() =>
            {
                try
                {
                    var ilr = new IterationListenerRegistry();
                    ilr.addListener(new EmbedderIterationListener(this), 1);

                    string idpDbFilepath = session.Connection.GetDataSource();
                    if (scanTimeOnlyCheckBox.Checked)
                        Embedder.EmbedScanTime(idpDbFilepath, searchPath.ToString(), extensions, quantitationMethodBySource, ilr);
                    else
                        Embedder.Embed(idpDbFilepath, searchPath.ToString(), extensions, quantitationMethodBySource, ilr);
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("no filepath"))
                        Program.HandleException(ex);

                    bool multipleMissingFilepaths = ex.Message.Contains("\n");
                    string missingFilepaths = ex.Message.Replace("\n", "\r\n");
                    missingFilepaths = missingFilepaths.Replace("[embed] no", "No");
                    missingFilepaths = missingFilepaths.Replace("[embedScanTime] no", "No");
                    MessageBox.Show(missingFilepaths + "\r\n\r\nCheck that " +
                                    (multipleMissingFilepaths ? "these source files" : "this source file") +
                                    " can be found in the search path with one of the specified extensions.");
                }
                BeginInvoke(new MethodInvoker(() => Refresh()));
            }).Start();
        }

        private void deleteAllButton_Click (object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete\r\n" +
                                "all embedded spectra and quantitation data?", "Confirm",
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
            DialogResult = embeddedChanges ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }

        private void dataGridView_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == quantitationMethodColumn.Index)
            {
                embedAllButton.Enabled = true;

                var selectedRows = dataGridView.SelectedCells.Cast<DataGridViewCell>().Select(o => o.OwningRow).ToList();
                if (selectedRows.Count > 1)
                    selectedRows.ForEach(o => o.Cells[e.ColumnIndex].Value = dataGridView[e.ColumnIndex, e.RowIndex].Value);
            }
        }
    }
}
