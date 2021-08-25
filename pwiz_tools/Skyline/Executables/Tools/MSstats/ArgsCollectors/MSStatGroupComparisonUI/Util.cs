using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public static class Util
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
    }
}
