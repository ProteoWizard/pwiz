//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using pwiz.CLI.cv;
using pwiz.CLI.data;
using pwiz.CLI.msdata;
using pwiz.CLI.analysis;

namespace seems
{
    public interface IProcessing
    {
        /// <summary>
        /// Returns a short description of the processing, e.g. "Centroiding"
        /// </summary>
        string ToString();

        /// <summary>
        /// Takes a inner SpectrumList/ChromatogramList and wraps it with a
        /// SpectrumListWrapper to cause some processing to happen to any
        /// spectra/chromatograms that are retrieved through the returned list
        /// </summary>
        ProcessableListType ProcessList<ProcessableListType>( ProcessableListType innerList ) where ProcessableListType : class;

        /// <summary>
        /// Returns a ProteoWizard ProcessingMethod object to describe the processing 
        /// </summary>
        ProcessingMethod ToProcessingMethod();

        /// <summary>
        /// Gets a controlled vocabulary identifier for the processing
        /// </summary>
        CVID CVID { get; }

        /// <summary>
        /// Gets or sets whether the processing is currently active
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Gets the panel containing controls to configure the processing,
        /// e.g. how many smoothing iterations to do
        /// </summary>
        Panel OptionsPanel { get; }

        /// <summary>
        /// Occurs when the options stored in the panel are changed.
        /// </summary>
        event EventHandler OptionsChanged;
    }

    public class ProcessingFactory
    {
        public static IProcessing ParseArgument( string arg )
        {
            if( String.IsNullOrEmpty( arg ) )
                return null;

            return null;
        }
    }

    public abstract class ProcessingBase : IProcessing
    {
        bool enabled;
        public bool Enabled { get { return enabled; } set { enabled = value; } }

        public event EventHandler OptionsChanged;
        protected void OnOptionsChanged( object sender, EventArgs e )
        {
            if( OptionsChanged != null )
                OptionsChanged( sender, e );
        }

        public ProcessingBase()
        {
            enabled = true;
        }

        public abstract ProcessableListType ProcessList<ProcessableListType>( ProcessableListType innerList ) where ProcessableListType : class;
        public abstract ProcessingMethod ToProcessingMethod();
        public abstract CVID CVID { get; } 
        public abstract Panel OptionsPanel { get; }

        internal static ProcessingPanels processingPanels = new ProcessingPanels();
    }

    public class SmoothingProcessor : ProcessingBase
    {
        Panel panel = processingPanels.smootherPanel;
        private Smoother algorithm;
        private int polynomialOrder, windowSize;
        private double lambda;

        public SmoothingProcessor()
        {
            polynomialOrder = 2;
            windowSize = 15;
            lambda = 2.0;
            algorithm = new SavitzkyGolaySmoother( polynomialOrder, windowSize );

            processingPanels.smootherAlgorithmComboBox.SelectedIndexChanged += new EventHandler( optionsChanged );
            processingPanels.smootherSavitzkyGolayPolynomialOrderTrackBar.ValueChanged += new EventHandler( optionsChanged );
            processingPanels.smootherSavitzkyGolayWindowSizeTrackBar.ValueChanged += new EventHandler( optionsChanged );
            processingPanels.smootherWhittakerLambdaTextBox.TextChanged += new EventHandler( optionsChanged );
        }

        void optionsChanged( object sender, EventArgs e )
        {
            if( panel.Tag != this )
                return;

            switch( processingPanels.smootherAlgorithmComboBox.SelectedIndex )
            {
                case 0:
                    int newOrder = processingPanels.smootherSavitzkyGolayPolynomialOrderTrackBar.Value;
                    int newSize = processingPanels.smootherSavitzkyGolayWindowSizeTrackBar.Value;
                    if( polynomialOrder != newOrder )
                    {
                        polynomialOrder = newOrder;
                        if(polynomialOrder > windowSize)
                        {
                            windowSize = processingPanels.smootherSavitzkyGolayWindowSizeTrackBar.Value = newOrder % 2 > 0 ? newOrder : newOrder +1;
                            panel.Refresh();
                        }
                    } else if( newSize != windowSize )
                    {
                        windowSize = newSize;
                        if( windowSize < polynomialOrder )
                        {
                            polynomialOrder = processingPanels.smootherSavitzkyGolayPolynomialOrderTrackBar.Value = newSize;
                            panel.Refresh();
                        }
                    }
                    algorithm = new SavitzkyGolaySmoother( polynomialOrder, windowSize );
                    break;
                case 1:
                    lambda = Convert.ToDouble( processingPanels.smootherWhittakerLambdaTextBox.Text );
                    algorithm = new WhittakerSmoother( lambda );
                    break;
            }
            OnOptionsChanged( sender, e );
        }

