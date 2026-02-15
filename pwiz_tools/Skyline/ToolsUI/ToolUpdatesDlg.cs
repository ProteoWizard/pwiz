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
            try
            {
                if (DownloadToolsToUpdate(toolsToUpdate))
                {
                    progressBar.Value = 50;
                    InstallUpdates(toolsToUpdate);
                }
            }
            finally
            {
                // Clean up ToolUpdateInfo objects (auto-deletes all ~SK*.tmp files in system temp folder)
                // This ensures cleanup whether download fails, install fails, or everything succeeds
                foreach (var tool in toolsToUpdate)
                {
                    tool.Dispose();
                }
            }
        }

        /// <summary>
        /// Uses an <see cref="IToolStoreClient"/> to download (zip files) for each of the tools selected in the form's checklistbox.
        /// Returns a collection of all download attempts (success and failure tracked via DownloadException property).
        /// </summary>
        private bool DownloadToolsToUpdate(ICollection<ToolUpdateInfo> downloadAttempts)
        {
            labelOperation.Text = ToolsUIResources.ToolUpdatesDlg_GetTools_Downloading_Updates;
            var toolsToDownload = new Collection<ToolUpdateInfo>();
            var toolList = _tools.Keys.ToList();
            // get tools to update based on which ones are checked
            foreach (int index in checkedListBoxTools.CheckedIndices)
            {
                toolsToDownload.Add(toolList[index]);
            }
            
            try
            {
                using (var dlg = new LongWaitDlg())
                {
                    dlg.Message = ToolsUIResources.ToolUpdatesDlg_GetToolsToUpdate_Downloading_Updates;
                    var status = dlg.PerformWork(this, 1000, pm =>
                        DownloadTools(pm, toolsToDownload, downloadAttempts));

                    DisplayDownloadSummary(downloadAttempts);
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

        private void DownloadTools(IProgressMonitor pm,
                                   ICollection<ToolUpdateInfo> toolsToDownload,
                                   ICollection<ToolUpdateInfo> downloadAttempts)
        {
            IProgressStatus status = new ProgressStatus().ChangeSegments(0, toolsToDownload.Count);
            foreach (var tool in toolsToDownload)
            {
                if (pm.IsCanceled)
                    throw new OperationCanceledException();
                
                // Always add to downloadAttempts for cleanup (FileSaver creates temp file immediately)
                downloadAttempts.Add(tool);
                
                try
                {
                    var message = string.Format(ToolsUIResources.ToolStoreDlg_DownloadSelectedTool_Downloading__0_, tool.PackageName);
                    status = status.ChangeMessage(message);

                    // Download to system temp folder (not Tools directory) to avoid conflict with CheckToolDirConsistency()
                    // FileSaver creates unique ~SK*.tmp file and auto-cleanup on disposal
                    string zipDestination = Path.Combine(Path.GetTempPath(), tool.PackageName + ToolDescription.EXT_INSTALL);
                    tool.FileSaver = new FileSaver(zipDestination);
                    
                    _updateHelper.GetToolZipFile(pm, status, tool.PackageIdentifier, tool.FileSaver);

                    status = status.NextSegment();
                    // Success - DownloadException remains null
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (ExceptionUtil.IsProgrammingDefect(ex))
                        throw;
                    // Store exception on tool for later reporting
                    tool.DownloadException = ex;
                }
            }
        }

        private void DisplayDownloadSummary(ICollection<ToolUpdateInfo> downloadAttempts)
        {
            if (downloadAttempts == null)
                return;

            // Filter to only failed downloads
            var failedDownloads = downloadAttempts.Where(t => !t.DownloadSucceeded).ToList();
            if (failedDownloads.Count == 0)
                return;

            // Check if all failures have the same error message (e.g., network failure)
            var distinctMessages = failedDownloads.Select(f => f.DownloadException.Message).Distinct().ToList();
            
            string message;
            if (distinctMessages.Count == 1)
            {
                // All failed with same error - use multi-tool formatter
                message = FormatDownloadFailureSummary(
                    failedDownloads.Select(f => f.PackageName),
                    distinctMessages[0]);
            }
            else
            {
                // Different errors - format each tool individually
                var formattedFailures = failedDownloads.Select(f => 
                    FormatFailureMessage(f.PackageName, f.DownloadException.Message));

                message = failedDownloads.Count == 1
                    ? FormatDownloadFailureSummary(failedDownloads.First().PackageName,
                        failedDownloads.First().DownloadException.Message)
                    : FormatDownloadFailureSummary(formattedFailures);
            }

            MessageDlg.Show(this, message);
        }

        private void InstallUpdates(ICollection<ToolUpdateInfo> downloadAttempts)
        {
            if (downloadAttempts == null || downloadAttempts.Count == 0 || TestingDownloadOnly)
                return;

            // Only install tools that downloaded successfully
            var toolsToInstall = downloadAttempts.Where(t => t.DownloadSucceeded).ToList();
            if (toolsToInstall.Count == 0)
                return;

            var failedUpdates = new Dictionary<string, string>();
            var successfulUpdates = new Collection<string>();

            int installCount = 0;
            foreach (var tool in toolsToInstall)
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
                    // Don't show temp filename (~SK*.tmp) in error message - use complete message
                    // The tool.PackageName is already shown in the error summary
                    failedUpdates.Add(tool.PackageName,
                                      TextUtil.LineSeparate(Resources.ConfigureToolsDlg_UnpackZipTool_Failed_attempting_to_extract_the_tool, x.Message));
                }

                progressBar.Value = Convert.ToInt32((((((double)++installCount) / toolsToInstall.Count) * 100) / 2) + 50);

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

            progressBar.Value = 100;
            DisplayInstallSummary(successfulUpdates, failedUpdates);
        }

        private void DisplayInstallSummary(ICollection<string> successfulUpdates, ICollection<KeyValuePair<string, string>> failedUpdates)
        {
            string message;
            
            if (successfulUpdates.Count != 0 && !failedUpdates.Any())
            {
                // Only successes
                message = successfulUpdates.Count == 1
                    ? FormatInstallSuccessSummary(successfulUpdates.First())
                    : FormatInstallSuccessSummary(successfulUpdates);
            } 
            else if (successfulUpdates.Count == 0 && failedUpdates.Any())
            {
                // Only failures
                var formattedFailures = failedUpdates.Select(pair => FormatFailureMessage(pair.Key, pair.Value));
                message = failedUpdates.Count == 1
                    ? FormatInstallFailureSummary(formattedFailures.First())
                    : FormatInstallFailureSummary(formattedFailures);
            }
            else // both successes and failures
            {
                message = FormatMixedInstallSummary(
                    successfulUpdates,
                    failedUpdates.Select(pair => FormatFailureMessage(pair.Key, pair.Value)));
            }

            MessageDlg.Show(this, message);
        }

        public static string FormatFailureMessage(string toolName, string errorMsg)
        {
            return string.Format(@"{0}: {1}", toolName, errorMsg);
        }

        #region Message formatting helpers for testing

        /// <summary>
        /// Formats download failure summary message for a single tool with single error.
        /// </summary>
        public static string FormatDownloadFailureSummary(string toolName, string errorMessage)
        {
            return TextUtil.LineSeparate(
                ToolsUIResources.ToolUpdatesDlg_DisplayDownloadSummary_Failed_to_download_updates_for_the_following_packages,
                string.Empty,
                toolName,
                string.Empty,
                errorMessage);
        }

        /// <summary>
        /// Formats download failure summary message for multiple tools with common error.
        /// </summary>
        public static string FormatDownloadFailureSummary(IEnumerable<string> toolNames, string commonErrorMessage)
        {
            return TextUtil.LineSeparate(
                ToolsUIResources.ToolUpdatesDlg_DisplayDownloadSummary_Failed_to_download_updates_for_the_following_packages,
                string.Empty,
                TextUtil.LineSeparate(toolNames),
                string.Empty,
                commonErrorMessage);
        }

        /// <summary>
        /// Formats download failure summary message for multiple tools with individual errors.
        /// </summary>
        public static string FormatDownloadFailureSummary(IEnumerable<string> formattedFailureMessages)
        {
            return TextUtil.LineSeparate(
                ToolsUIResources.ToolUpdatesDlg_DisplayDownloadSummary_Failed_to_download_updates_for_the_following_packages,
                string.Empty,
                TextUtil.LineSeparate(formattedFailureMessages));
        }

        /// <summary>
        /// Formats install success summary message for single tool.
        /// </summary>
        public static string FormatInstallSuccessSummary(string toolName)
        {
            return TextUtil.LineSeparate(
                ToolsUIResources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tool,
                string.Empty,
                toolName);
        }

        /// <summary>
        /// Formats install success summary message for multiple tools.
        /// </summary>
        public static string FormatInstallSuccessSummary(IEnumerable<string> toolNames)
        {
            return TextUtil.LineSeparate(
                ToolsUIResources.ToolUpdatesDlg_DisplayInstallSummary_Successfully_updated_the_following_tools,
                string.Empty,
                TextUtil.LineSeparate(toolNames));
        }

        /// <summary>
        /// Formats install failure summary message for single tool with single error.
        /// </summary>
        public static string FormatInstallFailureSummary(string formattedFailureMessage)
        {
            return TextUtil.LineSeparate(
                ToolsUIResources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tool,
                string.Empty,
                formattedFailureMessage);
        }

        /// <summary>
        /// Formats install failure summary message for multiple tools with individual errors.
        /// </summary>
        public static string FormatInstallFailureSummary(IEnumerable<string> formattedFailureMessages)
        {
            return TextUtil.LineSeparate(
                ToolsUIResources.ToolUpdatesDlg_DisplayInstallSummary_Failed_to_update_the_following_tools,
                string.Empty,
                TextUtil.LineSeparate(formattedFailureMessages));
        }

        /// <summary>
        /// Formats mixed success/failure summary message.
        /// </summary>
        public static string FormatMixedInstallSummary(IEnumerable<string> successfulToolNames, IEnumerable<string> formattedFailureMessages)
        {
            return TextUtil.LineSeparate(
                FormatInstallSuccessSummary(successfulToolNames),
                string.Empty,
                FormatInstallFailureSummary(formattedFailureMessages));
        }

        #endregion

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
    public class ToolUpdateInfo : IDisposable
    {
        public string PackageIdentifier { get; }
        public string PackageName { get; }

        private FileSaver _fileSaver;

        /// <summary>
        /// Gets the file path for the downloaded tool zip file.
        /// Returns the FileSaver's SafeName if available, otherwise the stored FilePath.
        /// </summary>
        public string FilePath => _fileSaver?.SafeName;

        public FileSaver FileSaver
        {
            get => _fileSaver;
            set
            {
                _fileSaver?.Dispose(); // Dispose any existing FileSaver
                _fileSaver = value;
            }
        }

        /// <summary>
        /// Exception that occurred during download, or null if download succeeded.
        /// </summary>
        public Exception DownloadException { get; set; }

        /// <summary>
        /// Returns true if the download succeeded (no exception).
        /// </summary>
        public bool DownloadSucceeded => DownloadException == null;

        public ToolUpdateInfo(string packageIdentifier, string packageName)
        {
            PackageIdentifier = packageIdentifier;
            PackageName = packageName;
        }

        public void Dispose()
        {
            _fileSaver?.Dispose();
        }

        #region object overrides

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            ToolUpdateInfo other = obj as ToolUpdateInfo;
            if (other == null)
                return false;
            return Equals(PackageIdentifier, other.PackageIdentifier) &&
                   Equals(PackageName, other.PackageName);
        }

        public override int GetHashCode()
        {
            return PackageIdentifier.GetHashCode() + PackageName.GetHashCode();
        }

        #endregion
    }

    public interface IToolUpdateHelper
    {
        ToolInstaller.UnzipToolReturnAccumulator UnpackZipTool(string pathToZip, IUnpackZipToolSupport unpackSupport);
        void GetToolZipFile(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier, FileSaver fileSaver);
    }

    public class ToolUpdateHelper : IToolUpdateHelper
    {
        public ToolInstaller.UnzipToolReturnAccumulator UnpackZipTool(string pathToZip, IUnpackZipToolSupport unpackSupport)
        {
            return ToolInstaller.UnpackZipTool(pathToZip, unpackSupport);
        }

        public void GetToolZipFile(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier, FileSaver fileSaver)
        {
            var client = ToolStoreUtil.CreateClient();
            client.GetToolZipFile(progressMonitor, progressStatus, packageIdentifier, fileSaver);
        }
    }

}
