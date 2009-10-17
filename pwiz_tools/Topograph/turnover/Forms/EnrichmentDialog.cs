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
        public EnrichmentDialog(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            var enrichment = workspace.GetEnrichmentDef();
            tbxTracerSymbol.Text = enrichment.TracerSymbol;
            tbxMassDifference.Text = enrichment.DeltaMass.ToString();
            tbxAtomCount.Text = enrichment.AtomCount.ToString();
            tbxAtomicPercentEnrichment.Text = enrichment.AtomPercentEnrichment.ToString();
            tbxInitialApe.Text = enrichment.InitialApe.ToString();
            tbxFinalApe.Text = enrichment.FinalApe.ToString();
            cbxEluteEarlier.Checked = enrichment.IsotopesEluteEarlier;
            cbxEluteLater.Checked = enrichment.IsotopesEluteLater;
        }

        private void setEnrichment(DbEnrichment enrichment)
        {
            tbxTracerSymbol.Text = enrichment.TracerSymbol;
            tbxMassDifference.Text = enrichment.DeltaMass.ToString();
            tbxAtomCount.Text = enrichment.AtomCount.ToString();
            tbxAtomicPercentEnrichment.Text = enrichment.AtomPercentEnrichment.ToString();
        }
        
        private void btn15N_Click(object sender, EventArgs e)
        {
            setEnrichment(EnrichmentDef.GetN15Enrichment());
            cbxEluteLater.Checked = false;
            cbxEluteEarlier.Checked = false;
        }

        private void btnD3Leu_Click(object sender, EventArgs e)
        {
            setEnrichment(EnrichmentDef.GetD3LeuEnrichment());
            cbxEluteLater.Checked = false;
            cbxEluteEarlier.Checked = true;
        }

        private void btnSaveAndClose_Click(object sender, EventArgs e)
        {
            DbEnrichment dbEnrichment = new DbEnrichment
                                            {
                                                TracerSymbol = tbxTracerSymbol.Text,
                                                DeltaMass = double.Parse(tbxMassDifference.Text),
                                                AtomCount = int.Parse(tbxAtomCount.Text),
                                                AtomPercentEnrichment = double.Parse(tbxAtomicPercentEnrichment.Text),
                                                IsotopesEluteEarlier = cbxEluteEarlier.Checked,
                                                IsotopesEluteLater = cbxEluteLater.Checked,
                                                InitialEnrichment = double.Parse(tbxInitialApe.Text),
                                                FinalEnrichment = double.Parse(tbxFinalApe.Text),
                                            };
            Workspace.SetEnrichment(dbEnrichment);
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
    }
}
