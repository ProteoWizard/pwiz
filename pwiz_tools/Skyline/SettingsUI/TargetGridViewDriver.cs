/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Makes it possible to show "caffeine" in a grid view instead of "#$#caffeine#C8H10N4O2#", and adds formula, InChiKey etc columns as needed
    /// </summary>
    public abstract class TargetGridViewDriver<TItem> : SimpleGridViewDriver<TItem>
    {
        protected TargetGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource, SortableBindingList<TItem> items, 
            IEnumerable<Target> targets, TargetResolver targetResolver, SrmDocument.DOCUMENT_TYPE modeUI, bool smallMolDetailColumnsReadOnly)
            : base(gridView, bindingSource, items)
        {
            SmallMoleculeColumnsManager = new SmallMoleculeColumnsManager(gridView,
                targetResolver ?? TargetResolver.MakeTargetResolver(Program.ActiveDocumentUI, targets), 
                modeUI, smallMolDetailColumnsReadOnly);
            // Find any TargetColumns and have them use same small molecule column manager
            foreach (var col in this.GridView.Columns)
            {
                (col as TargetColumn)?.SetSmallMoleculesColumnManagementProvider(SmallMoleculeColumnsManager); // Makes it possible to show "caffeine" instead of "#$#caffeine#C8H10N4O2#",and adds formula, InChiKey etc columns as needed
            }

            // Adjust parent form width for any small molecule columns that were automatically added (assumes the gridView is suitably anchored)
            if (SmallMoleculeColumnsManager.HeadersAdded != null)
            {
                for (var parent = gridView.Parent; parent != null; parent = parent.Parent)
                {
                    if (parent is Form form)
                    {
                        var g = form.CreateGraphics();
                        var extraWidth = 0;
                        foreach (var header in SmallMoleculeColumnsManager.HeadersAdded)
                        {
                            extraWidth += (int)g.MeasureString(header + @" ", gridView.Font).Width; // Rough adjustment - not worrying about precise margins etc
                        }

                        form.Width += extraWidth;
                        break;
                    }
                }
            }
        }

        public TargetResolver TargetResolver { get { return SmallMoleculeColumnsManager.TargetResolver; } }
        public SmallMoleculeColumnsManager SmallMoleculeColumnsManager { get; private set;  }

        public void SetTargetResolver(TargetResolver targetResolver)
        {
            SmallMoleculeColumnsManager = SmallMoleculeColumnsManager.ChangeTargetResolver(targetResolver);
        }

        // For verifying data already in the grid
        protected Target TryResolveTarget(string targetText, int row, out string errorText)
        {
            var cells = GridView.Rows[row].Cells;
            var values = new List<string>();
            for (var i = 0; i < cells.Count; i++)
            {
                var formattedValue = cells[i].FormattedValue;
                values.Add(formattedValue?.ToString());
            }
            return TryResolveTarget(targetText, values, row, out errorText);
        }

        // For verifying data not yet in the grid
        protected Target TryResolveTarget(string targetText, IEnumerable<object> values, int row, out string errorText)
        {
            if (string.IsNullOrEmpty(targetText))
            {
                errorText = Resources
                    .MeasuredPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry;
                return null;
            }

            var target = TargetResolver.TryResolveTarget(targetText, out errorText);
            if (target == null)
            {
                target = SmallMoleculeColumnsManager.TryGetSmallMoleculeTargetFromDetails(targetText, values, row, out _);
                if (target != null)
                {
                    errorText = null;
                }
            }

            return target;
        }

    }
}
