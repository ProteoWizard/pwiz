using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public partial class ArgsCollectorForm : Form
    {

        public ArgsCollectorForm()
        {
            InitializeComponent();
        }
        public static void SelectComboBoxValue<T>(ComboBox comboBox, T value, IList<T> values = null)
        {
            values = values ?? comboBox.Items.OfType<T>().ToList();
            int index = values.IndexOf(value);
            if (index >= 0)
            {
                comboBox.SelectedIndex = index;
            }
        }
        protected static readonly IList<string> _normalizationOptionValues = new ReadOnlyCollection<string>(new[]
        {
            "FALSE",
            "equalizeMedians",
            "quantile",
            "globalStandards"
        });

        protected static IEnumerable<string> GetNormalizationOptionLabels()
        {
            yield return MSstatsResources.ArgsCollectorForm_GetNormalizationOptionLabels_None;
            yield return MSstatsResources.ArgsCollectorForm_GetNormalizationOptionLabels_Equalize_Medians;
            yield return MSstatsResources.ArgsCollectorForm_GetNormalizationOptionLabels_Quantile;
            yield return MSstatsResources.ArgsCollectorForm_GetNormalizationOptionLabels_Global_Standards;
        }

        // Constants
        protected const string FeatureSubsetHighQuality = "highQuality";
        protected const string FeatureSubsetAll = "all";
    }
}
