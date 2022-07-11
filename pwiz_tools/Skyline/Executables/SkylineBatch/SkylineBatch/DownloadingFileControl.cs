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
        private string _initialFile;
        
        private readonly bool _isDataServer;
        private readonly string _variableDescription;
        private readonly string _filter;
        private EventHandler _addedPathChangedHandler;
        private IMainUiControl _mainControl;

        private Action<SkylineBatchConfigManagerState> _setMainState;
        private Func<SkylineBatchConfigManagerState> _getMainState;

        public DownloadingFileControl(string label, string variableDescription, string initialPath, string filter, Server server, bool isDataServer, string toolTip, IMainUiControl mainControl, 
            Action<SkylineBatchConfigManagerState> setMainState, Func<SkylineBatchConfigManagerState> getMainState)
        {
            InitializeComponent();

            _initialFile = !string.IsNullOrEmpty(initialPath) && server != null ? System.IO.Path.GetFileName(initialPath) : string.Empty;
            Path = initialPath ?? string.Empty;
            Server = server;
            _isDataServer = isDataServer;
            _variableDescription = variableDescription;
            _filter = filter;
            _mainControl = mainControl;
            _setMainState = setMainState;
            _getMainState = getMainState;

            labelPath.Text = string.Format(Resources.DownloadingFileControl_DownloadingFileControl__0__, label);
            ToggleDownload(Server != null);
            textPath.Text = Path;
            textPath.TextChanged += textPath_TextChanged;
            textPath.TextChanged += updatePathVariable;

            btnDownload.ToolTipText = toolTip;
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
            var initialDirectory = FileUtil.GetInitialDirectory(textPath.Text, _lastEnteredPath);

            // Show open folder dialog if this is a data folder or if a file will be downloaded
            if (Server != null || _isDataServer)
            {
                textPath.TextChanged -= textPath_TextChanged;

                var newPath = UiFileUtil.OpenFolder(initialDirectory);
                if (newPath != null)
                {
                    textPath.Text = Server != null && !_isDataServer
                        ? System.IO.Path.Combine(newPath, ((PanoramaFile) Server).FileName)
                        : newPath;
                    _lastEnteredPath = newPath;
                }
                textPath.TextChanged += textPath_TextChanged;
                return;
            }

            // otherwise show the open file dialog
            var file = UiFileUtil.OpenFile(initialDirectory, _filter, false);
            if (file != null)
            {
                textPath.Text = file;
                _lastEnteredPath = file;
            }
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
            // data server paths do not have file names and can always be changed
            if (_isDataServer || Server == null)
                return;

            bool fileNameChanged;
            try
            {
                var serverPath = ServerPath();
                if (string.IsNullOrEmpty(serverPath))
                {
                    fileNameChanged = false;
                    Server = ((PanoramaFile) Server).ReplaceFolder(textPath.Text);
                    textPath.Text += "\\" + ((PanoramaFile)Server).FileName;
                }
                else
                {
                    var fileName = System.IO.Path.GetFileName(ServerPath());
                    var changedServerFileName = !textPath.Text.EndsWith("\\" + fileName);
                    var changedInitialDownloadingName = string.IsNullOrEmpty(_initialFile) ||
                                                        !textPath.Text.EndsWith("\\" + _initialFile);
                    fileNameChanged = changedServerFileName && changedInitialDownloadingName;
                }
            }
            catch (Exception)
            {
                // assume name was changed if path could not be compared
                fileNameChanged = true;
            }

            if (fileNameChanged)
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
                Path = textPath.Text;
                if (Server != null)
                {
                    if (_isDataServer)
                    {
                        Server = new DataServerInfo(Server.FileSource, Server.RelativePath, ((DataServerInfo) Server).DataNamingPattern, textPath.Text);
                    }
                    else
                    {
                        Server = ((PanoramaFile) Server).ReplaceFolder(System.IO.Path.GetDirectoryName(textPath.Text));
                    }
                }
            }
            _addedPathChangedHandler?.Invoke(sender, e);
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            if (_isDataServer)
            {
                var addServerForm = new DataServerForm((DataServerInfo)Server, textPath.Text, _getMainState(), _mainControl);
                if (DialogResult.OK == addServerForm.ShowDialog(this))
                {
                    _setMainState(addServerForm.State);
                    Server = addServerForm.Server;
                    ToggleDownload(Server != null);
                }
            }
            else
            {
                var addPanoramaTemplate = new RemoteFileForm(Server, textPath.Text, string.Format("Download {0} From Panorama", _variableDescription), _mainControl, _getMainState());
                if (DialogResult.OK == addPanoramaTemplate.ShowDialog(this))
                {
                    _setMainState(addPanoramaTemplate.State);
                    Server = addPanoramaTemplate.PanoramaServer;
                    ToggleDownload(Server != null);
                }
            }
            
        }
    }
}
