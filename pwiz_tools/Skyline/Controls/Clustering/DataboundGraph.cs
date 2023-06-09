/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
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

        public virtual bool IsComplete
        {
            get
            {
                return !_refreshDataPending && !_updateSelectionPending;
            }
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
            string title = MakeTitle(baseTitle, _dataGridId, DataboundGridControl?.GetViewName());

            Text = TabText = title;
        }

        public static string MakeTitle(string baseTitle, DataGridId dataGridId, ViewName? viewName)
        {
            string title = baseTitle;
            if (dataGridId != null)
            {
                title = title + @":" + dataGridId;
            }

            if (viewName.HasValue && !Equals(viewName.Value.GroupId, ViewGroup.BUILT_IN.Id))
            {
                title = title + @"(" + viewName.Value.Name + @")";
            }

            return title;
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
            // ReSharper disable once SuspiciousTypeConversion.Global
            var ownerForm = formGroup.SiblingForms.OfType<IDataboundGridForm>()
                .FirstOrDefault(form => Equals(_dataGridId, form.DataGridId));
            if (ownerForm == null)
            {
                if (GraphControl != null)
                {
                    GraphControl.GraphPane.Title.Text = Resources.DataboundGraph_AttachToOwner_Disconnected;
                }
                Close();
                return;
            }

            if (GraphControl != null)
            {
                GraphControl.GraphPane.Title.Text = Resources.DataboundGraph_AttachToOwner_Waiting_for_data;
            }

            OwnerGridForm = ownerForm;
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
