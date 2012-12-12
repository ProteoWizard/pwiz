using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Controls
{
    public class ViewColumn : DataGridViewColumn
    {
        public ViewColumn(IViewContext viewContext, ColumnPropertyDescriptor columnPropertyDescriptor) : this(viewContext, columnPropertyDescriptor, new ViewCell())
        {
        }
        public ViewColumn(IViewContext viewContext, ColumnPropertyDescriptor columnPropertyDescriptor, DataGridViewCell cellTemplate) : base(cellTemplate)
        {
            ViewContext = viewContext;
            ColumnPropertyDescriptor = columnPropertyDescriptor;
            SortMode = DataGridViewColumnSortMode.Automatic;
            if (columnPropertyDescriptor != null)
            {
                HeaderText = columnPropertyDescriptor.DisplayName;
                DataPropertyName = columnPropertyDescriptor.Name;
                Name = columnPropertyDescriptor.Name;
            }
        }
        public ViewColumn() : this(null, null)
        {
        }

        public IViewContext ViewContext { get; private set; }
        public ColumnPropertyDescriptor ColumnPropertyDescriptor { get; private set; }
        public override object Clone()
        {
            var result = base.Clone();
            return result;
        }
    }
}
