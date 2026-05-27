/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Databinding.AuditLog;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results.Spectra;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Databinding
{
    /// <summary>
    /// Document grid column for the Precursor "Spectrum Filter" property. Clicking the
    /// icon opens the structured <see cref="EditSpectrumFilterDlg"/> rather than parsing
    /// a typed filter string, which keeps locale-formatted text off the parse path. The
    /// cell remains a normal bound text cell, so copy (invariant ToFilterString) and paste
    /// (tolerant ParseFilterString) continue to work like any other text column.
    /// </summary>
    public class SpectrumFilterDataGridViewColumn : TextImageColumn, ICellValidatingColumn
    {
        // Shared so that creating columns repeatedly (different views/grids) does not leak a GDI
        // bitmap handle each time. Resources.Edit_Item returns a fresh Bitmap on every access.
        private static readonly Image EDIT_IMAGE = CreateEditImage();

        private static Image CreateEditImage()
        {
            // Resources.Edit_Item returns a fresh Bitmap; dispose it once we have copied it so the
            // intermediate GDI handle is not leaked.
            using (var resourceImage = Resources.Edit_Item)
            {
                var editImage = new Bitmap(resourceImage);
                editImage.MakeTransparent(Color.Magenta);
                return editImage;
            }
        }

        private readonly Image[] _images = { EDIT_IMAGE };

        public SpectrumFilterDataGridViewColumn()
        {
            CellTemplate = new SpectrumFilterImageCell();
        }

        public override IList<Image> Images
        {
            get { return _images; }
        }

        public override bool ShouldDisplay(object cellValue, int imageIndex)
        {
            return true;
        }

        public string GetValidationError(string proposedText)
        {
            return SpectrumClassFilter.ValidateFilterString(proposedText);
        }

        public override void OnClick(object cellValue, int imageIndex)
        {
            var grid = DataGridView;
            var bindingListSource = grid?.DataSource as BindingListSource;
            if (bindingListSource == null)
            {
                return;
            }
            int rowIndex = grid.CurrentCellAddress.Y;
            if (rowIndex < 0 || rowIndex >= bindingListSource.Count)
            {
                return;
            }
            if (!(bindingListSource[rowIndex] is RowItem rowItem))
            {
                return;
            }
            // A view that shows the precursor's Spectrum Filter can be rooted below the Precursor
            // level (e.g. a transition list), so the row entity may be a Transition or a results row
            // rather than the Precursor itself. Resolve the owning Precursor from whichever entity
            // backs the clicked row.
            var precursorEntity = GetPrecursor(rowItem.Value);
            if (precursorEntity == null)
            {
                return;
            }
            LaunchEditor(grid, precursorEntity);
        }

        private static Precursor GetPrecursor(object rowValue)
        {
            switch (rowValue)
            {
                case Precursor precursor:
                    return precursor;
                case Transition transition:
                    return transition.Precursor;
                case PrecursorResult precursorResult:
                    return precursorResult.Precursor;
                case TransitionResult transitionResult:
                    return transitionResult.Transition?.Precursor;
                default:
                    return null;
            }
        }

        private static void LaunchEditor(DataGridView grid, Precursor precursor)
        {
            var dataSchema = precursor.DataSchema;
            var rootColumn = ColumnDescriptor.RootColumn(dataSchema, typeof(SpectrumClass));
            var filterPages = SpectrumClassFilter.GetFilterPages(precursor.DocNode);
            using (var autoComplete = new SpectrumFilterAutoComplete(dataSchema.SkylineWindow))
            using (var dlg = new EditSpectrumFilterDlg(rootColumn, filterPages))
            {
                dlg.AutoComplete = autoComplete;
                // Grid editing sets this precursor's filter in place; the "create copy"
                // (multiplex) option only makes sense from the Targets menu, so hide it here.
                dlg.CreateCopyVisible = false;
                if (dlg.ShowDialog(grid.FindForm()) != DialogResult.OK)
                {
                    return;
                }
                var newFilter = SpectrumClassFilter.FromFilterPages(dlg.FilterPages);
                if (grid.IsCurrentCellInEditMode && grid.EditingControl is TextBox textBox)
                {
                    // Commit the new filter through the editing control so the cell displays it
                    // and any text typed before opening the dialog is replaced. The bound setter
                    // applies the filter; serialize/parse round-trips losslessly in one culture.
                    textBox.Text = newFilter.ToFilterString();
                    grid.EndEdit();
                }
                else
                {
                    precursor.SetSpectrumClassFilter(newFilter);
                }
            }
        }

        /// <summary>
        /// Reserves room for the edit icon while the cell is in edit mode so the pencil stays
        /// visible instead of being covered by the text editing control.
        /// </summary>
        private class SpectrumFilterImageCell : TextImageCell
        {
            public override void PositionEditingControl(bool setLocation, bool setSize, Rectangle cellBounds,
                Rectangle cellClip, DataGridViewCellStyle cellStyle, bool singleVerticalBorderAdded,
                bool singleHorizontalBorderAdded, bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
            {
                int reserve = Items.Length > 0 ? Items[0].Image.Width * 3 / 2 + 2 : 0;
                cellBounds.Width = Math.Max(0, cellBounds.Width - reserve);
                cellClip.Width = Math.Max(0, cellClip.Width - reserve);
                base.PositionEditingControl(setLocation, setSize, cellBounds, cellClip, cellStyle,
                    singleVerticalBorderAdded, singleHorizontalBorderAdded, isFirstDisplayedColumn, isFirstDisplayedRow);
            }
        }
    }

    /// <summary>
    /// Implemented by document-grid columns that validate hand-typed cell text. The grid
    /// shows the returned message as a red cell error (with tooltip) and blocks cell exit
    /// until the text is valid.
    /// </summary>
    public interface ICellValidatingColumn
    {
        /// <summary>
        /// Returns null if <paramref name="proposedText"/> is acceptable, otherwise an
        /// error message describing why it cannot be parsed.
        /// </summary>
        string GetValidationError(string proposedText);
    }
}
