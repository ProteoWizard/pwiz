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
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideAnalysisFrame : EntityModelForm
    {
        private DockPanel _dockPanel;
        public PeptideAnalysisFrame(PeptideAnalysis peptideAnalysis) : base(peptideAnalysis)
        {
            InitializeComponent();
            _dockPanel = new DockPanel
                             {
                                 Dock = DockStyle.Fill
                             };
            panel1.Controls.Add(_dockPanel);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            PeptideAnalysis.IncChromatogramRefCount();
            OnPeptideAnalysisChanged();
            if (PeptideAnalysisSummary == null)
            {
                PeptideAnalysisSummary = new PeptideAnalysisSummary(PeptideAnalysis);
                PeptideAnalysisSummary.Show(_dockPanel, DockState.Document);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            PeptideAnalysis.DecChromatogramRefCount();
        }
        protected override void EntityChanged()
        {
            base.EntityChanged();
            OnPeptideAnalysisChanged();
        }

        private void OnPeptideAnalysisChanged()
        {
            Text = TabText = PeptideAnalysis.GetLabel();
            tbxSequence.Text = PeptideAnalysis.Peptide.FullSequence;
            tbxProteinDescription.Text = PeptideAnalysis.Peptide.ProteinDescription;
            tbxProteinName.Text = PeptideAnalysis.Peptide.GetProteinKey();
        }

        protected override void WorkspaceOnChange(object sender, WorkspaceChangeArgs args)
        {
            base.WorkspaceOnChange(sender, args);
            if (null == Workspace.PeptideAnalyses.FindByKey(PeptideAnalysis.Id))
            {
                Close();
            }
        }

        public PeptideAnalysis PeptideAnalysis
        {
            get { return (PeptideAnalysis) EntityModel; }
        }
        public PeptideAnalysisSummary PeptideAnalysisSummary { get; private set; }

        public static PeptideAnalysisFrame ShowPeptideAnalysis(PeptideAnalysis peptideAnalysis)
        {
            if (peptideAnalysis == null)
            {
                return null;
            }
            var form = Program.FindOpenEntityForm<PeptideAnalysisFrame>(peptideAnalysis);
            if (form != null)
            {
                form.Activate();
                return form;
            }
            using (peptideAnalysis.IncChromatogramRefCount())
            {
                TopographForm.Instance.LoadPeptideAnalysis(peptideAnalysis.Id);
                if (!peptideAnalysis.ChromatogramsWereLoaded)
                {
                    return null;
                }
                form = new PeptideAnalysisFrame(peptideAnalysis);
                form.Show(TopographForm.Instance.DocumentPanel, DockState.Document);
                return form;
            }
        }

        private void PeptideAnalysisFrameOnResize(object sender, EventArgs e)
        {
            SuspendLayout();
            ResumeLayout(true);
        }
    }
}
