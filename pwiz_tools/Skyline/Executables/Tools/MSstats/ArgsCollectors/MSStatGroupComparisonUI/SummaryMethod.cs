using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public sealed class SummaryMethod
    {
        private readonly Func<string> _getLabelFunc; 
        private SummaryMethod(string name, Func<string> getLabel)
        {
            Name = name;
            _getLabelFunc = getLabel;
        }

        public string Name { get; private set; }
        public String Label { get { return _getLabelFunc(); } }
        public override string ToString()
        {
            return Label;
        }

        public static readonly SummaryMethod Linear = new SummaryMethod("linear", ()=>MSstatsResources.SummaryMethod_Linear_Linear_model);
        public static readonly SummaryMethod Tmp = new SummaryMethod("TMP", ()=>MSstatsResources.SummaryMethod_Tmp_Tukey_s_median_polish);
        public static readonly SummaryMethod Skyline = new SummaryMethod("skyline", ()=>MSstatsResources.SummaryMethod_Skyline_Skyline);

        public static IList<SummaryMethod> ListMethods()
        {
            return new[]
            {
                Linear, Tmp, Skyline
            };
        }

        public static SummaryMethod Parse(string name)
        {
            return ListMethods().FirstOrDefault(item => item.Name == name);
        }

        public static void InitCombo(ComboBox comboBox)
        {
            comboBox.Items.Clear();
            comboBox.Items.AddRange(ListMethods().ToArray<object>());
            comboBox.SelectedIndex = 0;
        }

        public static void SelectValue(ComboBox comboBox, string name)
        {
            comboBox.SelectedItem = Parse(name) ?? Linear;
        }
    }
}
