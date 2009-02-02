using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using pwiz.CLI.analysis;

namespace seems
{
    public partial class ProcessingPanels : UserControl
    {
        public ProcessingPanels()
        {
            InitializeComponent();

            thresholderTypeComboBox.Items.Add( "Count" );
            thresholderTypeComboBox.Items.Add( "Count" );
            thresholderTypeComboBox.Items.Add( "Absolute Intensity" );
            thresholderTypeComboBox.Items.Add( "Fraction of BPI"  );
            thresholderTypeComboBox.Items.Add( "Fraction of TIC" );
            thresholderTypeComboBox.Items.Add( "Fraction cutoff of TIC" );

            thresholderOrientationComboBox.Items.Add( "Most Intense" );
            thresholderOrientationComboBox.Items.Add( "Least Intense" );

            smootherAlgorithmComboBox.Items.Add( "Savitzky-Golay" );
            //smootherAlgorithmComboBox.Items.Add( "Whittaker" );

            smootherSavitzkyGolayParameters.Location = smootherParametersGroupBox.Location;
            smootherWhittakerParameters.Location = smootherParametersGroupBox.Location;

            // initialize labels
            smootherSavitzkyGolayTrackBar_ValueChanged( null, null );

            peakPickerAlgorithmComboBox.Items.Add( "Local Maximum" );

            peakPickerLocalMaximumParameters.Location = peakPickerParametersGroupBox.Location;
        }

        private void smootherSavitzkyGolayTrackBar_ValueChanged( object sender, EventArgs e )
        {
            if( ( smootherSavitzkyGolayWindowSizeTrackBar.Value % 2 ) == 0 )
                smootherSavitzkyGolayWindowSizeTrackBar.Value -= 1;
            smootherSavitzkyGolayPolynomialOrderLabel.Text = String.Format( "Polynomial order: ({0})", smootherSavitzkyGolayPolynomialOrderTrackBar.Value );
            smootherSavitzkyGolayWindowSizeLabel.Text = String.Format( "Window size: ({0})", smootherSavitzkyGolayWindowSizeTrackBar.Value );
            smootherSavitzkyGolayParameters.Refresh();
        }

        private void peakPickerLocalMaximumWindowSizeTrackBar_ValueChanged( object sender, EventArgs e )
        {
            if( ( peakPickerLocalMaximumWindowSizeTrackBar.Value % 2 ) == 0 )
                peakPickerLocalMaximumWindowSizeTrackBar.Value -= 1;
            peakPickerLocalMaximumWindowSizeLabel.Text = String.Format( "Window size: ({0})", peakPickerLocalMaximumWindowSizeTrackBar.Value );
            peakPickerLocalMaximumParameters.Refresh();
        }
    }
}
