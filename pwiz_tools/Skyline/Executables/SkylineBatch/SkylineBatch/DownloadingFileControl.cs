using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class DownloadingFileControl : UserControl
    {
        private static string _lastEnteredPath;

        private readonly bool _isDataServer;
        private readonly string _variableDescription;
        private readonly string _filter;
        private EventHandler _addedPathChangedHandler;

        public DownloadingFileControl(string label, string variableDescription, string initialPath, string filter, Server server, bool isDataServer)
        {
            InitializeComponent();

            Path = initialPath ?? string.Empty;
            Server = server;
            _isDataServer = isDataServer;
            _variableDescription = variableDescription;
            _filter = filter;

            labelPath.Text = string.Format(Resources.DownloadingFileControl_DownloadingFileControl__0__, label);
            textPath.Text = Path;
            ToggleDownload(Server != null);
            textPath.TextChanged += textPath_TextChanged;
            textPath.TextChanged += updatePathVariable;

        }

        public string Path { get; private set; }
        public Server Server { get; private set; }

        public void SetPath(string newPath)
        {
            textPath.Text = newPath;
        }

        public void AddPathChangedHandler(EventHandler eventHandler)
        {
            _addedPathChangedHandler = eventHandler;
        }

        private string ServerPath()
        {
            if (Server != null)
            {
                if (_isDataServer)
                    return ((DataServerInfo)Server).Folder;
                return ((PanoramaFile)Server).FilePath;
            }
            return null;
        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            if (Server != null || _isDataServer)
            {
                textPath.TextChanged -= textPath_TextChanged;
                if (OpenFolder(textPath, out string newPath))
                {
                    textPath.Text = Server != null && !_isDataServer
                        ? System.IO.Path.Combine(newPath, ((PanoramaFile) Server).FileName)
                        : newPath;
                }
                textPath.TextChanged += textPath_TextChanged;
                return;
            }
            OpenFile(textPath, _filter);
        }

        private void ToggleDownload(bool downloading)
        {
            Server = downloading ? Server : null;
            btnDownload.BackColor = downloading ? Color.SteelBlue : Color.Transparent;
            btnDownload.Image = downloading ? Resources.downloadSelected : Resources.download;
            if (Server != null)
                textPath.Text = ServerPath();
        }

        private void textPath_TextChanged(object sender, EventArgs e)
        {
            if (Server != null && !textPath.Text.Equals(ServerPath()))
            {
                var newText = textPath.Text;
                textPath.Text = ServerPath();
                if (DialogResult.OK == AlertDlg.ShowOkCancel(this, Program.AppName(),
                    string.Format(Resources.DownloadingFileControl_textPath_TextChanged_Changing_the__0__will_prevent_it_from_being_downloaded_through_Panorama__Are_you_sure_you_want_to_continue_, _variableDescription.ToLower(CultureInfo.CurrentCulture)))
                )
                {
                    ToggleDownload(false);
                    textPath.Text = newText;
                }
            }
        }

        private void updatePathVariable(object sender, EventArgs e)
        {
            if (!Path.Equals(textPath.Text))
            {
                if (Server != null)
                {
                    if (_isDataServer)
                    {
                        Server = new DataServerInfo(Server.URI, Server.Username, Server.Password, Server.Encrypt,
                            ((DataServerInfo) Server).DataNamingPattern, textPath.Text);
                    }
                    else
                    {
                        Server = ((PanoramaFile) Server).ReplacedFolder(System.IO.Path.GetDirectoryName(textPath.Text));
                    }
                }
                Path = textPath.Text;
            }
            _addedPathChangedHandler?.Invoke(sender, e);
        }

        private void OpenFile(Control textBox, string filter, bool save = false)
        {
            FileDialog dialog = save ? (FileDialog)new SaveFileDialog() : new OpenFileDialog();
            var initialDirectory = FileUtil.GetInitialDirectory(textBox.Text, _lastEnteredPath);
            dialog.InitialDirectory = initialDirectory;
            dialog.Filter = filter;
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                textBox.Text = dialog.FileName;
                _lastEnteredPath = dialog.FileName;
            }
        }

        private bool OpenFolder(Control textBox, out string path)
        {
            path = null;
            var dialog = new FolderBrowserDialog();
            var initialPath = FileUtil.GetInitialDirectory(textBox.Text, _lastEnteredPath);
            dialog.SelectedPath = initialPath;
            DialogResult result = dialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                textBox.Text = dialog.SelectedPath;
                path = dialog.SelectedPath;
            }
            return result == DialogResult.OK;
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            if (_isDataServer)
            {
                var addServerForm = new AddServerForm((DataServerInfo)Server, textPath.Text);
                if (DialogResult.OK == addServerForm.ShowDialog(this))
                {
                    Server = addServerForm.Server;
                    ToggleDownload(Server != null);
                }
            }
            else
            {
                var addPanoramaTemplate = new PanoramaFileForm(Server, textPath.Text, string.Format("Download {0} From Panorama", _variableDescription));
                if (DialogResult.OK == addPanoramaTemplate.ShowDialog(this))
                {
                    Server = addPanoramaTemplate.PanoramaServer;
                    ToggleDownload(Server != null);
                }
            }
            
        }
    }
}