        public override string ToString()
        {
            if( algorithm is SavitzkyGolaySmoother )
                return "Smoother (Savitzky-Golay)";
            else if( algorithm is WhittakerSmoother )
                return "Smoother (Whittaker)";
            else
                throw new Exception( "Invalid smoothing algorithm!" );
        }

        public override ProcessingMethod ToProcessingMethod()
        {
            ProcessingMethod pm = new ProcessingMethod();
            if( algorithm is SavitzkyGolaySmoother )
                pm.userParams.Add( new UserParam( "algorithm", "Savitzky-Golay", "SeeMS" ) );
            else if( algorithm is WhittakerSmoother )
                pm.userParams.Add( new UserParam( "algorithm", "Whittaker", "SeeMS" ) );
            return pm;
        }

        public override CVID CVID { get { return CVID.MS_smoothing; } }

        public override ProcessableListType ProcessList<ProcessableListType>( ProcessableListType innerList )
        {
            if( innerList is SpectrumList )
                return new SpectrumList_Smoother( innerList as SpectrumList, algorithm, new int[] { 1, 2, 3, 4, 5, 6 } ) as ProcessableListType;
            else //if( innerList is ChromatogramList )
                return innerList;
        }

        public override Panel OptionsPanel
        {
            get
            {
                panel.Tag = null;

                if( algorithm is SavitzkyGolaySmoother )
                    processingPanels.smootherAlgorithmComboBox.SelectedIndex = 0;
                else
                    processingPanels.smootherAlgorithmComboBox.SelectedIndex = 1;

                processingPanels.smootherSavitzkyGolayParameters.Visible = algorithm is SavitzkyGolaySmoother;
                processingPanels.smootherWhittakerParameters.Visible = algorithm is WhittakerSmoother;

                processingPanels.smootherSavitzkyGolayPolynomialOrderTrackBar.Value = polynomialOrder;
                processingPanels.smootherSavitzkyGolayWindowSizeTrackBar.Value = windowSize;
                processingPanels.smootherWhittakerLambdaTextBox.Text = lambda.ToString();
                panel.Tag = this;

                return panel;
            }
        }
    }

    public class PeakPickingProcessor : ProcessingBase
    {
        Panel panel = processingPanels.peakPickerPanel;
        private PeakDetector algorithm;
        private bool preferVendorPeakPicking;
        private uint localMaximumWindowSize;

        public PeakPickingProcessor()
        {
            preferVendorPeakPicking = true;
            localMaximumWindowSize = 3;
            algorithm = new LocalMaximumPeakDetector( localMaximumWindowSize );

            processingPanels.peakPickerPreferVendorCentroidingCheckbox.CheckedChanged += new EventHandler( optionsChanged );
            processingPanels.peakPickerAlgorithmComboBox.SelectedIndexChanged += new EventHandler( optionsChanged );
            processingPanels.peakPickerLocalMaximumWindowSizeTrackBar.ValueChanged += new EventHandler( optionsChanged );
        }

        void optionsChanged( object sender, EventArgs e )
        {
            if( panel.Tag != this )
                return;

            preferVendorPeakPicking = processingPanels.peakPickerPreferVendorCentroidingCheckbox.Checked;
            switch( processingPanels.peakPickerAlgorithmComboBox.SelectedIndex )
            {
                case 0:
                    localMaximumWindowSize = (uint) processingPanels.peakPickerLocalMaximumWindowSizeTrackBar.Value;
                    algorithm = new LocalMaximumPeakDetector( localMaximumWindowSize );
                    break;
            }

            OnOptionsChanged( sender, e );
        }

        public override string ToString()
        {
            if( algorithm is LocalMaximumPeakDetector )
                return "Peak Picker (Local Maximum)";
            else
                throw new Exception( "Invalid peak detection algorithm!" );
        }

        public override ProcessingMethod ToProcessingMethod()
        {
            ProcessingMethod pm = new ProcessingMethod();
            if( algorithm is LocalMaximumPeakDetector )
                pm.userParams.Add( new UserParam( "algorithm", "Local Maximum", "SeeMS" ) );
            return pm;
        }

        public override CVID CVID { get { return CVID.MS_peak_picking; } }

        public override ProcessableListType ProcessList<ProcessableListType>( ProcessableListType innerList )
        {
            if( innerList is SpectrumList )
                return new SpectrumList_PeakPicker( innerList as SpectrumList, algorithm, preferVendorPeakPicking, new int[] { 1, 2, 3, 4, 5, 6 } ) as ProcessableListType;
            else //if( innerList is ChromatogramList )
                return innerList;
        }

