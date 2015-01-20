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
using System.Linq;
using pwiz.CLI.cv;
using pwiz.CLI.msdata;
using pwiz.CLI.proteome;
using pwiz.CLI.chemistry;
using ZedGraph;

namespace seems
{
    public interface IAnnotation
    {
        /// <summary>
        /// Returns a short description of the annotation,
        /// e.g. "Fragmentation (PEPTIDE)"
        /// </summary>
        string ToString ();

        /// <summary>
        /// Updates the list of ZedGraph graph objects to display the annotation;
        /// the update can use the graph item, the pointList argument and/or
        /// any existing annotations to modify how this annotation is presented
        /// </summary>
        void Update (GraphItem item, pwiz.MSGraph.MSPointList pointList, GraphObjList annotations);

        /// <summary>
        /// Gets or sets whether the annotation is currently active
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Gets the panel containing controls to configure the annotation,
        /// e.g. which ion series to annotate
        /// </summary>
        Panel OptionsPanel { get; }

        /// <summary>
        /// Occurs when the options stored in the panel are changed.
        /// </summary>
        event EventHandler OptionsChanged;
    }

    public class AnnotationFactory
    {
        public static IAnnotation ParseArgument (string arg)
        {
            if (String.IsNullOrEmpty(arg))
                return null;

            try
            {
                string[] annotationArgs = arg.Split(" ".ToCharArray());
                if (annotationArgs.Length > 0 &&
                    annotationArgs[0] == "pfr") // peptide fragmentation
                {
                    if (annotationArgs.Length != 5)
                        throw new ArgumentException("peptide fragmentation annotation requires 5 arguments");

                    string sequence = annotationArgs[1];
                    int minCharge = Math.Min(1, Convert.ToInt32(annotationArgs[2]));
                    int maxCharge = Math.Max(minCharge, Convert.ToInt32(annotationArgs[3]));
                    string seriesArgs = annotationArgs[4];
                    string[] seriesList = seriesArgs.Split(",".ToCharArray());
                    bool a, b, c, x, y, z, zRadical;
                    a = b = c = x = y = z = zRadical = false;
                    foreach (string series in seriesList)
                        switch (series)
                        {
                            case "a": a = true; break;
                            case "b": b = true; break;
                            case "c": c = true; break;
                            case "x": x = true; break;
                            case "y": y = true; break;
                            case "z": z = true; break;
                            case "z*": zRadical = true; break;
                        }
                    return (IAnnotation) new PeptideFragmentationAnnotation(sequence, minCharge, maxCharge, a, b, c, x, y, z, zRadical, true, false, true, false);
                }

                return null;
            }
            catch (Exception e)
            {
                throw new ArgumentException("Caught exception parsing command-line arguments: " + e.Message);
            }
        }
    }

    public abstract class AnnotationBase : IAnnotation
    {
        bool enabled;
        public bool Enabled { get { return enabled; } set { enabled = value; } }

        public event EventHandler OptionsChanged;
        protected void OnOptionsChanged (object sender, EventArgs e)
        {
            if (OptionsChanged != null)
                OptionsChanged(sender, e);
        }

        public AnnotationBase ()
        {
            enabled = true;
        }

        public virtual void Update (GraphItem item, pwiz.MSGraph.MSPointList pointList, GraphObjList annotations)
        {
            throw new NotImplementedException();
        }

        public static AnnotationPanels annotationPanels = new AnnotationPanels();

        public abstract Panel OptionsPanel { get; }
    }

    public class PeptideFragmentationAnnotation : AnnotationBase
    {
        public enum IonSeries
        {
            Off = 0,
            Auto = 1,
            a = 2,
            b = 4,
            c = 8,
            x = 16,
            y = 32,
            z = 64,
            zRadical = 128,
            Immonium = 256,
            All = a | b | c | x | y | z | zRadical | Immonium
        }

        Map<CVID, IonSeries> ionSeriesByDissociationMethod = new Map<CVID, IonSeries>
        {
            {CVID.MS_collision_induced_dissociation, IonSeries.b | IonSeries.y},
            {CVID.MS_beam_type_collision_induced_dissociation, IonSeries.y | IonSeries.Immonium}, // HCD
            {CVID.MS_trap_type_collision_induced_dissociation, IonSeries.b | IonSeries.y},
            {CVID.MS_higher_energy_beam_type_collision_induced_dissociation, IonSeries.b | IonSeries.y | IonSeries.Immonium}, // TOF-TOF
            {CVID.MS_electron_transfer_dissociation, IonSeries.c | IonSeries.zRadical},
            {CVID.MS_electron_capture_dissociation, IonSeries.c | IonSeries.z},
        };

        // the most specific analyzer types should be listed first, i.e. a special type of TOF or ion trap
        Map<CVID, MZTolerance> mzToleranceByAnalyzer = new Map<CVID, MZTolerance>
        {
            {CVID.MS_ion_trap, new MZTolerance(0.5)},
            {CVID.MS_quadrupole, new MZTolerance(0.5)},
            {CVID.MS_FT_ICR, new MZTolerance(10, MZTolerance.Units.PPM)},
            {CVID.MS_orbitrap, new MZTolerance(15, MZTolerance.Units.PPM)},
            {CVID.MS_TOF, new MZTolerance(25, MZTolerance.Units.PPM)},
        };

        static Map<char, double> immoniumIonByResidue;
        static PeptideFragmentationAnnotation ()
        {
            immoniumIonByResidue = new Map<char, double>();
            var immoniumMod = new Formula("C-1O-1H1");
            foreach (AminoAcid aa in Enum.GetValues(typeof(AminoAcid)))
            {
                var record = AminoAcidInfo.record(aa);
                immoniumIonByResidue[record.symbol] = (record.residueFormula + immoniumMod).monoisotopicMass();
            }
        }

        Panel panel = annotationPanels.peptideFragmentationPanel;
        string sequence;
        int min, max;
        MZTolerance manualTolerance;
        MZTolerance tolerance;
        int precursorMassType; // 0=mono, 1=avg
        int fragmentMassType; // 0=mono, 1=avg
        IonSeries ionSeries;
        bool showLadders;
        bool showMisses;
        bool showLabels;
        bool showFragmentationSummary;

