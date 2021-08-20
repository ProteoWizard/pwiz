using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharedBatch;

namespace SkylineBatch
{
    public partial class EditRemoteFileSourcesForm : Form
    {

        private SkylineBatchConfigManagerState _initialState;
        private IMainUiControl _mainControl;

        public EditRemoteFileSourcesForm(IMainUiControl mainControl, SkylineBatchConfigManagerState state)
        {
            InitializeComponent();
            Icon = Program.Icon();

            State = state;
            _initialState = state.Copy();
            _mainControl = mainControl;
            LastEditedName = null;
            UpdateRemoteFilesList();
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

        }

        private void btnEdit_Click(object sender, EventArgs e)
        {

        }

        private void listSources_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }
    }
}
