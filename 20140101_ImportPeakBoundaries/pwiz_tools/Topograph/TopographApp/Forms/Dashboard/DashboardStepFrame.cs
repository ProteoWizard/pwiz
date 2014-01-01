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
using System.Windows.Forms;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class DashboardStepFrame : UserControl
    {
        private DashboardStep _dashboardStep;
        private int _stepOrdinal;
        private bool _isExpanded;
        public DashboardStepFrame()
        {
            AutoExpand = true;
            InitializeComponent();
            AutoSize = true;
            IsExpanded = false;
        }

        public DashboardStep DashboardStep
        {
            get { return _dashboardStep; }
            set 
            {
                if (ReferenceEquals(value, DashboardStep))
                {
                    return;
                }
                panel1.Controls.Clear();
                _dashboardStep = value;
                if (null != DashboardStep)
                {
                    panel1.Controls.Add(DashboardStep);
                    DashboardStep.Dock = DockStyle.Fill;
                    DashboardStep.AutoSize = true;
                    DashboardStep.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                }
                UpdateTitle();
            }
        }

        public int StepOrdinal
        {
            get { return _stepOrdinal; }
            set 
            { 
                _stepOrdinal = value;
                UpdateTitle();
            }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                _isExpanded = value;
                if (IsExpanded)
                {
                    panel1.AutoSize = true;
                    imgExpandCollapse.Image = Resources.Collapse;
                }
                else
                {
                    panel1.AutoSize = false;
                    panel1.Height = 0;
                    imgExpandCollapse.Image = Resources.Expand;
                }
            }
        }

        public bool AutoExpand { get; set; }

        public void UpdateTitle()
        {
            string strTitle = string.Format("Step {0}: {1}", StepOrdinal + 1,
                                            DashboardStep == null ? "<error>" : DashboardStep.Title);
            lblTitle.Text = strTitle;
            if (IsHandleCreated && AutoExpand)
            {
                IsExpanded = DashboardStep != null && DashboardStep.IsCurrent;
            }
        }

        private void BtnExpandCollapseOnClick(object sender, EventArgs e)
        {
            AutoExpand = false;
            IsExpanded = !IsExpanded;
        }
    }
}
