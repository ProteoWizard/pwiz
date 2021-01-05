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

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Controls
{
    /// <summary>
    /// Column which displays a Target as a single string, and in the case of
    /// small molecules cooperates with other automatically generated columns
    /// via <see cref="SmallMoleculeColumnsManager"/> to show better detail (e.g. formula, InChIKey etc).
    /// The display string for a target may also contain charge information.
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
        public TargetResolver TargetResolver { get { return SmallMoleculeColumnsManager.TargetResolver; } }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SmallMoleculeColumnsManager SmallMoleculeColumnsManager { get; private set; }

        public void SetSmallMoleculesColumnManagementProvider(SmallMoleculeColumnsManager gridViewDriver)
        {
            SmallMoleculeColumnsManager = gridViewDriver;
        }

        public Target TryResolveTarget(string targetName, DataGridViewCellCollection cells, int rowIndex, out string errorMessage, bool strict = true)
        {
            var target = TargetResolver.TryResolveTarget(targetName, out errorMessage);
            if (target == null)
            {
                // Can we construct a target from what we find in the other columns?
                target = SmallMoleculeColumnsManager.TryGetSmallMoleculeTargetFromDetails(targetName, cells, rowIndex, out errorMessage, strict);
            }
            SmallMoleculeColumnsManager.UpdateSmallMoleculeDetails(target, rowIndex);
            return target;
        }
        public Target TryResolveTarget(string targetName, IEnumerable<string> values, int rowIndex, out string errorMessage, bool strict = true)
        {
            var target = TargetResolver.TryResolveTarget(targetName, out errorMessage);
            if (target == null)
            {
                // Can we construct a target from what we find in the other columns?
                target = SmallMoleculeColumnsManager.TryGetSmallMoleculeTargetFromDetails(targetName, values, rowIndex, out errorMessage, strict);
            }
            SmallMoleculeColumnsManager.UpdateSmallMoleculeDetails(target, rowIndex);
            return target;
        }

        public class TargetCell : DataGridViewTextBoxCell
        {
            public TargetColumn TargetColumn { get; set; }

            protected TargetResolver TargetResolver
            {
                get { return TargetColumn?.TargetResolver ?? TargetResolver.EMPTY; }
            }

            public Target ResolveTarget(string targetName)
            {
                return TargetResolver.ResolveTarget(targetName);
            }

            public SmallMoleculeColumnsManager SmallMoleculeColumnsManager
            {
                get { return TargetColumn?.SmallMoleculeColumnsManager; }
            }

            protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
                TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
            {
                var target = value as Target;
                if (target == null)
                {
                    return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
                }
                return FormatTarget(rowIndex, target);
            }

            public string FormatTarget(int rowIndex, Target target)
            {
                var result = TargetResolver.FormatTarget(target);
                SmallMoleculeColumnsManager?.UpdateSmallMoleculeDetails(target, rowIndex); // Update small molecule columns if any
                return result;
            }

            public override string ToString()
            {
                var target = Value as Target;
                return target == null ? base.ToString() : target.DisplayName;
            }

            public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle,
                TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
            {
                var target = Value as Target;
                if (target != null && !target.IsProteomic)
                {
                    // Understand this as just a name, not a complete description as with a peptide
                    var name = formattedValue as string;
                    if (!string.Equals(target.Molecule.Name, name))
                    {
                        var molecule = target.Molecule.ChangeName(name);
                        return new Target(molecule);
                    }
                }
                else if (TargetColumn?.SmallMoleculeColumnsManager != null && 
                         TargetColumn.SmallMoleculeColumnsManager.HasSmallMoleculeColumns )
                {
                    return Value; // Assume user is in the midst of hand edit, and still has other columns to populate
                }
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

    public class TargetDetailColumn : DataGridViewTextBoxColumn
    {
        [SuppressMessage("ReSharper", "VirtualMemberCallInConstructor")]
        public TargetDetailColumn(TargetColumn targetColumn)
        {
            CellTemplate = new TargetDetailCell(targetColumn);
        }

        public class TargetDetailCell : DataGridViewTextBoxCell
        {
            public TargetColumn TargetColumn { get; set; }

            public TargetDetailCell() {}

            public TargetDetailCell(TargetColumn targetColumn)
            {
                TargetColumn = targetColumn;

            }
            public override object Clone()
            {
                var targetDetailCell = (TargetDetailCell)base.Clone();
                targetDetailCell.TargetColumn = TargetColumn;
                return targetDetailCell;
            }
        }
    }

}
