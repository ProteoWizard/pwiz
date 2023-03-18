using System;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;

namespace pwiz.Skyline.EditUI.OptimizeTransitions
{
    public partial class OptimizeTransitionsSettingsControl : UserControl
    {
        private bool _inSettingsChange;
        public OptimizeTransitionsSettingsControl()
        {
            InitializeComponent();
        }

        public OptimizeTransitionSettings CurrentSettings
        {
            get
            {
                var settings = OptimizeTransitionSettings.DEFAULT
                    .ChangeMinimumNumberOfTransitions(MinNumberOfTransitions)
                    .ChangeOptimizeType(OptimizeType)
                    .ChangePreserveNonQuantitative(PreserveNonQuantitative)
                    .ChangeCombinePointsWithSameConcentration(CombinePointsWithSameConcentration);
                if (!string.IsNullOrEmpty(tbxRandomSeed.Text) && int.TryParse(tbxRandomSeed.Text, out int randomSeed))
                {
                    settings = settings.ChangeRandomSeed(randomSeed);
                }

                return settings;
            }
            set
            {
                if (Equals(value, CurrentSettings))
                {
                    return;
                }

                bool inSettingsChangeOld = _inSettingsChange;
                try
                {
                    _inSettingsChange = true;
                    tbxMinTransitions.Value = value.MinimumNumberOfTransitions;
                    if (value.OptimizeType == OptimizeType.LOD)
                    {
                        radioLOD.Checked = true;
                    }
                    else
                    {
                        radioLOQ.Checked = true;
                    }

                    cbxPreserveNonQuantitative.Checked = value.PreserveNonQuantitative;
                    tbxRandomSeed.Text = value.RandomSeed.ToString();
                }
                finally
                {
                    _inSettingsChange = inSettingsChangeOld;
                }
            }
        }

        public OptimizeTransitionSettings GetCurrentSettings()
        {
            if (string.IsNullOrEmpty(tbxRandomSeed.Text))
            {
                tbxRandomSeed.Text = ((int)DateTime.UtcNow.Ticks).ToString();
            }
            var helper = new MessageBoxHelper( ParentForm);
            if (!helper.ValidateNumberTextBox(tbxRandomSeed, null, null, out int randomSeed))
            {
                return null;
            }
            return OptimizeTransitionSettings.DEFAULT
                .ChangeMinimumNumberOfTransitions(MinNumberOfTransitions)
                .ChangeOptimizeType(OptimizeType)
                .ChangePreserveNonQuantitative(PreserveNonQuantitative)
                .ChangeCombinePointsWithSameConcentration(CombinePointsWithSameConcentration)
                .ChangeRandomSeed(randomSeed);
        }

        public int? RandomSeed
        {
            get
            {
                if (string.IsNullOrEmpty(tbxRandomSeed.Text) || !int.TryParse(tbxRandomSeed.Text, out int randomSeed))
                {
                    return null;
                }

                return randomSeed;
            }
            set
            {
                if (value.HasValue)
                {
                    tbxRandomSeed.Text = value.ToString();
                }
                else
                {
                    tbxRandomSeed.Text = string.Empty;
                }
            }
        }

        public int MinNumberOfTransitions
        {
            get
            {
                return (int)tbxMinTransitions.Value;
            }
            set
            {
                tbxMinTransitions.Value = value;
            }
        }

        public OptimizeType OptimizeType
        {
            get
            {
                return radioLOD.Checked ? OptimizeType.LOD : OptimizeType.LOQ;
            }
            set
            {
                if (value == OptimizeType.LOD)
                {
                    radioLOD.Checked = true;
                }
                else
                {
                    radioLOQ.Checked = true;
                }
            }
        }

        public bool PreserveNonQuantitative
        {
            get
            {
                return cbxPreserveNonQuantitative.Checked;
            }
            set
            {
                cbxPreserveNonQuantitative.Checked = value;
            }
        }

        public bool CombinePointsWithSameConcentration
        {
            get
            {
                return cbxAvgAtConcLevel.Checked;
            }
            set
            {
                cbxAvgAtConcLevel.Checked = value;
            }
        }

        public event EventHandler SettingsChanged;

        private void SettingsValueChange(object sender, EventArgs e)
        {
            FireSettingsChange();
        }

        private void FireSettingsChange()
        {
            if (!_inSettingsChange)
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
