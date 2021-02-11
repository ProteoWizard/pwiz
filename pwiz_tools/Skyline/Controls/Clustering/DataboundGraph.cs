using System;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.Clustering
{
    public class DataboundGraph : DockableFormEx
    {
        private BindingListSource _bindingListSource;
        private DataboundGridControl _databoundGridControl;
        private SkylineWindow _skylineWindow;
        private bool _updateSelectionPending;

        public string OwnerPersistedString
        {
            get; set;
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

                _bindingListSource = value;
            }
        }

        public DataboundGridControl DataboundGridControl
        {
            get
            {
                return _databoundGridControl;
            }
            set
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
                    OwnerPersistedString =
                        (DataboundGridControl.FindForm() as IDataboundGridForm)?.GetPersistentString();
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
                    SkylineWindow.ComboResults.SelectedIndexChanged -= ComboResults_OnSelectedIndexChanged;
                    SkylineWindow.SequenceTree.AfterSelect -= SequenceTree_OnAfterSelect;
                }

                _skylineWindow = value;
                if (SkylineWindow != null)
                {
                    SkylineWindow.ComboResults.SelectedIndexChanged += ComboResults_OnSelectedIndexChanged;
                    SkylineWindow.SequenceTree.AfterSelect += SequenceTree_OnAfterSelect;
                }
            }
        }

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

        public virtual bool RefreshData()
        {
            return true;
        }

        protected override string GetPersistentString()
        {
            return GetType() + @"|" + OwnerPersistedString;
        }

        protected override void OnShown(EventArgs e)
        {
            if (DataboundGridControl == null && !string.IsNullOrEmpty(OwnerPersistedString))
            {
                BeginInvoke(new Action(AttachToOwner));
            }
        }

        protected void AttachToOwner()
        {
            var formGroup = new FormGroup(this);
            var ownerForm = formGroup.SiblingForms.OfType<IDataboundGridForm>()
                .FirstOrDefault(form => form.GetPersistentString() == OwnerPersistedString);
            if (ownerForm == null)
            {
                Close();
                return;
            }

            DataboundGridControl = ownerForm.GetDataboundGridControl();
        }

        public static DataboundGraph RestoreDataboundGraph(SkylineWindow skylineWindow, string persistentString)
        {
            DataboundGraph databoundGraph = null;
            int ichPipe = persistentString.IndexOf('|');
            if (ichPipe < 0)
            {
                return null;
            }

            string className = persistentString.Substring(ichPipe);
            if (className == typeof(HierarchicalClusterGraph).ToString())
            {
                databoundGraph = new HierarchicalClusterGraph();
            }
            else if (className == typeof(PcaPlot).ToString())
            {
                databoundGraph = new HierarchicalClusterGraph();
            }
            else
            {
                return null;
            }
            databoundGraph.SkylineWindow = skylineWindow;
            databoundGraph.OwnerPersistedString = persistentString.Substring(ichPipe + 1);
            return databoundGraph;
        }
    }
}
