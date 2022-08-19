
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SharedBatch.Properties;

namespace SharedBatch
{
    public partial class FileOpenedForm : Form
    {

        // Form with two labels and a FilePathControl. Used in AutoQC and SkylineBatch
        // to allow user to choose a new directory when a configuration file is imported
        // from the downloads folder.

        private FilePathControl _filePathControl;
        private IMainUiControl _mainControl;

        public FileOpenedForm(IMainUiControl mainControl, string filePath, Icon icon)
        {
            InitializeComponent();
            
            Icon = icon;
            _mainControl = mainControl;
            _filePathControl = new FilePathControl(Resources.FileOpenedForm_FileOpenedForm_root_folder, FileUtil.GetInitialDirectory(filePath), string.Empty, RootFolderValidator, TextUtil.EXT_BCFG, PathDialogOptions.Folder);
            _filePathControl.label1.Text = string.Format(Resources.FileOpenedForm_FileOpenedForm_Preparing_to_import_configurations_from__0_, Path.GetFileName(filePath));
            _filePathControl.label2.Text = Resources.FileOpenedForm_FileOpenedForm_Please_specify_a_root_folder_for_the_configurations_;
            _filePathControl.Dock = DockStyle.Fill;
            _filePathControl.Show();
            panel1.Controls.Add(_filePathControl);
        }
        
        public string NewRootDirectory { get; private set; }

        private void RootFolderValidator(string path)
        {
            if (!Directory.Exists(path))
            {
                bool valid;
                try
                {
                    valid = Directory.Exists(Path.GetDirectoryName(path));
                    Directory.CreateDirectory(path);
                }
                catch (Exception)
                {
                    valid = false;
                }
                if (!valid)
                    throw new ArgumentException(string.Format(Resources.FileOpenedForm_RootFolderValidator_The_directory__0__does_not_exist_, path) + Environment.NewLine +
                                                Resources.FileOpenedForm_RootFolderValidator_Please_enter_an_existing_directory_);
            }
            FileUtil.ValidateNotInDownloads(path, Resources.FileOpenedForm_RootFolderValidator_root_folder);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (!_filePathControl.IsValid(out string errorMessage))
            {
                _mainControl.DisplayError(errorMessage);
            }
            else
            {
                NewRootDirectory = (string)_filePathControl.GetVariable();
                DialogResult = DialogResult.Yes;
                Close();
            }
        }

        //for testing only
        public void SetPath(string path)
        {
            _filePathControl.SetInput(path);
        }
    }

}
