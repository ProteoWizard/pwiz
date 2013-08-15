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
    public class WorkspaceForm : DockableForm
    {
        private WorkspaceForm()
        {
            Icon = Properties.Resources.TopographIcon;
        }
        public WorkspaceForm(Workspace workspace) : this()
        {
            Workspace = workspace;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Workspace != null)
            {
                Workspace.Change += WorkspaceOnChange;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            if (Workspace != null)
            {
                Workspace.Change -= WorkspaceOnChange;
            }
        }

        public Workspace Workspace { get; private set; }
        protected void SafeBeginInvoke(Action action)
        {
            lock(this)
            {
                if (InvokeRequired && !IsDisposed)
                {
                    BeginInvoke(action);
                }
            }
        }
        protected virtual void WorkspaceOnChange(object sender, WorkspaceChangeArgs args)
        {
            
        }
    }
}
