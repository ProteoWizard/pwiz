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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms.Dashboard
{
    public partial class RawFilesStep : DashboardStep
    {
        private DataDirectoryValidator _dataDirectoryValidator;

        public RawFilesStep()
        {
            InitializeComponent();
            Title = "Tell Topograph Where To Find Your MS Data Files";
        }

        public override bool IsCurrent
        {
            get { return Workspace != null && string.IsNullOrEmpty(Workspace.GetDataDirectory()) && 0 != Workspace.MsDataFiles.Count; }
        }

        private void BtnChooseDirectoryOnClick(object sender, EventArgs e)
        {
            TopographForm.BrowseForDataDirectory();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            DirectoryValidator = null;
            base.OnHandleDestroyed(e);
        }

        private DataDirectoryValidator DirectoryValidator
        {
            get { return _dataDirectoryValidator; }
            set
            {
                if (ReferenceEquals(value, DirectoryValidator))
                {
                    return;
                }
                if (DirectoryValidator != null)
                {
                    DirectoryValidator.Dispose();
                }
                _dataDirectoryValidator = value;
                if (DirectoryValidator != null)
                {
                    DirectoryValidator.Validate();
                }
            }
        }

        protected override void UpdateStepStatus()
        {
            base.UpdateStepStatus();
            if (Workspace == null)
            {
                lblStatus.Text = "No workspace is open.";
                Enabled = false;
                return;
            }
            if (!Workspace.IsLoaded)
            {
                lblStatus.Text = "The workspaces is in the process of being opened.  Please wait.";
                Enabled = false;
                return;
            }
            if (DirectoryValidator != null)
            {
                if (!DirectoryValidator.AppliesTo(Workspace))
                {
                    DirectoryValidator = null;
                }
            }
            if (DirectoryValidator == null)
            {
                DirectoryValidator = new DataDirectoryValidator(this);
            }
            lblStatus.Text = DirectoryValidator.GetStatusText();
            Enabled = true;
        }

        public class DataDirectoryValidator : MustDispose
        {
            private readonly RawFilesStep _rawFilesStep;
            private readonly Workspace _workspace;
            private readonly string _dataDirectory;
            private readonly HashSet<string> _dataFileNames;
            private bool _running;
            private bool _dataDirectoryExists;
            private bool _anyDataFiles;
            private bool _allDataFiles;

            public DataDirectoryValidator(RawFilesStep rawFilesStep)
            {
                _rawFilesStep = rawFilesStep;
                _workspace = _rawFilesStep.Workspace;
                if (_workspace != null)
                {
                    _dataDirectory = _workspace.GetDataDirectory();
                    _dataFileNames = new HashSet<string>(GetDataFileNames(_workspace));
                }
            }

            private ICollection<string> GetDataFileNames(Workspace workspace)
            {
                if (workspace == null)
                {
                    return new string[0];
                }
                return workspace.MsDataFiles.Select(dataFile => dataFile.Name).ToArray();
            }

            public bool AppliesTo(Workspace workspaceCompare)
            {
                if (!ReferenceEquals(_workspace, workspaceCompare))
                {
                    return false;
                }
                if (_workspace == null)
                {
                    return true;
                }
                return _dataDirectory == workspaceCompare.GetDataDirectory()
                       && _dataFileNames.SetEquals(GetDataFileNames(workspaceCompare));
            }
            public string GetStatusText()
            {
                if (string.IsNullOrEmpty(_dataDirectory))
                {
                    return "The data directory has not been set.";
                }
                string strStatus = string.Format("The data directory is set to '{0}'.", _dataDirectory);
                if (_running)
                {
                    return strStatus + " (Validating...)";
                }
                if (!_dataDirectoryExists)
                {
                    return strStatus + " This directory does not exist.";
                }
                if (_allDataFiles)
                {
                    return strStatus;
                }
                if (_anyDataFiles)
                {
                    return strStatus + " Only some of this workspace's data files can be found in this directory.";
                }
                return strStatus + " None of this workspace's data files could be found in this directory.";
            }
            public void Validate()
            {
                if (string.IsNullOrEmpty(_dataDirectory))
                {
                    return;
                }
                _running = true;
                new Action(ValidateBackground).BeginInvoke(null, null);
            }
            private void ValidateBackground()
            {
                try
                {
                    CheckDisposed();
                    _dataDirectoryExists = Directory.Exists(_dataDirectory);
                    _anyDataFiles = false;
                    _allDataFiles = true;
                    if (_dataDirectoryExists)
                    {
                        foreach (var dataFileName in _dataFileNames)
                        {
                            CheckDisposed();
                            if (null != Workspace.GetDataFilePath(dataFileName, _dataDirectory))
                            {
                                _anyDataFiles = true;
                            }
                            else
                            {
                                _allDataFiles = false;
                            }
                        }
                    }
                }
                finally
                {
                    _running = false;
                }
                if (ReferenceEquals(this, _rawFilesStep._dataDirectoryValidator))
                {
                    _rawFilesStep.BeginInvoke(new Action(_rawFilesStep.UpdateStepStatus));
                }
            }
        }
    }
}
