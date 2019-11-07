/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Column which displays a Target as a single string.
    /// </summary>
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
