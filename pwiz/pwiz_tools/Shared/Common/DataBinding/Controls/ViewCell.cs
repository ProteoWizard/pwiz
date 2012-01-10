using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Controls
{
    public class ViewCell : DataGridViewTextBoxCell
    {
        public ViewColumn ViewColumn
        {
            get
            {
                return OwningColumn as ViewColumn;
            }
        }
        public ColumnPropertyDescriptor PropertyDescriptor
        {
            get
            {
                return ViewColumn.ColumnPropertyDescriptor;
            }
        }
        public override object Clone()
        {
            var result = base.Clone();
            return result;
        }

        protected override bool SetValue(int rowIndex, object value)
        {
            return base.SetValue(rowIndex, value);
        }
    }
}
