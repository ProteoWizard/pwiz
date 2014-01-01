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
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public class DashboardStep : UserControl
    {
        private TopographForm _topographForm;
        private Workspace _workspace;
        public virtual bool IsCurrent
        {
            get { return false; }
        }

        public DashboardForm DashboardForm
        {
            get { return ParentForm as DashboardForm; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DashboardForm != null)
            {
                TopographForm = DashboardForm.TopographForm;
                UpdateStepStatus();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            TopographForm = null;
        }

        protected TopographForm TopographForm
        {
            get { return _topographForm; } 
            private set
            {
                if (ReferenceEquals(value, _topographForm))
                {
                    return;
                }
                if (TopographForm != null)
                {
                    TopographForm.WorkspaceChange -= TurnoverFormOnWorkspaceChange;
                }
                _topographForm = value;
                if (TopographForm != null)
                {
                    TopographForm.WorkspaceChange += TurnoverFormOnWorkspaceChange;
                    Workspace = TopographForm.Workspace;
                }
                else
                {
                    Workspace = null;
                }
            }
        }

        protected virtual Workspace Workspace
        {
            get { return _workspace; }
            set
            {
                if (ReferenceEquals(value, _workspace))
                {
                    return;
                }
                if (Workspace != null)
                {
                    Workspace.Change -= WorkspaceOnChange;
                }
                _workspace = value;
                if (Workspace != null)
                {
                    Workspace.Change += WorkspaceOnChange;
                }
                OnWorkspaceChanged();
            }
        }

        protected virtual void OnWorkspaceChanged()
        {
            UpdateStepStatus();
        }

        private void TurnoverFormOnWorkspaceChange(object sender, EventArgs eventArgs)
        {
            Workspace = TopographForm.Workspace;
        }

        protected virtual void WorkspaceOnChange(object sender, WorkspaceChangeArgs change)
        {
            UpdateStepStatus();
        }

        protected virtual void UpdateStepStatus()
        {
            for (Control parent = Parent; parent != null; parent = parent.Parent)
            {
                var dashboardFrame = parent as DashboardStepFrame;
                if (dashboardFrame != null)
                {
                    dashboardFrame.UpdateTitle();
                    return;
                }
            }
        }

        public string Title { get; protected set; }
    }
}
