/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.AuditLog
{
    public partial class AuditLogForm : DocumentGridForm
    {
        private readonly SkylineWindow _skylineWindow;
        //private readonly ToolStripButton _clearLogButton;
        private readonly CheckBox _enableAuditLogging;

        public AuditLogForm(SkylineViewContext viewContext, string defaultViewName)
            : base(viewContext, AuditLogStrings.AuditLogForm_AuditLogForm_Audit_Log)
        {
            InitializeComponent();

            _skylineWindow = viewContext.SkylineDataSchema.SkylineWindow;

            //_clearLogButton = new ToolStripButton(AuditLogStrings.AuditLogForm_AuditLogForm_Clear_log);

            _enableAuditLogging = new CheckBox
            {
                Text = AuditLogStrings.AuditLogForm_AuditLogForm_Enable_audit_logging,
                Checked = _skylineWindow.DocumentUI.Settings.DataSettings.AuditLogging,
                BackColor = Color.Transparent,
                AutoCheck = false
            };

            var checkBoxHost = new ToolStripControlHost(_enableAuditLogging)
            {
                Alignment = ToolStripItemAlignment.Right
            };

            //NavBar.BindingNavigator.Items.Add(_clearLogButton);
            NavBar.BindingNavigator.Items.Add(checkBoxHost);

            if (!string.IsNullOrEmpty(Settings.Default.AuditLogView))
            {
                var viewName = ViewName.Parse(Settings.Default.AuditLogView);
                if (viewName.HasValue)
                    DataboundGridControl.ChooseView(viewName.Value);
            }
            else
            {
                DataboundGridControl.ChooseView(defaultViewName);
            }
        }

        public void EnableAuditLogging(bool enable)
        {
            if (!enable && !_skylineWindow.DocumentUI.AuditLog.AuditLogEntries.IsRoot)
            {
                using (var dlg = new AlertDlg(
                        AuditLogStrings.AuditLogForm_EnableAuditLogging_This_will_clear_the_audit_log_and_delete_it_permanently_once_the_document_gets_saved__Do_you_want_to_proceed_,
                        MessageBoxButtons.YesNo))
                {
                    if (dlg.ShowDialog(this) != DialogResult.Yes)
                    {
                        return;
                    }
                }
            }

            _skylineWindow.ModifyDocument(string.Empty, // Audit logging takes care of this now
                doc => AuditLogList.ToggleAuditLogging(doc, enable),
                AuditLogEntry.SettingsLogFunction);
            _skylineWindow.StoreNewSettings(_skylineWindow.DocumentUI.Settings);
            _enableAuditLogging.Checked = enable;
        }

        private void enableAuditLogging_Click(object sender, EventArgs e)
        {
            EnableAuditLogging(!((CheckBox)sender).Checked);
        }

        private void _clearLogButton_Click(object sender, EventArgs e)
        {
            _skylineWindow.ClearAuditLog();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            _skylineWindow.DocumentUIChangedEvent += _skylineWindow_DocumentUIChangedEvent;
            BindingListSource.ListChanged += BindingListSource_ListChanged;
            //_clearLogButton.Click += _clearLogButton_Click;
            _enableAuditLogging.Click += enableAuditLogging_Click;
            DataGridView.RowPrePaint += DataGridView_RowPrePaint;
        }

        private void _skylineWindow_DocumentUIChangedEvent(object sender, Model.DocumentChangedEventArgs e)
        {
            _enableAuditLogging.Checked = ((SkylineWindow) sender).DocumentUI.Settings.DataSettings.AuditLogging;
        }

        void DataGridView_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            var rowItem = DataGridView.Rows[e.RowIndex].DataBoundItem as RowItem;
            if (rowItem == null)
                return;

            var auditLogRow = rowItem.Value as AuditLogRow;
            if (auditLogRow == null || rowItem.RowKey.Length == 0 || !rowItem.RowKey.Last.Key.Equals(PropertyPath.Root.Property(@"Details").LookupAllItems()))
                return;

            if ((int)rowItem.RowKey.Last.Value == 0)
                DataGridView.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightGray;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            DataGridView.RowPrePaint -= DataGridView_RowPrePaint;
            _enableAuditLogging.CheckedChanged -= enableAuditLogging_Click;
            //_clearLogButton.Click -= _clearLogButton_Click;
            BindingListSource.ListChanged -= BindingListSource_ListChanged;
            _skylineWindow.DocumentUIChangedEvent -= _skylineWindow_DocumentUIChangedEvent;

            base.OnHandleDestroyed(e);
        }

        void BindingListSource_ListChanged(object sender, System.ComponentModel.ListChangedEventArgs e)
        {
            if (ViewInfo != null && ViewInfo.ViewGroup != null)
                Settings.Default.AuditLogView = ViewInfo.ViewGroup.Id.ViewName(ViewInfo.Name).ToString();
        }

        private static ViewInfo CreateAuditLogViewInfo(SkylineDataSchema dataSchema, string name, params string[] columnNames)
        {
            var columnDescriptor = ColumnDescriptor.RootColumn(dataSchema, typeof(AuditLogRow));
            var viewSpec = new ViewSpec().SetName(name).SetRowType(columnDescriptor.PropertyType);
            var columns = columnNames.Select(c => new ColumnSpec(PropertyPath.Parse(c)));

            viewSpec = viewSpec.SetSublistId(PropertyPath.Root.Property(@"Details").LookupAllItems());
            viewSpec = viewSpec.SetColumns(columns);

            return new ViewInfo(columnDescriptor, viewSpec).ChangeViewGroup(ViewGroup.BUILT_IN);
        }

        public static AuditLogForm MakeAuditLogForm(SkylineWindow skylineWindow)
        {
            var dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var viewInfos = new[]
            {
                CreateAuditLogViewInfo(dataSchema, AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo, @"Time", @"UndoRedoMessage", @"Reason"),
                CreateAuditLogViewInfo(dataSchema, AuditLogStrings.AuditLogForm_MakeAuditLogForm_Summary, @"Time", @"SummaryMessage", @"Reason"),
                CreateAuditLogViewInfo(dataSchema, AuditLogStrings.AuditLogForm_MakeAuditLogForm_All_Info, @"Time", @"Details!*.AllInfoMessage", @"Reason")
            };

            var rowSource = new AuditLogRowSource(dataSchema);
            var rowSourceInfo = new RowSourceInfo(typeof(AuditLogRow), rowSource, viewInfos);
            var viewContext = new SkylineViewContext(dataSchema, new[] { rowSourceInfo });

            return new AuditLogForm(viewContext, viewInfos[2].Name);
        }

        private class AuditLogRowSource : SkylineObjectList<object, AuditLogRow>
        {
            public AuditLogRowSource(SkylineDataSchema dataSchema) : base(dataSchema)
            {
            }

            protected override IEnumerable<object> ListKeys()
            {
                throw new InvalidOperationException();
            }

            protected override AuditLogRow ConstructItem(object key)
            {
                throw new InvalidOperationException();
            }

            private static AuditLogRow GetRow(AuditLogEntry entry, SkylineDataSchema skylineDataSchema, int id)
            {
                return new AuditLogRow(skylineDataSchema, entry, id);
            }

            public override IEnumerable GetItems()
            {
                return DataSchema.Document.AuditLog.AuditLogEntries.Enumerate().Select((e, i) => GetRow(e, DataSchema, i + 1));
            }
        }
    }
}
