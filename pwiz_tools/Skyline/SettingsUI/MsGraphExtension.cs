using pwiz.MSGraph;
using System.Windows.Forms;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;

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
            Splitter.MouseUp += splitContainer1_MouseUp;
        }

        public void SetPropertiesObject(GlobalizedObject spectrumProperties)
        {
            spectrumInfoSheet.SelectedObject = spectrumProperties;
        }

        public void SetPropertiesVisibility(bool visible)
        {
            splitContainer1.Panel2Collapsed = !visible;
        }

        private void splitContainer1_MouseUp(object sender, MouseEventArgs e)
        {
            if (Width > 0)
                Settings.Default.ViewLibrarySplitPropsDist = 1 - 1.0f * e.X / Width;
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

        public void SaveSplitterWidthSetting()
        {
            if (Width > 0)
                Settings.Default.ViewLibrarySplitPropsDist = 1 - 1.0f * Splitter.SplitterDistance / Width;
        }

        public void RestoreSplitterWidthSetting()
        {
            if (Width > 0)
            {
                if (Settings.Default.ViewLibrarySplitPropsDist > 0 && Settings.Default.ViewLibrarySplitPropsDist < 1)
                    Splitter.SplitterDistance = (int)(Width * (1 - Settings.Default.ViewLibrarySplitPropsDist));
                else
                    Splitter.SplitterDistance = (int)(Width * 0.66);

            }


    }
    }
}
