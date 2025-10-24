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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using JetBrains.Annotations;
using Newtonsoft.Json;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ToolStoreDlg : FormEx
    {
        private IToolStoreClient _toolStoreClient;

        private readonly IList<ToolStoreItem> _tools;
        private readonly ToolInstallUI.InstallProgram _installProgram;
        
        public ToolStoreDlg(IToolStoreClient toolStoreClient, IList<ToolStoreItem> tools, ToolInstallUI.InstallProgram installProgram)
        {
            _toolStoreClient = toolStoreClient;
            _tools = tools;
            _installProgram = installProgram;

            InitializeComponent();

            Icon = Resources.Skyline;
        }
        
        public IToolStoreClient ToolStoreClient
        {
            set => _toolStoreClient = value;
        }

        private void ToolStore_Load(object sender, EventArgs e)
        {
            PopulateToolList();
        }

        private void PopulateToolList()
        {
            listBoxTools.DataSource = _tools;
            listBoxTools.DisplayMember = @"Name";
        }

        private void listBoxTools_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDisplayedTool();
        }

        private void UpdateDisplayedTool()
        {
            var toolStoreItem = _tools[listBoxTools.SelectedIndex];
            toolStoreItem.IconLoadComplete = null;
            if (toolStoreItem.IconDownloading)
            {
                toolStoreItem.IconLoadComplete = () =>
                {
                    try
                    {
                        if (IsHandleCreated)
                        {
                            Invoke(new Action(() => pictureBoxTool.Image = toolStoreItem.ToolImage));
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore
                    }
                };
            }
            pictureBoxTool.Image = toolStoreItem.ToolImage;
            textBoxOrganization.Text = toolStoreItem.Organization;
            textBoxAuthors.Text = toolStoreItem.Authors;

            linkLabelProvider.Text = toolStoreItem.Provider;
            linkLabelProvider.LinkArea = new LinkArea(0, linkLabelProvider.Text.Length);
            linkLabelProvider.Links.Clear();
            linkLabelProvider.Links.Add(0, linkLabelProvider.Text.Length, linkLabelProvider.Text);

            textBoxLanguages.Text = toolStoreItem.Languages;
            textBoxStatus.Text = FormatVersionText(toolStoreItem);
            textBoxDescription.Text = FormatDescriptionText(toolStoreItem.Description);

            if (toolStoreItem.Installed && toolStoreItem.IsMostRecentVersion)
                buttonInstallUpdate.Text = ToolsUIResources.ToolStoreDlg_UpdateDisplayedTool_Reinstall;
            else if (toolStoreItem.Installed)
                buttonInstallUpdate.Text = ToolsUIResources.ToolStoreDlg_UpdateDisplayedTool_Update;
            else
                buttonInstallUpdate.Text = ToolsUIResources.ToolStoreDlg_UpdateDisplayedTool_Install;
        }

        private static string FormatVersionText(ToolStoreItem tool)
        {
            if (!tool.Installed && !tool.IsMostRecentVersion)
            {
                return string.Format(ToolsUIResources.ToolStoreDlg_FormatVersionText_Not_currently_installed__Version___0__is_available, tool.Version);
            } 
            else if (!tool.IsMostRecentVersion)
            {
                return
                    string.Format(
                        ToolsUIResources.ToolStoreDlg_FormatVersionText_Version__0__currently_installed__Version__1__is_available_,
                        ToolStoreUtil.GetCurrentVersion(tool.Identifier), tool.Version);
            }
            else
            {
                return string.Format(ToolsUIResources.ToolStoreDlg_FormatVersionText_Currently_installed_and_fully_updated__Version___0___,
                                 ToolStoreUtil.GetCurrentVersion(tool.Identifier));
            }
        }

        private static string FormatDescriptionText(string description)
        {
            if (description == null)
                return null;

            description = description.Trim();
            
            // ReSharper disable LocalizableElement
            if (description.StartsWith("\"") && description.EndsWith("\""))
            // ReSharper restore LocalizableElement
                description = description.Substring(1, description.Length - 2);

            return description;
        }

        private void buttonInstallUpdate_Click(object sender, EventArgs e)
        {
            DownloadAndInstallSelectedTool();
        }

        public void DownloadTool(int index)
        {
            listBoxTools.SelectedIndex = index;
            DownloadAndInstallSelectedTool();
        }

        public void DownloadSelectedTool()
        {
            // For backward compatibility with tests - delegates to new method
            DownloadAndInstallSelectedTool();
        }

        private void DownloadAndInstallSelectedTool()
        {
            string identifier = _tools[listBoxTools.SelectedIndex].Identifier;
            string toolName = _tools[listBoxTools.SelectedIndex].Name;
            string toolsDirectory = ToolDescriptionHelpers.GetToolsDirectory();
            
            // Use FileSaver pattern: download to ~SK*.tmp, extract/install, never commit (auto-cleanup)
            // Logical destination is toolName.zip in tools directory (for error messages)
            string zipDestination = Path.Combine(toolsDirectory, toolName + ToolDescription.EXT_INSTALL);
            using (var fileSaver = new FileSaver(zipDestination))
            {
                try
                {
                    // Download to FileSaver temp file (~SK*.tmp in tools directory)
                    var downloadMessage = string.Format(ToolsUIResources.ToolStoreDlg_DownloadSelectedTool_Downloading__0_, toolName);
                    var progressStatus = new ProgressStatus(downloadMessage);
                    IProgressStatus downloadStatus;
                    using (var dlg = new LongWaitDlg())
                    {
                        downloadStatus = dlg.PerformWork(this, 500, progressMonitor =>
                        {
                            _toolStoreClient.GetToolZipFile(progressMonitor, progressStatus, identifier, fileSaver);
                        });
                    }
                    
                    if (downloadStatus.IsCanceled)
                        return; // FileSaver.Dispose() will auto-delete ~SK*.tmp
                    
                    // Extract and install the tool from the temp file
                    ToolInstallUI.InstallZipTool(this, fileSaver.SafeName, _installProgram);
                    
                    // Success - set DialogResult to dismiss form
                    // FileSaver.Dispose() will auto-delete ~SK*.tmp (never committed)
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    // FileSaver.Dispose() will auto-delete ~SK*.tmp
                    ExceptionUtil.DisplayOrReportException(this, ex);
                }
            }
        }

        private void buttonToolStore_Click(object sender, EventArgs e)
        {
            WebHelpers.OpenLink(this, _tools[listBoxTools.SelectedIndex].FilePath);
        }

        private void linkLabelProvider_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, e.Link.LinkData as string);
        }

        #region Functional testing support

        public int ToolCount { get { return _tools.Count; } }

        public IEnumerable<ToolStoreItem> GetTools()
        {
            return _tools.Select(tool => new ToolStoreItem(tool));
        }

        public void SelectTool(string toolName)
        {
            int toolIndex = _tools.IndexOf(tool => Equals(tool.Name, toolName));
            listBoxTools.SelectedIndex = toolIndex;
        }

        public bool IsIconDownloading => _tools[listBoxTools.SelectedIndex].IconDownloading;

        #endregion
    }

    public interface IToolStoreClient
    {
        /// <summary>
        /// Returns a list of ToolStoreItems which are used to populate the ToolStoreDlg.
        /// </summary>
        IList<ToolStoreItem> GetToolStoreItems();

        /// <summary>
        /// Downloads the tool zip file associated with the given packageIdentifer to the FileSaver's temp location.
        /// The downloaded file is written to fileSaver.SafeName (~SK*.tmp in the destination directory).
        /// Caller controls cleanup by committing or disposing the FileSaver.
        /// Uses IProgressMonitor for progress reporting and cancellation support.
        /// If the monitor is cancelled, this method must return promptly.
        /// </summary>
        /// <param name="progressMonitor">Progress monitor for reporting download progress and handling cancellation</param>
        /// <param name="progressStatus">Initial progress status with message to display during download</param>
        /// <param name="packageIdentifier">Unique identifier for the tool package to download</param>
        /// <param name="fileSaver">FileSaver managing the destination zip file (downloads to fileSaver.SafeName)</param>
        void GetToolZipFile(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier, FileSaver fileSaver);
        
        /// <summary>
        /// Returns true if the given package (identified by its version), has an update available. The
        /// version is the string representation of the version installed
        /// </summary>
        bool IsToolUpdateAvailable(string identifier, Version version);
    }

    public class WebToolStoreClient : IToolStoreClient
    {
        public static readonly Uri TOOL_STORE_URI = new Uri(@"https://skyline.ms");
        protected const string GET_TOOLS_URL = "/skyts/home/getToolsApi.view";
        protected const string DOWNLOAD_TOOL_URL = "/skyts/home/downloadTool.view";
        public const string TOOL_DETAILS_URL = "/skyts/home/details.view";

        protected Dictionary<String, Version> latestVersions_;

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        protected struct ToolStoreVersion
        {
            public string Identifier;
            public string Version;
        }

        public IList<ToolStoreItem> GetToolStoreItems()
        {
            return JsonConvert.DeserializeObject<ToolStoreItem[]>(GetToolsJson());
        }

        public void GetToolZipFile(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier,
            FileSaver fileSaver)
        {
            GetToolZipFileWithProgress(progressMonitor, progressStatus, packageIdentifier, fileSaver);
        }

        public static void GetToolZipFileWithProgress(IProgressMonitor progressMonitor, IProgressStatus progressStatus, string packageIdentifier, FileSaver fileSaver)
        {
            using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
            var uri = new UriBuilder(TOOL_STORE_URI)
            {
                Path = DOWNLOAD_TOOL_URL,
                Query = @"lsid=" + Uri.EscapeDataString(packageIdentifier)
            };
            
            // Download the tool zip file directly to FileSaver's temp location (~SK*.tmp)
            // Progress reporting and cancellation handled by HttpClientWithProgress
            // FileSaver provides automatic cleanup if not committed
            httpClient.DownloadFile(uri.Uri, fileSaver.SafeName);
        }

        public bool IsToolUpdateAvailable(string identifier, Version version)
        {
            if (latestVersions_ == null)
            {
                latestVersions_ = new Dictionary<string, Version>();
                var toolInfo = JsonConvert.DeserializeObject<ToolStoreVersion[]>(GetToolsJson());
                foreach (var info in toolInfo)
                    latestVersions_.Add(info.Identifier, new Version(info.Version));
            }

            return (latestVersions_.ContainsKey(identifier)) && version < latestVersions_[identifier];
        }

        protected string GetToolsJson()
        {
            using var httpClient = new HttpClientWithProgress(new SilentProgressMonitor());
            return httpClient.DownloadString(TOOL_STORE_URI + GET_TOOLS_URL);
        }
    }

    public class ToolStoreItem
    {
        public string Name { get; private set; }
        public string Authors { get; private set; }
        public string Organization { get; private set; }
        public string Provider { get; private set; }
        public string Version { get; private set; }
        public string Description { get; private set; }
        public string Identifier { get; private set; }
        public string Languages { get; private set; }
        public Image ToolImage { get; private set; }
        public bool Installed { get; private set; }
        public bool IsMostRecentVersion { get; private set; }
        public string FilePath { get; private set; }

        public bool IconDownloading { get; private set; }
        public Action IconLoadComplete { get; set; }
        
        public ToolStoreItem(ToolStoreItem item)
        {
            Name = item.Name;
            Authors = item.Authors;
            Organization = item.Organization;
            Provider = item.Provider;
            Version = item.Version;
            Description = item.Description;
            Identifier = item.Identifier;
            Languages = item.Languages;
            ToolImage = item.ToolImage;
            Installed = item.Installed;
            IsMostRecentVersion = item.IsMostRecentVersion;
            FilePath = item.FilePath;
        }

        public ToolStoreItem(string name,
                             string authors,
                             string providers,
                             string version,
                             string description,
                             string identifier,
                             Image toolImage,
                             string filePath = null)
        {
            Name = name;
            Authors = authors;
            Provider = providers;
            Version = version;
            Description = description;
            Identifier = identifier;
            ToolImage = toolImage;
            Installed = ToolStoreUtil.IsInstalled(identifier);
            IsMostRecentVersion = ToolStoreUtil.IsMostRecentVersion(identifier, version);
            FilePath = filePath;
        }

        [JsonConstructor]
        public ToolStoreItem(string name,
                             string authors,
                             string organization,
                             string provider,
                             string version,
                             string description,
                             string identifier,
                             string languages,
                             string iconUrl,
                             string downloadUrl)
        {
            Name = name;
            Authors = authors;
            Organization = organization;
            Provider = provider;
            Version = version;
            // ReSharper disable LocalizableElement
            Description = description != null ? description.Replace("\n", Environment.NewLine) : null;
            // ReSharper restore LocalizableElement
            Identifier = identifier;
            Languages = languages;
            ToolImage = ToolStoreUtil.DefaultImage;
            if (iconUrl != null)
            {
                IconDownloading = true;
                var iconUri = new Uri(WebToolStoreClient.TOOL_STORE_URI + iconUrl);
                
                // Download icon asynchronously on background thread
                ActionUtil.RunAsync(() =>
                {
                    try
                    {
                        using var httpClient = new HttpClientWithProgress(new SilentProgressMonitor());
                        byte[] iconData = httpClient.DownloadData(iconUri.AbsoluteUri);
                        
                        using (MemoryStream ms = new MemoryStream(iconData))
                        {
                            ToolImage = Image.FromStream(ms);
                        }
                        IconDownloading = false;
                        if (IconLoadComplete != null)
                            IconLoadComplete();
                    }
                    catch (Exception exception)
                    {
                        IconDownloading = false;
                        // Ignore but log to debug console in debug builds
                        Debug.WriteLine($@"Failed to download tool icon: {exception.Message}");
                    }
                });
            }
            Installed = ToolStoreUtil.IsInstalled(identifier);
            IsMostRecentVersion = ToolStoreUtil.IsMostRecentVersion(identifier, version);
            UriBuilder uri = new UriBuilder(WebToolStoreClient.TOOL_STORE_URI)
                {
                    Path = WebToolStoreClient.TOOL_DETAILS_URL,
                    Query = @"name=" + Uri.EscapeDataString(name)
                };
            FilePath = uri.Uri.AbsoluteUri;
        }
    }

    public static class ToolStoreUtil
    {
        public static Image DefaultImage
        {
            get { return Resources.ExternalTool; }
        }

        public static IToolStoreClient ToolStoreClient { get; set; }

        // for testing, return a TestToolStoreClient with the desired attributes and a path to a local directory with which to populate the store, updates etc.
        public static IToolStoreClient CreateClient()
        {
            return new WebToolStoreClient();
//            return new TestToolStoreClient(@"C:\proj\pwiz\pwiz_tools\Skyline\Executables\Tools\ToolStore");
        }

        static ToolStoreUtil()
        {
            ToolStoreClient = CreateClient();
        }

        /// <returns>True if the a tool with the given identifier is installed.</returns>
        public static bool IsInstalled(string identifier)
        {
            return Settings.Default.ToolList.Contains(
                description => Equals(description.PackageIdentifier, identifier));
        }

        /// <returns>True if the a tool with the given identifier is the most recent.</returns>
        public static bool IsMostRecentVersion(string identifier, string version)
        {
            return Settings.Default.ToolList.Contains(
                    description =>
                        Equals(description.PackageIdentifier, identifier) &&
                        !string.IsNullOrEmpty(description.PackageVersion) &&
                        new Version(description.PackageVersion) >= new Version(version));
        }

        /// <returns>The string representation of the version of the installed tool with the given identifier. If the tool
        /// is not installed, returns null.</returns>
        public static string GetCurrentVersion(string identifier)
        {
            var tool = Settings.Default.ToolList.FirstOrDefault(
                description => Equals(description.PackageIdentifier, identifier));
            return tool != null ? tool.PackageVersion : null;
        }

        public static IEnumerable<ToolDescription> UpdatableTools(IList<ToolDescription> tools)
        {
            return tools.Where(description => !string.IsNullOrWhiteSpace(description.PackageVersion));
        }

        /// <summary>
        /// Checks the web to see if there are updates available to any currently installed tools. If there are updates,
        /// sets the ToolDescription's UpdateAvailable bool to true.
        /// </summary>
        public static void CheckForUpdates(IList<ToolDescription> tools)
        {
            try
            {
                foreach (var toolDescription in UpdatableTools(tools))
                {
                    toolDescription.UpdateAvailable = ToolStoreClient.IsToolUpdateAvailable(toolDescription.PackageIdentifier,
                                                                                   new Version(toolDescription.PackageVersion));
                }
            }
// ReSharper disable EmptyGeneralCatchClause
            catch
// ReSharper restore EmptyGeneralCatchClause
            {
                // fail and give up; hopefully it will work the next time
            }
        }
    }

}
