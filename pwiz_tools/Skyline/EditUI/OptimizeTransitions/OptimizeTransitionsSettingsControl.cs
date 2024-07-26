using System;
using System.Windows.Forms;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;

namespace pwiz.Skyline.EditUI.OptimizeTransitions
{
    public partial class OptimizeTransitionsSettingsControl : UserControl
    {
        private bool _inSettingsChange;
        private OptimizeTransitionSettings _currentSettings = OptimizeTransitionSettings.DEFAULT;
        private bool _syncWithGlobalSettings = true;
        public OptimizeTransitionsSettingsControl()
        {
            InitializeComponent();
            FireSettingsChange(false);
        }

        public OptimizeTransitionSettings CurrentSettings
        {
            get
            {
                return _currentSettings;
            }
        }

        public bool SyncWithGlobalSettings
        {
            get
            {
                return _syncWithGlobalSettings;
            }
            set
            {
                _syncWithGlobalSettings = value;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            OptimizeTransitionSettings.GlobalSettingsChange += OptimizeTransitionSettings_OnGlobalSettingsChange;
            if (SyncWithGlobalSettings)
            {
                OptimizeTransitionSettings_OnGlobalSettingsChange();
            }
        }

        private void OptimizeTransitionSettings_OnGlobalSettingsChange()
        {
            if (!_syncWithGlobalSettings || _inSettingsChange)
            {
                return;
            }

            if (!Equals(_currentSettings, OptimizeTransitionSettings.GlobalSettings))
            {
                _currentSettings = OptimizeTransitionSettings.GlobalSettings;
                FireSettingsChange(false);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            OptimizeTransitionSettings.GlobalSettingsChange -= OptimizeTransitionSettings_OnGlobalSettingsChange;
            base.OnHandleDestroyed(e);
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

        public event EventHandler SettingsChange;

        private void SettingsValueChange(object sender, EventArgs e)
        {
            FireSettingsChange(true);
        }

        private void FireSettingsChange(bool fromUi)
        {
            var settings = _currentSettings;
            if (fromUi)
            {
                settings = settings
                    .ChangeMinimumNumberOfTransitions((int)tbxMinTransitions.Value)
                    .ChangeOptimizeType(radioLOD.Checked ? OptimizeType.LOD : OptimizeType.LOQ)
                    .ChangePreserveNonQuantitative(cbxPreserveNonQuantitative.Checked);
            }

            _currentSettings = settings;
            if (_inSettingsChange)
            {
                return;
            }

            try
            {
                _inSettingsChange = true;
                if (!fromUi)
                {
                    tbxMinTransitions.Value = settings.MinimumNumberOfTransitions;
                    radioLOD.Checked = settings.OptimizeType == OptimizeType.LOD;
                    radioLOQ.Checked = settings.OptimizeType == OptimizeType.LOQ;
                    cbxPreserveNonQuantitative.Checked = settings.PreserveNonQuantitative;
                }

                if (SyncWithGlobalSettings)
                {
                    OptimizeTransitionSettings.GlobalSettings = settings;
                }
                SettingsChange?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _inSettingsChange = false;
            }
        }
    }
}
