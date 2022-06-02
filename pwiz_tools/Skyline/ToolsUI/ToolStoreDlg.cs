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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ionic.Zip;
using JetBrains.Annotations;
using Newtonsoft.Json;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.ToolsUI
{
    public partial class ToolStoreDlg : FormEx
    {
        private readonly IToolStoreClient _toolStoreClient;
        private readonly IList<ToolStoreItem> _tools;
        
        public ToolStoreDlg(IToolStoreClient toolStoreClient, IList<ToolStoreItem> tools)
        {
            _toolStoreClient = toolStoreClient;
            _tools = tools;

            InitializeComponent();

            Icon = Resources.Skyline;
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
                buttonInstallUpdate.Text = Resources.ToolStoreDlg_UpdateDisplayedTool_Reinstall;
            else if (toolStoreItem.Installed)
                buttonInstallUpdate.Text = Resources.ToolStoreDlg_UpdateDisplayedTool_Update;
            else
                buttonInstallUpdate.Text = Resources.ToolStoreDlg_UpdateDisplayedTool_Install;
        }

        private static string FormatVersionText(ToolStoreItem tool)
        {
            if (!tool.Installed && !tool.IsMostRecentVersion)
            {
                return string.Format(Resources.ToolStoreDlg_FormatVersionText_Not_currently_installed__Version___0__is_available, tool.Version);
            } 
            else if (!tool.IsMostRecentVersion)
            {
                return
                    string.Format(
                        Resources.ToolStoreDlg_FormatVersionText_Version__0__currently_installed__Version__1__is_available_,
                        ToolStoreUtil.GetCurrentVersion(tool.Identifier), tool.Version);
            }
            else
            {
                return string.Format(Resources.ToolStoreDlg_FormatVersionText_Currently_installed_and_fully_updated__Version___0___,
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
            DownloadSelectedTool();
        }

        public string DownloadPath { get; private set; }

        public void DownloadTool(int index)
        {
            listBoxTools.SelectedIndex = index;
            DownloadSelectedTool();
        }

        public void DownloadSelectedTool()
        {
            try
            {
                string identifier = _tools[listBoxTools.SelectedIndex].Identifier;
                using (var dlg = new LongWaitDlg {ProgressValue = 0, Message = string.Format(Resources.ToolStoreDlg_DownloadSelectedTool_Downloading__0_, _tools[listBoxTools.SelectedIndex].Name)})
                {
                    dlg.PerformWork(this, 500, progressMonitor => DownloadPath = _toolStoreClient.GetToolZipFile(progressMonitor, identifier, Path.GetTempPath()));
                    if (!dlg.IsCanceled)
                        DialogResult = DialogResult.OK;
                }
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is ToolExecutionException || ex.InnerException is WebException)
                    MessageDlg.ShowException(this, ex);
                else
                {
                    throw;
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

        #endregion
    }

    public interface IToolStoreClient
    {
        /// <summary>
        /// Returns a list of ToolStoreItems which are used to populate the ToolStoreDlg.
        /// </summary>
        IList<ToolStoreItem> GetToolStoreItems();

        /// <summary>
        /// Downloads the tool zip file associated with the given packageIdentifer. In particular,
        /// the function is required to support the ILongWaitBroker by making the download process asynchronous, and
        /// listening for the waitBroker's cancellation. If the waitBroker is cancelled, this method must return
        /// promptly.
        /// </summary>
        /// <returns>The path to the downloaded zip tool, contained in the specified directory.</returns>
        string GetToolZipFile(ILongWaitBroker waitBroker, string packageIdentifier, string directory);
        
        /// <summary>
        /// Returns true if the given package (identified by its version), has an update available. The
        /// version is the string representation of the version installed
        /// </summary>
        bool IsToolUpdateAvailable(string identifier, Version version);
    }

    public class TestToolStoreClient : IToolStoreClient
    {
        private readonly DirectoryInfo _toolDir;
        
        public TestToolStoreClient(string toolDirPath)
        {
            _toolDir = new DirectoryInfo(toolDirPath);
        }

        private IList<ToolStoreItem> ToolStoreItems { get; set; }
        private static readonly string[] IMAGE_EXTENSIONS = {@".jpg", @".png", @".bmp"};

        public IList<ToolStoreItem> GetToolStoreItems()
        {
            if (FailToConnect)
                throw new ToolExecutionException(FailToConnectMessage);
            
            if (ToolStoreItems != null)
                return ToolStoreItems;
            
            var tools = new List<ToolStoreItem>();
            if (_toolDir == null)
                return tools;
            foreach (var toolDir in _toolDir.GetFiles())
            {                
                if (toolDir == null || string.IsNullOrEmpty(toolDir.DirectoryName))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(toolDir.Name);
                string path = Path.Combine(toolDir.DirectoryName, fileName);
                using (new TemporaryDirectory(path))
                {
                    using (var zipFile = new ZipFile(toolDir.FullName))
                    {
                        // extract files
                        zipFile.ExtractAll(path, ExtractExistingFileAction.OverwriteSilently);
                        
                        var toolInf = new DirectoryInfo(Path.Combine(path, ToolInstaller.TOOL_INF));
                        if (!Directory.Exists(toolInf.FullName))
                            continue;

                        if (!toolInf.GetFiles(ToolInstaller.INFO_PROPERTIES).Any())
                            continue;

                        var pictures = toolInf.GetFiles().Where(info => IMAGE_EXTENSIONS.Contains(info.Extension)).ToList();
                        Image toolImage = ToolStoreUtil.DefaultImage;
                        if (pictures.Count != 0)
                        {
                            // try and read in the image
                            try
                            {
                                using (var stream = new FileStream(pictures.First().FullName, FileMode.Open, FileAccess.Read))
                                {
                                    toolImage = Image.FromStream(stream);
                                }
                            }
// ReSharper disable EmptyGeneralCatchClause
                            catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
                            {
                                // if anything goes wrong -- just use the default image :)
                            }
                        }

                        ExternalToolProperties readin;
                        try
                        {
                            readin = new ExternalToolProperties(Path.Combine(toolInf.FullName, ToolInstaller.INFO_PROPERTIES));
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        tools.Add(new ToolStoreItem(readin.Name, readin.Author, readin.Provider, readin.Version,
                                                    readin.Description, readin.Identifier, toolImage, toolDir.FullName));
                    }
                } 
            }
            // sort toollist by name
            tools.Sort(((item, storeItem) => String.Compare(item.Name, storeItem.Name, StringComparison.Ordinal)));
            return tools;
        }

        public string GetToolZipFile(ILongWaitBroker waitBroker, string packageIdentifier, string directory)
        {
            if (FailDownload)
                throw new ToolExecutionException(Resources.TestToolStoreClient_GetToolZipFile_Error_downloading_tool);

            if (TestDownloadPath != null)
                return TestDownloadPath;

            foreach (var item in ToolStoreItems ?? GetToolStoreItems())
            {
                if (item.Identifier.Equals(packageIdentifier))
                {
                    string fileName = Path.GetFileName(item.FilePath);
                    if (fileName == null)
                        throw new ToolExecutionException(Resources.TestToolStoreClient_GetToolZipFile_Error_downloading_tool);
                    
                    string path = Path.Combine(directory, fileName);
                    File.Copy(item.FilePath, path, true);
                    return path;
                }
            }

            throw new ToolExecutionException(Resources.TestToolStoreClient_GetToolZipFile_Cannot_find_a_file_with_that_identifier_);
        }

        public bool IsToolUpdateAvailable(string identifier, Version version)
        {
            if (ToolStoreItems == null)
                ToolStoreItems = GetToolStoreItems();

            var tool = ToolStoreItems.FirstOrDefault(item => item.Identifier.Equals(identifier));
            return tool != null && version < new Version(tool.Version);
        }

        public bool FailToConnect { get; set; }
        public string FailToConnectMessage { get; set; }
        public bool FailDownload { get; set; }
        public string TestDownloadPath { get; set; }
    }

    public class WebToolStoreClient : IToolStoreClient
    {
        public static readonly Uri TOOL_STORE_URI = new Uri(@"https://skyline.gs.washington.edu");
        protected const string GET_TOOLS_URL = "/labkey/skyts/home/getToolsApi.view";
        protected const string DOWNLOAD_TOOL_URL = "/labkey/skyts/home/downloadTool.view";
        public const string TOOL_DETAILS_URL = "/labkey/skyts/home/details.view";

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

        public string GetToolZipFile(ILongWaitBroker waitBroker, string packageIdentifier, string directory)
        {
            var webClient = new WebClient();
            var uri = new UriBuilder(TOOL_STORE_URI)
            {
                Path = DOWNLOAD_TOOL_URL,
                Query = @"lsid=" + Uri.EscapeDataString(packageIdentifier)
            };
            byte[] toolZip = webClient.DownloadData(uri.Uri.AbsoluteUri);
            string contentDisposition = webClient.ResponseHeaders.Get(@"Content-Disposition");
            // contentDisposition is filename="ToolBasename.zip"
            // ReSharper disable LocalizableElement
            Match match = Regex.Match(contentDisposition, "^filename=\"(.+)\"$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            // ReSharper restore LocalizableElement
            string downloadedFile = directory + match.Groups[1].Value;
            File.WriteAllBytes(downloadedFile, toolZip);
            return downloadedFile;
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
            WebClient webClient = new WebClient();
            return webClient.DownloadString(TOOL_STORE_URI + GET_TOOLS_URL);
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
                var webClient = new WebClient();
                webClient.DownloadDataCompleted += DownloadIconDone;
                webClient.DownloadDataAsync(iconUri);
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

        protected void DownloadIconDone(object sender, DownloadDataCompletedEventArgs downloadDataCompletedEventArgs)
        {
            try
            {
                if (downloadDataCompletedEventArgs.Error != null || downloadDataCompletedEventArgs.Cancelled)
                {
                    return;
                }
                using (MemoryStream ms = new MemoryStream(downloadDataCompletedEventArgs.Result))
                {
                    ToolImage = Image.FromStream(ms);
                }
                IconDownloading = false;
                if (IconLoadComplete != null)
                    IconLoadComplete();
            }
            catch (Exception exception)
            {
                Program.ReportException(exception);
            }
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
