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
        private TurnoverForm _turnoverForm;
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
                TurnoverForm = DashboardForm.TurnoverForm;
                UpdateStepStatus();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            TurnoverForm = null;
        }

        protected TurnoverForm TurnoverForm
        {
            get { return _turnoverForm; } 
            private set
            {
                if (ReferenceEquals(value, _turnoverForm))
                {
                    return;
                }
                if (TurnoverForm != null)
                {
                    TurnoverForm.WorkspaceChange -= TurnoverFormOnWorkspaceChange;
                }
                _turnoverForm = value;
                if (TurnoverForm != null)
                {
                    TurnoverForm.WorkspaceChange += TurnoverFormOnWorkspaceChange;
                    Workspace = TurnoverForm.Workspace;
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
                    Workspace.EntitiesChange -= OnWorkspaceEntitiesChanged;
                    Workspace.WorkspaceLoaded -= OnWorkspaceLoaded;
                }
                _workspace = value;
                if (Workspace != null)
                {
                    Workspace.EntitiesChange += OnWorkspaceEntitiesChanged;
                    Workspace.WorkspaceLoaded += OnWorkspaceLoaded;
                }
                OnWorkspaceChanged();
            }
        }

        private void OnWorkspaceLoaded(Workspace workspace)
        {
            BeginInvoke(new Action(UpdateStepStatus));
        }

        protected virtual void OnWorkspaceChanged()
        {
            UpdateStepStatus();
        }

        private void TurnoverFormOnWorkspaceChange(object sender, EventArgs eventArgs)
        {
            Workspace = TurnoverForm.Workspace;
        }

        protected virtual void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs entitiesChangedEventArgs)
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
