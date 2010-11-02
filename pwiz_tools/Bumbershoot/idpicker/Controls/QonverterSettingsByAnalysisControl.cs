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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using Iesi.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;

namespace IDPicker.Controls
{
    /// <summary>
    /// Represents the method called when a user wants to view or manipulate their QonverterSettings presets.
    /// </summary>
    public delegate void QonverterSettingsManagerNeeded();

    /// <summary>
    /// Allows a user to assign QonverterSettings presets to Analysis instances.
    /// </summary>
    public partial class QonverterSettingsByAnalysisControl : UserControl
    {
        IDictionary<Analysis, QonverterSettings> qonverterSettingsByAnalysis;
        QonverterSettingsManagerNeeded qonverterSettingsManagerNeeded;

        IDictionary<string, QonverterSettings> qonverterSettingsByName;

        public QonverterSettingsByAnalysisControl (IDictionary<Analysis, QonverterSettings> qonverterSettingsByAnalysis,
                                                   QonverterSettingsManagerNeeded qonverterSettingsManagerNeeded)
        {
            InitializeComponent();

            if (qonverterSettingsManagerNeeded == null)
                throw new NullReferenceException();

            this.qonverterSettingsByAnalysis = qonverterSettingsByAnalysis;
            this.qonverterSettingsManagerNeeded = qonverterSettingsManagerNeeded;

            qonverterSettingsByName = QonverterSettings.LoadQonverterSettings();

            qonverterSettingsByName.Keys.ToList().ForEach(o => qonverterSettingsColumn.Items.Add(o));
            qonverterSettingsColumn.Items.Add("Edit...");

            foreach (var a in qonverterSettingsByAnalysis.Keys)
            {
                var row = new DataGridViewRow();
                row.CreateCells(dataGridView);

                ISet<AnalysisParameter> diffParameters = new SortedSet<AnalysisParameter>();
                foreach (var a2 in qonverterSettingsByAnalysis.Keys)
                {
                    if (a.Software.Name != a2.Software.Name)
                        continue;

                    diffParameters = diffParameters.Union(a.Parameters.Minus(a2.Parameters));
                }

                string key = a.Id + ": " + a.Name;
                foreach (var p in diffParameters)
                    key += String.Format("; {0}={1}", p.Name, p.Value);

                row.Tag = a;
                row.Cells[0].Value = key;
                row.Cells[1].Value = Properties.Settings.Default.DecoyPrefix;
                var comboBox = row.Cells[2] as DataGridViewComboBoxCell;
                comboBox.Value = qonverterSettingsByName.Keys.First(o => o.ToLower().Contains(a.Software.Name.ToLower()));
                dataGridView.Rows.Add(row);
            }

            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                qonverterSettingsByAnalysis[row.Tag as Analysis] = qonverterSettingsByName[(string) row.Cells[2].Value];
                qonverterSettingsByAnalysis[row.Tag as Analysis].Analysis = row.Tag as Analysis;
                qonverterSettingsByAnalysis[row.Tag as Analysis].DecoyPrefix = (string) row.Cells[1].Value;
            }

            dataGridView.CellBeginEdit += new DataGridViewCellCancelEventHandler(dataGridView_CellBeginEdit);
            dataGridView.CurrentCellDirtyStateChanged += new EventHandler(dataGridView_CurrentCellDirtyStateChanged);
        }

        string uneditedQonverterSettingsValue = null;
        void dataGridView_CellBeginEdit (object sender, DataGridViewCellCancelEventArgs e)
        {
            //throw new NotImplementedException();
            uneditedQonverterSettingsValue = (string) dataGridView[e.ColumnIndex, e.RowIndex].Value;
        }

        void dataGridView_CurrentCellDirtyStateChanged (object sender, EventArgs e)
        {
            var cell = dataGridView.CurrentCell;
            var row = cell.OwningRow;

            dataGridView.EndEdit();
            dataGridView.NotifyCurrentCellDirty(false);

            if ((string) cell.EditedFormattedValue == "Edit..." || (string) cell.Value == "Edit...")
            {
                qonverterSettingsManagerNeeded();

                qonverterSettingsByName = QonverterSettings.LoadQonverterSettings();

                cell.Value = uneditedQonverterSettingsValue;
                dataGridView.RefreshEdit();
            }

            qonverterSettingsByAnalysis[row.Tag as Analysis] = qonverterSettingsByName[(string) row.Cells[2].Value];
            qonverterSettingsByAnalysis[row.Tag as Analysis].DecoyPrefix = (string) row.Cells[1].Value;
        }
    }
}