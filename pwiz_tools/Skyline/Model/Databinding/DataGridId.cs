using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;

namespace pwiz.Skyline.Model.Databinding
{
    public class DataGridType
    {
        private Func<string> _titleTemplateStringFunc;
        public static DataGridType DOCUMENT_GRID = new DataGridType(typeof(DocumentGridForm), );
        public DataGridType(Type gridClass, Func<string> titleTemplateStringFunc)
        {
            GridClass = gridClass;
            _titleTemplateStringFunc = titleTemplateStringFunc;
        }

        public Type GridClass { get; private set; }
    }

    public class DataGridId
    {
        public DataGridId(DataGridType type, string name)
        {
            DataGridType = type;
            Name = name;
        }

        public DataGridType DataGridType { get; private set; }
        public string Name { get; private set; }
    }

    public class DataSourceId
    {
        public DataSourceId(DataGridId dataGridId, ViewName viewName, string layoutName)
        {
            DataGridId = dataGridId;
            ViewName = viewName;
        }
        public DataGridId DataGridId
        {
            get;
            private set;
        }

        public ViewName ViewName { get; private set; }
        public string LayoutName { get; private set; }
    }
}
