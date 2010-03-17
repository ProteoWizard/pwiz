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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.ComponentModel;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls
{
    public class MessageBoxHelper
    {
        private readonly Form _parent;

        public MessageBoxHelper(Form parent)
        {
            _parent = parent;
        }

        public bool ValidateNameTextBox(CancelEventArgs e, TextBox control, out string val)
        {
            bool valid = false;
            val = control.Text.Trim();
            if (val.Length == 0)
            {
                ShowTextBoxError(control, "{0} cannot be empty.");
            }
            else
            {
                valid = true;
            }
            e.Cancel = !valid;
            return valid;
        }

        public bool ValidateDecimalTextBox(CancelEventArgs e, TextBox control, out double val)
        {
            bool valid = false;
            val = default(double);
            try
            {
                val = double.Parse(control.Text);
                valid = true;
            }
            catch (FormatException)
            {
                ShowTextBoxError(control, "{0} must contain a decimal value.");
            }
            e.Cancel = !valid;
            return valid;
        }

        public bool ValidateDecimalTextBox(CancelEventArgs e, TextBox control,
                                           double min, double max, out double val)
        {
            if (!ValidateDecimalTextBox(e, control, out val))
                return false;

            bool valid = false;
            if (val < min)
                ShowTextBoxError(control, "{0} must be greater than or equal to {1}.", null, min);
            else if (val > max)
                ShowTextBoxError(control, "{0} must be less than or equal to {1}.", null, max);
            else
                valid = true;
            e.Cancel = !valid;
            return valid;
        }

        public bool ValidateDecimalTextBox(CancelEventArgs e, TabControl tabControl, int tabIndex,
            TextBox control, double min, double max, out double val)
        {
            bool valid = ValidateDecimalTextBox(e, control, min, max, out val);
            if (!valid && tabControl.SelectedIndex != tabIndex)
            {
                tabControl.SelectedIndex = tabIndex;
                control.Focus();
            }
            return valid;
        }

        /// <summary>
        /// Validates a TextBox that should containe an integer value.
        /// </summary>
        /// <param name="e">CancelEvenArgs to cancel validation, if the integer is invalid</param>
        /// <param name="control">The TextBox control to validate</param>
        /// <param name="min">Minimum allowed value</param>
        /// <param name="max">Maximum allowed value</param>
        /// <param name="val">The integer value in the TextBox, if the function returns true</param>
        /// <returns>True if a valid integer was found</returns>
        public bool ValidateNumberTextBox(CancelEventArgs e, TextBox control,
                                          int min, int max, out int val)
        {
            bool valid = false;
            val = min - 1;  // Invalid value in case of failure
            try
            {
                int n = int.Parse(control.Text);
                if (n < min)
                    ShowTextBoxError(control, "{0} must be greater than or equal to {1}.", null, min);
                else if (n > max)
                    ShowTextBoxError(control, "{0} must be less than or equal to {1}.", null, max);
                else
                {
                    val = n;
                    valid = true;
                }
            }
            catch (FormatException)
            {
                ShowTextBoxError(control, "{0} must contain an integer.");
            }
            e.Cancel = !valid;
            return valid;
        }

        public bool ValidateNumberTextBox(CancelEventArgs e, TabControl tabControl, int tabIndex,
            TextBox control, int min, int max, out int val)
        {
            bool valid = ValidateNumberTextBox(e, control, min, max, out val);
            if (!valid && tabControl.SelectedIndex != tabIndex)
            {
                tabControl.SelectedIndex = tabIndex;
                control.Focus();                
            }
            return valid;
        }

        public bool ValidateNumberListTextBox(CancelEventArgs e, TextBox control,
                                              int min, int max, out int[] val)
        {
            val = ArrayUtil.Parse(control.Text, Convert.ToInt32, ',', new int[0]);
            if (val.Length > 0)
                return true;

            ShowTextBoxError(control,
                             "{0} must contain a comma separated list of integers from {1} to {2}.",
                             null, min, max);
            e.Cancel = true;
            return false;
        }

        public void ShowTextBoxError(CancelEventArgs e, TextBox control, string message)
        {
            ShowTextBoxError(control, message);
            e.Cancel = true;
        }

        public void ShowTextBoxError(CancelEventArgs e, TextBox control, string message, params object[] vals)
        {
            ShowTextBoxError(control, message, vals);
            e.Cancel = true;
        }

        public void ShowTextBoxError(TextBox control, string message)
        {
            ShowTextBoxError(control, message, new string[] { null });
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
        public void ShowTextBoxError(TextBox control, string message, params object[] vals)
        {
            if (vals.Length > 0 && vals[0] == null)
                vals[0] = GetControlMessage(control);
            MessageBox.Show(_parent, string.Format(message, vals), Program.Name);
            control.Focus();
            control.SelectAll();
        }

        /// <summary>
        /// Gets the text of a control's label, and cleans it for use in a message box.
        /// </summary>
        /// <param name="control">A control whith a label one tabstop before it</param>
        /// <returns>Message box text</returns>
        public string GetControlMessage(Control control)
        {
            Control label = _parent.GetNextControl(control, false);
            string message = (label == null ? "Field" : label.Text);
            message = message.Replace("&", "");
            if (message[message.Length - 1] == ':')
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
        /// <param name="parent">Parent for the message box</param>
        /// <param name="firstLine">First line of the message specific to the situation</param>
        /// <param name="path">Path to the XML file</param>
        /// <param name="x">An <see cref="Exception"/> thrown during XML parsing</param>
        public static void ShowXmlParsingError(IWin32Window parent, string firstLine, string path, Exception x)
        {
            string messageException = GetInvalidDataMessage(path, x);
            MessageBox.Show(parent, firstLine + "\n" + messageException, Program.Name);
        }

        private static string GetInvalidDataMessage(string path, Exception x)
        {
            StringBuilder sb = new StringBuilder();
            int line, column;
            if (!TryGetXmlLineColumn(x.Message, out line, out column))
                sb.Append(x.Message);
            else
            {
                if (line != 0)
                    sb.Append(string.Format("The file contains an error on line {0} at column {1}.", line, column));
                else
                {
                    if (column == 0 && IsSmallAndWhiteSpace(path))
                        return "The file is empty.\nIt may have been truncated during file transfer.";
                    else
                        return "The file does not appear to be valid XML.";
                }
            }
            while (x != null)
            {
                if (x is InvalidDataException)
                {
                    sb.AppendLine().Append(x.Message);
                    break;
                }
                x = x.InnerException;
            }
            return sb.ToString();
        }

        private static readonly Regex REGEX_XML_ERROR = new Regex(@"There is an error in XML document \((\d+), (\d+)\).");

        private static bool TryGetXmlLineColumn(string message, out int line, out int column)
        {
            line = column = 0;

            Match match = REGEX_XML_ERROR.Match(message);
            if (!match.Success)
                return false;
            if (!int.TryParse(match.Groups[1].Value, out line))
                return false;
            if (!int.TryParse(match.Groups[2].Value, out column))
                return false;
            return true;
        }

        /// <summary>
        /// Returns true, if a file is less than or equal to 10 characters and
        /// all whitespace, or empty.
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>True if small and whitespace</returns>
        private static bool IsSmallAndWhiteSpace(string path)
        {
            if (new FileInfo(path).Length > 10)
                return false;
            try
            {
                string text = File.ReadAllText(path);
                foreach (char c in text)
                {
                    if (!char.IsWhiteSpace(c))
                        return false;
                }
            }
            catch (Exception)
            {
                return false;   // Can't tell, really
            }
            return true;
        }
    }
}