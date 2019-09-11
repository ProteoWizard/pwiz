using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ionic.Zip;
using KeepResxW.Properties;

namespace KeepResxW
{
    public partial class KeepResxForm : Form
    {
        /// <summary>
        /// Project relative paths to exclude from search for RESX files
        /// </summary>
        private static readonly string[] Excludes =
        {
            // ReSharper disable StringLiteralTypo
            @"msconvertgui\*",
            @"seems\*",
            @"topograph\*",
            @"bumbershoot\*",
            @"shared\zedgraph\*",
            @"shared\proteomedb\forms\proteomedbform.resx",
            @"skyline\executables\autoqc\*",
            @"skyline\executables\keepresxw\*",
            @"skyline\executables\localizationhelper\*",
            @"skyline\executables\multiload\*",
            @"skyline\executables\skylinepeptidecolorgenerator\*",
            @"skyline\executables\skylinerunner\*",
            @"skyline\executables\tools\exampleargcollector\*",
            @"skyline\executables\tools\exampleinteractivetool\*",
            @"skyline\executables\tools\TFExport\TFExportTool\TFExportTool\Properties\Resources.resx",
            @"skyline\executables\tools\XLTCalc\c#\SkylineIntegration\Properties\Resources.resx",
            @"skyline\executables\keepresxw\*",
            @"skyline\controls\startup\tutoriallinkresources.resx",
            @"skyline\skylinenightly\*",
            @"skyline\skylinetester\*",
            @"skyline\testutil\*"
            // ReSharper restore StringLiteralTypo
        };

        /// <summary>
        /// Text that has shown up in translated RESX files and its replacement to match
        /// the committed project files.
        /// </summary>
        private static readonly string[][] FindReplace =
        {
            new[] {"<?xml version='1.0' encoding='UTF-8'?>", "<?xml version=\"1.0\" encoding=\"utf-8\"?>" },
            new[] {"<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "<?xml version=\"1.0\" encoding=\"utf-8\"?>" },
            new[] {"<xsd:schema xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\" id=\"root\">",
                "<xsd:schema id=\"root\" xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">"},
            new[] {"\"/>", "\" />"},
            new[] {"<data name=\">>", "<data name=\"&gt;&gt;"},
            new[] {"<value/>", "<value />"}
        };

        /// <summary>
        /// Language names and the extensions they use (e.g. .zh-CHS.resx)
        /// </summary>
        private readonly Dictionary<string, string> _dictLangToExt = new Dictionary<string, string>()
        {
            { "English", string.Empty },
            { "Chinese", "zh-CHS" },
            { "Japanese", "ja" }
        };

        public KeepResxForm()
        {
            InitializeComponent();

            receivingProgress.Maximum = 4;

            // Hide the tabs
            tabControl1.SizeMode = TabSizeMode.Fixed;
            tabControl1.ItemSize = new Size(0, 1);

            // Restore values saved in settings
            selectedPathBox.Text = Settings.Default.ProjectPath;
            versionBox.Text = Settings.Default.ProjectVersion;
            receivingProjBox.Text = Settings.Default.ReceivingProject;
            zipLocation.Text = Settings.Default.ReceivingZip;
            if (Settings.Default.ReceivingChinese)
            {
                expectedZH.Checked = true;
            }
            else if (Settings.Default.ReceivingJapanese)
            {
                expectedJa.Checked = true;
            }
        }

        private void ClearProgress()
        {
            receivingProgress.Visible = false;
            receivingProgress.Value = 0;
            finishedReceiving.Visible = false;
            filesChangedLabel.Visible = false;
            fileChangesView.Visible = false;
            fileChangesView.Items.Clear();
        }

        private void KeepResxForm_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < languageCheckList.Items.Count; i++)
            {
                languageCheckList.SetItemChecked(i, true);
            }

            sendingButton.Checked = true;
            receivingProgress.Visible = false;
            fileChangesView.Columns.Add("Old Name");
            fileChangesView.Columns.Add("New Name");
        }

        private void sendingButton_CheckedChanged(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = sendingButton.Checked ? sendingPage : receivingPage;
        }

        private void browse_Click(object sender, EventArgs e)
        {
            using (var inputBrowserDialog = new FolderBrowserDialog())
            {
                inputBrowserDialog.SelectedPath = selectedPathBox.Text;

                if (inputBrowserDialog.ShowDialog(this) == DialogResult.OK)
                {
                    selectedPathBox.Text = inputBrowserDialog.SelectedPath;
                }
            }
        }

        private void receivingZipBrowse_Click(object sender, EventArgs e)
        {
            using (var browser = new OpenFileDialog())
            {
                if (browser.ShowDialog(this) == DialogResult.OK)
                {
                    zipLocation.Text = browser.FileName;
                    if (zipLocation.Text.ToLower().Contains("chs") || zipLocation.Text.ToLower().Contains("zh"))
                    {
                        expectedZH.Checked = true;
                    }
                    else if (zipLocation.Text.ToLower().Contains("ja"))
                    {
                        expectedJa.Checked = true;
                    }
                }
            }
        }

