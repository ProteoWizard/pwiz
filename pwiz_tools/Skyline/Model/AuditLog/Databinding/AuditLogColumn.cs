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
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class AuditLogColumn : TextImageColumn
    {
        private readonly Image[] _images;

        public enum ImageIndex { extra_info, undo_redo, multi_undo_redo }

        public AuditLogColumn()
        {
            var undoRedoImage = new Bitmap(Resources.Edit_Undo);
            undoRedoImage.MakeTransparent(Color.Magenta);
            var undoRedoMultipleImage = new Bitmap(Resources.Edit_Undo_Multiple);
            undoRedoMultipleImage.MakeTransparent(Color.Magenta);
            _images = new Image[]
            {
                Resources.magnifier_zoom_in,
                undoRedoImage,
                undoRedoMultipleImage,
            };
        }

        public override bool ShouldDisplay(object cellValue, int imageIndex)
        {
            if (!(cellValue is AuditLogRow.AuditLogRowText value))
                return false;

            switch ((ImageIndex)imageIndex)
            {
                case ImageIndex.extra_info:
                    return !string.IsNullOrEmpty(value.ExtraInfo);
                case ImageIndex.undo_redo:
                    return value.UndoAction != null && !value.IsMultipleUndo;
                case ImageIndex.multi_undo_redo:
                    return value.UndoAction != null && value.IsMultipleUndo;
                default:
                    return false;
            }
        }

        public void Click(object cellValue, int imageIndex)
        {
            if (!(cellValue is AuditLogRow.AuditLogRowText value))
                return;

            switch ((ImageIndex)imageIndex)
            {
                case ImageIndex.extra_info:
                {
                    using (var form = new AuditLogExtraInfoForm(value.Text, value.ExtraInfo))
                    {
                        form.ShowDialog(DataGridView.FindForm());
                    }
                    break;
                }
                case ImageIndex.undo_redo:
                case ImageIndex.multi_undo_redo:
                {
                    value.UndoAction();
                    break;
                }

            }

        }

        public override void OnClick(object cellValue, int imageIndex)
        {
            Click(cellValue, imageIndex);
        }

        public override IList<Image> Images
        {
            get { return _images; }
        }
    }
}
