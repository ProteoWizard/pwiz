using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public class DataboundGraph : DockableFormEx
    {
        private BindingListSource _bindingListSource;
        private DataboundGridControl _databoundGridControl;
        private IDataboundGridForm _ownerGridForm;
        private DataGridId _dataGridId;
        private SkylineWindow _skylineWindow;
        private bool _updateSelectionPending;
        private bool _refreshDataPending;

        public IDataboundGridForm OwnerGridForm
        {
            get
            {
                return _ownerGridForm;
            }
            set
            {
                if (ReferenceEquals(OwnerGridForm, value))
                {
                    return;
                }

                _ownerGridForm = value;
                _dataGridId = OwnerGridForm?.DataGridId;
                DataboundGridControl = OwnerGridForm?.GetDataboundGridControl();
            }
        }

        public BindingListSource BindingListSource
        {
            get { return _bindingListSource; }

            set
            {
                if (ReferenceEquals(BindingListSource, value))
                {
                    return;
                }

                if (BindingListSource != null)
                {
                    BindingListSource.AllRowsChanged -= BindingListSource_OnAllRowsChanged;
                    BindingListSource.ListChanged -= BindingListSource_OnListChanged;
                }

                _bindingListSource = value;
                if (BindingListSource != null)
                {
                    BindingListSource.AllRowsChanged += BindingListSource_OnAllRowsChanged;
                    BindingListSource.ListChanged += BindingListSource_OnListChanged;
                }
            }
        }

        private void BindingListSource_OnListChanged(object sender, ListChangedEventArgs e)
        {
            DataChanged();
        }

        private void BindingListSource_OnAllRowsChanged(object sender, EventArgs e)
        {
            DataChanged();
        }

        public virtual PersistentString RestoreFromViewFile(SkylineWindow skylineWindow, PersistentString persistentString)
        {
            SkylineWindow = skylineWindow;
            PersistentString remainingParts;
            _dataGridId = DataGridId.FromPersistentString(persistentString, out remainingParts);
            return remainingParts;
        }

        public DataboundGridControl DataboundGridControl
        {
            get
            {
                return _databoundGridControl;
            }
            private set
            {
                if (ReferenceEquals(DataboundGridControl, value))
                {
                    return;
                }

                if (DataboundGridControl != null)
                {
                    DataboundGridControl.Disposed -= DataboundGridControl_OnDisposed;
                }
                _databoundGridControl = value;
                if (DataboundGridControl != null)
                {
                    DataboundGridControl.Disposed += DataboundGridControl_OnDisposed;
                }

                BindingListSource = DataboundGridControl?.BindingListSource;
            }
        }

        private void DataboundGridControl_OnDisposed(object sender, EventArgs e)
        {
            DataboundGridControl = null;
            Close();
        }

        public SkylineWindow SkylineWindow
        {
            get
            {
                return _skylineWindow;
            }
            set
            {
                if (ReferenceEquals(SkylineWindow, value))
                {
                    return;
                }

                if (SkylineWindow != null)
                {
                    if (SkylineWindow.ComboResults != null)
                    {
                        SkylineWindow.ComboResults.SelectedIndexChanged -= ComboResults_OnSelectedIndexChanged;
                    }

                    if (SkylineWindow.SequenceTree != null)
                    {
                        SkylineWindow.SequenceTree.AfterSelect -= SequenceTree_OnAfterSelect;
                    }
                }

                _skylineWindow = value;
                if (SkylineWindow != null)
                {
                    if (SkylineWindow.ComboResults != null)
                    {
                        SkylineWindow.ComboResults.SelectedIndexChanged += ComboResults_OnSelectedIndexChanged;
                    }
                    if (SkylineWindow.SequenceTree != null)
                    {
                        SkylineWindow.SequenceTree.AfterSelect += SequenceTree_OnAfterSelect;
                    }
                }
            }
        }

        public virtual ZedGraphControl GraphControl => null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SkylineWindow = null;
                DataboundGridControl = null;
            }
            base.Dispose(disposing);
        }

        private void ComboResults_OnSelectedIndexChanged(object sender, EventArgs e)
        {
            QueueUpdateGraph();
        }


        private void SequenceTree_OnAfterSelect(object sender, TreeViewEventArgs e)
        {
            QueueUpdateGraph();
        }

        public void QueueUpdateGraph()
        {
            if (!IsHandleCreated)
                return;

            if (!_updateSelectionPending)
            {
                _updateSelectionPending = true;
                BeginInvoke(new Action(() =>
                {
                    _updateSelectionPending = false;
                    UpdateSelection();
                }));
            }
        }

        public virtual void UpdateSelection()
        {
        }

        public virtual void RefreshData()
        {
        }

        public void QueueRefreshData()
        {
            if (!IsHandleCreated)
                return;
            if (_refreshDataPending)
            {
                return;
            }
            _refreshDataPending = true;
            BeginInvoke(new Action(() =>
            {
                _refreshDataPending = false;
                RefreshData();
            }));
        }

        protected virtual void DataChanged()
        {
            QueueRefreshData();
        }

        protected void UpdateTitle(string baseTitle)
        {
            string title = baseTitle;
            if (_dataGridId != null)
            {
                title = title + @":" + _dataGridId;
            }

            var viewInfo = BindingListSource?.ViewInfo;
            if (viewInfo != null && !Equals(viewInfo.ViewGroup?.Id, ViewGroup.BUILT_IN.Id))
            {
                title = title + @"(" + viewInfo.Name + @")";
            }

            Text = TabText = title;
        }

        protected override string GetPersistentString()
        {
            return PersistentString.FromParts(GetType().ToString())
                .Concat(OwnerGridForm?.DataGridId?.ToPersistedString()).ToString();
        }

        protected override void OnShown(EventArgs e)
        {
            if (DataboundGridControl == null && _dataGridId != null)
            {
                BeginInvoke(new Action(AttachToOwner));
            }
        }

        protected void AttachToOwner()
        {
            var formGroup = new FormGroup(this);
            var ownerForm = formGroup.SiblingForms.OfType<IDataboundGridForm>()
                .FirstOrDefault(form => Equals(_dataGridId, form.DataGridId));
            if (ownerForm == null)
            {
                if (GraphControl != null)
                {
                    GraphControl.GraphPane.Title.Text = "Disconnected";
                }
                Close();
                return;
            }

            if (GraphControl != null)
            {
                GraphControl.GraphPane.Title.Text = "Waiting for data";
            }
            DataboundGridControl = ownerForm.GetDataboundGridControl();
            DataChanged();
        }

        public static DataboundGraph RestoreDataboundGraph(SkylineWindow skylineWindow, string persistentString)
        {
            var parsed = PersistentString.Parse(persistentString);
          
            DataboundGraph databoundGraph;
            string className = parsed.Parts[0];
            if (className == typeof(HeatMapGraph).ToString())
            {
                databoundGraph = new HeatMapGraph();
            }
            else if (className == typeof(PcaPlot).ToString())
            {
                databoundGraph = new PcaPlot();
            }
            else
            {
                return null;
            }

            databoundGraph.RestoreFromViewFile(skylineWindow, parsed.Skip(1));
            return databoundGraph;
        }
    }
}
