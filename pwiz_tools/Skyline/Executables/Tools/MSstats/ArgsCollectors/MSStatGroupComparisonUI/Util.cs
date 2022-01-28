using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public class Util
    {
        public static void SelectComboBoxValue<T>(ComboBox comboBox, T value, IList<T> values = null)
        {
            values = values ?? comboBox.Items.OfType<T>().ToList();
            int index = values.IndexOf(value);
            if (index >= 0)
            {
                comboBox.SelectedIndex = index;
            }
        }
        public static bool ValidateDouble(TextBox textBox, out double value)
        {
            if (!double.TryParse(textBox.Text, out value))
            {
                ShowControlMessage(textBox,
                    string.Format(MSstatsResources.SampleSizeUi_ValidateNumber_The_number___0___is_not_valid_,
                        textBox.Text));
                return false;
            }
            return true;
        }

        public static bool ValidateOptionalDouble(TextBox textBox, out double? value)
        {
            value = null;
            string text = textBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            if (!double.TryParse(text, out double parsedValue))
            {
                ShowControlMessage(textBox, "This value must either be blank or a valid number.");
                return false;
            }

            value = parsedValue;
            return true;
        }

        public static bool ValidateOptionalInteger(TextBox textBox, out int? value)
        {
            value = null;
            string text = textBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            
            if (!int.TryParse(text, out int parsedValue))
            {
                ShowControlMessage(textBox, "This value must either be blank or a valid integer.");
                return false;
            }

            value = parsedValue;
            return true;
        }

        public static bool ValidateInteger(TextBox textBox, out int value)
        {
            if (!int.TryParse(textBox.Text, out value))
            {
                ShowControlMessage(textBox, "This must be an integer");
                return false;
            }

            return true;
        }

        public static void ShowControlMessage(Control control, string message)
        {
            MessageBox.Show(control, message);
            control.Focus();
        }
    }
}
