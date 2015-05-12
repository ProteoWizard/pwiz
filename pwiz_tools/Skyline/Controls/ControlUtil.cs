/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls
{
    public class MessageBoxHelper
    {
        private readonly Form _parent;
        private readonly bool _showMessages;

        public MessageBoxHelper(Form parent)
            : this(parent, true)
        {
        }

        public MessageBoxHelper(Form parent, bool showMessages)
        {
            _parent = parent;
            _showMessages = showMessages;
        }

        public bool ShowMessages { get { return _showMessages; } }

        public bool ValidateNameTextBox(Control control, out string val)
        {
            bool valid = false;
            val = control.Text.Trim();
            if (val.Length == 0)
            {
                ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty);
            }
            else
            {
                valid = true;
            }
            return valid;
        }

        public bool IsZeroOrEmpty(TextBox control)
        {
            double value;
            return string.IsNullOrWhiteSpace(control.Text) ||
                   (double.TryParse(control.Text, out value) && Equals(value, 0.0));
        }

        public bool ValidateDecimalTextBox(TextBox control, out double val)
        {
            bool valid = false;
            val = default(double);
            try
            {
                val = double.Parse(control.Text, LocalizationHelper.CurrentCulture);
                valid = true;
            }
            catch (FormatException)
            {
                ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_contain_a_decimal_value);
            }
            return valid;
        }

        public bool ValidateDecimalTextBox(TextBox control,
                                           double? min, double? max, out double val)
        {
            if (!ValidateDecimalTextBox(control, out val))
                return false;

            bool valid = false;
            if (min.HasValue && val < min.Value)
                ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, null, min);
            else if (max.HasValue && val > max.Value)
                ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, null, max);
            else
                valid = true;
            return valid;
        }

        public bool ValidateDecimalTextBox(TabControl tabControl, int tabIndex,
            TextBox control, double? min, double? max, out double val)
        {
            bool valid = ValidateDecimalTextBox(control, min, max, out val);
            if (!valid && tabControl.SelectedIndex != tabIndex && _showMessages)
            {
                tabControl.SelectedIndex = tabIndex;
                control.Focus();
            }
            return valid;
        }

        public bool ValidateDecimalListTextBox(TabControl tabControl, int tabIndex,
                                              TextBox control, double? min, double? max, out double[] val)
        {
            bool valid = ValidateDecimalListTextBox(control, min, max, out val);
            if (!valid && tabControl.SelectedIndex != tabIndex)
            {
                tabControl.SelectedIndex = tabIndex;
                control.Focus();
            }
            return valid;
        }

        public bool ValidateDecimalListTextBox(TextBox control,
                                              double? min, double? max, out double[] val)
        {
            val = ArrayUtil.Parse(control.Text, Convert.ToDouble, ',', new double[0]);
            if (val.Length > 0 && !val.Contains(i => (min.HasValue && min.Value > i) || (max.HasValue && i > max.Value)))
                return true;

            ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateDecimalListTextBox__0__must_contain_a_comma_separated_list_of_decimal_values_from__1__to__2__,
                             null, min, max);
            return false;
        }

        public void ShowTextBoxError(Control control, string message)
        {
            ShowTextBoxError(control, message, new object[] { null });
        }




        /// <summary>
        /// Validates a TextBox that should contain a integer value, possibly negative.
        /// </summary>
        /// <param name="control">The TextBox control to validate</param>
        /// <param name="absMin">Minimum allowed absolute value</param>
        /// <param name="absMax">Maximum allowed absolute value</param>
        /// <param name="val">The integer value in the TextBox, if the function returns true</param>
        /// <returns>True if a valid integer was found</returns>
        public bool ValidateSignedNumberTextBox(TextBox control,
                                          int absMin, int absMax, out int val)
        {
            bool valid = false;
            val = int.MinValue;  // Invalid value in case of failure
            try
            {
                var n = int.Parse(control.Text);
                if ((n == 0) || (Math.Abs(n) < absMin) || (Math.Abs(n) > absMax))
                {
                    ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateSignedNumberTextBox_Value__0__must_be_between__1__and__2__or__3__and__4__, n, -absMax, -absMin, absMin, absMax);
                }
                else
                {
                    val = n;
                    valid = true;
                }
            }
            catch (FormatException)
            {
                ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateNumberTextBox__0__must_contain_an_integer);
            }
            return valid;
        }

        /// <summary>
        /// Validates a TextBox that should contain a positive integer value.
        /// </summary>
        /// <param name="control">The TextBox control to validate</param>
        /// <param name="min">Minimum allowed value</param>
        /// <param name="max">Maximum allowed value</param>
        /// <param name="val">The integer value in the TextBox, if the function returns true</param>
        /// <returns>True if a valid integer was found</returns>
        public bool ValidateNumberTextBox(TextBox control,
                                          int? min, int? max, out int val)
        {
            bool valid = false;
            val = -1;  // Invalid value in case of failure
            try
            {
                int n = int.Parse(control.Text);
                if (min.HasValue && n < min.Value)
                    ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_greater_than_or_equal_to__1__, null, min);
                else if (max.HasValue && n > max.Value)
                    ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateDecimalTextBox__0__must_be_less_than_or_equal_to__1__, null, max);
                else
                {
                    val = n;
                    valid = true;
                }
            }
            catch (FormatException)
            {
                ShowTextBoxError(control, Resources.MessageBoxHelper_ValidateNumberTextBox__0__must_contain_an_integer);
            }
            return valid;
        }

        public bool ValidateNumberTextBox(TabControl tabControl, int tabIndex,
            TextBox control, int? min, int? max, out int val)
        {
            bool valid = ValidateNumberTextBox(control, min, max, out val);
            if (!valid && tabControl.SelectedIndex != tabIndex && _showMessages)
            {
                tabControl.SelectedIndex = tabIndex;
                control.Focus();                
            }
            return valid;
        }

        public bool ValidateNumberListTextBox(TabControl tabControl, int tabIndex,
                                              TextBox control, int min, int max, out int[] val)
        {
            bool valid = ValidateNumberListTextBox(control, min, max, out val);
            if (!valid && tabControl.SelectedIndex != tabIndex)
            {
                tabControl.SelectedIndex = tabIndex;
                control.Focus();
            }
            return valid;
        }

        public bool ValidateNumberListTextBox(TextBox control,
                                              int min, int max, out int[] val)
        {
            val = ArrayUtil.Parse(control.Text, Convert.ToInt32, TextUtil.SEPARATOR_CSV, new int[0]);
            if (val.Length > 0 && !val.Contains(i => min > i || i > max))
                return true;

            ShowTextBoxError(control, Resources. MessageBoxHelper_ValidateNumberListTextBox__0__must_contain_a_comma_separated_list_of_integers_from__1__to__2__,
                             null, min, max);

            return false;
        }

        public void ShowTextBoxError(TabControl tabControl, int tabIndex, TextBox control, string message)
        {
            ShowTextBoxError(control, message);
            tabControl.SelectedIndex = tabIndex;
            control.Focus();
        }

        /// <summary>
        /// Show a message box for a value error in a text box control,
        /// including the preceding label in a formatted message string.
        /// If the first object in the value array is null, it is filled
        /// with the label text of the control.
        /// </summary>
        /// <param name="control">A text box with a validation error</param>
        /// <param name="message">A message format string</param>
        /// <param name="vals">Objects for use in the format string</param>
        public void ShowTextBoxError(Control control, string message, params object[] vals)
        {
            if(!_showMessages)
                return;
            if (vals.Length > 0 && vals[0] == null)
                vals[0] = GetControlMessage(control);
            MessageDlg.Show(_parent, string.Format(message, vals));
            control.Focus();
            var textBox = control as TextBox;
            if(textBox != null)
                textBox.SelectAll();
        }

        /// <summary>
        /// Gets the text of a control's label, and cleans it for use in a message box.
        /// </summary>
        /// <param name="control">A control with a label one tabstop before it</param>
        /// <returns>Message box text</returns>
        public string GetControlMessage(Control control)
        {
            Control label = control;
            while(label != null && !(label is Label))
                label = _parent.GetNextControl(label, false);
            string message = (label == null ? Resources.MessageBoxHelper_GetControlMessage_Field : label.Text);
            int ampIndex = message.IndexOf('&');
            // Chinese and Japanese use the mnemonic format <label>(&A):
            if (ampIndex > 0 && message[ampIndex - 1] == '(' &&
                ampIndex < message.Length - 2 && char.IsLetter(message[ampIndex + 1]) && message[ampIndex + 2] == ')')
            {
                // Strip the (&A)<punctuation>
                message = message.Substring(0, ampIndex - 1);
            }
            else
            {
                // For roman character languages, just remove the ambersand
                message = message.Replace("&", string.Empty); // Not L10N
            }
            if (message.Length > 0 && message[message.Length - 1] == ':')
                message = message.Substring(0, message.Length - 1);
            return message;
        }

        /// <summary>
        /// Inspects an exception thrown during XML deserialization, constructs an
        /// appropriate error message, and shows it in a message box.
        /// <para>
        /// Common problems like files truncated during transfer are handled, and
        /// line and column numbers are displayed in a sentence understandable by
        /// a normal user.</para>
        /// </summary>
        /// <param name="firstLine">First line of the message specific to the situation</param>
        /// <param name="path">Path to the XML file</param>
        /// <param name="x">An <see cref="Exception"/> thrown during XML parsing</param>
        public void ShowXmlParsingError(string firstLine, string path, Exception x)
        {
            if(!_showMessages)
                return;
            string messageException = XmlUtil.GetInvalidDataMessage(path, x);
            MessageDlg.ShowWithException(_parent, TextUtil.LineSeparate(firstLine, messageException), x);
        }
    }
}