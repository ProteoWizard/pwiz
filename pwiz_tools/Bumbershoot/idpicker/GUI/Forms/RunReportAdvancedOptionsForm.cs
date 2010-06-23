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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Configuration;

using IdPickerGui.MODEL;
using IdPickerGui.BLL;

namespace IdPickerGui
{
    public partial class RunReportAdvancedOptionsForm : Form
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public RunReportAdvancedOptionsForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Log Exceptions (and inner exceptions) to file. Show ExceptionsDialogForm.
        /// </summary>
        /// <param name="exc"></param>
        private void HandleExceptions(Exception exc)
        {
            ExceptionsDialogForm excForm = new ExceptionsDialogForm();
            StringBuilder sbDetails = new StringBuilder();

            try
            {
                ExceptionManager.logExceptionsByFormToFile(this, exc, DateTime.Now);

                Exception subExc = exc.InnerException;
                sbDetails.Append(exc.Message);

                while (subExc != null)
                {
                    sbDetails.Append(subExc.Message + "\r\n");
                    subExc = subExc.InnerException;
                }

                excForm.Details = sbDetails.ToString();
                excForm.Msg = "An error has occurred in the application.\r\n\r\n";
                excForm.loadForm(ExceptionsDialogForm.ExceptionType.Error);

                excForm.ShowDialog(this);
            }
            catch
            {
                throw exc;
            }
        }

        /// <summary>
        /// Add default score weight row to datagridview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAddScore_Click(object sender, EventArgs e)
        {
            try
            {
				dgvScoreInfo.Rows.Add();
                DataGridViewRow row = dgvScoreInfo.Rows[dgvScoreInfo.Rows.GetLastRow(DataGridViewElementStates.None)];
                row.Tag = new ScoreInfo("new score", "1.0");
				row.Cells["ScoreName"].Value = "new score";
				row.Cells["ScoreWeight"].Value = "1.0";
				row.Cells["ScoreOrder"].Value = "Ascending";
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while adding score\r\n", exc));
            }
            
        }

        /// <summary>
        /// Update current request (in RunReportForm) with values on
        /// this advanced options form (per request not stored in properties)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                updateRequestScoresAndWeights();

                updateRequestModOverrides();

