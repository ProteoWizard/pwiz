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
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
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
        private readonly ToolStripButton _clearLogButton;
        private readonly CheckBox _enableAuditLogging;

        public AuditLogForm(SkylineViewContext viewContext)
            : base(viewContext)
        {
            InitializeComponent();

            _skylineWindow = viewContext.SkylineDataSchema.SkylineWindow;

            _clearLogButton = new ToolStripButton(AuditLogStrings.AuditLogForm_AuditLogForm_Clear_log);

            _enableAuditLogging = new CheckBox
            {
                Text = AuditLogStrings.AuditLogForm_AuditLogForm_Enable_audit_logging,
                Checked = Settings.Default.AuditLogging,
                BackColor = Color.Transparent
            };

            var checkBoxHost = new ToolStripControlHost(_enableAuditLogging)
            {
                Alignment = ToolStripItemAlignment.Right
            };

            NavBar.BindingNavigator.Items.Add(_clearLogButton);
            NavBar.BindingNavigator.Items.Add(checkBoxHost);

            if (!string.IsNullOrEmpty(Settings.Default.AuditLogView))
            {
                var viewName = ViewName.Parse(Settings.Default.AuditLogView);
                if (viewName.HasValue)
                    DataboundGridControl.ChooseView(viewName.Value);
            }
        }


        public static void EnableAuditLogging(bool enable, SkylineWindow window)
        {
            Settings.Default.AuditLogging = enable;

            var entry = AuditLogEntry.MakeLogSettingsChangeEntry(window.Document.FormatVersion,
                DateTime.Now);

            window.ModifyDocumentNoUndo(doc =>
                doc.ChangeAuditLog(ImmutableList<AuditLogEntry>.ValueOf(doc.AuditLog.AuditLogEntries.Concat(new[] {entry}))));
        }

        private void enableAuditLogging_CheckedChanged(object sender, EventArgs e)
        {
            EnableAuditLogging(((CheckBox)sender).Checked, _skylineWindow);
        }

        private void _clearLogButton_Click(object sender, EventArgs e)
        {
            _skylineWindow.ClearAuditLog();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            BindingListSource.ListChanged += BindingListSource_ListChanged;
            _clearLogButton.Click += _clearLogButton_Click;
            _enableAuditLogging.CheckedChanged += enableAuditLogging_CheckedChanged;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _enableAuditLogging.CheckedChanged -= enableAuditLogging_CheckedChanged;
            _clearLogButton.Click -= _clearLogButton_Click;
            BindingListSource.ListChanged -= BindingListSource_ListChanged;

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

            viewSpec = viewSpec.SetSublistId(PropertyPath.Root.Property("Details").LookupAllItems()); // Not L10N
            viewSpec = viewSpec.SetColumns(columns);
           
            return new ViewInfo(columnDescriptor, viewSpec).ChangeViewGroup(ViewGroup.BUILT_IN);
        }

        public static AuditLogForm MakeAuditLogForm(SkylineWindow skylineWindow)
        {
            var dataSchema = new SkylineDataSchema(skylineWindow, SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var viewInfos = new[]
            {
                CreateAuditLogViewInfo(dataSchema, AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo, "TimeStamp", "UndoRedoMessage"), // Not L10N
                CreateAuditLogViewInfo(dataSchema, AuditLogStrings.AuditLogForm_MakeAuditLogForm_Summary, "TimeStamp", "SummaryMessage"), // Not L10N
                CreateAuditLogViewInfo(dataSchema, AuditLogStrings.AuditLogForm_MakeAuditLogForm_All_Info, "TimeStamp", "Details!*.AllInfoMessage") // Not L10N
            };

            var rowSource = new AuditLogRowSource(dataSchema);
            var rowSourceInfo = new RowSourceInfo(typeof(AuditLogRow), rowSource, viewInfos);
            var viewContext = new SkylineViewContext(dataSchema, new[] { rowSourceInfo });
            
            return new AuditLogForm(viewContext);
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

            private AuditLogRow GetRow(AuditLogEntry entry, SkylineDataSchema skylineDataSchema)
            {
                return new AuditLogRow(skylineDataSchema, entry, entry.SkylineVersion, entry.FormatVersion, entry.TimeStamp, entry.User);
            }

            public override IEnumerable GetItems()
            {
                return DataSchema.Document.AuditLog.AuditLogEntries.Select(e => GetRow(e, DataSchema));
            }
        }
    }
}