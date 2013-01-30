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
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Chemistry;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.ui.Forms
{
    public partial class DefineLabelDialog : WorkspaceForm
    {
        private TracerDefData _originalTracerDef;
        public DefineLabelDialog(Workspace workspace, TracerDefData tracerDefData) : base(workspace)
        {
            InitializeComponent();
            _originalTracerDef = tracerDefData;
            if (_originalTracerDef != null)
            {
                SetTracerDef(_originalTracerDef);
            }
        }

        private void SetTracerDef(TracerDefData tracerDef)
        {
            tbxTracerName.Text = tracerDef.Name;
            tbxTracerSymbol.Text = tracerDef.TracerSymbol;
            tbxMassDifference.Text = tracerDef.DeltaMass.ToString(CultureInfo.CurrentCulture);
            tbxAtomCount.Text = tracerDef.AtomCount.ToString(CultureInfo.CurrentCulture);
            tbxAtomicPercentEnrichment.Text = tracerDef.AtomPercentEnrichment.ToString(CultureInfo.CurrentCulture);
            cbxEluteEarlier.Checked = tracerDef.IsotopesEluteEarlier;
            cbxEluteLater.Checked = tracerDef.IsotopesEluteLater;
            tbxInitialApe.Text = tracerDef.InitialEnrichment.ToString(CultureInfo.CurrentCulture);
            tbxFinalApe.Text = tracerDef.FinalEnrichment.ToString(CultureInfo.CurrentCulture);
        }

        private TracerDefData GetTracerDef()
        {
            string name = NormalizeName(tbxTracerName.Text);
            if (_originalTracerDef == null || name != _originalTracerDef.Name)
            {
                if (Workspace.GetTracerDefs().Any(pair=>pair.Name == name))
                {
                    ShowError("There is already a label defined with this name.", tbxTracerName);
                    return null;
                }
            }
            foreach (var ch in name.ToLower())
            {
                if (ch < 'a' || ch > 'z')
                {
                    ShowError("Name can consist only of alphabetic characters", tbxTracerName);
                    return null;
                }
            }
            if (IsotopeAbundances.Default.ContainsKey(name) || AminoAcidFormulas.LongNames.ContainsKey(name))
            {
                ShowError("Name cannot be the same as a 3 letter amino acid code, or an atomic element", 
                                 tbxTracerName);
                return null;
            }

            string tracerSymbol = NormalizeName(tbxTracerSymbol.Text);
            if (string.IsNullOrEmpty(tracerSymbol))
            {
                ShowError("Symbol cannot be blank", tbxTracerSymbol);
                return null;
            }
            if (!IsotopeAbundances.Default.ContainsKey(tracerSymbol) && !AminoAcidFormulas.LongNames.ContainsKey(tracerSymbol))
            {
                ShowError("Symbol must be either a three letter amino acid code, or an atomic element",
                                 tbxTracerSymbol);
                return null;
            }

            double deltaMass;
            try
            {
                deltaMass = double.Parse(tbxMassDifference.Text);
            }
            catch
            {
                ShowError("This must be a number", tbxMassDifference);
                return null;
            }
            if (deltaMass == 0)
            {
                ShowError("Mass cannot be 0", tbxMassDifference);
                return null;
            }

            int atomCount;
            try
            {
                atomCount = int.Parse(tbxAtomCount.Text);
            }
            catch
            {
                ShowError("This must be a positive integer", tbxAtomCount);
                return null;
            }
            if (atomCount <= 0)
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


            var dbTracerDef = new DbTracerDef
                                  {
                                      AtomCount = atomCount,
                                      AtomPercentEnrichment = atomicPercentEnrichment,
                                      DeltaMass = deltaMass,
                                      InitialEnrichment = initialApe,
                                      FinalEnrichment = finalApe,
                                      IsotopesEluteEarlier = cbxEluteEarlier.Checked,
                                      IsotopesEluteLater = cbxEluteLater.Checked,
                                      Name = name,
                                      TracerSymbol = tracerSymbol,
                                  };
            return new TracerDefData(dbTracerDef);
        }
        
        private void Btn15NOnClick(object sender, EventArgs e)
        {
            SetTracerDef(new TracerDefData(TracerDef.GetN15Enrichment()));
        }

        private void BtnD3LeuOnClick(object sender, EventArgs e)
        {
            SetTracerDef(new TracerDefData(TracerDef.GetD3LeuEnrichment()));
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

        private void BtnSaveAndCloseOnClick(object sender, EventArgs e)
        {
            var newTracerDef = GetTracerDef();
            if (newTracerDef == null)
            {
                return;
            }
            Workspace.Data = Workspace.Data.SetTracerDefs(Workspace.Data.TracerDefs.Replace(newTracerDef.Name, newTracerDef));
            Close();
        }

        private void TbxEnrichedSymbolOnLeave(object sender, EventArgs e)
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

        private void BtnCancelOnClick(object sender, EventArgs e)
        {
            Close();
        }
    }
}