                Close();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while saving advanced options\r\n", exc));
            }

        }

        /// <summary>
        /// Scores and Weights panel and Mods panel are on top each other with
        /// navigation on left
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tvAdvOptionsNav_AfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {

                if (e.Node.Index == 0)
                {
                    gbScores.Visible = true;
                    gbMods.Visible = false;
                }
                if (e.Node.Index == 1)
                {
                    gbScores.Visible = false;
                    gbMods.Visible = true;
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while selecting advanced option\r\n", exc));
            }

        }

        /// <summary>
        /// Add default mod row to datagridview
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAddMod_Click(object sender, EventArgs e)
        {           
            try
            {
				dgvModOverrides.Rows.Add();
				DataGridViewRow row = dgvModOverrides.Rows[dgvModOverrides.Rows.GetLastRow( DataGridViewElementStates.None )];
                row.Tag = new ModOverrideInfo("A", "0.0", (rbDistinct.Checked ? "Indistinct" : "Distinct"));
				row.Cells["AminoAcid"].Value = "A";
				row.Cells["Mass"].Value = "0.0";
				row.Cells["Type"].Value = ( rbDistinct.Checked ? "Indistinct" : "Distinct" );
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while adding mod\r\n", exc));
            }
        }

        /// <summary>
        /// Setup values on form according to values in current report request
        /// (stored in RunReportForm) so they hold their value per report run
        /// but are reset each time to the defaults in the properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunReportAdvancedOptionsForm_Load(object sender, EventArgs e)
        {
            try
            {
                RunReportForm parentForm = (RunReportForm)this.Owner;

                loadFormDefaultsFromRequest(parentForm.IdPickerRequest);

                tvAdvOptionsNav.SelectedNode = tvAdvOptionsNav.Nodes[0];
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while loading advanced options\r\n", exc));
            }
        }

        /// <summary>
        /// Clear mods dgv
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClearMods_Click(object sender, EventArgs e)
        {
            try
            {
				dgvModOverrides.Rows.Clear();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while clearing mods\r\n", exc));
            }

        }

        /// <summary>
        /// Clear scores dgv
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClearScores_Click(object sender, EventArgs e)
        {
            try
            {
				dgvScoreInfo.Rows.Clear();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while clearing scores\r\n", exc));
            }
        }

        /* TODOL: Orig were store all report values in user settings
        private void loadFormDefaultsFromProperties()
        {
            try
            {
                if (Properties.Settings.Default.NormalizeSearchScores.Equals("1"))
                {
                    cbNormalizeScores.Checked = true;
                }

                if (Properties.Settings.Default.ApplyScoreOptimization.Equals("1"))
                {
                    cbApplyScoreOptimization.Checked = true;
                }

                foreach (string s in Properties.Settings.Default.Scores)
                {
                    string[] scoreData = s.Split(',');

                    lbScores.Items.Add(new ScoreInfo(scoreData[0], scoreData[1]));
                }

                foreach (string s in Properties.Settings.Default.Mods)
                {
                    string[] modData = s.Split(',');

                    lbMods.Items.Add(new ModOverrideInfo(modData[0].Trim(), modData[1].Trim(), Convert.ToInt32(modData[2].Trim())));
                }

            }
            catch (Exception)
            {
                MessageBox.Show("Error loading default values test.", "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

         */
      
        /// <summary>
        /// Load form values with values in current request
        /// </summary>
        /// <param name="IdPickerRequest"></param>
        private void loadFormDefaultsFromRequest(IDPickerInfo IdPickerRequest)
        {
            try
            {
                tbOptimizeScorePermutations.Text = IdPickerRequest.OptimizeScorePermutations.ToString();
                cbNormalizeScores.Checked = Convert.ToBoolean(IdPickerRequest.NormalizeSearchScores);
                cbApplyScoreOptimization.Checked = Convert.ToBoolean(IdPickerRequest.OptimizeScoreWeights);

                foreach (ScoreInfo score in IdPickerRequest.ScoreWeights)
                {
					dgvScoreInfo.Rows.Add();
					DataGridViewRow row = dgvScoreInfo.Rows[dgvScoreInfo.Rows.GetLastRow( DataGridViewElementStates.None )];
					row.Tag = score;
					row.Cells["ScoreName"].Value = score.ScoreName;
					row.Cells["ScoreWeight"].Value = Math.Abs( score.ScoreWeight );
					row.Cells["ScoreOrder"].Value = ( score.ScoreWeight >= 0 ? "Ascending" : "Descending" );
                }

				if( IdPickerRequest.ModsAreDistinctByDefault == true )
					rbDistinct.Checked = true;
				else
					rbIndistinct.Checked = true;

                foreach (ModOverrideInfo mod in IdPickerRequest.ModOverrides)
                {
                    dgvModOverrides.Rows.Add();
					DataGridViewRow row = dgvModOverrides.Rows[dgvModOverrides.Rows.GetLastRow( DataGridViewElementStates.None )];
					row.Tag = mod;
					row.Cells["AminoAcid"].Value = mod.Name;
					row.Cells["Mass"].Value = mod.Mass;
					row.Cells["Type"].Value = mod.Type.ModTypeDesc;
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading default values\r\n", exc);
            }



        }

        /// <summary>
        /// Save scores and weights values to properties if click
        /// set as default
        /// </summary>
        private void saveScoresAndWeightsOptionsAsDefault()
        {
            try
            {
                IDPicker.Properties.Settings.Default.Scores.Clear();

				foreach( DataGridViewRow row in dgvScoreInfo.Rows )
				{
					ScoreInfo score = row.Tag as ScoreInfo;
                    IDPicker.Properties.Settings.Default.Scores.Add(score.ScoreName + "," + score.ScoreWeight);
				}

                IDPicker.Properties.Settings.Default.NormalizeSearchScores = cbNormalizeScores.Checked;
                IDPicker.Properties.Settings.Default.ApplyScoreOptimization = cbApplyScoreOptimization.Checked;
                IDPicker.Properties.Settings.Default.OptimizeScorePermutations = Convert.ToInt32(tbOptimizeScorePermutations.Text);

                IDPicker.Properties.Settings.Default.Save();

            }
            catch (Exception e)
            {
                throw new Exception("Error saving scores and weights values\r\n", e);
            }
        }

        /// <summary>
        /// Save mod values to properties if click set as default
        /// </summary>
        private void saveModsOverridesOptionsAsDefault()
        {
            try
            {
                IDPicker.Properties.Settings.Default.ModsAreDistinctByDefault = rbDistinct.Checked;

                IDPicker.Properties.Settings.Default.Mods.Clear();

                foreach( DataGridViewRow row in dgvModOverrides.Rows )
                {
					ModOverrideInfo mod = row.Tag as ModOverrideInfo;
                    IDPicker.Properties.Settings.Default.Mods.Add(mod.Name + "," + mod.Mass.ToString() + "," + mod.Type.ModTypeValue.ToString());
                }

                IDPicker.Properties.Settings.Default.Save();

            }
            catch (Exception e)
            {
                throw new Exception("Error saving mod override values\r\n", e);
            }
        }

        /// <summary>
        /// Update mods in currect request (resides in RunReportForm)
        /// </summary>
        private void updateRequestModOverrides()
        {
			List<ModOverrideInfo> modInfos = new List<ModOverrideInfo>();

            try
            {
                RunReportForm parentForm = (RunReportForm)this.Owner;

				foreach( DataGridViewRow row in dgvModOverrides.Rows )
				{
					ModOverrideInfo mod = row.Tag as ModOverrideInfo;
					modInfos.Add( mod );
				}

				parentForm.IdPickerRequest.ModsAreDistinctByDefault = rbDistinct.Checked;
                parentForm.IdPickerRequest.ModOverrides = modInfos.ToArray();

            }
            catch (Exception exc)
            {
                throw new Exception("Error updating mod override values\r\n", exc);
            }


        }


        /// <summary>
        /// Update scores in current request (resides in RunReportForm)
        /// </summary>
        private void updateRequestScoresAndWeights()
        {
			List<ScoreInfo> scoreInfos = new List<ScoreInfo>();

            try
            {
                RunReportForm parentForm = (RunReportForm)this.Owner;

                if (!tbOptimizeScorePermutations.Text.Equals(string.Empty))
                {
                    parentForm.IdPickerRequest.OptimizeScorePermutations = Convert.ToInt32(tbOptimizeScorePermutations.Text);
                }
                else
                {
                    parentForm.IdPickerRequest.OptimizeScorePermutations = 0;
                }

                parentForm.IdPickerRequest.NormalizeSearchScores = cbNormalizeScores.Checked;
                parentForm.IdPickerRequest.OptimizeScoreWeights = cbApplyScoreOptimization.Checked;

				foreach( DataGridViewRow row in dgvScoreInfo.Rows )
				{
					ScoreInfo score = row.Tag as ScoreInfo;
					scoreInfos.Add( score );
				}

                parentForm.IdPickerRequest.ScoreWeights = scoreInfos.ToArray();
            }
            catch (Exception exc)
            {
                throw new Exception("Error updating scores and weights values\r\n", exc);
            }


        }

        /// <summary>
        /// (Set as default) Save scores and weights in properties and in current request
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveScoresAndWeightsOptions_Click(object sender, EventArgs e)
        {
            try
            {
                if (DialogResult.Yes == MessageBox.Show(this, "Are you sure you wish to set these values as default?", "Advanced Options", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                {
                    saveScoresAndWeightsOptionsAsDefault();

                    updateRequestScoresAndWeights();
                    
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while saving advanced options\r\n", exc));
             
            }

        }

        /// <summary>
        /// (Set as default) Save mods in properties and in current request
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveModOptions_Click(object sender, EventArgs e)
        {
            try
            {
                if (DialogResult.Yes == MessageBox.Show(this, "Are you sure you wish to set these values as default?", "Advanced Options", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                {
                    saveModsOverridesOptionsAsDefault();

                    updateRequestModOverrides();
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while saving advanced options\r\n", exc));
            }

        }

        /// <summary>
        /// Set mod tag for edited row in dgv
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void dgvModOverrides_CellEndEdit( object sender, DataGridViewCellEventArgs e )
		{
            DataGridViewRow row = dgvModOverrides.Rows[e.RowIndex];

            try
            {
                if (row.Tag == null)
                    row.Tag = new ModOverrideInfo();

                ModOverrideInfo mod = row.Tag as ModOverrideInfo;
                mod.Name = row.Cells["AminoAcid"].Value.ToString();
                mod.Mass = Convert.ToSingle(row.Cells["Mass"].Value.ToString());
                mod.Type = new ModType(row.Cells["Type"].Value.ToString());
            }
            catch (Exception exc)
            {
                row.Cells["AminoAcid"].Value = "A";
                row.Cells["Mass"].Value = "0.0";
                row.Cells["Type"].Value = (rbDistinct.Checked ? "Indistinct" : "Distinct");


                HandleExceptions(new Exception("Error occurred while editing advanced options\r\n", exc));
            }

		}

        /// <summary>
        /// Set score tag for edited row in dgv
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void dgvScoreInfo_CellEndEdit( object sender, DataGridViewCellEventArgs e )
		{
            DataGridViewRow row = dgvScoreInfo.Rows[e.RowIndex];

            try
            {
                if (row.Tag == null)
                    row.Tag = new ScoreInfo();

                ScoreInfo score = row.Tag as ScoreInfo;
                score.ScoreName = row.Cells["ScoreName"].Value.ToString();
                score.ScoreWeight = Convert.ToSingle(row.Cells["ScoreWeight"].Value.ToString());
                if (row.Cells["ScoreOrder"].Value.ToString() == "Descending")
                    score.ScoreWeight *= -1;
            }
            catch (Exception exc)
            {
                row.Cells["ScoreName"].Value = "new score";
                row.Cells["ScoreWeight"].Value = "1.0";
                row.Cells["ScoreOrder"].Value = "Ascending";

                HandleExceptions(new Exception("Error occurred while editing advanced options\r\n", exc));
            }

		}

        /// <summary>
        /// If applyscoreoptimization checked then optimizescorepermutations value must be 1 or greater.
        /// This handler notifies and resets each accordingly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cbApplyScoreOptimization_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (cbApplyScoreOptimization.Checked)
                {
                    if (Convert.ToInt32(tbOptimizeScorePermutations.Text) == 0)
                    {
                        MessageBox.Show(this, "Optimize Score Permutations must be 1 or greater.", "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        tbOptimizeScorePermutations.Text = "1";
                    }
                }
                else
                {
                    if (Convert.ToInt32(tbOptimizeScorePermutations.Text) != 0)
                    {
                        tbOptimizeScorePermutations.Text = "0";

                        MessageBox.Show(this, "Optimize Score Permutations has been reset to 0.", "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error occurred while editing advanced options\r\n", exc));
            }

        }
       
    }
}