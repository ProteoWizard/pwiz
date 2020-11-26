using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Controls
{
    public partial class ClusteringEditor : Form
    {
        private ReportResults _reportResults = ReportResults.EMPTY;
        private bool _inUpdateUi;
        private WrappedDataSchema _wrappedDataSchema = new WrappedDataSchema(new DataSchema());
        public ClusteringEditor()
        {
            InitializeComponent();
        }

        public DataSchema DataSchema
        {
            get { return _wrappedDataSchema.InnerDataSchema; }
            set
            {
                if (ReferenceEquals(DataSchema, value))
                {
                    return;
                }

                _wrappedDataSchema = new WrappedDataSchema(value);
                UpdateUi();
            }
        }

        public ReportResults ReportResults { get
            {
                return _reportResults;
            }
            set
            {
                if (ReferenceEquals(ReportResults, value))
                {
                    return;
                }

                _reportResults = value;
                UpdateUi();
            }
        }

        public void UpdateUi()
        {
            bool wasInUpdateUi = _inUpdateUi;
            try
            {
                _inUpdateUi = true;
                var seriesIds = new HashSet<object>();
                comboRowColumn.Items.Clear();
                foreach (var property in ReportResults.ItemProperties)
                {
                    var pivotId = property.PivotedColumnId;
                    if (pivotId == null)
                    {
                        continue;
                    }

                    if (!seriesIds.Add(pivotId.SeriesId))
                    {
                        continue;
                    }

                    ColumnSeries columnSeries = new ColumnSeries(property.DataSchemaLocalizer, pivotId.SeriesId,
                        pivotId.SeriesCaption, property.PropertyType);
                    if (pivotId.PivotKey == null)
                    {
                        comboRowColumn.Items.Add(columnSeries);
                    }
                    else
                    {
                        comboColumnColumn.Items.Add(columnSeries);
                    }
                }
            }
            finally
            {
                _inUpdateUi = wasInUpdateUi;
            }
        }

        public class ColumnSeries
        {
            public ColumnSeries(DataSchemaLocalizer dataSchemaLocalizer, object seriesId, IColumnCaption caption, Type propertyType)
            {
                DataSchemaLocalizer = dataSchemaLocalizer;
                SeriesId = seriesId;
                Caption = caption;
                PropertyType = propertyType;
            }
            public DataSchemaLocalizer DataSchemaLocalizer { get; private set; }
            public object SeriesId { get; private set; }
            public IColumnCaption Caption { get; private set; }
            public Type PropertyType { get; private set; }

            public override string ToString()
            {
                return Caption.GetCaption(DataSchemaLocalizer);
            }
        }

        private void comboRowColumn_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inUpdateUi)
            {
                return;
            }

            var columnSeries = (ColumnSeries) comboRowColumn.SelectedItem;
            if (columnSeries != null)
            {
                availableFieldsTreeRows.RootColumn = ColumnDescriptor.RootColumn(_wrappedDataSchema, columnSeries.PropertyType);
            }
        }

        private class WrappedDataSchema : DataSchema
        {
            public WrappedDataSchema(DataSchema innerDataSchema) : base(innerDataSchema.DataSchemaLocalizer)
            {
                InnerDataSchema = innerDataSchema;
            }
            public DataSchema InnerDataSchema { get; private set; }
            public override IEnumerable<PropertyDescriptor> GetPropertyDescriptors(Type type)
            {
                return InnerDataSchema.GetPropertyDescriptors(type)
                    .Where(pd => null == CollectionInfo.ForType(pd.PropertyType));
            }
        }

        private void comboColumnColumn_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_inUpdateUi)
            {
                return;
            }

            var columnSeries = (ColumnSeries)comboColumnColumn.SelectedItem;
            if (columnSeries != null)
            {
                availableFieldsTreeColumns.RootColumn = ColumnDescriptor.RootColumn(_wrappedDataSchema, columnSeries.PropertyType);
            }

        }
    }
}
