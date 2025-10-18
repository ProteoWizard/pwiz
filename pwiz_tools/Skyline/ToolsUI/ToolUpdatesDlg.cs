/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ToolUpdatesDlg : ModeUIInvariantFormEx // Neither proteomic nor small mol, never wants the "peptide"=>"molecule" translation
    {
        private readonly SkylineWindow _parent;
        private readonly IDictionary<ToolUpdateInfo, ICollection<ToolDescription>> _tools;
        private readonly IToolUpdateHelper _updateHelper;

        public ToolUpdatesDlg(SkylineWindow parent, IEnumerable<ToolDescription> tools, IToolUpdateHelper updateHelper)
        {
            InitializeComponent();

            checkedListBoxTools.Height += labelOperation.Bottom - checkedListBoxTools.Bottom;
            Icon = Resources.Skyline;

            _parent = parent;
            _tools = new Dictionary<ToolUpdateInfo, ICollection<ToolDescription>>();
            _updateHelper = updateHelper;

            // group tools with multiple components together by their package identifier
            foreach (var tool in tools)
            {
                var info = new ToolUpdateInfo(tool.PackageIdentifier, tool.PackageName);
                ICollection<ToolDescription> toolComponents;
                if (_tools.TryGetValue(info, out toolComponents))
                {
                    toolComponents.Add(tool);
                }
                else
                {
                    _tools.Add(info, new List<ToolDescription> {tool});
                }
            }

            // populate the checklistbox with the tools package names
            checkedListBoxTools.Items.AddRange(_tools.Select(pair => pair.Key.PackageName).Cast<Object>().ToArray());

            // set all tools to checked
            for (int i = 0; i < checkedListBoxTools.Items.Count; i++)
            {
                checkedListBoxTools.SetItemCheckState(i, CheckState.Checked);
            }
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            if (checkedListBoxTools.CheckedIndices.Count == 0)
            {
                MessageDlg.Show(this, Resources.ToolUpdatesDlg_btnUpdate_Click_Please_select_at_least_one_tool_to_update_);
            }
            else
            {
                UpdateTools();

                DialogResult = DialogResult.OK;
            }
        }

        public void UpdateTools()
        {
            checkedListBoxTools.Enabled = btnUpdate.Enabled = btnExit.Enabled = false;
            checkedListBoxTools.Height += progressBar.Top - btnUpdate.Top;
            progressBar.Enabled = progressBar.Visible = labelOperation.Enabled = labelOperation.Visible = true;

            var toolsToUpdate = new List<ToolUpdateInfo>();
            if (DownloadToolsToUpdate(toolsToUpdate))
            {
                progressBar.Value = 50;
                InstallUpdates(toolsToUpdate);
            }
        }

        /// <summary>
        /// Uses an <see cref="IToolStoreClient"/> to download (zip files) for each of the tools selected in the form's checklistbox.
        /// Returns a collection of tools to be updated. 
        /// </summary>
        private bool DownloadToolsToUpdate(ICollection<ToolUpdateInfo> successfulDownloads)
        {
            labelOperation.Text = ToolsUIResources.ToolUpdatesDlg_GetTools_Downloading_Updates;
            var toolsToDownload = new Collection<ToolUpdateInfo>();
            var toolList = _tools.Keys.ToList();
            // get tools to update based on which ones are checked
            foreach (int index in checkedListBoxTools.CheckedIndices)
            {
                toolsToDownload.Add(toolList[index]);
            }

            ICollection<ToolUpdateFailure> failedDownloads = new List<ToolUpdateFailure>();
            
            try
            {
                using (var dlg = new LongWaitDlg())
                {
                    dlg.Message = ToolsUIResources.ToolUpdatesDlg_GetToolsToUpdate_Downloading_Updates;
                    var status = dlg.PerformWork(this, 1000, pm =>
                        DownloadTools(pm, toolsToDownload, successfulDownloads, failedDownloads));

                    DisplayDownloadSummary(failedDownloads);
                    if (status.IsCanceled)
                        return false;
                }

            }
            catch (Exception e)
            {
                ExceptionUtil.DisplayOrReportException(this, e);
                return false;
            }

            return true;
        }

        private DirectoryInfo ToolDir { get; set; }

        private void DownloadTools(IProgressMonitor pm,
                                   ICollection<ToolUpdateInfo> tools,
                                   ICollection<ToolUpdateInfo> successfulDownloads,
                                   ICollection<ToolUpdateFailure> failedDownloads)
        {
            ToolDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), @"ToolDir"));
            IProgressStatus status = new ProgressStatus().ChangeSegments(0, tools.Count);
            foreach (var tool in tools)
            {
                if (pm.IsCanceled)
                    throw new OperationCanceledException();
                
                var individualDir = Directory.CreateDirectory(Path.Combine(ToolDir.FullName, tool.PackageName));
                try
                {
                    var message = string.Format(ToolsUIResources.ToolStoreDlg_DownloadSelectedTool_Downloading__0_, tool.PackageName);
                    status = status.ChangeMessage(message);

                    tool.FilePath =
                        _updateHelper.GetToolZipFile(pm, status, tool.PackageIdentifer, individualDir.FullName);

                    status = status.NextSegment();
                    
                    successfulDownloads.Add(tool);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (ExceptionUtil.IsProgrammingDefect(ex))
                        throw;
                    failedDownloads.Add(new ToolUpdateFailure(tool, ex));
                }
            }
        }

        private void DisplayDownloadSummary(ICollection<ToolUpdateFailure> failedDownloads)
        {
            if (failedDownloads == null || failedDownloads.Count == 0)
                return;

            // Check if all failures have the same error message (e.g., network failure)
            var distinctMessages = failedDownloads.Select(f => f.Exception.Message).Distinct().ToList();
            
            string message;
            if (distinctMessages.Count == 1)
            {
                // All failed with same error - list names, show common error at bottom
                message = TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayDownloadSummary_Failed_to_download_updates_for_the_following_packages,
                    string.Empty,
                    TextUtil.LineSeparate(failedDownloads.Select(f => f.Tool.PackageName)),
                    string.Empty,
                    distinctMessages[0]);
            }
            else
            {
                // Different errors - show "ToolName: Error" format
                message = TextUtil.LineSeparate(
                    Resources.ToolUpdatesDlg_DisplayDownloadSummary_Failed_to_download_updates_for_the_following_packages,
                    string.Empty,
                    TextUtil.LineSeparate(failedDownloads.Select(f => 
                        FormatFailureMessage(f.Tool.PackageName, f.Exception.Message))));
            }

            MessageDlg.Show(this, message);
        }

        private void InstallUpdates(ICollection<ToolUpdateInfo> tools)
        {
            if (tools != null && tools.Count != 0 && !TestingDownloadOnly)
            {
                var failedUpdates = new Dictionary<string, string>();
                var successfulUpdates = new Collection<string>();

                int installCount = 0;
                foreach (var tool in tools)
                {
                    labelOperation.Text =
                        string.Format(ToolsUIResources.ToolUpdatesDlg_InstallUpdates_Installing_updates_to__0_,
                                      tool.PackageName);

                    var toolList = ToolList.CopyTools(Settings.Default.ToolList);
                    ToolInstaller.UnzipToolReturnAccumulator result = null;
                    try
                    {
                        result = _updateHelper.UnpackZipTool(tool.FilePath, new ToolInstallUI.InstallZipToolHelper(this, _parent.InstallProgram));

                        if (result == null)
                            failedUpdates.Add(tool.PackageName, Resources.ToolUpdatesDlg_InstallUpdates_User_cancelled_installation);
                    }
                    catch (ToolExecutionException x)
                    {
                        failedUpdates.Add(tool.PackageName, x.Message);
                    }
                    catch (IOException x)
                    {
                        failedUpdates.Add(tool.PackageName,
                                          TextUtil.LineSeparate(string.Format(Resources.ConfigureToolsDlg_UnpackZipTool_Failed_attempting_to_extract_the_tool_from__0_,
                                                  Path.GetFileName(tool.FilePath)), x.Message));
                    }

                    progressBar.Value = Convert.ToInt32((((((double)++installCount) / tools.Count) * 100) / 2) + 50);

                    if (result == null)
                        continue;

                    if (result.MessagesThrown.Count > 0)
                    {
                        // ZIP extracted but no valid tools found - treat as failure
                        var errorMessage = TextUtil.LineSeparate(result.MessagesThrown);
                        failedUpdates.Add(tool.PackageName, errorMessage);
                    }
                    else
                    {
                        // tool was successfully updated
                        successfulUpdates.Add(tool.PackageName);
                    }
                }

                // clean-up
                DirectoryEx.SafeDelete(ToolDir.FullName);

                progressBar.Value = 100;
                DisplayInstallSummary(successfulUpdates, failedUpdates);
            }
        }

        private void DisplayInstallSummary(ICollection<string> successfulUpdates, ICollection<KeyValuePair<string, string>> failedUpdates)
        {
            string oneUpdate = Resources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tool;
            string multipleUpdates =
                Resources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tools;

            string oneFailure = Resources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tool;
            string multipleFailures =
                Resources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tools;
            
            string success = TextUtil.LineSeparate(successfulUpdates.Count == 1 ? oneUpdate : multipleUpdates, 
                                                   string.Empty,
                                                   TextUtil.LineSeparate(successfulUpdates));

            string failure = TextUtil.LineSeparate(failedUpdates.Count == 1 ? oneFailure : multipleFailures, 
                                                   string.Empty,
                                                   TextUtil.LineSeparate(failedUpdates.Select(pair => FormatFailureMessage(pair.Key, pair.Value))));

            if (successfulUpdates.Count != 0 && !failedUpdates.Any())
            {
                MessageDlg.Show(this, success);
            } 
            else if (successfulUpdates.Count == 0 & failedUpdates.Any())
            {
                MessageDlg.Show(this, failure);
            }
            else // both successes and failures
            {
                MessageDlg.Show(this, TextUtil.LineSeparate(success, string.Empty, failure));
            }
        }

        public static string FormatFailureMessage(string toolName, string errorMsg)
        {
            return string.Format(@"{0}: {1}", toolName, errorMsg);
        }

        #region Functional test support

        public bool TestingDownloadOnly { get; set; }       
 
        public void SelectAll()
        {
            for (int i = 0; i < checkedListBoxTools.Items.Count; i++)
            {
                checkedListBoxTools.SetItemCheckState(i, CheckState.Checked);
            }
        }

        public void DeselectAll()
        {
            foreach (int index in checkedListBoxTools.CheckedIndices)
            {
                checkedListBoxTools.SetItemCheckState(index, CheckState.Unchecked);
            }
        }

        public int ItemCount
        {
            get { return checkedListBoxTools.Items.Count; }
        } 

        #endregion
    }

    /// <summary>
    /// Encapsulates all the information used by the ToolUpdatesDlg to update and display information
    /// about a given tool.
    /// </summary>
    public class ToolUpdateInfo
    {
        public string PackageIdentifer { get; }
        public string PackageName { get; }

        public string FilePath { get; set; }

        public ToolUpdateInfo(string packageIdentifer, string packageName, string filePath = null)
        {
            PackageIdentifer = packageIdentifer;
            PackageName = packageName;

            FilePath = filePath;
        }

        #region object overrides

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            ToolUpdateInfo other = obj as ToolUpdateInfo;
            if (other == null)
                return false;
            return Equals(PackageIdentifer, other.PackageIdentifer) &&
                   Equals(PackageName, other.PackageName);
        }

        public override int GetHashCode()
        {
            return PackageIdentifer.GetHashCode() + PackageName.GetHashCode();
        }

        #endregion
    }

    internal class ToolUpdateFailure
    {
        public ToolUpdateInfo Tool { get; }
        public Exception Exception { get; }

        public ToolUpdateFailure(ToolUpdateInfo tool, Exception exception)
        {
            Tool = tool;
            Exception = exception;
        }
    }

    public interface IToolUpdateHelper
    {
        ToolInstaller.UnzipToolReturnAccumulator UnpackZipTool(string pathToZip, IUnpackZipToolSupport unpackSupport);
        string GetToolZipFile(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier, string directory);
    }

    public class ToolUpdateHelper : IToolUpdateHelper
    {
        public ToolInstaller.UnzipToolReturnAccumulator UnpackZipTool(string pathToZip, IUnpackZipToolSupport unpackSupport)
        {
            return ToolInstaller.UnpackZipTool(pathToZip, unpackSupport);
        }

        public string GetToolZipFile(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier, string directory)
        {
            var client = ToolStoreUtil.CreateClient();
            return client.GetToolZipFile(progressMonitor, progressStatus, packageIdentifier, directory);
        }
    }

}
