using System;
using System.Collections.Immutable;
using System.Threading;
using System.Windows.Forms;
using SharedBatch;
using SkylineBatch.Properties;

namespace SkylineBatch
{
    public partial class RemoteSourceForm : Form
    {
        private readonly ImmutableDictionary<string, RemoteFileSource> _remoteFileSources;
        private CancellationTokenSource _cancelSource;
        private readonly string _editingSourceName;
        private readonly IMainUiControl _mainControl;
        private readonly bool _adding;
        private readonly RemoteFileSource _initialRemoteSource;
        private readonly SkylineBatchConfigManagerState _initialState;
        private readonly bool _preferPanoramaSource;

        public RemoteSourceForm(RemoteFileSource editingRemoteSource, IMainUiControl mainControl, SkylineBatchConfigManagerState state, bool preferPanoramaSource)
        {
            InitializeComponent();
            _remoteFileSources = state.FileSources;
            _editingSourceName = null;
            _mainControl = mainControl;
            State = state;
            _initialState = state.Copy();
            _initialRemoteSource = editingRemoteSource;
            _adding = editingRemoteSource == null;
            _preferPanoramaSource = preferPanoramaSource;

            if (editingRemoteSource != null)
            {
                _editingSourceName = editingRemoteSource.Name;
                textName.Text = editingRemoteSource.Name;
                textFolderUrl.Text = editingRemoteSource.URI.AbsoluteUri;
                textUserName.Text = editingRemoteSource.Username;
                textPassword.Text = editingRemoteSource.Password;
                checkBoxNoEncryption.Checked = !editingRemoteSource.Encrypt;
            }

            Icon = Program.Icon();
        }

        public RemoteFileSource RemoteFileSource { get; private set; }
        public SkylineBatchConfigManagerState State { get; private set; }

        private void textPassword_TextChanged(object sender, EventArgs e)
        {
            checkBoxNoEncryption.Enabled = textPassword.Text.Length > 0;
            if (textPassword.Text.Length == 0)
                checkBoxNoEncryption.Checked = false;
        }

        private void checkBoxNoEncryption_CheckedChanged(object sender, EventArgs e)
        {
            textPassword.PasswordChar = checkBoxNoEncryption.Checked ? '\0' : '*';
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            _cancelSource = new CancellationTokenSource();

            if (_remoteFileSources.ContainsKey(textName.Text) && !textName.Text.Equals(_editingSourceName))
            {
                AlertDlg.ShowError(this, Program.AppName(), string.Format(Resources.RemoteSourceForm_btnSave_Click_Another_remote_file_location_with_the_name__0__already_exists__Please_choose_a_unique_name_, textName.Text));
                return;
            }

            try
            {
                RemoteFileSource = RemoteFileSource.RemoteSourceFromUi(textName.Text, textFolderUrl.Text,
                    textUserName.Text, textPassword.Text, !checkBoxNoEncryption.Checked);
                if (_adding)
                    State.UserAddRemoteFileSource(RemoteFileSource, _preferPanoramaSource, _mainControl);
                else
                {
                    State.ReplaceRemoteFileSource(_initialRemoteSource, RemoteFileSource, _mainControl);
                }
                DialogResult = DialogResult.OK;
            }
            catch (ArgumentException ex)
            {
                AlertDlg.ShowError(this, Program.AppName(), ex.Message);
            }
        }

        private void CancelValidate()
        {
            if (_cancelSource != null)
                _cancelSource.Cancel();
        }

        private void RemoteSourceForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CancelValidate();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            State = _initialState;
        }
    }
}
