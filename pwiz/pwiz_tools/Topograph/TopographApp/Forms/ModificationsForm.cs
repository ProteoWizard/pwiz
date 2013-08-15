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
using pwiz.Common.Collections;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ModificationsForm : WorkspaceForm
    {
        public ModificationsForm(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            dataGridView1.Rows.Clear();
            foreach (var modification in Workspace.Modifications)
            {
                    var row = dataGridView1.Rows[dataGridView1.Rows.Add()];
                    row.Cells[colSymbol.Index].Value = modification.Key;
                    row.Cells[colMassDelta.Index].Value = modification.Value;
            }
        }

        private void BtnOkOnClick(object sender, EventArgs e)
        {
            var modifications = new Dictionary<String,double>();
            for (int i = 0; i < dataGridView1.Rows.Count; i++)
            {
                var row = dataGridView1.Rows[i];
                var deltaMass = Convert.ToDouble(row.Cells[colMassDelta.Index].Value);
                if (deltaMass == 0)
                {
                    continue;
                }
                modifications[Convert.ToString(row.Cells[colSymbol.Index].Value)] = deltaMass;
            }
            Workspace.Data = Workspace.Data.SetModifications(ImmutableSortedList.FromValues(modifications));
            Close();
        }
    }
}
