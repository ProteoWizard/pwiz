/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class EnrichmentDialog : WorkspaceForm
    {
        private const string newTracerText = "<Define new tracer>";
        private List<DbTracerDef> _tracerDefs = new List<DbTracerDef>();
        private int _tracerIndex = -1;
        public EnrichmentDialog(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            _tracerDefs = workspace.GetDbTracerDefs();
            foreach (var tracerDef in _tracerDefs)
            {
                comboTracers.Items.Add(tracerDef.Name);
            }
            comboTracers.Items.Add(newTracerText);
            comboTracers.SelectedIndex = 0;
        }

        private void setEnrichment(DbTracerDef tracerDef)
        {
            tbxTracerSymbol.Text = tracerDef.TracerSymbol;
            tbxMassDifference.Text = tracerDef.DeltaMass.ToString();
            tbxAtomCount.Text = tracerDef.AtomCount.ToString();
            tbxAtomicPercentEnrichment.Text = tracerDef.AtomPercentEnrichment.ToString();
            cbxEluteEarlier.Checked = tracerDef.IsotopesEluteEarlier;
            cbxEluteLater.Checked = tracerDef.IsotopesEluteLater;
        }
        
        private void btn15N_Click(object sender, EventArgs e)
        {
            setEnrichment(TracerDef.GetN15Enrichment());
        }

        private void btnD3Leu_Click(object sender, EventArgs e)
        {
            setEnrichment(TracerDef.GetD3LeuEnrichment());
        }

        private bool ValidateTracers()
        {
            var tracerSymbols = new HashSet<KeyValuePair<String, double>>();
            var tracerNames = new HashSet<String>();
            for (int i = 0; i < _tracerDefs.Count; i++)
            {
                var tracerDef = _tracerDefs[i];
                if (string.IsNullOrEmpty(tracerDef.Name))
                {
                    return ShowError("Name cannot be blank", i, tbxTracerName);
                }
                foreach (var ch in tracerDef.Name.ToLower())
                {
                    if (ch < 'a' || ch > 'z')
                    {
                        return ShowError("Name can consist only of alphabetic characters", i, tbxTracerName);
                    }
                }
                tracerDef.Name = NormalizeName(tracerDef.Name);
                if (tracerNames.Contains(tracerDef.Name))
                {
                    return ShowError("Name must be unique", i, tbxTracerName);
                }
                if (IsotopeAbundances.Default.ContainsKey(tracerDef.Name) || AminoAcidFormulas.LongNames.ContainsKey(tracerDef.Name))
                {
                    return ShowError("Name cannot be the same as a 3 letter amino acid code, or an atomic element", i,
                                     tbxTracerName);
                }
                tracerNames.Add(tracerDef.Name);
                if (string.IsNullOrEmpty(tracerDef.TracerSymbol))
                {
                    return ShowError("Symbol cannot be blank", i, tbxTracerSymbol);
                }
                tracerDef.TracerSymbol = NormalizeName(tracerDef.TracerSymbol);
                if (!IsotopeAbundances.Default.ContainsKey(tracerDef.TracerSymbol) && !AminoAcidFormulas.LongNames.ContainsKey(tracerDef.TracerSymbol))
                {
                    return ShowError("Symbol must be either a three letter amino acid code, or an atomic element", i,
                                     tbxTracerSymbol);
                }
                if (tracerDef.DeltaMass == 0)
                {
                    return ShowError("Mass cannot be 0", i, tbxMassDifference);
                }
                var key = new KeyValuePair<String, double>(tracerDef.TracerSymbol, tracerDef.DeltaMass);
                if (tracerSymbols.Contains(key))
                {
                    return ShowError("There is already a tracer defined with this symbol and mass", i, tbxMassDifference);
                }
                tracerSymbols.Add(key);
            }
            return true;
        }

        private String NormalizeName(String name)
        {
            return name.Substring(0, 1).ToUpper() + name.Substring(1).ToLower();
        }

        private bool ShowError(String message, int tracerIndex, Control control)
        {
            comboTracers.SelectedIndex = tracerIndex;
            MessageBox.Show(this, message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            control.Focus();
            return false;
        }

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        {
            if (!SavePage())
            {
                return;
            }
            if (!ValidateTracers())
            {
                return;
            }
            Workspace.SetDbTracerDefs(_tracerDefs);
            Close();
        }

        private void tbxEnrichedSymbol_Leave(object sender, EventArgs e)
        {
            String symbol = tbxTracerSymbol.Text;
            if (AminoAcidFormulas.LongNames.ContainsKey(symbol))
            {
                tbxAtomCount.Enabled = true;
                tbxAtomicPercentEnrichment.Enabled = true;
            }
            else
            {
                if (IsotopeAbundances.Default.ContainsKey(symbol))
                {
                    tbxAtomCount.Enabled = false;
                    tbxAtomicPercentEnrichment.Enabled = false;
                }
                else
                {
                    MessageBox.Show(
                        "Specify either a three letter amino acid abbreviation, or an atomic element symbol.");
                    tbxTracerSymbol.Focus();
                }
            }
        }

        private bool SavePage()
        {
            if (_tracerIndex == -1)
            {
                return true;
            }
            if (_tracerIndex == _tracerDefs.Count)
            {
                if (tbxTracerName.Text.Length == 0 && tbxTracerSymbol.Text.Length == 0)
                {
                    return true;
                }
                _tracerDefs.Add(new DbTracerDef());
                comboTracers.Items.Add(newTracerText);
            }
            if (_tracerIndex >= _tracerDefs.Count)
            {
                return true;
            }
            var tracerDef = _tracerDefs[_tracerIndex];
            comboTracers.Items[_tracerIndex] = tracerDef.Name = tbxTracerName.Text;
            tracerDef.TracerSymbol = tbxTracerSymbol.Text;
            try
            {
                tracerDef.DeltaMass = double.Parse(tbxMassDifference.Text);
            }
            catch
            {
                return ShowError("This must be a number", _tracerIndex, tbxMassDifference);
            }
            try
            {
                tracerDef.AtomCount = int.Parse(tbxAtomCount.Text);
            }
            catch
            {
                return ShowError("This must be a positive integer", _tracerIndex, tbxAtomCount);
            }
            if (tracerDef.AtomCount <= 0)
            {
                return ShowError("This must be a positive integer", _tracerIndex, tbxAtomCount);
            }
            double atomicPercentEnrichment, initialApe, finalApe;
            if (!ValidatePercent(tbxAtomicPercentEnrichment, out atomicPercentEnrichment)
                || !ValidatePercent(tbxInitialApe, out initialApe)
                || !ValidatePercent(tbxFinalApe, out finalApe))
            {
                return false;
            }
            tracerDef.AtomPercentEnrichment = atomicPercentEnrichment;
            tracerDef.InitialEnrichment = initialApe;
            tracerDef.FinalEnrichment = finalApe;
            tracerDef.IsotopesEluteEarlier = cbxEluteEarlier.Checked;
            tracerDef.IsotopesEluteLater = cbxEluteLater.Checked;
            return true;
        }

        private bool ValidatePercent(TextBox textBox, out double percent)
        {
            try
            {
                percent = double.Parse(textBox.Text);
            }
            catch
            {
                percent = 0;
                return ShowError("This must be a number between 0 and 100", _tracerIndex, textBox);
            }
            if (percent < 0 || percent > 100)
            {
                return ShowError("This must be a number between 0 and 100", _tracerIndex, textBox);
            }
            return true;
        }

        private void comboTracers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_tracerIndex == comboTracers.SelectedIndex)
            {
                return;
            }
            if (!SavePage())
            {
                comboTracers.SelectedIndex = _tracerIndex;
                return;
            }
            _tracerIndex = comboTracers.SelectedIndex;
            if (_tracerIndex == _tracerDefs.Count)
            {
                tbxTracerName.Text = "";
                tbxTracerSymbol.Text = "";
                tbxAtomCount.Text = "1";
                tbxAtomicPercentEnrichment.Text = "100";
                tbxInitialApe.Text = "0";
                tbxFinalApe.Text = "100";
                cbxEluteEarlier.Checked = false;
                cbxEluteLater.Checked = false;
            }
            else
            {
                var tracerDef = _tracerDefs[_tracerIndex];
                tbxTracerName.Text = tracerDef.Name;
                tbxTracerSymbol.Text = tracerDef.TracerSymbol;
                tbxMassDifference.Text = tracerDef.DeltaMass.ToString();
                tbxAtomCount.Text = tracerDef.AtomCount.ToString();
                tbxAtomicPercentEnrichment.Text = tracerDef.AtomPercentEnrichment.ToString();
                tbxInitialApe.Text = tracerDef.InitialEnrichment.ToString();
                tbxFinalApe.Text = tracerDef.FinalEnrichment.ToString();
                cbxEluteEarlier.Checked = tracerDef.IsotopesEluteEarlier;
                cbxEluteLater.Checked = tracerDef.IsotopesEluteLater;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (_tracerIndex == _tracerDefs.Count)
            {
                return;
            }
            int tracerIndex = _tracerIndex;
            _tracerIndex = -1;
            _tracerDefs.RemoveAt(tracerIndex);
            comboTracers.Items.RemoveAt(tracerIndex);
            comboTracers.SelectedIndex = tracerIndex;
        }
    }
}
