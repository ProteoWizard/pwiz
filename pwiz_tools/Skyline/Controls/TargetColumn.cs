using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Controls
{
    public class TargetColumn : DataGridViewTextBoxColumn
    {
        [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
        public TargetColumn()
        {
            CellTemplate = new TargetCell(){TargetColumn = this};
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public TargetResolver TargetResolver { get; set; }

        public class TargetCell : DataGridViewTextBoxCell
        {
            public TargetColumn TargetColumn { get; set; }

            public TargetResolver TargetResolver
            {
                get { return TargetColumn?.TargetResolver ?? TargetResolver.EMPTY; }
            }

            protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
                TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
            {
                var target = value as Target;
                if (target == null)
                {
                    return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
                }
                return TargetResolver.FormatTarget(target);
            }

            public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle,
                TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
            {
                return TargetResolver.ResolveTarget(formattedValue as string);
            }

            public override object Clone()
            {
                TargetCell targetCell = (TargetCell) base.Clone();
                targetCell.TargetColumn = TargetColumn;
                return targetCell;
            }
        }
    }
}
