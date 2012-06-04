//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;

namespace IDPicker.Controls
{
    public class DataGridViewPreviewCellClickEventArgs : System.Windows.Forms.DataGridViewCellMouseEventArgs
    {
        public DataGridViewPreviewCellClickEventArgs (int columnIndex, int rowIndex, int localX, int localY, MouseEventArgs mouseEventArgs)
            : base(columnIndex, rowIndex, localX, localY, mouseEventArgs)
        {
            Handled = false;
        }

        public bool Handled { get; set; }
    }

    public delegate void DataGridViewPreviewCellClickEventHandler (object sender, DataGridViewPreviewCellClickEventArgs e);

    public class PreviewDataGridView : System.Windows.Forms.DataGridView
    {
        /// <summary>
        /// Occurs before the CellClick event, allowing "handling" of a cell-click, similar to KeyPress.
        /// </summary>
        public event DataGridViewPreviewCellClickEventHandler PreviewCellClick;

        protected override void OnMouseDown (MouseEventArgs e)
        {
            if (PreviewCellClick != null)
            {
                var hitTestInfo = HitTest(e.X, e.Y);
                var localPoint = PointToClient(e.Location);
                var eventArgs = new DataGridViewPreviewCellClickEventArgs(hitTestInfo.ColumnIndex,
                                                                          hitTestInfo.RowIndex,
                                                                          localPoint.X, localPoint.Y,
                                                                          e);
                PreviewCellClick(this, eventArgs);
                if (eventArgs.Handled)
                    return;
            }
            base.OnMouseDown(e);
        }
    }
}