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
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public class KeyValueGridDlg : Control
    {
        /// <summary>
        /// Shows a simple dialog with a grid of labels (for keys) and textboxes (for values).
        /// The keys are always strings and the values are typed (but must be convertible to and from string obviously).
        /// If the (optional) validateValue action throws an exception, a message box will show the user
        /// the exception message and the invalid textbox value will be returned to its previous value.
        /// </summary>

        public static void Show<TValue>(string title, IDictionary<string, TValue> gridValues, Func<TValue, string> valueToString,
            Action<string, TValue> stringToValue, Action<string, TValue> validateValue = null, Func<TValue, IEnumerable<string>> validValuesForValue = null, 
            Func<TValue, string> keyToName = null, ControlCollection control = null)
        {
            Show(null, title, gridValues, valueToString, stringToValue, validateValue, validValuesForValue, keyToName, control);
        }

        /// <summary>
        /// Shows a simple dialog with a grid of labels (for keys) and textboxes (for values).
        /// The keys are always strings and the values are typed (but must be convertible to and from string obviously).
        /// If the (optional) validateValue action throws an exception, a message box will show the user
        /// the exception message and the invalid textbox value will be returned to its previous value.
        /// </summary>

        public static Dictionary<string, Control> Show<TValue>(IWin32Window parent, string title, IDictionary<string, TValue> gridValues, Func<TValue, string> valueToString,
            Action<string, TValue> stringToValue, Action<string, TValue> validateValue = null, Func<TValue, IEnumerable<string>> validValuesForValue = null, 
            Func<TValue, string> keyToName = null, ControlCollection control = null)
        {
            var layout = new TableLayoutPanel
            {
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                AutoScroll = true
            };
            layout.RowCount = gridValues.Count + 2; // empty first and last row
            layout.ColumnCount = 2; // key and value
            foreach (ColumnStyle style in layout.ColumnStyles)
            {
                style.Width = 50;
                style.SizeType = SizeType.Percent;
            }

            layout.Controls.Add(new Label
            {
                //Text = kvp.Key,
                Dock = DockStyle.Fill,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight,
            }, 0, 0);

            var keyToControl = new Dictionary<string, Control>();
            var controlToSetting = new Dictionary<object, TValue>();
            int row = 1;
            var ctlTextRepresentation = new StringBuilder();
            foreach (var kvp in gridValues) //.OrderBy(kvp => kvp.Key))
            {
                var lbl = new Label
                {
                    Text = kvp.Key,
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleRight,
                };
                

                Control valueControl = null;
                var validValues = validValuesForValue?.Invoke(kvp.Value)?.ToArray();
                if (validValues != null && validValues.Length > 0)
                {
                    var comboBox = new ComboBox
                    {
                        Dock = DockStyle.Fill,
                        Height = lbl.Height,
                    };


                    comboBox.Items.AddRange(validValues.Cast<object>().ToArray());
                    
                    if ((kvp.Value as AbstractDdaSearchEngine.Setting)!.OtherAction != null)
                    {
                        comboBox.DropDownStyle = ComboBoxStyle.DropDown;
                        comboBox.AutoCompleteMode = AutoCompleteMode.Suggest;
                    }
                    else
                    {
                        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                        comboBox.SelectedIndex = validValues.IndexOf(s => s == valueToString(kvp.Value));
                    }

                    comboBox.SelectedIndexChanged += (sender, args) =>
                    {
                        ComboBox thisBox = sender as ComboBox;
                        if (kvp.Value.GetType() == typeof(AbstractDdaSearchEngine.Setting) && sender is ComboBox)
                        {
                            AbstractDdaSearchEngine.Setting setting = kvp.Value as AbstractDdaSearchEngine.Setting;

                            foreach (var other_kvp in gridValues)
                            {
                                AbstractDdaSearchEngine.Setting other_setting = other_kvp.Value as AbstractDdaSearchEngine.Setting;

                                if (other_setting != null && setting != null && setting.OtherSettingName == other_setting.Name)
                                {
                                    if (setting.OtherAction != null)
                                    {
                                        var newText = setting.OtherAction(keyToControl[other_kvp.Key].Text, comboBox.Text);
                                        keyToControl[other_kvp.Key].Text = newText;
                                    }
                                    thisBox.Text = String.Empty;
                                    thisBox.DroppedDown = false;
                                    thisBox.Focus();
                                    break;
                                }

                            }
                        }
                    };

                    comboBox.TextUpdate += (sender, args) =>
                    {
                        ComboBox thisBox = sender as ComboBox;
                        if (thisBox != null)
                        {
                            string currentText = thisBox.Text;

                            // Clear current items
                            thisBox.Items.Clear();

                            // Filter items that contain the typed text (case-insensitive)
                            var filteredItems = validValues.ToList()
                                .FindAll(item => item.ToLower().Contains(currentText.ToLower()));

                            // Add filtered items back to ComboBox
                            thisBox.Items.AddRange(filteredItems.Cast<object>().ToArray());
                            
                            // Restore text and cursor position
                            if (thisBox.Text != currentText)
                                thisBox.Text = currentText;
                        
                            thisBox.Cursor = Cursors.Default;    
                            //thisBox.Focus(); // Ensure focus is retained
                            thisBox.SelectionLength = 0;
                            thisBox.SelectionStart = thisBox.Text.Length;
                            thisBox.DroppedDown = true;

                            if (thisBox.Items.Count == 0)
                            {
                                thisBox.Items.AddRange(validValues.Cast<object>().ToArray());
                            }
                        }
                    };

                    comboBox.KeyDown += (sender, args) =>
                    {
                        // Handle Enter key to select the first item in the filtered list
                        ComboBox thisBox = sender as ComboBox;
                        if (thisBox != null)
                        {
                            if (args.KeyCode == Keys.Enter && thisBox.Items.Count > 0)
                            {
                                args.Handled = true;
                                thisBox.Focus();
                                thisBox.Text = String.Empty;
                                thisBox.DroppedDown = false;
                            }
                        }
                    };

                    comboBox.KeyPress += (sender, args) =>
                    {
                        // Handle Enter key to select the first item in the filtered list
                        ComboBox thisBox = sender as ComboBox;
                        if (thisBox != null)
                        {
                            if (!(char.IsLetterOrDigit(args.KeyChar) || char.IsPunctuation(args.KeyChar)) && !char.IsControl(args.KeyChar) && thisBox.Items.Count > 0)
                            {
                                args.Handled = true;
                                thisBox.Focus();
                                thisBox.Text = String.Empty;
                                thisBox.DroppedDown = false;
                            }
                        }
                    };
                    valueControl = comboBox;
                }
                else if (bool.TryParse(valueToString(kvp.Value), out bool b))
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
                        Height = lbl.Height,
                        UseSystemPasswordChar = kvp.Key.ToLowerInvariant().Contains(@"password")
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

                ToolTip toolTip = new ToolTip();
                toolTip.AutoPopDelay = 5000;
                if (keyToName != null) toolTip.SetToolTip(lbl, keyToName(controlToSetting[keyToControl[kvp.Key]]));
                ctlTextRepresentation.AppendLine($@"{kvp.Key} = {valueToString(kvp.Value)}");
                row++;
            }

            var activeScreen = parent == null ? Screen.PrimaryScreen : Screen.FromHandle(parent.Handle); 
            int defaultHeight = Math.Min(3 * activeScreen.Bounds.Height / 4, layout.GetRowHeights().Sum() + 50);

            if (control == null) 
            {
                using (var dlg = new MultiButtonMsgDlg(layout, Resources.OK, ctlTextRepresentation.ToString()))
                {
                    dlg.Text = title;
                    dlg.ClientSize = new Size(400, defaultHeight);
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.ShowInTaskbar = false;
                    dlg.MinimumSize = dlg.Size;
                    layout.Size = dlg.ClientSize;
                    layout.Height -= 35;

                    var result = parent == null ? dlg.ShowParentlessDialog() : dlg.ShowWithTimeout(parent, title);
                    if (result == DialogResult.Cancel)
                        return null;

                    foreach (var kvp in keyToControl)
                    {
                        if (kvp.Value is TextBox tb)
                            stringToValue(tb.Text, gridValues[kvp.Key]);
                        else if (kvp.Value is CheckBox cb)
                            stringToValue(cb.Checked.ToString(), gridValues[kvp.Key]);
                        else if (kvp.Value is ComboBox cmb)
                        {
                            if (cmb.SelectedItem != null)
                            {
                                stringToValue(cmb.SelectedItem.ToString(), gridValues[kvp.Key]);
                            }
                        }
                        else
                            throw new InvalidOperationException();
                    }
                }
            }
            else
            {
                layout.Dock = DockStyle.Fill;
                control.Add(layout);
                return keyToControl;
            }

            return null;
        }
    }
}
