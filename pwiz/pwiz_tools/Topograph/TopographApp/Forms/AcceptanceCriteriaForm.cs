/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Topograph.Model;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AcceptanceCriteriaForm : WorkspaceForm
    {
        public AcceptanceCriteriaForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            checkedListBoxAcceptableIntegrationNotes.Items.AddRange(IntegrationNote.Values().Cast<object>().ToArray());
            AcceptSamplesWithoutMs2Id = workspace.GetAcceptSamplesWithoutMs2Id();
            MinDeconvolutionScore = workspace.GetAcceptMinDeconvolutionScore();
            MinAuc = workspace.GetAcceptMinAreaUnderChromatogramCurve();
            IntegrationNotes = workspace.GetAcceptIntegrationNotes();
            MinTurnoverScore = workspace.GetAcceptMinTurnoverScore();
        }

        public void Save()
        {
            Workspace.SetAcceptSamplesWithoutMs2Id(AcceptSamplesWithoutMs2Id);
            Workspace.SetAcceptMinDeconvolutionScore(MinDeconvolutionScore);
            Workspace.SetAcceptMinAreaUnderChromatogramCurve(MinAuc);
            Workspace.SetAcceptIntegrationNotes(IntegrationNotes);
            Workspace.SetAcceptMinTurnoverScore(MinTurnoverScore);
        }

        public bool AcceptSamplesWithoutMs2Id
        {
            get { return cbxAllowNoMs2Id.Checked; }
            set { cbxAllowNoMs2Id.Checked = value;}
        }

        public double MinDeconvolutionScore
        {
            get
            {
                double result;
                if (Double.TryParse(tbxMinDeconvolutionScore.Text, out result))
                {
                    return result;
                }
                return 0;
            }
            set
            {
                tbxMinDeconvolutionScore.Text = value.ToString(CultureInfo.CurrentCulture);
            }
        }

        public double MinAuc
        {
            get { 
                double result;
                if (Double.TryParse(tbxMinAuc.Text, out result))
                {
                    return result;
                }
                return 0;
            }
            set
            {
                tbxMinAuc.Text = value.ToString(CultureInfo.CurrentCulture);
            }
        }

        public double MinTurnoverScore
        {
            get 
            { 
                double result;
                if (Double.TryParse(tbxMinTurnoverScore.Text, out result))
                {
                    return result;
                }
                return 0;
            }
            set { tbxMinTurnoverScore.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public IEnumerable<IntegrationNote> IntegrationNotes
        {
            get
            {
                return checkedListBoxAcceptableIntegrationNotes.CheckedItems.Cast<IntegrationNote>();
            }
            set
            {
                var set = new HashSet<IntegrationNote>(value);
                for (int i = 0; i < checkedListBoxAcceptableIntegrationNotes.Items.Count; i++)
                {
                    var item = (IntegrationNote) checkedListBoxAcceptableIntegrationNotes.Items[i];
                    checkedListBoxAcceptableIntegrationNotes.SetItemChecked(i, set.Contains(item));
                }
            }
        }

        private void BtnSaveOnClick(object sender, EventArgs e)
        {
            Save();
            Close();
        } 
    }
}
