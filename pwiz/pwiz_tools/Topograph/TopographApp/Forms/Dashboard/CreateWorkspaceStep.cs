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
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class CreateWorkspaceStep : DashboardStep
    {
        public CreateWorkspaceStep()
        {
            InitializeComponent();
            Title = "Create or Open a Workspace";
        }

        public override bool IsCurrent
        {
            get { return null == Workspace; }
        }

        protected override void UpdateStepStatus()
        {
            {
                base.UpdateStepStatus();
                if (null == Workspace)
                {
                    panelWorkspaceOpen.Visible = false;
                    panelNoWorkspace.Visible = true;
                    var mruList = Settings.Default.MruList;
                    if (mruList == null || mruList.Count == 0)
                    {
                        groupBoxOpenRecent.Visible = false;
                    }
                    else
                    {
                        groupBoxOpenRecent.Visible = true;
                        listBoxRecentWorkspaces.Items.Clear();
                        foreach (var mruItem in mruList)
                        {
                            listBoxRecentWorkspaces.Items.Add(mruItem);
                        }
                    }
                }
                else
                {
                    panelWorkspaceOpen.Visible = true;
                    labelCurrentWorkspace.Text = string.Format("The current workspace is {0}",
                                                               TopographForm.Workspace.DatabasePath);
                    panelNoWorkspace.Visible = false;
                }
                base.UpdateStepStatus();
            }
        }

        private void BtnCloseWorkspaceOnClick(object sender, EventArgs e)
        {
            DashboardForm.TopographForm.CloseWorkspace();
        }

        private void BtnOpenDifferentWorkspaceOnClick(object sender, EventArgs e)
        {
            DashboardForm.TopographForm.DisplayOpenWorkspaceDialog();
        }

        private void ListBoxRecentWorkspacesOnSelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedItem = listBoxRecentWorkspaces.SelectedItem as string;
            if (selectedItem != null)
            {
                TopographForm.Workspace = TopographForm.OpenWorkspace(selectedItem);
                listBoxRecentWorkspaces.SelectedItems.Clear();
            }
        }

        private void BtnCreateOnlineWorkspaceOnClick(object sender, EventArgs e)
        {
            TopographForm.NewOnlineWorkspace();
        }

        private void BtnCreateLocalWorkspaceOnClick(object sender, EventArgs e)
        {
            TopographForm.NewWorkspace();
        }

        private void BtnConnectToOnlineWorkspaceOnClick(object sender, EventArgs e)
        {
            TopographForm.ConnectToOnlineWorkspace();
        }

        private void BtnBrowseOnClick(object sender, EventArgs e)
        {
            TopographForm.DisplayOpenWorkspaceDialog();
        }
    }
}