        public PeptideFragmentationAnnotation ()
        {
            sequence = "PEPTIDE";
            min = 1;
            max = 1;
            tolerance = manualTolerance = new MZTolerance(0.5);
            precursorMassType = 0;
            fragmentMassType = 0;
            showLadders = true;
            showMisses = false;
            showLabels = true;
            showFragmentationSummary = false;

            annotationPanels.precursorMassTypeComboBox.SelectedIndex = precursorMassType;
            annotationPanels.fragmentMassTypeComboBox.SelectedIndex = fragmentMassType;
            annotationPanels.fragmentToleranceUnitsComboBox.SelectedIndex = 0;

            annotationPanels.sequenceTextBox.TextChanged += sequenceTextBox_TextChanged;
            annotationPanels.minChargeUpDown.ValueChanged += checkBox_CheckedChanged;
            annotationPanels.maxChargeUpDown.ValueChanged += checkBox_CheckedChanged;
            annotationPanels.fragmentToleranceTextBox.TextChanged += toleranceChanged;
            annotationPanels.fragmentToleranceUnitsComboBox.SelectedIndexChanged += toleranceChanged;
            annotationPanels.precursorMassTypeComboBox.SelectedIndexChanged += checkBox_CheckedChanged;
            annotationPanels.fragmentMassTypeComboBox.SelectedIndexChanged += checkBox_CheckedChanged;
            annotationPanels.aCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.bCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.cCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.xCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.yCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.zCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.zRadicalCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.immoniumCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.showFragmentationLaddersCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.showMissesCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.showFragmentationSummaryCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.showFragmentationTableCheckBox.CheckedChanged += checkBox_CheckedChanged;
        }

        public PeptideFragmentationAnnotation (string sequence,
                                               int minCharge, int maxCharge,
                                               bool a, bool b, bool c,
                                               bool x, bool y, bool z, bool zRadical,
                                               bool showFragmentationLadders,
                                               bool showMissedFragments,
                                               bool showLabels,
                                               bool showFragmentationSummary)
            : this(sequence, minCharge, maxCharge,
                   null,
                   (a ? IonSeries.a : IonSeries.Off) |
                   (b ? IonSeries.b : IonSeries.Off) |
                   (c ? IonSeries.c : IonSeries.Off) |
                   (x ? IonSeries.x : IonSeries.Off) |
                   (y ? IonSeries.y : IonSeries.Off) |
                   (z ? IonSeries.z : IonSeries.Off) |
                   (zRadical ? IonSeries.zRadical : IonSeries.Off),
                   showFragmentationLadders,
                   showMissedFragments,
                   showLabels,
                   showFragmentationSummary)
        {
        }

        public PeptideFragmentationAnnotation (string sequence,
                                               int minCharge, int maxCharge,
                                               MZTolerance tolerance,
                                               IonSeries ionSeries,
                                               bool showFragmentationLadders,
                                               bool showMissedFragments,
                                               bool showLabels,
                                               bool showFragmentationSummary)
        {
            this.sequence = sequence;
            this.min = minCharge;
            this.max = maxCharge;
            this.manualTolerance = tolerance;
            this.tolerance = new MZTolerance(0.5);
            this.ionSeries = ionSeries;
            this.showLadders = showFragmentationLadders;
            this.showMisses = showMissedFragments;
            this.showLabels = showLabels;
            this.showFragmentationSummary = showFragmentationSummary;

            annotationPanels.precursorMassTypeComboBox.SelectedIndex = 0;
            annotationPanels.fragmentMassTypeComboBox.SelectedIndex = 0;

            if (!ReferenceEquals(tolerance, null))
                annotationPanels.fragmentToleranceUnitsComboBox.SelectedIndex = (int) tolerance.units;
            else
                annotationPanels.fragmentToleranceUnitsComboBox.SelectedIndex = 0;

            annotationPanels.sequenceTextBox.TextChanged += sequenceTextBox_TextChanged;
            annotationPanels.minChargeUpDown.ValueChanged += checkBox_CheckedChanged;
            annotationPanels.maxChargeUpDown.ValueChanged += checkBox_CheckedChanged;
            annotationPanels.fragmentToleranceTextBox.TextChanged += toleranceChanged;
            annotationPanels.fragmentToleranceUnitsComboBox.SelectedIndexChanged += toleranceChanged;
            annotationPanels.precursorMassTypeComboBox.SelectedIndexChanged += checkBox_CheckedChanged;
            annotationPanels.fragmentMassTypeComboBox.SelectedIndexChanged += checkBox_CheckedChanged;
            annotationPanels.aCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.bCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.cCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.xCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.yCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.zCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.zRadicalCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.immoniumCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.showFragmentationLaddersCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.showMissesCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.showFragmentationSummaryCheckBox.CheckedChanged += checkBox_CheckedChanged;
            annotationPanels.showFragmentationTableCheckBox.CheckedChanged += checkBox_CheckedChanged;

            annotationPanels.fragmentInfoGridView.Columns.Clear();
        }

        void sequenceTextBox_TextChanged (object sender, EventArgs e)
        {
            if (panel.Tag != this)
                return;

            sequence = annotationPanels.sequenceTextBox.Text;
            OnOptionsChanged(this, EventArgs.Empty);
        }

