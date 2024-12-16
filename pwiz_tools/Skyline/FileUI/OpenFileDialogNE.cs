﻿/*
 * Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
 *
 * Copyright 2009 Vanderbilt University - Nashville, TN 37232
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
using System.Windows.Forms;
using System.IO;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.Util;


namespace pwiz.Skyline.FileUI
{
    public class OpenFileDialogNE : BaseFileDialogNE
    {
        /// <summary>
        /// File picker which is aware of mass spec "files" that are really directories
        /// </summary>
        /// <param name="sourceTypes"></param>
        /// <param name="remoteAccounts">For UNIFI</param>
        /// <param name="specificDataSourceFilter">Optional list of specific files the user needs to located, ignoring the rest</param>
        public OpenFileDialogNE(string[] sourceTypes, IList<RemoteAccount> remoteAccounts, IList<string> specificDataSourceFilter = null)
            : base(sourceTypes, remoteAccounts, specificDataSourceFilter )
        {
            actionButton.Text = FileUIResources.OpenFileDialogNE_OpenFileDialogNE_Open;
        }

        protected override void DoMainAction()
        {
            Open();
        }

        public void Open()
        {
            List<MsDataFileUri> dataSourceList = new List<MsDataFileUri>();
            foreach (ListViewItem item in listView.SelectedItems)
            {
                if (!DataSourceUtil.IsFolderType(item.SubItems[1].Text))
                {
                    dataSourceList.Add(((SourceInfo)item.Tag).MsDataFileUri);
                }
            }
            if (dataSourceList.Count > 0)
            {
                FileNames = dataSourceList.ToArray();
                _abortPopulateList = true;
                DialogResult = DialogResult.OK;
                return;
            }

            // No files selected: see if there is a folder selected that we
            // should navigate to
            foreach (ListViewItem item in listView.SelectedItems)
            {
                if (DataSourceUtil.IsFolderType(item.SubItems[1].Text))
                {
                    OpenFolderItem(item);
                    return;
                }
            }

            try
            {
                // perhaps the user has typed an entire filename into the text box - or just garbage
                var fileOrDirName = sourcePathTextBox.Text;
                bool exists;
                bool triedAddingDirectory = false;
                while (!(exists = ((File.Exists(fileOrDirName) || Directory.Exists(fileOrDirName)))))
                {
                    if (triedAddingDirectory)
                        break;
                    MsDataFilePath currentDirectoryPath = CurrentDirectory as MsDataFilePath;
                    if (null == currentDirectoryPath)
                    {
                        break;
                    }
                    fileOrDirName = Path.Combine(currentDirectoryPath.FilePath, fileOrDirName);
                    triedAddingDirectory = true;
                }

                if (exists)
                {
                    if (DataSourceUtil.IsDataSource(fileOrDirName))
                    {
                        FileNames = new[] { MsDataFileUri.Parse(fileOrDirName) };
                        DialogResult = DialogResult.OK;
                        return;
                    }
                    else if (Directory.Exists(fileOrDirName))
                    {
                        OpenFolder(new MsDataFilePath(fileOrDirName));
                        return;
                    }
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { } // guard against user typed-in-garbage

            // No files or folders selected: Show an error message.
            MessageDlg.Show(this, FileUIResources.OpenDataSourceDialog_Open_Please_select_one_or_more_data_sources);
        }

    }
}
