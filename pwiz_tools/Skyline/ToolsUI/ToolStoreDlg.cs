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
using System.Reflection;
using System.Windows.Forms;
using Ionic.Zip;
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
            listBoxTools.DisplayMember = "Name"; // Not L10N
        }

        private void listBoxTools_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateDisplayedTool();
        }

        private void UpdateDisplayedTool()
        {
            var toolStoreItem = _tools[listBoxTools.SelectedIndex];
            pictureBoxTool.Image = toolStoreItem.ToolImage;
            textBoxAuthors.Text = toolStoreItem.Authors;

            linkLabelProvider.Text = toolStoreItem.Provider;
            linkLabelProvider.LinkArea = new LinkArea(0, linkLabelProvider.Text.Length);
            linkLabelProvider.Links.Clear();
            linkLabelProvider.Links.Add(0, linkLabelProvider.Text.Length, linkLabelProvider.Text);
            
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
            description = description.Trim();
            
            if (description.StartsWith("\"") && description.EndsWith("\"")) // Not L10N
                description = description.Substring(1, description.Length - 2);

            return description;
        }

        private void buttonInstallUpdate_Click(object sender, EventArgs e)
        {
            DownloadSelectedTool();
        }

        public string DownloadPath { get; private set; }

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
                if (ex.InnerException.GetType() == typeof (MessageException))
                    MessageDlg.Show(this, ex.Message);
                else
                {
                    throw;
                }
            }
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
        private static readonly string[] IMAGE_EXTENSIONS = {".jpg", ".png", ".bmp"};    // Not L10N

        public IList<ToolStoreItem> GetToolStoreItems()
        {
            if (FailToConnect)
                throw new MessageException(FailToConnectMessage);
            
            if (ToolStoreItems != null)
                return ToolStoreItems;
            
            var tools = new List<ToolStoreItem>();
            if (_toolDir == null)
                return tools;
            foreach (var toolDir in _toolDir.GetFiles())
            {                
                if (toolDir == null || toolDir.DirectoryName == null)
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
                throw new MessageException(Resources.TestToolStoreClient_GetToolZipFile_Error_downloading_tool);

            if (TestDownloadPath != null)
                return TestDownloadPath;

            foreach (var item in ToolStoreItems ?? GetToolStoreItems())
            {
                if (item.Identifier.Equals(packageIdentifier))
                {
                    string fileName = Path.GetFileName(item.FilePath);
                    if (fileName == null)
                        throw new MessageException(Resources.TestToolStoreClient_GetToolZipFile_Error_downloading_tool);
                    
                    string path = Path.Combine(directory, fileName);
                    File.Copy(item.FilePath, path, true);
                    return path;
                }
            }

            throw new MessageException(Resources.TestToolStoreClient_GetToolZipFile_Cannot_find_a_file_with_that_identifier_);
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
        public IList<ToolStoreItem> GetToolStoreItems()
        {
            throw new NotImplementedException();
        }

        public string GetToolZipFile(ILongWaitBroker waitBroker, string packageIdentifier, string directory)
        {
            throw new NotImplementedException();
        }

        public bool IsToolUpdateAvailable(string identifier, Version version)
        {
            throw new NotImplementedException();
        }
    }

    public class ToolStoreItem
    {
        public string Name { get; private set; }
        public string Authors { get; private set; }
        public string Provider { get; private set; }
        public string Version { get; private set; }
        public string Description { get; private set; }
        public string Identifier { get; private set; }
        public Image ToolImage { get; private set; }
        public bool Installed { get; private set; }
        public bool IsMostRecentVersion { get; private set; }
        public string FilePath { get; private set; }
        
        public ToolStoreItem(ToolStoreItem item)
        {
            Name = item.Name;
            Authors = item.Authors;
            Provider = item.Provider;
            Version = item.Version;
            Description = item.Description;
            Identifier = item.Identifier;
            ToolImage = item.ToolImage;
            Installed = item.Installed;
            IsMostRecentVersion = item.IsMostRecentVersion;
            FilePath = item.FilePath;
        }
        
        public ToolStoreItem(string name, string authors, string providers, string version, string description, string identifier, Image toolImage, string filePath = null)
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
    }

    public static class ToolStoreUtil
    {
        public static Image DefaultImage
        {
            get { return Resources.ExternalTool; }
        }

        // for testing, return a TestToolStoreClient with the desired attributes and a path to a local directory with which to populate the store, updates etc.
        public static IToolStoreClient CreateClient()
        {
//            return new WebToolStoreClient();
//            return new TestToolStoreClient(@"C:\proj\pwiz\pwiz_tools\Skyline\Executables\Tools\ToolStore");
            return null;
        }

        /// <returns>True if the a tool with the given identifier is installed.</returns>
        public static bool IsInstalled(string identifier)
        {
            return Settings.Default.ToolList.Contains(
                description =>
                    description.PackageIdentifier != null &&
                    description.PackageIdentifier.Equals(identifier));
        }

        /// <returns>True if the a tool with the given identifier is the most recent.</returns>
        public static bool IsMostRecentVersion(string identifier, string version)
        {
            return Settings.Default.ToolList.Contains(
                    description =>
                        !string.IsNullOrEmpty(description.PackageIdentifier) &&
                        description.PackageIdentifier.Equals(identifier) &&
                        !string.IsNullOrEmpty(description.PackageVersion) &&
                        new Version(description.PackageVersion) >= new Version(version));
        }

        /// <returns>The string representation of the version of the installed tool with the given identifier. If the tool
        /// is not installed, returns null.</returns>
        public static string GetCurrentVersion(string identifier)
        {
            var tool =
                Settings.Default.ToolList.FirstOrDefault(description => description.PackageIdentifier.Equals(identifier));
            return tool != null ? tool.PackageVersion : null;
        }

        /// <summary>
        /// Checks the web to see if there are updates available to any currently installed tools. If there are updates,
        /// sets the ToolDescription's UpdateAvailable bool to true.
        /// </summary>
        public static void CheckForUpdates(IList<ToolDescription> tools)
        {
            try
            {
                var client = CreateClient();
                if (client == null)
                    return;
                foreach (ToolDescription toolDescription in tools.Where(description => !string.IsNullOrWhiteSpace(description.PackageVersion)))
                {
                    toolDescription.UpdateAvailable = client.IsToolUpdateAvailable(toolDescription.PackageIdentifier,
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
