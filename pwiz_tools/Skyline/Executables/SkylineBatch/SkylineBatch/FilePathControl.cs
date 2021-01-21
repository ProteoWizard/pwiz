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
        private readonly string _type;

        private readonly Validator _pathValidator;

        //public delegate void Validator(string variable);

        public FilePathControl(string variableName, string invalidPath, string lastInputPath, bool folder, Validator pathValidator)
        {
            _path = invalidPath;
            InitializeComponent();

            _lastUsedPath = lastInputPath ?? invalidPath;
            _pathValidator = pathValidator;
            _folder = folder;
            if (!folder && invalidPath.Contains("."))
            {
                var suffix = invalidPath.Substring(invalidPath.LastIndexOf(".", StringComparison.Ordinal));
                _type = $"{suffix.Substring(1).ToUpper()}|*{suffix}|All files|*.*";
            }
            label1.Text = string.Format(Resources.FilePathControl_Could_not_find_path_to_the__0___, variableName);
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
            while (!Directory.Exists(initialDirectory) || initialDirectory == "")
                initialDirectory = Path.GetDirectoryName(initialDirectory);
            
            if (_folder)
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog
                {
                    Description = Resources.FilePathControl_Select_Folder,
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
            if (!string.IsNullOrEmpty(_type))
                openDialog.Filter = _type;
            openDialog.Title = Resources.FilePathControl_Open_File;
            openDialog.InitialDirectory = initialDirectory;
            if (openDialog.ShowDialog() == DialogResult.OK)
                textFilePath.Text = openDialog.FileName;
        }

        private void textFilePath_TextChanged(object sender, EventArgs e)
        {
            _path = textFilePath.Text;
        }
    }
}
