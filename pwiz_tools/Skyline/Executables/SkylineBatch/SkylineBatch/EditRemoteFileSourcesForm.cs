using System;
using System.Windows.Forms;
using SharedBatch;

namespace SkylineBatch
{
    public partial class EditRemoteFileSourcesForm : Form
    {

        private SkylineBatchConfigManagerState _initialState;
        private IMainUiControl _mainControl;
        private readonly bool _preferPanoramaSource;

        public EditRemoteFileSourcesForm(IMainUiControl mainControl, SkylineBatchConfigManagerState state, int selectedIndex, bool preferPanoramaSource)
        {
            InitializeComponent();
            Icon = Program.Icon();

            State = state;
            _initialState = state.Copy();
            _mainControl = mainControl;
            _preferPanoramaSource = preferPanoramaSource;
            LastEditedName = null;
            UpdateRemoteFilesList();
            if (selectedIndex >= 0)
                listSources.SelectedIndex = selectedIndex;
        }

        public SkylineBatchConfigManagerState State { get; private set; }
        public string LastEditedName { get; private set; }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            State = _initialState;
            LastEditedName = null;
        }

        private void UpdateRemoteFilesList()
        {
            listSources.Items.Clear();
            foreach (var source in State.FileSources.Keys)
                listSources.Items.Add(source);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var remoteSourceForm = new RemoteSourceForm(null, _mainControl, State, _preferPanoramaSource);
            var dialogResult = remoteSourceForm.ShowDialog(this);
            State = remoteSourceForm.State;
            if (DialogResult.OK == dialogResult)
            {
                LastEditedName = remoteSourceForm.RemoteFileSource.Name;
                var index = listSources.Items.Count;
                listSources.Items.Insert(index, LastEditedName);
                listSources.SelectedIndex = index;
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            var initialName = listSources.SelectedItem;
            var remoteSourceForm = new RemoteSourceForm(State.FileSources[(string)initialName], _mainControl, State, _preferPanoramaSource);
            var dialogResult = remoteSourceForm.ShowDialog(this);
            State = remoteSourceForm.State;
            if (DialogResult.OK == dialogResult)
            {
                LastEditedName = remoteSourceForm.RemoteFileSource.Name;
                var index = listSources.Items.IndexOf(initialName);
                listSources.Items.RemoveAt(index);
                listSources.Items.Insert(index, LastEditedName);
                listSources.SelectedIndex = index;
            }
        }

        private void listSources_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnEdit.Enabled = listSources.SelectedIndex >= 0;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