        void checkBox_CheckedChanged (object sender, EventArgs e)
        {
            if (panel.Tag != this)
                return;

            min = (int) annotationPanels.minChargeUpDown.Value;
            max = (int) annotationPanels.maxChargeUpDown.Value;

            precursorMassType = annotationPanels.precursorMassTypeComboBox.SelectedIndex;
            fragmentMassType = annotationPanels.fragmentMassTypeComboBox.SelectedIndex;

            showLadders = annotationPanels.showFragmentationLaddersCheckBox.Checked;
            showFragmentationSummary = annotationPanels.showFragmentationSummaryCheckBox.Checked;

            // any control which affects the columns displayed for fragments clears the column list;
            // it gets repopulated on the next call to Update()
            if (!ReferenceEquals(sender, annotationPanels.showMissesCheckBox))
                annotationPanels.fragmentInfoGridView.Columns.Clear();

            // when showLadders is checked, the ion series checkboxes act like radio buttons:
            // series from the same terminus are grouped together
            if ((showLadders || showFragmentationSummary) && sender is CheckBox)
            {
                panel.Tag = null;
                if (ReferenceEquals(sender, annotationPanels.showFragmentationLaddersCheckBox) ||
                    ReferenceEquals(sender, annotationPanels.showFragmentationSummaryCheckBox))
                {
                    // uncheck all but the first checked checkbox
                    annotationPanels.bCheckBox.Checked = !annotationPanels.aCheckBox.Checked;
                    annotationPanels.cCheckBox.Checked = !annotationPanels.aCheckBox.Checked && !annotationPanels.bCheckBox.Checked;
                    annotationPanels.yCheckBox.Checked = !annotationPanels.xCheckBox.Checked;
                    annotationPanels.zCheckBox.Checked = !annotationPanels.xCheckBox.Checked && !annotationPanels.yCheckBox.Checked;
                    annotationPanels.zRadicalCheckBox.Checked = !annotationPanels.xCheckBox.Checked && !annotationPanels.yCheckBox.Checked && !annotationPanels.zCheckBox.Checked;
                }
                else if (ReferenceEquals(sender, annotationPanels.aCheckBox))
                    annotationPanels.bCheckBox.Checked = annotationPanels.cCheckBox.Checked = false;

                else if (ReferenceEquals(sender, annotationPanels.bCheckBox))
                    annotationPanels.aCheckBox.Checked = annotationPanels.cCheckBox.Checked = false;

                else if (ReferenceEquals(sender, annotationPanels.cCheckBox))
                    annotationPanels.aCheckBox.Checked = annotationPanels.bCheckBox.Checked = false;

                else if (ReferenceEquals(sender, annotationPanels.xCheckBox))
                    annotationPanels.yCheckBox.Checked = annotationPanels.zCheckBox.Checked = annotationPanels.zRadicalCheckBox.Checked = false;

                else if (ReferenceEquals(sender, annotationPanels.yCheckBox))
                    annotationPanels.xCheckBox.Checked = annotationPanels.zCheckBox.Checked = annotationPanels.zRadicalCheckBox.Checked = false;

                else if (ReferenceEquals(sender, annotationPanels.zCheckBox))
                    annotationPanels.xCheckBox.Checked = annotationPanels.yCheckBox.Checked = annotationPanels.zRadicalCheckBox.Checked = false;

                else if (ReferenceEquals(sender, annotationPanels.zRadicalCheckBox))
                    annotationPanels.xCheckBox.Checked = annotationPanels.yCheckBox.Checked = annotationPanels.zCheckBox.Checked = false;

                panel.Tag = this;
            }

            ionSeries = IonSeries.Off;
            ionSeries |= annotationPanels.aCheckBox.Checked ? IonSeries.a : IonSeries.Off;
            ionSeries |= annotationPanels.bCheckBox.Checked ? IonSeries.b : IonSeries.Off;
            ionSeries |= annotationPanels.cCheckBox.Checked ? IonSeries.c : IonSeries.Off;
            ionSeries |= annotationPanels.xCheckBox.Checked ? IonSeries.x : IonSeries.Off;
            ionSeries |= annotationPanels.yCheckBox.Checked ? IonSeries.y : IonSeries.Off;
            ionSeries |= annotationPanels.zCheckBox.Checked ? IonSeries.z : IonSeries.Off;
            ionSeries |= annotationPanels.zRadicalCheckBox.Checked ? IonSeries.zRadical : IonSeries.Off;
            ionSeries |= annotationPanels.immoniumCheckBox.Checked ? IonSeries.Immonium : IonSeries.Off;
            showMisses = annotationPanels.showMissesCheckBox.Checked;
            OnOptionsChanged(this, EventArgs.Empty);
        }

        void toleranceChanged (object sender, EventArgs e)
        {
            if (panel.Tag != this)
                return;

            if (String.IsNullOrEmpty(annotationPanels.fragmentToleranceTextBox.Text))
                manualTolerance = null;
            else
            {
                manualTolerance = new MZTolerance();
                manualTolerance.value = Convert.ToDouble(annotationPanels.fragmentToleranceTextBox.Text);
                manualTolerance.units = (MZTolerance.Units) annotationPanels.fragmentToleranceUnitsComboBox.SelectedIndex;
            }

            OnOptionsChanged(this, EventArgs.Empty);
        }

        public override string ToString ()
        {
            return "Peptide Fragmentation (" + sequence + ")";
        }

        private int findPointWithTolerance(pwiz.MSGraph.MSPointList points, double mz, MZTolerance tolerance, bool scaled = true)
        {
            double lowestMatchMz = mz - tolerance;
            double highestMatchMz = mz + tolerance;

            var pointPairList = scaled ? points.ScaledList : points.FullList;
            int index = scaled ? points.ScaledLowerBound(mz) : points.FullLowerBound(mz);

            // if index is below the tolerance threshold, bump it to the next one or set to -1 if doing so would exceed the list size
            if (index > -1 && pointPairList[index].X < lowestMatchMz)
                index = index + 1 == pointPairList.Count ? -1 : index + 1;

            if (index == -1 || pointPairList[index].X > highestMatchMz)
                return -1;

            return index;
        }

