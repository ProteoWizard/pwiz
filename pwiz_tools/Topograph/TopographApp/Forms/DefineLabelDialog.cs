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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DefineLabelDialog : WorkspaceForm
    {
        private DbTracerDef _originalTracerDef;
        public DefineLabelDialog(Workspace workspace, DbTracerDef originalTracerDef) : base(workspace)
        {
            InitializeComponent();
            _originalTracerDef = originalTracerDef;
            if (_originalTracerDef != null)
            {
                SetTracerDef(_originalTracerDef);
            }
        }

        private void SetTracerDef(DbTracerDef tracerDef)
        {
            tbxTracerName.Text = tracerDef.Name;
            tbxTracerSymbol.Text = tracerDef.TracerSymbol;
            tbxMassDifference.Text = tracerDef.DeltaMass.ToString();
            tbxAtomCount.Text = tracerDef.AtomCount.ToString();
            tbxAtomicPercentEnrichment.Text = tracerDef.AtomPercentEnrichment.ToString();
            cbxEluteEarlier.Checked = tracerDef.IsotopesEluteEarlier;
            cbxEluteLater.Checked = tracerDef.IsotopesEluteLater;
        }

        private DbTracerDef GetTracerDef()
        {
            var tracerDef = new DbTracerDef();
            if (_originalTracerDef != null)
            {
                tracerDef.Id = _originalTracerDef.Id;
            }
            tracerDef.Name = NormalizeName(tbxTracerName.Text);
            if (_originalTracerDef == null || tracerDef.Name != _originalTracerDef.Name)
            {
                if (Workspace.GetTracerDefs().Any(td=>tracerDef.Name == td.Name))
                {
                    ShowError("There is already a label defined with this name.", tbxTracerName);
                    return null;
                }
            }
            foreach (var ch in tracerDef.Name.ToLower())
            {
                if (ch < 'a' || ch > 'z')
                {
                    ShowError("Name can consist only of alphabetic characters", tbxTracerName);
                    return null;
                }
            }
            if (IsotopeAbundances.Default.ContainsKey(tracerDef.Name) || AminoAcidFormulas.LongNames.ContainsKey(tracerDef.Name))
            {
                ShowError("Name cannot be the same as a 3 letter amino acid code, or an atomic element", 
                                 tbxTracerName);
                return null;
            }

            tracerDef.TracerSymbol = NormalizeName(tbxTracerSymbol.Text);
            if (string.IsNullOrEmpty(tracerDef.TracerSymbol))
            {
                ShowError("Symbol cannot be blank", tbxTracerSymbol);
                return null;
            }
            if (!IsotopeAbundances.Default.ContainsKey(tracerDef.TracerSymbol) && !AminoAcidFormulas.LongNames.ContainsKey(tracerDef.TracerSymbol))
            {
                ShowError("Symbol must be either a three letter amino acid code, or an atomic element",
                                 tbxTracerSymbol);
                return null;
            }

            try
            {
                tracerDef.DeltaMass = double.Parse(tbxMassDifference.Text);
            }
            catch
            {
                ShowError("This must be a number", tbxMassDifference);
                return null;
            }
            if (tracerDef.DeltaMass == 0)
            {
                ShowError("Mass cannot be 0", tbxMassDifference);
                return null;
            }

            try
            {
                tracerDef.AtomCount = int.Parse(tbxAtomCount.Text);
            }
            catch
            {
                ShowError("This must be a positive integer", tbxAtomCount);
                return null;
            }
            if (tracerDef.AtomCount <= 0)
            {
                ShowError("This must be a positive integer", tbxAtomCount);
                return null;
            }
            double atomicPercentEnrichment, initialApe, finalApe;
            if (!ValidatePercent(tbxAtomicPercentEnrichment, out atomicPercentEnrichment)
                || !ValidatePercent(tbxInitialApe, out initialApe)
                || !ValidatePercent(tbxFinalApe, out finalApe))
            {
                return null;
            }


            tracerDef.AtomPercentEnrichment = atomicPercentEnrichment;
            tracerDef.InitialEnrichment = initialApe;
            tracerDef.FinalEnrichment = finalApe;
            tracerDef.IsotopesEluteEarlier = cbxEluteEarlier.Checked;
            tracerDef.IsotopesEluteLater = cbxEluteLater.Checked;
            return tracerDef;
        }
        
        private void btn15N_Click(object sender, EventArgs e)
        {
            SetTracerDef(TracerDef.GetN15Enrichment());
        }

        private void btnD3Leu_Click(object sender, EventArgs e)
        {
            SetTracerDef(TracerDef.GetD3LeuEnrichment());
        }

        private String NormalizeName(String name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            return name.Substring(0, 1).ToUpper() + name.Substring(1).ToLower();
        }

        private bool ShowError(String message, Control control)
        {
            MessageBox.Show(this, message, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            control.Focus();
            return false;
        }

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        {
            var newTracerDef = GetTracerDef();
            if (newTracerDef == null)
            {
                return;
            }
            var tracerDefs = Workspace.GetDbTracerDefs();
            if (_originalTracerDef != null)
            {
                tracerDefs = tracerDefs.Where(td => _originalTracerDef.Name != td.Name).ToList();
            }
            tracerDefs.Add(newTracerDef);
            Workspace.SetDbTracerDefs(tracerDefs);
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

        private bool ValidatePercent(TextBox textBox, out double percent)
        {
            try
            {
                percent = double.Parse(textBox.Text);
            }
            catch
            {
                percent = 0;
                return ShowError("This must be a number between 0 and 100", textBox);
            }
            if (percent < 0 || percent > 100)
            {
                return ShowError("This must be a number between 0 and 100", textBox);
            }
            return true;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
