using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI
{
    public partial class DefineExtractedMetadataDlg : Form
    {
        private SkylineDataSchema _dataSchema;
        private DocumentAnnotations _documentAnnotations;

        public DefineExtractedMetadataDlg(IDocumentContainer documentContainer)
        {
            InitializeComponent();
            _dataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
            _documentAnnotations = new DocumentAnnotations(_dataSchema);
            var rootColumn = ColumnDescriptor.RootColumn(_dataSchema, typeof(ResultFile));
            availableFieldsTree2.RootColumn = rootColumn;
        }

        private class TargetDataSchema : SkylineDataSchema
        {
            public TargetDataSchema(IDocumentContainer documentContainer, DataSchemaLocalizer dataSchemaLocalizer): 
                base(documentContainer, dataSchemaLocalizer)
            {

            }

            public override IEnumerable<PropertyDescriptor> GetPropertyDescriptors(Type type)
            {
                foreach (var propertyDescriptor in base.GetPropertyDescriptors(type))
                {
                    if (propertyDescriptor.IsReadOnly)
                    {
                        if (typeof(IEnumerable).IsAssignableFrom(propertyDescriptor.PropertyType))
                        {
                            continue;
                        }

                        if (!typeof(SkylineObject).IsAssignableFrom(propertyDescriptor.PropertyType))
                        {
                            continue;
                        }
                    }

                    yield return propertyDescriptor;
                }
            }

            public override bool IsRootTypeSelectable(Type type)
            {
                return false;
            }
        }
    }
}