        public override Panel OptionsPanel
        {
            get
            {
                panel.Tag = null;
                processingPanels.peakPickerPreferVendorCentroidingCheckbox.Checked = preferVendorPeakPicking;
                if( algorithm is LocalMaximumPeakDetector )
                    processingPanels.peakPickerAlgorithmComboBox.SelectedIndex = 0;

                processingPanels.peakPickerLocalMaximumParameters.Visible = algorithm is LocalMaximumPeakDetector;

                processingPanels.peakPickerLocalMaximumWindowSizeTrackBar.Value = (int) localMaximumWindowSize;
                panel.Tag = this;

                return panel;
            }
        }
    }

    public class ThresholdingProcessor : ProcessingBase
    {
        Panel panel = processingPanels.thresholderPanel;
        private ThresholdFilter.ThresholdingBy_Type type;
        private ThresholdFilter.ThresholdingOrientation orientation;
        private double threshold;

        public ThresholdingProcessor()
        {
            type = ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_Count;
            orientation = ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense;

            processingPanels.thresholderValueTextBox.TextChanged += new EventHandler( optionsChanged );
            processingPanels.thresholderOrientationComboBox.TextChanged += new EventHandler( optionsChanged );
            processingPanels.thresholderTypeComboBox.TextChanged += new EventHandler( optionsChanged );
        }

        public ThresholdingProcessor( ProcessingMethod method )
        {
            // parse type, orientation, and threshold from method
            UserParam param = method.userParam( "threshold" );
            if( param.type == "SeeMS" )
                threshold = (double) param.value;

            param = method.userParam( "type" );
            if( param.type == "SeeMS" )
                type = (ThresholdFilter.ThresholdingBy_Type) (int) param.value;

            param = method.userParam( "orientation" );
            if( param.type == "SeeMS" )
                orientation = (ThresholdFilter.ThresholdingOrientation) (int) param.value;
        }

        void optionsChanged( object sender, EventArgs e )
        {
            if( panel.Tag != this )
                return;

            if( !Double.TryParse( processingPanels.thresholderValueTextBox.Text, out threshold ) )
                threshold = 0;

            type = (ThresholdFilter.ThresholdingBy_Type) processingPanels.thresholderTypeComboBox.SelectedIndex;
            orientation = (ThresholdFilter.ThresholdingOrientation) processingPanels.thresholderOrientationComboBox.SelectedIndex;
            OnOptionsChanged( sender, e );
        }

        public override string ToString()
        {
            switch( type )
            {
                default:
                case ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_Count:
                case ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_CountAfterTies:
                    return String.Format( "Thresholder (keeping {0} {1} points)",
                                          threshold,
                                          orientation == ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense ? "most intense" : "least intense" );

                case ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_AbsoluteIntensity:
                    return String.Format( "Thresholder (keeping points {1} than {0})",
                                          threshold,
                                          orientation == ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense ? "more intense" : "less intense" );

                case ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_FractionOfBasePeakIntensity:
                    return String.Format( "Thresholder (keeping points {1} than {0}% of BPI)",
                                          threshold * 100,
                                          orientation == ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense ? "more intense" : "less intense" );

                case ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_FractionOfTotalIntensity:
                    return String.Format( "Thresholder (keeping points {1} than {0}% of TIC)",
                                          threshold * 100,
                                          orientation == ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense ? "more intense" : "less intense" );

                case ThresholdFilter.ThresholdingBy_Type.ThresholdingBy_FractionOfTotalIntensityCutoff:
                    return String.Format( "Thresholder (keeping points that make up the {1} {0}% of TIC)",
                                          threshold * 100,
                                          orientation == ThresholdFilter.ThresholdingOrientation.Orientation_MostIntense ? "most intense" : "least intense" );
            }
        }

        public override ProcessingMethod ToProcessingMethod()
        {
            ProcessingMethod pm = new ProcessingMethod();
            pm.userParams.Add( new UserParam( "threshold", threshold.ToString(), "SeeMS" ) );
            pm.userParams.Add( new UserParam( "type", type.ToString(), "SeeMS" ) );
            pm.userParams.Add( new UserParam( "orientation", orientation.ToString(), "SeeMS" ) );
            return pm;
        }

        public override CVID CVID { get { return CVID.MS_thresholding; } }

        public override ProcessableListType ProcessList<ProcessableListType>( ProcessableListType innerList )
        {
            if( innerList is SpectrumList )
                return new SpectrumList_PeakFilter( innerList as SpectrumList, new ThresholdFilter(type, threshold, orientation) ) as ProcessableListType;
            else //if( innerList is ChromatogramList )
                return innerList;
        }

        public override Panel OptionsPanel
        {
            get
            {
                panel.Tag = null;
                processingPanels.thresholderTypeComboBox.SelectedIndex = (int) type;
                processingPanels.thresholderOrientationComboBox.SelectedIndex = (int) orientation;
                processingPanels.thresholderValueTextBox.Text = threshold.ToString();
                panel.Tag = this;

                return panel;
            }
        }
    }

