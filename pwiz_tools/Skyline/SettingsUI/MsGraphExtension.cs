using pwiz.MSGraph;
using System.Windows.Forms;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.SettingsUI
{
    public partial class MsGraphExtension : UserControl
    {
        public bool PropertiesVisible => !splitContainer1.Panel2Collapsed;

        public MSGraphControl Graph => graphControl;

        public PropertyGrid PropertiesSheet => spectrumInfoSheet;

        public SplitContainer Splitter => splitContainer1;

        public MsGraphExtension()
        {
            InitializeComponent();

            SetPropertiesVisibility(false);
        }

        public void SetPropertiesObject(GlobalizedObject spectrumProperties)
        {
            spectrumInfoSheet.SelectedObject = spectrumProperties;
        }

        public void SetPropertiesVisibility(bool visible)
        {
            splitContainer1.Panel2Collapsed = !visible;
        }

        public void ShowProperties()
        {
            SetPropertiesVisibility(true);
        }

        public void HideProperties()
        {
            SetPropertiesVisibility(false);
        }

        public SpectrumProperties SpectrumProperties => spectrumInfoSheet.SelectedObject as SpectrumProperties;
    }
}
