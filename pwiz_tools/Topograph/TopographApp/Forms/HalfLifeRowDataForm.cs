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
using System.Collections.Generic;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;
using pwiz.Topograph.ui.DataBinding;

namespace pwiz.Topograph.ui.Forms
{
    public partial class HalfLifeRowDataForm : WorkspaceForm
    {
        public HalfLifeRowDataForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            var viewContext = new TopographViewContext(workspace, typeof(HalfLifeCalculator.ProcessedRowData), new HalfLifeCalculator.ProcessedRowData[0]);
            bindingSource1.SetViewContext(viewContext);
        }

        public IList<HalfLifeCalculator.ProcessedRowData> RowDatas
        {
            get { return (IList<HalfLifeCalculator.ProcessedRowData>)bindingSource1.RowSource; }
            set { bindingSource1.RowSource = value; }
        }

        public string Peptide
        {
            get { return tbxPeptide.Text; }
            set { tbxPeptide.Text = value; }
        }
        public string Protein
        {
            get { return tbxProtein.Text; }
            set { tbxProtein.Text = value; }
        }
        public string Cohort
        {
            get { return tbxCohort.Text; }
            set { tbxCohort.Text = value; }
        }
    }
}
