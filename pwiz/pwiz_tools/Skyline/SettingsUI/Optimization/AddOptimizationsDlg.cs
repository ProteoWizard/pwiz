/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Optimization
{
    public enum AddOptimizationsAction { skip, replace, average }

    public partial class AddOptimizationsDlg : FormEx
    {
        public AddOptimizationsDlg(int optimizationCount, IEnumerable<OptimizationKey> existingOptimizations)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            OptimizationsCount = optimizationCount;

            if (optimizationCount == 0)
                labelOptimizationsAdded.Text = Resources.AddOptimizationsDlg_AddOptimizationsDlg_No_new_optimizations_will_be_added_to_the_library_;
            else if (optimizationCount == 1)
                labelOptimizationsAdded.Text = Resources.AddOptimizationsDlg_AddOptimizationsDlg__1_new_optimization_will_be_added_to_the_library_;
            else
                labelOptimizationsAdded.Text = string.Format(labelOptimizationsAdded.Text, optimizationCount);

            listExisting.Items.AddRange(existingOptimizations.Cast<object>().ToArray());

            labelExisting.Text = string.Format(labelExisting.Text, listExisting.Items.Count);

            if (listExisting.Items.Count == 0)
            {
                panelExisting.Visible = false;
                Height -= panelExisting.Height;
                FormBorderStyle = FormBorderStyle.FixedDialog;
            }
        }

        public int OptimizationsCount { get; private set; }
        public int ExistingOptimizationsCount { get { return listExisting.Items.Count; } }

        public AddOptimizationsAction Action
        {
            get
            {
                if (radioAverage.Checked)
                    return AddOptimizationsAction.average;
                if (radioReplace.Checked)
                    return AddOptimizationsAction.replace;
                return AddOptimizationsAction.skip;
            }

            set
            {
                switch (value)
                {
                    case AddOptimizationsAction.average:
                        radioAverage.Checked = true;
                        break;
                    case AddOptimizationsAction.replace:
                        radioReplace.Checked = true;
                        break;
                    default:
                        radioSkip.Checked = true;
                        break;
                }
            }
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }
    }
}
