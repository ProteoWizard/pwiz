using System;
using System.IO;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class FilePathControl : UserControl, IValidatorControl
    {
        private string _path;
        private string _lastUsedPath;
        private readonly bool _folder;
        private readonly string _filter;

        private readonly Validator _pathValidator;
        

        public FilePathControl(string variableName, string invalidPath, string lastInputPath, bool folder, Validator pathValidator)
        {
            _path = invalidPath;
            InitializeComponent();

            _lastUsedPath = lastInputPath ?? invalidPath;
            _pathValidator = pathValidator;
            _folder = folder;
            if (!folder)
            {
                var suffix = invalidPath.Contains(".") ? 
                    invalidPath.Substring(invalidPath.LastIndexOf(".", StringComparison.Ordinal)) : 
                    string.Empty;
                switch (suffix)
                {
                    case TextUtil.EXT_R:
                        _filter = TextUtil.FILTER_R;
                        break;
                    case TextUtil.EXT_SKY:
                        _filter = TextUtil.FILTER_SKY;
                        break;
                    case TextUtil.EXT_SKYR:
                        _filter = TextUtil.FILTER_SKYR;
                        break;
                    default:
                        _filter = TextUtil.FILTER_ALL;
                        break;
                }
            }
            label1.Text = string.Format(Resources.FilePathControl_FilePathControl_Could_not_find_the__0__, variableName);
            label2.Text = string.Format("Please specify the path to the {0}:", variableName);
            textFilePath.Text = _path;
        }

        public object GetVariable() => _path;

        public bool IsValid(out string errorMessage)
        {
            errorMessage = null;
            try
            {
                _pathValidator(_path);
                return true;
            } catch (ArgumentException e)
            {
                errorMessage = e.Message;
                return false;
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var initialDirectory = _lastUsedPath;
            while (!Directory.Exists(initialDirectory) || initialDirectory == string.Empty)
                initialDirectory = Path.GetDirectoryName(initialDirectory);
            
            if (_folder)
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog
                {
                    SelectedPath = initialDirectory
                })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    textFilePath.Text = dlg.SelectedPath;
                    _lastUsedPath = dlg.SelectedPath;
                }
                return;
            }

            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = _filter;
            openDialog.InitialDirectory = initialDirectory;
            if (openDialog.ShowDialog() == DialogResult.OK)
            {
                textFilePath.Text = openDialog.FileName;
                _lastUsedPath = openDialog.FileName;
            }
        }

        private void textFilePath_TextChanged(object sender, EventArgs e)
        {
            _path = textFilePath.Text;
        }
    }
}