        private void receivingProjBrowse_Click(object sender, EventArgs e)
        {
            using (var browser = new FolderBrowserDialog())
            {
                if (browser.ShowDialog(this) == DialogResult.OK)
                {
                    receivingProjBox.Text = browser.SelectedPath;
                }
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void confirmButton_Click(object sender, EventArgs e)
        {
            if (sendingButton.Checked)
            {
                DoSend();
            }
            else if (receivingButton.Checked)
            {
                DoReceive();
            }
        }

        private void DoSend()
        {
            doneLabel.Text = string.Empty;

            SaveSendSettings();

            string project = selectedPathBox.Text;
            string version = versionBox.Text;

            foreach (var item in languageCheckList.CheckedItems)
            {
                while (true)
                {
                    try
                    {
                        SendResX(project, version, _dictLangToExt[item.ToString()]);
                        break;
                    }
                    catch (Exception)
                    {
                        string zipName = CreateZipPath(project + "\\pwiz_tools", version, _dictLangToExt[item.ToString()]);
                        string errorMsg = "The zipped file for " + item + " already exists at:\n";
                        errorMsg += zipName + "\n\n";
                        errorMsg += "Would you like to overwrite the existing file?";

                        var res = MessageBox.Show(errorMsg, "Warning", MessageBoxButtons.YesNoCancel);
                        if (res == DialogResult.Yes)
                        {
                            File.Delete(zipName);
                        }
                        else if (res == DialogResult.No)
                        {
                            break;
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            }

            doneLabel.Text = "Finished zipping.";
        }

        private void SaveSendSettings()
        {
            try
            {
                Settings.Default.ProjectPath = selectedPathBox.Text;
                Settings.Default.ProjectVersion = versionBox.Text;
                Settings.Default.Save();
            }
            catch (Exception)
            {
                // Do our best to save, but ignore any errors
            }
        }

        private void DoReceive()
        {
            ClearProgress();

            receivingProgress.Visible = true;

            SaveReceiveSettings();

            var lang = expectedJa.Checked ? _dictLangToExt["Japanese"] : _dictLangToExt["Chinese"];
            if (ReceiveResX(zipLocation.Text, receivingProjBox.Text, lang))
            {
                var noChanges = "The file names were not modified.";
                var changes = "The following changes were made to your files:";
                string fileChangesText = fileChangesView.Items.Count == 0 ? noChanges : changes;
                filesChangedLabel.Text = fileChangesText;
                finishedReceiving.Visible = true;
                filesChangedLabel.Visible = true;
                if (fileChangesText == changes)
                    fileChangesView.Visible = true;
            }
            else
            {
                receivingProgress.Visible = false;
            }
        }

        private void SaveReceiveSettings()
        {
            try
            {
                Settings.Default.ReceivingProject = receivingProjBox.Text;
                Settings.Default.ReceivingZip = zipLocation.Text;
                Settings.Default.ReceivingChinese = expectedZH.Checked;
                Settings.Default.ReceivingJapanese = expectedJa.Checked;
                Settings.Default.Save();
            }
            catch (Exception)
            {
                // Ignore errors when saving
            }
        }

        private static void SendResX(string projectPath, string version, string language)
        {
            projectPath += "\\pwiz_tools";
            var dInfo = new DirectoryInfo(projectPath);
            IList<FileInfo> fInfo;
            if (language.Equals(string.Empty))
            {
                fInfo = dInfo
                    .GetFiles("*.resx", SearchOption.AllDirectories)
                    .Where(f => !f.Name.Contains("ja.resx")
                                && !f.Name.Contains("zh-CHS.resx")
                                && !Excludes.Any(exclude => MatchesExclude(f.FullName, exclude.ToLower()))
                                && !f.FullName.Contains("/bin/"))
                    .ToList();
            }
            else
            {
                fInfo = dInfo
                    .GetFiles("*" + GetResxExtension(language), SearchOption.AllDirectories)
                    .Where(f => !Excludes.Any(exclude => MatchesExclude(f.FullName, exclude.ToLower()))
                                && !f.FullName.Contains("/bin/"))
                    .ToList();
            }

            string fullZipName = CreateZipPath(projectPath, version, language);
            using (var archive = new ZipFile(fullZipName))
            {
                foreach (var file in fInfo)
                {
                    string shortenedName = Path.GetDirectoryName(file.FullName)?.Replace(projectPath, string.Empty);
                    archive.AddFile(file.FullName, shortenedName);
                }

                archive.Save();
            }
        }

        private bool ReceiveResX(string zipPath, string projectPath, string language)
        {
            projectPath += "\\pwiz_tools";
            using (var zip = ZipFile.Read(zipPath))
            {
                receivingProgress.Increment(1);

                var ext = GetResxExtension(language);
                int pathRootIndex = GetRootIndex(zip, projectPath);
                var wrongExt = new List<ZipEntry>();
                int fileCount = zip.Count;

                foreach (var entry in zip.Entries.ToArray())
                {
                    // Fix-up entry paths to skip any extra root path information
                    if (pathRootIndex > 0)
                        entry.FileName = string.Join("/", entry.FileName.Split('/').Skip(pathRootIndex));

                    if (!entry.FileName.Contains("."))
                    {
                        fileCount--;
                        continue;
                    }

                    if (!entry.FileName.EndsWith(ext))
                    {
                        if (language == "ja" && entry.FileName.EndsWith(GetResxExtension("zh-CHS")) ||
                            language == "zh-CHS" && entry.FileName.EndsWith(GetResxExtension("ja")))
                        {
                            MessageBox.Show("The language selection does not match the contents of the given zip file.", "Error", MessageBoxButtons.OK);
                            return false;
                        }

                        wrongExt.Add(entry);
                    }
                }
                receivingProgress.Increment(1);

                bool allWrong = false;
                if (wrongExt.Count == fileCount)
                {
                    // Ask to rename everything
                    var renameAllText = "All of the files are incorrectly named, would you like to rename them all?";
                    var renameAllMsg = MessageBox.Show(renameAllText, "Warning", MessageBoxButtons.YesNo);
                    if (renameAllMsg == DialogResult.No)
                    {
                        return false;
                    }
                    allWrong = true;
                }

                foreach (var entry in wrongExt)
                {
                    var extInZip = entry.FileName.Substring(entry.FileName.IndexOf(".", StringComparison.Ordinal));
                    if (extInZip.Equals(GetResxExtension(null)) && !allWrong)
                    {
                        fileChangesView.Items.Add(new ListViewItem(new[] { entry.FileName, string.Empty }));
                        zip.RemoveEntry(entry);
                        continue;
                    }

                    var newName = entry.FileName.Replace(extInZip, ext);
                    var oldName = entry.FileName;
                    entry.FileName = newName;
                    fileChangesView.Items.Add(new ListViewItem(new[] { oldName, newName }));
                }
                fileChangesView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
                fileChangesView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
                receivingProgress.Increment(1);

                zip.ExtractAll(projectPath, ExtractExistingFileAction.OverwriteSilently);

                receivingProgress.Increment(1);

                var sbErrors = new StringBuilder();
                foreach (var entry in zip)
                {
                    if (!entry.IsDirectory)
                    {
                        string fileName = GetPath(projectPath, entry);
                        try
                        {
                            ApplyFixes(fileName);
                        }
                        catch (Exception)
                        {
                            sbErrors.AppendLine(fileName);
                        }
                    }
                }
                receivingProgress.Value = receivingProgress.Maximum;
                if (sbErrors.Length != 0)
                {
                    var errorMsg = "Errors occurred while applying fixes to the following files: \n";
                    MessageBox.Show(errorMsg + sbErrors, "Warning", MessageBoxButtons.OK);
                }
            }
            return true;
        }

        private static string GetPath(string projectPath, ZipEntry entry)
        {
            return projectPath + "\\" + entry.FileName.Replace("/", "\\");
        }

        private int GetRootIndex(ZipFile zip, string projectPath)
        {
            var firstEntry = zip.FirstOrDefault();
            if (firstEntry == null)
                return -1;
            var projectSubdirs = new[] { "Shared", "Skyline" };
            var pathParts = firstEntry.FileName.Split('/');
            for (int i = 0; i < pathParts.Length; i++)
            {
                if (projectSubdirs.Any(s => Equals(s, pathParts[i])))
                    return i;
            }

            return -1;
        }

        private static string CreateZipPath(string project, string version, string language)
        {
            return project + "\\" + "Skyline-" + version.Replace('.', '_') + GetResxExtension(language) + ".zip";
        }

        private static string GetResxExtension(string language)
        {
            string ext = ".resx";
            if (!string.IsNullOrEmpty(language))
                ext = "." + language + ext;
            return ext;
        }

        private static bool MatchesExclude(string fileName, string exclude)
        {
            string fullPathLower = Path.GetFullPath(fileName).ToLower();
            if (exclude.EndsWith("*"))
                return fullPathLower.IndexOf(exclude.Substring(0, exclude.Length - 1), StringComparison.Ordinal) != -1;

            return fullPathLower.EndsWith(exclude);
        }

        private static void ApplyFixes(string fileName)
        {
            FixNewlines(fileName);
            FixUtf8Prefix(fileName);
            FindReplaceText(fileName);
        }

        private static void FixNewlines(string fileName)
        {
            string fileText = File.ReadAllText(fileName);
            fileText = Regex.Replace(fileText, @"\r\n?|\n", "\r\n");
            File.WriteAllText(fileName, fileText);
        }

        private static void FixUtf8Prefix(string fileName)
        {
            // ReadAllText will strip encoding characters at the beginning
            var bytes = File.ReadAllBytes(fileName);
            if (bytes[0] != 0xEF)
                return;
            var listBytes = bytes.ToList();
            listBytes.RemoveRange(0, 3);
            File.WriteAllBytes(fileName, listBytes.ToArray());
        }

        private static void FindReplaceText(string fileName)
        {
            string fileText = File.ReadAllText(fileName);
            foreach (var rep in FindReplace)
            {
                fileText = fileText.Replace(rep[0], rep[1]);
            }
            File.WriteAllText(fileName, fileText);
        }
    }
}
