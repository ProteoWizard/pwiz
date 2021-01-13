using System;
using System.Windows.Forms;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class FilePathControl : UserControl, IVariableInputControl
    {
        private string _path;
        private readonly bool _folder;
        private readonly string _type;

        private readonly Validator _pathValidator;

        //public delegate void Validator(string variable);

        public FilePathControl(string variableName, string invalidPath, bool folder, Validator pathValidator)
        {
            _path = invalidPath;
            InitializeComponent();


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
            if (_folder)
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog
                {
                    Description = Resources.FilePathControl_Select_Folder,
                    ShowNewFolderButton = false,
                    SelectedPath = textFilePath.Text
                })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    textFilePath.Text = dlg.SelectedPath;
                }
                return;
            }


            OpenFileDialog openDialog = new OpenFileDialog();
            if (!string.IsNullOrEmpty(_type))
                openDialog.Filter = _type;
            openDialog.Title = Resources.FilePathControl_Open_File;
            openDialog.InitialDirectory = System.IO.Path.GetDirectoryName(textFilePath.Text);
            if (openDialog.ShowDialog() == DialogResult.OK)
                textFilePath.Text = openDialog.FileName;
        }

        private void textFilePath_TextChanged(object sender, EventArgs e)
        {
            _path = textFilePath.Text;
        }
    }
}
