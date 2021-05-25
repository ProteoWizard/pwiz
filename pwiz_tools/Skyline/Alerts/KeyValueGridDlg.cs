/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    public class KeyValueGridDlg
    {
        /// <summary>
        /// Shows a simple dialog with a grid of labels (for keys) and textboxes (for values).
        /// The keys are always strings and the values are typed (but must be convertible to and from string obviously).
        /// If the (optional) validateValue action throws an exception, a message box will show the user
        /// the exception message and the invalid textbox value will be returned to its previous value.
        /// </summary>
        public static void Show<TValue>(string title, IDictionary<string, TValue> gridValues, Func<TValue, string> valueToString,
            Action<string, TValue> stringToValue, Action<string, TValue> validateValue = null)
        {
            Show(null, title, gridValues, valueToString, stringToValue, validateValue);
        }

        /// <summary>
        /// Shows a simple dialog with a grid of labels (for keys) and textboxes (for values).
        /// The keys are always strings and the values are typed (but must be convertible to and from string obviously).
        /// If the (optional) validateValue action throws an exception, a message box will show the user
        /// the exception message and the invalid textbox value will be returned to its previous value.
        /// </summary>
        public static void Show<TValue>(IWin32Window parent, string title, IDictionary<string, TValue> gridValues, Func<TValue, string> valueToString,
            Action<string, TValue> stringToValue, Action<string, TValue> validateValue = null)
        {
            var layout = new TableLayoutPanel
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                AutoScroll = true
            };
            layout.RowCount = gridValues.Count; // empty last row
            layout.ColumnCount = 2; // key and value
            foreach (ColumnStyle style in layout.ColumnStyles)
            {
                style.Width = 50;
                style.SizeType = SizeType.Percent;
            }

            var keyToControl = new Dictionary<string, Control>();
            var controlToSetting = new Dictionary<object, TValue>();
            int row = 0;
            foreach (var kvp in gridValues.OrderBy(kvp => kvp.Key))
            {
                var lbl = new Label
                {
                    Text = kvp.Key,
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleCenter
                };

                Control valueControl = null;
                if (bool.TryParse(valueToString(kvp.Value), out bool b))
                {
                    valueControl = new CheckBox
                    {
                        Checked = Convert.ToBoolean(valueToString(kvp.Value)),
                        Dock = DockStyle.Fill,
                        Height = lbl.Height
                    };
                    valueControl.Margin = new Padding(valueControl.Margin.Left, 0, 0, 0);
                }

                if (valueControl == null)
                {
                    valueControl = new TextBox
                    {
                        Text = valueToString(kvp.Value),
                        Dock = DockStyle.Fill,
                        Height = lbl.Height
                    };
                    valueControl.LostFocus += (o, args) =>
                    {
                        if (validateValue != null && o is TextBox tb)
                            try
                            {
                                validateValue(tb.Text, controlToSetting[tb]);
                            }
                            catch (Exception ex)
                            {
                                tb.Text = valueToString(controlToSetting[tb]);
                                MessageDlg.Show(tb.Parent, ex.Message);
                            }
                    };
                }

                layout.Controls.Add(lbl, 0, row);
                layout.Controls.Add(valueControl, 1, row);
                keyToControl[kvp.Key] = valueControl;
                controlToSetting[valueControl] = kvp.Value;
                row++;
            }

            var activeScreen = parent == null ? Screen.PrimaryScreen : Screen.FromHandle(parent.Handle); 
            int defaultHeight = Math.Min(3 * activeScreen.Bounds.Height / 4, layout.GetRowHeights().Sum() + 50);

            using (var dlg = new MultiButtonMsgDlg(layout, Resources.OK)
            {
                Text = title,
                ClientSize = new Size(300, defaultHeight)
            })
            {
                dlg.MinimumSize = dlg.Size;
                layout.Size = dlg.ClientSize;
                layout.Height -= 35;

                var result = parent == null ? dlg.ShowParentlessDialog() : dlg.ShowWithTimeout(parent, title);
                if (result == DialogResult.Cancel)
                    return;

                foreach (var kvp in keyToControl)
                {
                    if (kvp.Value is TextBox tb)
                        stringToValue(tb.Text, gridValues[kvp.Key]);
                    else if (kvp.Value is CheckBox cb)
                        stringToValue(cb.Checked.ToString(), gridValues[kvp.Key]);
                    else
                        throw new InvalidOperationException();
                }
            }
        }
    }
}