        private void addFragment (GraphObjList list, pwiz.MSGraph.MSPointList points, string series, int length, int charge, double mz)
        {
            string label = String.Format("{0}{1}{2}",
                                         series,
                                         (length > 0 ? length.ToString() : ""),
                                         (charge > 1 ? "+" + charge.ToString() : ""));

            Color color;
            double offset;
            switch (series)
            {
                default:
                    color = series.StartsWith("immonium") ? Color.Black : Color.Gray;
                    offset = 0.1;
                    break;
                case "a": color = Color.YellowGreen; offset = 0.1; break;
                case "x": color = Color.Green; offset = 0.12; break;
                case "b": color = Color.BlueViolet; offset = 0.14; break;
                case "y": color = Color.Blue; offset = 0.16; break;
                case "c": color = Color.Orange; offset = 0.18; break;
                case "z": color = Color.OrangeRed; offset = 0.2; break;
                case "z*": color = Color.Crimson; offset = 0.4; break;
            }


            int index = -1;
            if (points != null)
                index = findPointWithTolerance(points, mz, tolerance);

            if (index == -1)
            // no matching point: present a "missed" fragment annotation
            {
                if (!showMisses)
                    return;

                color = Color.FromArgb(115, color); // transparent to emphasize miss

                if (list.Count(o => o.Location.X == mz) == 0)
                {
                    LineObj stick = new LineObj(color, mz, offset, mz, 1);
                    stick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                    stick.Line.Width = 2;
                    stick.Line.Style = System.Drawing.Drawing2D.DashStyle.Dot;
                    stick.IsClippedToChartRect = true;
                    list.Add(stick);

                    if (showLabels)
                    {
                        TextObj text = new TextObj(label, mz, offset, CoordType.XScaleYChartFraction,
                                                   AlignH.Center, AlignV.Bottom);
                        text.ZOrder = ZOrder.A_InFront;
                        text.FontSpec = new FontSpec("Arial", 12, color, false, false, false);
                        text.FontSpec.Border.IsVisible = false;
                        //text.IsClippedToChartRect = true;
                        list.Add(text);
                    }
                }
            }
            else
            // matching point found: present the point as the fragment
            {
                // try to find a closer point
                double minError = Math.Abs(points.ScaledList[index].X - mz);
                for (int i = index; i < points.ScaledCount; ++i)
                {
                    double curError = Math.Abs(points.ScaledList[i].X - mz);
                    if (curError < minError)
                    {
                        minError = curError;
                        index = i;
                    }
                    else if (points.ScaledList[i].X > mz)
                        break;
                }

                if (list.Count(o => o.Location.X == mz) == 0)
                {
                    LineObj stick = new LineObj(color, mz, points.ScaledList[index].Y, mz, 0);
                    stick.Location.CoordinateFrame = CoordType.AxisXYScale;
                    stick.Line.Width = 2;
                    stick.IsClippedToChartRect = true;
                    list.Add(stick);
                }

                if (showLabels)
                {
                    // use an existing text point annotation if possible
                    TextObj text = null;
                    minError = Double.MaxValue;
                    foreach (GraphObj obj in list)
                    {
                        if (obj is TextObj &&
                            (obj.Location.CoordinateFrame == CoordType.AxisXYScale ||
                             obj.Location.CoordinateFrame == CoordType.XScaleYChartFraction))
                        {
                            double curError = Math.Abs(obj.Location.X - points.ScaledList[index].X);
                            if (curError < 1e-4 && curError < minError)
                            {
                                minError = curError;
                                text = obj as TextObj;
                                if (!text.Text.Contains(label))
                                    text.Text = String.Format("{0}\n{1}", label, text.Text);
                            }
                        }
                    }

                    if (text == null)
                    {
                        text = new TextObj(label, mz, points.ScaledList[index].Y, CoordType.AxisXYScale,
                                            AlignH.Center, AlignV.Bottom);
                        list.Add(text);
                    }

                    text.ZOrder = ZOrder.A_InFront;
                    text.FontSpec = new FontSpec("Arial", 12, color, false, false, false);
                    text.FontSpec.Border.IsVisible = false;
                    //text.IsClippedToChartRect = true;
                }
            }
        }

        ///<summary>
        /// Takes a left mz value and right mz value and returns true if both are found in the spectrum.
        ///</summary>
        private bool aminoAcidHasFragmentEvidence (pwiz.MSGraph.MSPointList points, double leftMZ, double rightMZ)
        {
            return points != null &&
                   findPointWithTolerance(points, leftMZ, tolerance, false) > -1 &&
                   findPointWithTolerance(points, rightMZ, tolerance, false) > -1;
        }

        ///<summary>Adds user requested ion series to the fragmentation summary.</summary>
        private void addFragmentationSummary (GraphObjList list, pwiz.MSGraph.MSPointList points, Peptide peptide, Fragmentation fragmentation, string topSeries, string bottomSeries)
        {
            int ionSeriesChargeState = min;
            string sequence = peptide.sequence;
            ModificationMap modifications = peptide.modifications();

            // Select the color for the ion series.
            Color topSeriesColor;
            Color bottomSeriesColor;
            switch (topSeries)
            {
                default: topSeriesColor = Color.Gray; break;
                case "a": topSeriesColor = Color.YellowGreen; break;
                case "b": topSeriesColor = Color.BlueViolet; break;
                case "c": topSeriesColor = Color.Orange; break;
            }

            switch (bottomSeries)
            {
                default: bottomSeriesColor = Color.Gray; break;
                case "x": bottomSeriesColor = Color.Green; break;
                case "y": bottomSeriesColor = Color.Blue; break;
                case "z": bottomSeriesColor = Color.OrangeRed; break;
                case "z*": bottomSeriesColor = Color.Crimson; break;
            }

            // Ion series offsets. These offsets control where on the chart a particular ion series get displayed
            double seriesTopLeftOffset = 0.2;

            // Set the constants for starting the label paint
            double topSeriesLeftPoint = 0.025;
            double residueWidth = 0.5 / ((double) sequence.Length);
            double tickStart = residueWidth / 2.0;

            // Process all the series except c and x
            for (int i = 1; i <= sequence.Length; ++i)
            {
                double topSeriesFragmentMZ = 0.0;
                double bottomSeriesFragmentMZ = 0.0;
                switch (topSeries)
                {
                    case "a": topSeriesFragmentMZ = fragmentation.a(i, ionSeriesChargeState); break;
                    case "b": topSeriesFragmentMZ = fragmentation.b(i, ionSeriesChargeState); break;
                    case "c": if (i < sequence.Length) topSeriesFragmentMZ = fragmentation.c(i, ionSeriesChargeState); break;
                    default: continue;
                }
                switch (bottomSeries)
                {
                    case "x": if (i < sequence.Length) bottomSeriesFragmentMZ = fragmentation.x(i, ionSeriesChargeState); break;
                    case "y": bottomSeriesFragmentMZ = fragmentation.y(i, ionSeriesChargeState); break;
                    case "z": bottomSeriesFragmentMZ = fragmentation.z(i, ionSeriesChargeState); break;
                    case "z*": bottomSeriesFragmentMZ = fragmentation.zRadical(i, ionSeriesChargeState); break;
                    default: continue;
                }

                // Check if the top and bottom fragments have evidence
                bool topSeriesHasMatch = false;
                bool bottomSeriesHasMatch = false;
                if (points != null)
                {
                    topSeriesHasMatch = topSeriesFragmentMZ > 0 && findPointWithTolerance(points, topSeriesFragmentMZ, tolerance) > -1;
                    bottomSeriesHasMatch = bottomSeriesFragmentMZ > 0 && findPointWithTolerance(points, bottomSeriesFragmentMZ, tolerance) > -1;
                }

                // Build the label for the amino acid
                // Add a text box in the middle of the left and right mz boundaries
                StringBuilder label = new StringBuilder(sequence[i - 1].ToString());

                // Figure out if any mods are there on this amino acid
                double deltaMass = modifications[i - 1].monoisotopicDeltaMass();

                // Round the mod mass and append it to the amino acid as a string
                if (deltaMass > 0.0)
                {
                    label.Append("+" + Math.Round(deltaMass));
                }
                else if (deltaMass < 0.0)
                {
                    label.Append(Math.Round(deltaMass));
                }

                TextObj text = new TextObj(label.ToString(), topSeriesLeftPoint, seriesTopLeftOffset, CoordType.ChartFraction, AlignH.Center, AlignV.Center);
                text.ZOrder = ZOrder.A_InFront;
                text.FontSpec = new FontSpec("Arial", 13, Color.Black, true, false, false);
                text.FontSpec.Border.IsVisible = false;
                text.FontSpec.Fill.Color = Color.White;
                text.IsClippedToChartRect = true;
                list.Add(text);

                if (topSeriesHasMatch)
                {
                    // Paint the tick in the middle
                    LineObj tick = new LineObj(topSeriesColor, topSeriesLeftPoint + tickStart, (seriesTopLeftOffset - 0.05), topSeriesLeftPoint + tickStart, seriesTopLeftOffset);
                    tick.Location.CoordinateFrame = CoordType.ChartFraction;
                    tick.Line.Width = 2;
                    tick.IsClippedToChartRect = true;
                    list.Add(tick);
                    // Paint the hook
                    LineObj hook = new LineObj(topSeriesColor, topSeriesLeftPoint, (seriesTopLeftOffset - 0.08), topSeriesLeftPoint + tickStart, seriesTopLeftOffset - 0.05);
                    hook.Location.CoordinateFrame = CoordType.ChartFraction;
                    hook.Line.Width = 2;
                    hook.IsClippedToChartRect = true;
                    list.Add(hook);
                }

                if (bottomSeriesHasMatch)
                {
                    // Paint the tick in the middle
                    LineObj tick = new LineObj(bottomSeriesColor, topSeriesLeftPoint + tickStart - residueWidth, seriesTopLeftOffset, topSeriesLeftPoint + tickStart - residueWidth, seriesTopLeftOffset + 0.05);
                    tick.Location.CoordinateFrame = CoordType.ChartFraction;
                    tick.Line.Width = 2;
                    tick.IsClippedToChartRect = true;
                    list.Add(tick);
                    // Paint the hook
                    LineObj hook = new LineObj(bottomSeriesColor, topSeriesLeftPoint + tickStart - residueWidth, seriesTopLeftOffset + 0.05, topSeriesLeftPoint + 2.0 * tickStart - residueWidth, seriesTopLeftOffset + 0.08);
                    hook.Location.CoordinateFrame = CoordType.ChartFraction;
                    hook.Line.Width = 2;
                    hook.IsClippedToChartRect = true;
                    list.Add(hook);
                }
                // Update the next paint point
                topSeriesLeftPoint += residueWidth;
            }
        }