    public class ChargeStateCalculationProcessor : ProcessingBase
    {
        Panel panel = processingPanels.chargeStateCalculatorPanel;
        private bool overrideExistingCharge;
        private int minCharge;
        private int maxCharge;
        private double threshold;

        public ChargeStateCalculationProcessor()
        {
            overrideExistingCharge = false;
            minCharge = 2;
            maxCharge = 3;
            threshold = 0.9;

            processingPanels.chargeStateCalculatorOverrideExistingCheckBox.CheckedChanged += new EventHandler( optionsChanged );
            processingPanels.chargeStateCalculatorMinChargeUpDown.ValueChanged += new EventHandler( optionsChanged );
            processingPanels.chargeStateCalculatorMaxChargeUpDown.ValueChanged += new EventHandler( optionsChanged );
            processingPanels.chargeStateCalculatorIntensityFraction.TextChanged += new EventHandler( optionsChanged );
        }

        void optionsChanged( object sender, EventArgs e )
        {
            if( panel.Tag != this )
                return;

            if( !Double.TryParse( processingPanels.chargeStateCalculatorIntensityFraction.Text, out threshold ) )
                threshold = 0.9;

            overrideExistingCharge = processingPanels.chargeStateCalculatorOverrideExistingCheckBox.Checked;
            minCharge = (int) processingPanels.chargeStateCalculatorMinChargeUpDown.Value;
            maxCharge = (int) processingPanels.chargeStateCalculatorMaxChargeUpDown.Value;

            OnOptionsChanged( sender, e );
        }

        public override string ToString()
        {
           return "Charge State Calculation";
        }

        public override ProcessingMethod ToProcessingMethod()
        {
            ProcessingMethod pm = new ProcessingMethod();
            return pm;
        }

        public override CVID CVID { get { return CVID.MS_charge_state_calculation; } }

        public override ProcessableListType ProcessList<ProcessableListType>( ProcessableListType innerList )
        {
            if( innerList is SpectrumList )
                return new SpectrumList_ChargeStateCalculator( innerList as SpectrumList, overrideExistingCharge, maxCharge, minCharge, threshold ) as ProcessableListType;
            else //if( innerList is ChromatogramList )
                return innerList;
        }

        public override Panel OptionsPanel
        {
            get
            {
                panel.Tag = null;
                processingPanels.chargeStateCalculatorOverrideExistingCheckBox.Checked = overrideExistingCharge;
                processingPanels.chargeStateCalculatorMinChargeUpDown.Value = minCharge;
                processingPanels.chargeStateCalculatorMaxChargeUpDown.Value = maxCharge;
                processingPanels.chargeStateCalculatorIntensityFraction.Text = threshold.ToString();
                panel.Tag = this;

                return panel;
            }
        }
    }

    public class LockmassRefinerProcessor : ProcessingBase
    {
        Panel panel = processingPanels.lockmassRefinerPanel;
        private double mz;
        private double tolerance;

        public LockmassRefinerProcessor()
        {
            mz = 0;
            tolerance = 0.1;
            processingPanels.lockmassMzTextBox.TextChanged += new EventHandler(optionsChanged);
            processingPanels.lockmassToleranceTextBox.TextChanged += new EventHandler(optionsChanged);
        }

        void optionsChanged(object sender, EventArgs e)
        {
            if (panel.Tag != this)
                return;

            mz = Convert.ToDouble(processingPanels.lockmassMzTextBox.Text);
            tolerance = Convert.ToDouble(processingPanels.lockmassToleranceTextBox.Text);

            OnOptionsChanged(sender, e);
        }

        public override string ToString()
        {
            return String.Format("Lockmass Refiner (m/z {0} ± {1})", mz, tolerance);
        }

        public override ProcessingMethod ToProcessingMethod()
        {
            ProcessingMethod pm = new ProcessingMethod();
            pm.userParams.Add(new UserParam("mz", mz.ToString(), "SeeMS"));
            pm.userParams.Add(new UserParam("tolerance", tolerance.ToString(), "SeeMS"));
            return pm;
        }

        public override CVID CVID { get { return CVID.MS_peak_picking; } }

        public override ProcessableListType ProcessList<ProcessableListType>(ProcessableListType innerList)
        {
            if (innerList is SpectrumList)
                return new SpectrumList_LockmassRefiner(innerList as SpectrumList, mz, tolerance) as ProcessableListType;
            else //if( innerList is ChromatogramList )
                return innerList;
        }

        public override Panel OptionsPanel
        {
            get
            {
                panel.Tag = null;
                processingPanels.lockmassMzTextBox.Text = mz.ToString();
                processingPanels.lockmassToleranceTextBox.Text = tolerance.ToString();
                panel.Tag = this;

                return panel;
            }
        }
    }
}