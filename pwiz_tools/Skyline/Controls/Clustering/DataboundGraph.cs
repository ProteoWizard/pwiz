using System;
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

        protected override string GetPersistentString()
        {
            string persistentString = base.GetPersistentString();
            var ownerForm = DataboundGridControl.FindForm() as DataboundGridForm;
            if (ownerForm != null)
            {
                persistentString += @"|" + Uri.EscapeDataString(ownerForm.GetPersistentString());
            }

            return persistentString;
        }
    }
}
