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
using DigitalRune.Windows.Docking;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PeptideFileAnalysisForm : EntityModelForm
    {
        private PeptideFileAnalysisForm() : base(null)
        {
        }
        protected PeptideFileAnalysisForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
        }
        public PeptideFileAnalysis PeptideFileAnalysis
        {
            get { return (PeptideFileAnalysis) EntityModel; }
        }
        public PeptideAnalysis PeptideAnalysis
        {
            get { return PeptideFileAnalysis == null ? null : PeptideFileAnalysis.PeptideAnalysis; }
        }

        private void UpdateTitle()
        {
            if (DockState == DockState.Floating)
            {
                TabText = Text + ": " + PeptideFileAnalysis.GetLabel();
            }
            else
            {
                TabText = Text;
            }
        }

        protected virtual void PeptideFileAnalysisChanged()
        {
            UpdateTitle();
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (PeptideAnalysis != null)
            {
                PeptideFileAnalysisChanged();
            }
        }

        protected override void OnDockStateChanged(EventArgs e)
        {
            base.OnDockStateChanged(e);
            UpdateTitle();
        }
    }
}