        ///<summary>Adds user requested ion series on top of the chart.</summary>
        private void addIonSeries (GraphObjList list, pwiz.MSGraph.MSPointList points, Peptide peptide, Fragmentation fragmentation, string topSeries, string bottomSeries)
        {
            int ionSeriesChargeState = min;
            string sequence = peptide.sequence;
            ModificationMap modifications = peptide.modifications();

            // Select the color for the ion series.
            Color topSeriesColor;
            Color bottomSeriesColor;
            switch (topSeries)
            {
                default: topSeriesColor = Color.Gray; break;
                case "a": topSeriesColor = Color.YellowGreen; break;
                case "b": topSeriesColor = Color.BlueViolet; break;
                case "c": topSeriesColor = Color.Orange; break;
            }

            switch (bottomSeries)
            {
                default: bottomSeriesColor = Color.Gray; break;
                case "x": bottomSeriesColor = Color.Green; break;
                case "y": bottomSeriesColor = Color.Blue; break;
                case "z": bottomSeriesColor = Color.OrangeRed; break;
                case "z*": bottomSeriesColor = Color.Crimson; break;
            }
            // Ion series offsets. These offsets control where on the chart a particular ion series
            // get displayed
            double topSeriesOffset = 0.025;
            double bottomSeriesOffset = 0.1;
            if (topSeries.Length == 0)
                bottomSeriesOffset = topSeriesOffset;

            double topSeriesLeftPoint = 0.0;
            double bottomSeriesLeftPoint = 0.0;

            // Step through each fragmentation site
            for (int i = 1; i <= sequence.Length; ++i)
            {
                // Paint the top series first
                double rightPoint = 0.0;
                // Figure out the right mz for this fragmentaion site
                switch (topSeries)
                {
                    case "a": rightPoint = fragmentation.a(i, ionSeriesChargeState); break;
                    case "b": rightPoint = fragmentation.b(i, ionSeriesChargeState); break;
                    case "c": if (i < sequence.Length) rightPoint = fragmentation.c(i, ionSeriesChargeState); break;
                    default: continue;
                }

                // If the left mz and right mz are different
                if (rightPoint > 0 && topSeriesLeftPoint != rightPoint)
                {
                    LineObj line;
                    // Use a dashed line format if there are fragment ions supporting this
                    // amino acid
                    if (!aminoAcidHasFragmentEvidence(points, topSeriesLeftPoint, rightPoint))
                    {
                        // Draw the line from previous mz to site to this mz in trasparent color.
                        line = new LineObj(Color.FromArgb(115, topSeriesColor), topSeriesLeftPoint, topSeriesOffset, rightPoint, topSeriesOffset);
                        line.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                    }
                    else
                    {
                        // Draw the line from previous mz to site to this mz in solid color.
                        line = new LineObj(topSeriesColor, topSeriesLeftPoint, topSeriesOffset, rightPoint, topSeriesOffset);
                    }
                    line.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                    line.Line.Width = 2;
                    line.ZOrder = ZOrder.F_BehindGrid;
                    line.IsClippedToChartRect = true;
                    list.Add(line);
                    // Add a tick demarking the fragmentation site.
                    LineObj tick = new LineObj(topSeriesColor, rightPoint, (topSeriesOffset - 0.015), rightPoint, (topSeriesOffset + 0.015));
                    tick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                    tick.Line.Width = 2;
                    tick.IsClippedToChartRect = true;
                    list.Add(tick);
                    // Add a text box in the middle of the left and right mz boundaries
                    StringBuilder label = new StringBuilder(sequence[i - 1].ToString());
                    // Figure out if any mods are there on this amino acid
                    double deltaMass = modifications[i - 1].monoisotopicDeltaMass();
                    // Round the mod mass and append it to the amino acid as a string
                    if (deltaMass > 0.0)
                    {
                        label.Append("+" + Math.Round(deltaMass));
                    }
                    else if (deltaMass < 0.0)
                    {
                        label.Append(Math.Round(deltaMass));
                    }
                    TextObj text = new TextObj(label.ToString(), (topSeriesLeftPoint + rightPoint) / 2.0,
                        topSeriesOffset, CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Center);
                    text.ZOrder = ZOrder.A_InFront;
                    text.FontSpec = new FontSpec("Arial", 13, Color.Black, true, false, false);
                    text.FontSpec.Border.IsVisible = false;
                    text.FontSpec.Fill.Color = Color.White;
                    text.IsClippedToChartRect = true;
                    list.Add(text);
                    topSeriesLeftPoint = rightPoint;
                }

                // Time to paint the bottom series
                // Get the right mz for this series
                switch (bottomSeries)
                {
                    case "x": if (i < sequence.Length) rightPoint = fragmentation.x(i, ionSeriesChargeState); break;
                    case "y": rightPoint = fragmentation.y(i, ionSeriesChargeState); break;
                    case "z": rightPoint = fragmentation.z(i, ionSeriesChargeState); break;
                    case "z*": rightPoint = fragmentation.zRadical(i, ionSeriesChargeState); break;
                    default: rightPoint = 0.0; break;
                }

                // If the left and right mz are different
                if (rightPoint > 0 && bottomSeriesLeftPoint != rightPoint)
                {
                    LineObj line;
                    // Use a dashed line format if there are fragment ions supporting this
                    // amino acid
                    if (!aminoAcidHasFragmentEvidence(points, bottomSeriesLeftPoint, rightPoint))
                    {
                        // Draw the line from previous mz to site to this mz in trasparent color.
                        line = new LineObj(Color.FromArgb(115, bottomSeriesColor), bottomSeriesLeftPoint, bottomSeriesOffset, rightPoint, bottomSeriesOffset);
                        line.Line.Style = System.Drawing.Drawing2D.DashStyle.Dash;
                    }
                    else
                    {
                        // Draw the line from previous mz to site to this mz in solid color.
                        line = new LineObj(bottomSeriesColor, bottomSeriesLeftPoint, bottomSeriesOffset, rightPoint, bottomSeriesOffset);
                    }
                    line.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                    line.Line.Width = 2;
                    line.ZOrder = ZOrder.F_BehindGrid;
                    line.IsClippedToChartRect = true;
                    list.Add(line);
                    // Draw a tick mark demarking the fragmentation site
                    LineObj tick = new LineObj(bottomSeriesColor, rightPoint, (bottomSeriesOffset - 0.015), rightPoint, (bottomSeriesOffset + 0.015));
                    tick.Location.CoordinateFrame = CoordType.XScaleYChartFraction;
                    tick.Line.Width = 2;
                    tick.IsClippedToChartRect = true;
                    list.Add(tick);
                    // Add the text label containing the amino acid
                    StringBuilder label = new StringBuilder(sequence[sequence.Length - i].ToString());
                    // Figure out if any mods are there on this amino acid
                    double deltaMass = modifications[sequence.Length - i].monoisotopicDeltaMass();
                    // Round the mod mass and append it to the amino acid as a string
                    if (deltaMass > 0.0)
                    {
                        label.Append("+" + Math.Round(deltaMass));
                    }
                    else if (deltaMass < 0.0)
                    {
                        label.Append(Math.Round(deltaMass));
                    }
                    TextObj text = new TextObj(label.ToString(), (bottomSeriesLeftPoint + rightPoint) / 2.0,
                        bottomSeriesOffset, CoordType.XScaleYChartFraction, AlignH.Center, AlignV.Center);
                    text.ZOrder = ZOrder.A_InFront;
                    text.FontSpec = new FontSpec("Arial", 13, Color.Black, true, false, false);
                    text.FontSpec.Border.IsVisible = false;
                    text.FontSpec.Fill.Color = Color.White;
                    text.IsClippedToChartRect = true;
                    list.Add(text);
                    bottomSeriesLeftPoint = rightPoint;
                }
            }
        }

