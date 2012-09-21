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
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using ZedGraph;

namespace IDPicker.Forms
{
    /// <summary>
    /// A simple wrapper for DockableForm filled with a ZedGraphControl and providing other convenient features.
    /// </summary>
    public partial class DockableGraphForm : DockableForm
    {
        private ZedGraphControl zedGraphControl;
        public ZedGraphControl ZedGraphControl { get { return zedGraphControl; } }

        public MouseButtonClicks UnzoomButtons { get; set; }
        public MouseButtonClicks UnzoomAllButtons { get; set; }
        public Keys UnzoomModifierKeys { get; set; }

        public event EventHandler<ShowDataTableEventArgs> ShowDataTable;

        public DockableGraphForm ()
        {
            zedGraphControl = new ZedGraphControl
            {
                Dock = DockStyle.Fill
            };
            zedGraphControl.GraphPane.Title.IsVisible = false;
            Controls.Add(zedGraphControl);

            zedGraphControl.ContextMenuBuilder += zedGraphControl_ContextMenuBuilder;
            zedGraphControl.MouseClick += zedGraphControl_MouseClick;
            zedGraphControl.MouseDoubleClick += zedGraphControl_MouseClick;

            UnzoomButtons = new MouseButtonClicks(MouseButtons.Middle);
            UnzoomAllButtons = new MouseButtonClicks(MouseButtons.Left, 2);
            UnzoomModifierKeys = Keys.None;

            zedGraphControl.PanButtons = MouseButtons.Left;
            zedGraphControl.PanModifierKeys = Keys.Control;

            // graphs can't be closed
            FormClosing += (sender, e) => { e.Cancel = true; };
        }

        #region Additional mouse events (Unzoom and UnzoomAll)
        public class MouseButtonClicks
        {
            private readonly MouseButtons _buttons;
            private readonly int _clicks;

            public MouseButtonClicks(MouseButtons buttons)
            {
                _buttons = buttons;
                _clicks = 1;
            }

            public MouseButtonClicks(string value)
            {
                string[] tokens = value.Split(",".ToCharArray());
                if (tokens.Length != 2)
                    throw new FormatException("format string must have 2 tokens"); // Not L10N

                switch (tokens[0])
                {
                    case "None": _buttons = MouseButtons.None; break;
                    case "Left": _buttons = MouseButtons.Left; break;
                    case "Middle": _buttons = MouseButtons.Middle; break;
                    case "Right": _buttons = MouseButtons.Right; break;
                    case "XButton1": _buttons = MouseButtons.XButton1; break;
                    case "XButton2": _buttons = MouseButtons.XButton2; break;
                    default: throw new FormatException("first format string token must be one of (None,Left,Middle,Right,XButton1,XButton2)"); // Not L10N
                }

                if (!Int32.TryParse(tokens[1], out _clicks))
                    throw new FormatException("second format string token must be an integer specifying the number of button clicks"); // Not L10N
            }

            public MouseButtonClicks(MouseButtons buttons, int clicks)
            {
                _buttons = buttons;
                _clicks = clicks;
            }

            public bool MatchesEvent(MouseEventArgs e)
            {
                return (_buttons == e.Button && _clicks == e.Clicks);
            }

        }

        void zedGraphControl_MouseClick(object sender, MouseEventArgs e)
        {
            GraphPane pane = zedGraphControl.MasterPane.FindChartRect(e.Location);

            if (pane != null && (zedGraphControl.IsEnableHZoom || zedGraphControl.IsEnableVZoom))
            {
                if (UnzoomButtons.MatchesEvent(e) && ModifierKeys == UnzoomModifierKeys)
                {
                    if (zedGraphControl.IsSynchronizeXAxes)
                    {
                        foreach (GraphPane syncPane in zedGraphControl.MasterPane.PaneList)
                            syncPane.ZoomStack.Pop(syncPane);
                    }
                    else
                        pane.ZoomStack.Pop(pane);

                }
                else if (UnzoomAllButtons.MatchesEvent(e) && ModifierKeys == UnzoomModifierKeys)
                {
                    if (zedGraphControl.IsSynchronizeXAxes)
                    {
                        foreach (GraphPane syncPane in zedGraphControl.MasterPane.PaneList)
                            syncPane.ZoomStack.PopAll(syncPane);
                    }
                    else
                        pane.ZoomStack.PopAll(pane);

                }
                else
                    return;

                zedGraphControl.Refresh();
            }
        }
        #endregion

        void zedGraphControl_ContextMenuBuilder (ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            if (ShowDataTable == null)
                return;

            var e = new ShowDataTableEventArgs();
            ShowDataTable(this, e);

            int index = menuStrip.Items.Cast<ToolStripMenuItem>().TakeWhile(o => o.Text != "Show Point Values").Count();
            menuStrip.Items.Insert(index,
                new ToolStripMenuItem("Show Data Table", null,
                (x, y) =>
                {
                    var tableForm = new DockableForm
                    {
                        Text = this.Text + " Data Table",
                        Size = new Size(480, 600)
                    };

                    var dgv = new DataGridView
                    {
                        Dock = DockStyle.Fill,
                        DataSource = e.DataTable,
                        RowHeadersVisible = false,
                        AllowUserToDeleteRows = false,
                        AllowUserToAddRows = false,
                        ReadOnly = true,
                        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
                    };
                    tableForm.Controls.Add(dgv);
                    tableForm.Show(this);
                }));
        }
    }

    public class ShowDataTableEventArgs : EventArgs
    {
        public DataTable DataTable { get; set; }
    }
}
