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
using pwiz.Topograph.Model.Data;

namespace pwiz.Topograph.Model
{
    public class WorkspaceChangeArgs : EventArgs
    {
        public WorkspaceChangeArgs(Workspace workspace) : this(workspace.Data, workspace.SavedData)
        {
        }
        public WorkspaceChangeArgs(WorkspaceData originalData, WorkspaceData originalSavedData)
        {
            OriginalData = originalData;
            OriginalSavedData = originalSavedData;
        }
        public WorkspaceData OriginalData { get; private set; }
        public WorkspaceData OriginalSavedData { get; private set; }
        public bool HasChromatogramMassChange { get; private set; }
        public bool HasPeakPickingChange { get; private set; }
        public bool HasTurnoverChange { get; private set; }
        public bool HasSettingChange { get; private set; }

        public void AddChromatogramMassChange()
        {
            if (!HasChromatogramMassChange)
            {
                HasChromatogramMassChange = true;
                AddPeakPickingChange();
            }
        }

        public void AddPeakPickingChange()
        {
            if (!HasPeakPickingChange)
            {
                HasPeakPickingChange = true;
                AddTurnoverChange();
            }
        }

        public void AddTurnoverChange()
        {
            if (!HasTurnoverChange)
            {
                HasTurnoverChange = true;
                AddSettingChange();
            }
        }
        public void AddSettingChange()
        {
            if (!HasSettingChange)
            {
                HasSettingChange = true;
            }
        }
    }
}
