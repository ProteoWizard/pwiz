/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class TextImageCell : DataGridViewTextBoxCell
    {
        private TextImageColumnItem[] _items;
        public TextImageColumnItem[] Items
        {
            get { return _items ?? (_items = Column.Images.Select(img => new TextImageColumnItem(img)).ToArray()); }
        }

        private TextImageColumn Column
        {
            get { return OwningColumn as TextImageColumn; }
        }

        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
            DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
            DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);

            var container = graphics.BeginContainer();
            graphics.SetClip(cellBounds);

            var drawIndex = 0;
            for (var i = Items.Length - 1; i >= 0; --i)
            {
                var item = Items[i];
                item.Visible = Column.ShouldDisplay(value, i);
                if (item.Visible)
                {
                    graphics.DrawImageUnscaled(item.Image, CalculateImageRectangle(item.Image, drawIndex++, cellBounds));
                }    
            }

            graphics.EndContainer(container);
        }

        protected override void OnClick(DataGridViewCellEventArgs e)
        {
            for (var i = 0; i < Items.Length; ++i)
            {
                if (Items[i].MouseOver)
                    Column.OnClick(DataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value, i);
            }
        }

        public void ClickImage(int imageIndex)
        {
            Column.OnClick(Value, imageIndex);
        }

        protected override void OnMouseMove(DataGridViewCellMouseEventArgs e)
        {
            var mouseOverAny = false;
            var drawIndex = 0;
            for (var i = Items.Length - 1; i >= 0; --i)
            {
                if (Items[i].Visible)
                {
                    var cellBounds = DataGridView.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
                    var rectangle = CalculateImageRectangle(Items[i].Image, drawIndex++, cellBounds);
                    rectangle.Offset(-cellBounds.X, -cellBounds.Y);
                    Items[i].MouseOver = rectangle.Contains(e.Location);
                    mouseOverAny |= Items[i].MouseOver;
                }
                else
                {
                    Items[i].MouseOver = false;
                }  
            }

            Cursor.Current = mouseOverAny ? Cursors.Hand : Cursors.Default;
        }

        private static Rectangle CalculateImageRectangle(Image image, int drawIndex, Rectangle cellBounds)
        {
            var result = new Rectangle();

            result.Width = image.Width;
            result.Height = image.Height;
            result.X = cellBounds.Right - (drawIndex + 1) * result.Width * 3 / 2;
            result.Y = cellBounds.Location.Y + cellBounds.Height / 2 - result.Height / 2;

            return result;
        }
    }

    public class TextImageColumnItem
    {
        public TextImageColumnItem(Image image)
        {
            Image = image;
        }

        public Image Image { get; protected set; }
        public bool Visible { get; set; }
        public bool MouseOver { get; set; }
    }

    public abstract class TextImageColumn : DataGridViewTextBoxColumn
    {
        public TextImageColumn()
        {
            CellTemplate = new TextImageCell();
        }

        public abstract bool ShouldDisplay(object cellValue, int imageIndex);
        public abstract void OnClick(object cellValue, int imageIndex);

        public sealed override DataGridViewCell CellTemplate
        {
            get { return base.CellTemplate; }
            set { base.CellTemplate = value; }
        }

        public abstract IList<Image> Images { get; }
    }
}