        bool ionSeriesIsEnabled (IonSeries series) { return (ionSeries & series) == series; }

        public override void Update (GraphItem item, pwiz.MSGraph.MSPointList points, GraphObjList annotations)
        {
            if (!Enabled)
                return;

            if (!(item is MassSpectrum))
                return; // throw exception?

            GraphObjList list = annotations;
            Peptide peptide;

            try
            {
                peptide = new Peptide(sequence,
                    pwiz.CLI.proteome.ModificationParsing.ModificationParsing_Auto,
                    pwiz.CLI.proteome.ModificationDelimiter.ModificationDelimiter_Brackets);
            }
            catch (Exception)
            {
                return;
            }

            var spectrum = (item as MassSpectrum).Element;

            if (ReferenceEquals(manualTolerance, null))
            {
                MZTolerance maxTolerance = new MZTolerance(0.5);
                foreach (var scan in spectrum.scanList.scans.Where(o => o.instrumentConfiguration != null))
                {
                    // assume the last analyzer of the instrument configuration is responsible for the resolution
                    if (scan.instrumentConfiguration.componentList.Count == 0)
                        continue;
                    var analyzer = scan.instrumentConfiguration.componentList.Where(o => o.type == ComponentType.ComponentType_Analyzer).Last().cvParamChild(CVID.MS_mass_analyzer_type);
                    if (analyzer.cvid == CVID.CVID_Unknown)
                        continue;

                    MZTolerance analyzerTolerance = null;
                    foreach (var kvp in mzToleranceByAnalyzer)
                        if (CV.cvIsA(analyzer.cvid, kvp.Key))
                        {
                            analyzerTolerance = kvp.Value;
                            break;
                        }

                    if (analyzerTolerance == null)
                        continue;

                    if (maxTolerance.units == analyzerTolerance.units)
                    {
                        if (maxTolerance.value < analyzerTolerance.value)
                            maxTolerance = analyzerTolerance;
                    }
                    else if (analyzerTolerance.units == MZTolerance.Units.PPM)
                        maxTolerance = analyzerTolerance;
                }
                tolerance = maxTolerance;
            }
            else
                tolerance = manualTolerance;

            if (ionSeriesIsEnabled(IonSeries.Auto))
                foreach (var precursor in spectrum.precursors)
                    foreach (var method in precursor.activation.cvParamChildren(CVID.MS_dissociation_method))
                    {
                        if (!ionSeriesByDissociationMethod.Contains(method.cvid))
                            ionSeries = IonSeries.All;
                        else
                            ionSeries |= ionSeriesByDissociationMethod[method.cvid];
                    }

            int nSeries = (ionSeriesIsEnabled(IonSeries.a) ? 1 : 0) +
                          (ionSeriesIsEnabled(IonSeries.b) ? 1 : 0) +
                          (ionSeriesIsEnabled(IonSeries.c) ? 1 : 0);
            int cSeries = (ionSeriesIsEnabled(IonSeries.x) ? 1 : 0) +
                          (ionSeriesIsEnabled(IonSeries.y) ? 1 : 0) +
                          (ionSeriesIsEnabled(IonSeries.z) ? 1 : 0) +
                          (ionSeriesIsEnabled(IonSeries.zRadical) ? 1 : 0);

            showLadders = showLadders && nSeries < 2 && cSeries < 2;

            string unmodifiedSequence = peptide.sequence;
            int sequenceLength = unmodifiedSequence.Length;
            Fragmentation fragmentation = peptide.fragmentation(fragmentMassType == 0 ? true : false, true);

            for (int i = 1; i <= sequenceLength; ++i)
            {
                if (ionSeriesIsEnabled(IonSeries.Immonium))
                    addFragment(list, points, "immonium-" + unmodifiedSequence[i - 1], 0, 1, immoniumIonByResidue[unmodifiedSequence[i - 1]]);

                for (int charge = min; charge <= max; ++charge)
                {
                    if (ionSeriesIsEnabled(IonSeries.a)) addFragment(list, points, "a", i, charge, fragmentation.a(i, charge));
                    if (ionSeriesIsEnabled(IonSeries.b)) addFragment(list, points, "b", i, charge, fragmentation.b(i, charge));
                    if (ionSeriesIsEnabled(IonSeries.y)) addFragment(list, points, "y", i, charge, fragmentation.y(i, charge));
                    if (ionSeriesIsEnabled(IonSeries.z)) addFragment(list, points, "z", i, charge, fragmentation.z(i, charge));
                    if (ionSeriesIsEnabled(IonSeries.zRadical)) addFragment(list, points, "z*", i, charge, fragmentation.zRadical(i, charge));

                    if (i < sequenceLength)
                    {
                        if (ionSeriesIsEnabled(IonSeries.c)) addFragment(list, points, "c", i, charge, fragmentation.c(i, charge));
                        if (ionSeriesIsEnabled(IonSeries.x)) addFragment(list, points, "x", i, charge, fragmentation.x(i, charge));
                    }
                }
            }

            if (showLadders || showFragmentationSummary)
            {
                string topSeries = ionSeriesIsEnabled(IonSeries.a) ? "a" : ionSeriesIsEnabled(IonSeries.b) ? "b" : ionSeriesIsEnabled(IonSeries.c) ? "c" : "";
                string bottomSeries = ionSeriesIsEnabled(IonSeries.x) ? "x" : ionSeriesIsEnabled(IonSeries.y) ? "y" : ionSeriesIsEnabled(IonSeries.z) ? "z" : ionSeriesIsEnabled(IonSeries.zRadical) ? "z*" : "";
                if (showLadders)
                    addIonSeries(list, points, peptide, fragmentation, topSeries, bottomSeries);
                if (showFragmentationSummary)
                    addFragmentationSummary(list, points, peptide, fragmentation, topSeries, bottomSeries);
            }

            // fill peptide info table
            annotationPanels.peptideInfoGridView.Rows.Clear();

            if (spectrum.precursors.Count > 0 &&
                spectrum.precursors[0].selectedIons.Count > 0 &&
                spectrum.precursors[0].selectedIons[0].hasCVParam(CVID.MS_selected_ion_m_z) &&
                spectrum.precursors[0].selectedIons[0].hasCVParam(CVID.MS_charge_state))
            {
                double selectedMz = (double) spectrum.precursors[0].selectedIons[0].cvParam(CVID.MS_selected_ion_m_z).value;
                int chargeState = (int) spectrum.precursors[0].selectedIons[0].cvParam(CVID.MS_charge_state).value;
                double calculatedMass = (precursorMassType == 0 ? peptide.monoisotopicMass(chargeState) : peptide.molecularWeight(chargeState)) * chargeState;
                double observedMass = selectedMz * chargeState;
                annotationPanels.peptideInfoGridView.Rows.Add("Calculated mass:", calculatedMass, "Mass error (daltons):", observedMass - calculatedMass);
                annotationPanels.peptideInfoGridView.Rows.Add("Observed mass:", observedMass, "Mass error (ppm):", ((observedMass - calculatedMass) / calculatedMass) * 1e6);
            }
            else
                annotationPanels.peptideInfoGridView.Rows.Add("Calculated neutral mass:", precursorMassType == 0 ? peptide.monoisotopicMass() : peptide.molecularWeight());

            annotationPanels.peptideInfoGridView.Columns[1].DefaultCellStyle.Format = "F4";
            foreach (DataGridViewRow row in annotationPanels.peptideInfoGridView.Rows)
                row.Height = row.InheritedStyle.Font.Height + 2;

            // show/hide/update fragment table
            if (!annotationPanels.showFragmentationTableCheckBox.Checked || ionSeries <= IonSeries.Auto)
            {
                annotationPanels.fragmentInfoGridView.Visible = false;
                annotationPanels.fragmentInfoGridView.Rows.Clear();
                return;
            }

            annotationPanels.fragmentInfoGridView.Visible = true;
            annotationPanels.fragmentInfoGridView.SuspendLayout();

            if (annotationPanels.fragmentInfoGridView.Columns.Count == 0)
            {
                #region Add columns for fragment types
                if (ionSeriesIsEnabled(IonSeries.a))
                    for (int charge = min; charge <= max; ++charge)
                        annotationPanels.fragmentInfoGridView.Columns.Add(
                            "a" + charge.ToString(),
                            "a" + (charge > 1 ? "(+" + charge.ToString() + ")" : ""));
                if (ionSeriesIsEnabled(IonSeries.b))
                    for (int charge = min; charge <= max; ++charge)
                        annotationPanels.fragmentInfoGridView.Columns.Add(
                            "b" + charge.ToString(),
                            "b" + (charge > 1 ? "(+" + charge.ToString() + ")" : ""));
                if (ionSeriesIsEnabled(IonSeries.c))
                    for (int charge = min; charge <= max; ++charge)
                        annotationPanels.fragmentInfoGridView.Columns.Add(
                            "c" + charge.ToString(),
                            "c" + (charge > 1 ? "(+" + charge.ToString() + ")" : ""));

                annotationPanels.fragmentInfoGridView.Columns.Add("N", "");
                annotationPanels.fragmentInfoGridView.Columns.Add("Sequence", "");
                annotationPanels.fragmentInfoGridView.Columns.Add("C", "");

                if (ionSeriesIsEnabled(IonSeries.x))
                    for (int charge = min; charge <= max; ++charge)
                        annotationPanels.fragmentInfoGridView.Columns.Add(
                            "x" + charge.ToString(),
                            "x" + (charge > 1 ? "(+" + charge.ToString() + ")" : ""));
                if (ionSeriesIsEnabled(IonSeries.y))
                    for (int charge = min; charge <= max; ++charge)
                        annotationPanels.fragmentInfoGridView.Columns.Add(
                            "y" + charge.ToString(),
                            "y" + (charge > 1 ? "(+" + charge.ToString() + ")" : ""));
                if (ionSeriesIsEnabled(IonSeries.z))
                    for (int charge = min; charge <= max; ++charge)
                        annotationPanels.fragmentInfoGridView.Columns.Add(
                            "z" + charge.ToString(),
                            "z" + (charge > 1 ? "(+" + charge.ToString() + ")" : ""));
                if (ionSeriesIsEnabled(IonSeries.zRadical))
                    for (int charge = min; charge <= max; ++charge)
                        annotationPanels.fragmentInfoGridView.Columns.Add(
                            "z*" + charge.ToString(),
                            "z*" + (charge > 1 ? "(+" + charge.ToString() + ")" : ""));
                #endregion

                foreach (DataGridViewColumn column in annotationPanels.fragmentInfoGridView.Columns)
                {
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    if (column.Name != "N" && column.Name != "C" && column.Name != "Sequence")
                        column.DefaultCellStyle.Format = "F3";
                }
            }

            while (annotationPanels.fragmentInfoGridView.Rows.Count > sequenceLength)
                annotationPanels.fragmentInfoGridView.Rows.RemoveAt(annotationPanels.fragmentInfoGridView.Rows.Count - 1);
            if (sequenceLength - annotationPanels.fragmentInfoGridView.Rows.Count > 0)
                annotationPanels.fragmentInfoGridView.Rows.Add(sequenceLength - annotationPanels.fragmentInfoGridView.Rows.Count);
            for (int i = 1; i <= sequenceLength; ++i)
            {
                int cTerminalLength = sequenceLength - i + 1;
                var row = annotationPanels.fragmentInfoGridView.Rows[i - 1];
                var values = new List<object>(10);
                //var row = annotationPanels.fragmentInfoGridView.Rows.Add()];

                if (ionSeriesIsEnabled(IonSeries.a))
                    for (int charge = min; charge <= max; ++charge)
                        values.Add(fragmentation.a(i, charge));
                if (ionSeriesIsEnabled(IonSeries.b))
                    for (int charge = min; charge <= max; ++charge)
                        values.Add(fragmentation.b(i, charge));
                if (ionSeriesIsEnabled(IonSeries.c))
                    for (int charge = min; charge <= max; ++charge)
                        if (i < sequenceLength)
                            values.Add(fragmentation.c(i, charge));
                        else
                            values.Add("");

                values.Add(i);
                values.Add(unmodifiedSequence[i - 1]);
                values.Add(cTerminalLength);

                if (ionSeriesIsEnabled(IonSeries.x))
                    for (int charge = min; charge <= max; ++charge)
                        if (i > 1)
                            values.Add(fragmentation.x(cTerminalLength, charge));
                        else
                            values.Add("");
                if (ionSeriesIsEnabled(IonSeries.y))
                    for (int charge = min; charge <= max; ++charge)
                        values.Add(fragmentation.y(cTerminalLength, charge));
                if (ionSeriesIsEnabled(IonSeries.z))
                    for (int charge = min; charge <= max; ++charge)
                        values.Add(fragmentation.z(cTerminalLength, charge));
                if (ionSeriesIsEnabled(IonSeries.zRadical))
                    for (int charge = min; charge <= max; ++charge)
                        values.Add(fragmentation.zRadical(cTerminalLength, charge));
                row.SetValues(values.ToArray());
            }

            foreach (DataGridViewRow row in annotationPanels.fragmentInfoGridView.Rows)
            {
                row.Height = row.InheritedStyle.Font.Height + 2;

                foreach (DataGridViewCell cell in row.Cells)
                {
                    if (!(cell.Value is double))
                        continue;

                    double mz = (double) cell.Value;

                    if (findPointWithTolerance(points, mz, tolerance) > -1)
                        cell.Style.Font = new Font(annotationPanels.fragmentInfoGridView.Font, FontStyle.Bold);
                }
            }

            annotationPanels.fragmentInfoGridView.ResumeLayout();
        }

