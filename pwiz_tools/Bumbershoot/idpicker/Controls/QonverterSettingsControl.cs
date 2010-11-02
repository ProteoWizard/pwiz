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
    /// Allows viewing and manipulation of a QonverterSettings instance.
    /// </summary>
    public partial class QonverterSettingsControl : UserControl
    {
        public QonverterSettingsControl ()
        {
            InitializeComponent();

            scoreNameColumn.ValueType = typeof(string);
            scoreWeightColumn.ValueType = typeof(double);
            scoreOrderColumn.Items.AddRange(Enum.GetNames(typeof(Qonverter.Settings.Order)));
            scoreNormalizationColumn.Items.AddRange(Enum.GetNames(typeof(Qonverter.Settings.NormalizationMethod)));
        }

        private QonverterSettings qonverterSettings = null;
        public QonverterSettings QonverterSettings
        {
            set
            {
                qonverterSettings = value;
                if(qonverterSettings == null)
                    return;

                qonvertMethodComboBox.SelectedIndex = (int) value.QonverterMethod;
                rerankingCheckbox.Checked = value.RerankMatches;

                scoreGridView.Rows.Clear();
                foreach (var kvp in value.ScoreInfoByName)
                    scoreGridView.Rows.Add(kvp.Key,
                                           kvp.Value.Weight,
                                           Enum.GetName(typeof(Qonverter.Settings.Order), kvp.Value.Order),
                                           Enum.GetName(typeof(Qonverter.Settings.NormalizationMethod), kvp.Value.NormalizationMethod));
            }

            get { return qonverterSettings; }
        }

        public QonverterSettings EditedQonverterSettings
        {
            get
            {
                var qonverterSettings = new QonverterSettings()
                {
                    QonverterMethod = (Qonverter.QonverterMethod) qonvertMethodComboBox.SelectedIndex,
                    RerankMatches = rerankingCheckbox.Checked,
                    ScoreInfoByName = new Dictionary<string, Qonverter.Settings.ScoreInfo>()
                };

                foreach (DataGridViewRow row in scoreGridView.Rows)
                {
                    if (row.IsNewRow)
                        continue;

                    var scoreInfo = new Qonverter.Settings.ScoreInfo()
                    {
                        Weight = (double) row.Cells[1].Value,
                        Order = (Qonverter.Settings.Order) scoreOrderColumn.Items.IndexOf((string) row.Cells[2].Value),
                        NormalizationMethod = (Qonverter.Settings.NormalizationMethod) scoreNormalizationColumn.Items.IndexOf((string) row.Cells[3].Value)
                    };
                    qonverterSettings.ScoreInfoByName[(string) row.Cells[0].Value] = scoreInfo;
                }
                return qonverterSettings;
            }
        }

        public void CommitChanges ()
        {
            qonverterSettings = EditedQonverterSettings;
        }

        public bool IsDirty
        {
            get
            {
                if (qonverterSettings == null)
                    return false;

                var editedQonverterSettings = EditedQonverterSettings;
                bool isDirty = qonverterSettings.QonverterMethod != editedQonverterSettings.QonverterMethod ||
                               qonverterSettings.RerankMatches != editedQonverterSettings.RerankMatches ||
                               qonverterSettings.ScoreInfoByName.Count != editedQonverterSettings.ScoreInfoByName.Count;

                if (isDirty)
                    return true;

                foreach (var kvp in qonverterSettings.ScoreInfoByName)
                {
                    if (editedQonverterSettings.ScoreInfoByName.ContainsKey(kvp.Key))
                    {
                        var scoreInfo = editedQonverterSettings.ScoreInfoByName[kvp.Key];
                        if (scoreInfo.Weight != kvp.Value.Weight ||
                            scoreInfo.Order != kvp.Value.Order ||
                            scoreInfo.NormalizationMethod != kvp.Value.NormalizationMethod)
                            return true;
                    }
                    else
                        return true;
                }

                return false;
            }
        }

        private void flowLayoutPanel_Resize (object sender, EventArgs e)
        {
            scoreGridView.Width = flowLayoutPanel.Width;
        }
    }
}