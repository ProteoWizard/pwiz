using System.Linq;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public partial class CommonOptionsControl : UserControl
    {
        public CommonOptionsControl()
        {
            InitializeComponent();
        }

        public void InitializeOptions(DataSetInfo dataSetInfo, Arguments arguments)
        {
            SetDataSetInfo(dataSetInfo);
            RestoreArguments(arguments);
        }

        public void SetDataSetInfo(DataSetInfo dataSetInfo)
        {
            comboBoxNormalizeTo.Items.Clear();
            comboBoxNormalizeTo.Items.Add(NormalizationMethod.NONE);
            comboBoxNormalizeTo.Items.Add(NormalizationMethod.EQUALIZE_MEDIANS);
            if (dataSetInfo.HasGlobalStandards)
            {
                comboBoxNormalizeTo.Items.Add(NormalizationMethod.GLOBAL_STANDARDS);
            }

            lblQValueCutoff.Visible = tbxQValue.Visible = dataSetInfo.HasQValues;
        }

        public void RestoreArguments(Arguments arguments)
        {
            var normalizationMethod = arguments.Get(Arg.normalization);
            var item = comboBoxNormalizeTo.Items.Cast<NormalizationMethod>()
                .FirstOrDefault(v => v.ParameterValue == normalizationMethod);
            if (item != null)
            {
                comboBoxNormalizeTo.SelectedItem = item;
            }
            else
            {
                comboBoxNormalizeTo.SelectedIndex = 0;
            }

            tbxQValue.Text = arguments.GetInt(Arg.qValueCutoff).ToString();
        }

        // Constants
        protected const string FeatureSubsetHighQuality = "highQuality";
        protected const string FeatureSubsetAll = "all";

        public bool GetArguments(Arguments arguments)
        {
            arguments.Set(Arg.normalization, (comboBoxNormalizeTo.SelectedItem as NormalizationMethod)?.ParameterValue);
            arguments.Set(Arg.featureSelection, cbxHighQualityFeatures.Checked ? FeatureSubsetHighQuality : FeatureSubsetAll);
            if (!string.IsNullOrEmpty(tbxQValue.Text))
            {
                if (!Util.ValidateDouble(tbxQValue, out double qValueCutoff))
                {
                    return false;
                }

                if (qValueCutoff < 0 || qValueCutoff > 1)
                {
                    Util.ShowControlMessage(tbxQValue, "Q-Value cutoff must be between 0 and 1");
                    return false;
                }
                arguments.Set(Arg.qValueCutoff, qValueCutoff);
            }
            return true;
        }
    }
}
