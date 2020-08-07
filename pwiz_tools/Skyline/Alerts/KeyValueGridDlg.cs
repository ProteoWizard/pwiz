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
            var layout = new TableLayoutPanel {Dock = DockStyle.Fill};
            layout.RowCount = gridValues.Count + 1; // empty last row
            layout.ColumnCount = 2; // key and value
            foreach (ColumnStyle style in layout.ColumnStyles)
            {
                style.Width = 50;
                style.SizeType = SizeType.Percent;
            }

            var keyToControl = new Dictionary<string, TextBox>();
            var controlToSetting = new Dictionary<object, TValue>();
            int row = 0;
            foreach (var kvp in gridValues)
            {
                var lbl = new Label
                {
                    Text = kvp.Key,
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                var value = new TextBox {Text = valueToString(kvp.Value), Dock = DockStyle.Fill};
                value.LostFocus += (o, args) =>
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
                layout.Controls.Add(lbl, 0, row);
                layout.Controls.Add(value, 1, row);
                keyToControl[kvp.Key] = value;
                controlToSetting[value] = kvp.Value;
                row++;
            }

            using (var dlg = new MultiButtonMsgDlg(layout, Resources.OK) {Text = title})
            {
                if (dlg.ShowParentlessDialog() == DialogResult.Cancel)
                    return;

                foreach (var kvp in keyToControl)
                    stringToValue(keyToControl[kvp.Key].Text, gridValues[kvp.Key]);
            }
        }
    }
}