        public override Panel OptionsPanel
        {
            get
            {
                // disable update handlers
                panel.Tag = null;

                // toggle docking to fix docking glitches
                annotationPanels.peptideFragmentationPanel.Dock = DockStyle.None;
                annotationPanels.peptideFragmentationPanel.Dock = DockStyle.Fill;

                // set form controls based on model values
                annotationPanels.sequenceTextBox.Text = sequence;
                annotationPanels.minChargeUpDown.Value = min;
                annotationPanels.maxChargeUpDown.Value = max;
                annotationPanels.precursorMassTypeComboBox.SelectedIndex = precursorMassType;
                annotationPanels.fragmentMassTypeComboBox.SelectedIndex = fragmentMassType;
                annotationPanels.aCheckBox.Checked = ionSeriesIsEnabled(IonSeries.a);
                annotationPanels.bCheckBox.Checked = ionSeriesIsEnabled(IonSeries.b);
                annotationPanels.cCheckBox.Checked = ionSeriesIsEnabled(IonSeries.c);
                annotationPanels.xCheckBox.Checked = ionSeriesIsEnabled(IonSeries.x);
                annotationPanels.yCheckBox.Checked = ionSeriesIsEnabled(IonSeries.y);
                annotationPanels.zCheckBox.Checked = ionSeriesIsEnabled(IonSeries.z);
                annotationPanels.zRadicalCheckBox.Checked = ionSeriesIsEnabled(IonSeries.zRadical);
                annotationPanels.immoniumCheckBox.Checked = ionSeriesIsEnabled(IonSeries.Immonium);
                annotationPanels.showFragmentationLaddersCheckBox.Checked = showLadders;
                annotationPanels.showMissesCheckBox.Checked = showMisses;
                annotationPanels.showFragmentationSummaryCheckBox.Checked = showFragmentationSummary;

                // enable update handlers
                panel.Tag = this;

                return panel;
            }
        }
    }
}
