using System;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public partial class AutoCalculateAnnotationDlg : FormEx
    {
        private SkylineDataSchema _dataSchema;
        public AutoCalculateAnnotationDlg()
        {
            InitializeComponent();
            var document = new SrmDocument(SrmSettingsList.GetDefault());
            _dataSchema = SkylineDataSchema.MemoryDataSchema(document, SkylineDataSchema.GetLocalizedSchemaLocalizer());
        }

        public Type RootColumnType
        {
            get { return availableFieldsTree1.RootColumn?.PropertyType; }
            set
            {
                availableFieldsTree1.RootColumn = ColumnDescriptor.RootColumn(_dataSchema, value, UiModes.FromDocumentType(Program.MainWindow.ModeUI));
            }
        }

        public PropertyPath PropertyPath
        {
            get
            {
                var selectedNode = availableFieldsTree1.SelectedNode;
                if (selectedNode == null)
                {
                    return null;
                }

                return availableFieldsTree1.GetValueColumn(selectedNode).PropertyPath;
            }
            set
            {
                availableFieldsTree1.SelectColumn(value);
            }
        }
    }
}
