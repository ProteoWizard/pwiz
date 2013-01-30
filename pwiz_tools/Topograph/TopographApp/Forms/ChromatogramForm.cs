/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Topograph.Model;
using pwiz.Topograph.Model.Data;
using pwiz.Topograph.ui.Properties;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ChromatogramForm : AbstractChromatogramForm
    {
        public ChromatogramForm(PeptideFileAnalysis peptideFileAnalysis)
            : base(peptideFileAnalysis)
        {
            InitializeComponent();

            gridIntensities.PeptideFileAnalysis = PeptideFileAnalysis;
            gridIntensities.SelectionChanged += (o, e) => UpdateUi();
            gridIntensities.CurrentCellChanged += (o, e) => UpdateUi();
            splitContainer1.Panel2.Controls.Add(MsGraphControl);
        }

        protected void ShowChromatograms()
        {
            if (MsGraphControl.GraphPane == null)
            {
                // TODO: How can this happen?
                return;
            }
            MsGraphControl.GraphPane.GraphObjList.Clear();
            MsGraphControl.GraphPane.CurveList.Clear();
            var selectedCharges = gridIntensities.GetSelectedCharges();
            var selectedMasses = gridIntensities.GetSelectedMasses();
            var chromatograms = PeptideFileAnalysis.ChromatogramSet;
            var mzRanges = PeptideFileAnalysis.TurnoverCalculator.GetMzs(0);
            var monoisotopicMass = Workspace.GetAminoAcidFormulas().GetMonoisotopicMass(PeptideAnalysis.Peptide.Sequence);
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
            {
                if (selectedCharges.Count > 0 && !selectedCharges.Contains(charge))
                {
                    continue;
                }
                for (int iMass = 0; iMass < PeptideAnalysis.GetMassCount(); iMass++)
                {
                    var mzKey = new MzKey(charge, iMass);
                    double massDifference = mzRanges[iMass].Center - monoisotopicMass;

                    var label = massDifference.ToString("0.#");
                    if (label[0] != '-')
                    {
                        label = "+" + label;
                    }
                    label = "M" + label;
                    label += new string('+', charge);

                    if (selectedMasses.Count > 0)
                    {
                        if (!selectedMasses.Contains(iMass))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (ExcludedMasses.IsExcluded(mzKey.MassIndex))
                        {
                            continue;
                        }
                    }
                    ChromatogramSetData.Chromatogram chromatogram;
                    if (!chromatograms.Chromatograms.TryGetValue(mzKey, out chromatogram))
                    {
                        continue;
                    }
                    var graphItem = new ChromatogramGraphItem
                                        {
                                            Title = label,
                                        };
                    graphItem.Color = TracerChromatogramForm.GetColor(iMass, PeptideAnalysis.GetMassCount());
                    var mzRange = PeptideFileAnalysis.TurnoverCalculator.GetMzs(mzKey.Charge)[mzKey.MassIndex];
                    var massAccuracy = PeptideAnalysis.GetMassAccuracy();
                    var intensities = chromatogram.ChromatogramPoints.Select(point => point.GetIntensity(mzRange, massAccuracy)).ToArray();
                    if (Smooth)
                    {
                        intensities = TracerChromatograms.SavitzkyGolaySmooth(intensities);
                    }
                    PointPairList points = new PointPairList(chromatograms.Times, intensities);
                    graphItem.Points = points;
                    var lineItem = (LineItem)MsGraphControl.AddGraphItem(MsGraphControl.GraphPane, graphItem);
                    lineItem.Line.Style = TracerChromatogramForm.GetDashStyle(charge - PeptideAnalysis.MinCharge);
                    lineItem.Line.Width = Settings.Default.ChromatogramLineWidth;
                    lineItem.Label.IsVisible = false;
                }
            }
        }

        public ExcludedMasses ExcludedMasses
        {
            get
            {
                if (PeptideFileAnalysis != null)
                {
                    return PeptideFileAnalysis.ExcludedMasses;
                }
                return PeptideAnalysis.ExcludedMasses;
            }
        }

        protected override void Recalc()
        {
            cbxAutoFindPeak.Checked = PeptideFileAnalysis.AutoFindPeak;
            cbxSmooth.Checked = Smooth;
            if (MsGraphControl.GraphPane == null)
            {
                // TODO(nicksh): listeners should have been detached.
                return;
            }
            ShowChromatograms();
            if (Times.Count > 0)
            {
//                var backgroundLine = new LineObj(Color.DarkGray, times[0], PeptideFileAnalysis.Background, times[times.Count - 1], PeptideFileAnalysis.Background);
//                msGraphControl.GraphPane.GraphObjList.Add(backgroundLine);
            }
            double detectedLineHeight = MsGraphControl.GraphPane.YAxis.Scale.Max * .9;
            if (null != PeptideFileAnalysis.PsmTimes)
            {
                foreach (var time in PeptideFileAnalysis.PsmTimes.SelectMany(entry => entry.Value))
                {
                    MsGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.LightBlue, time, detectedLineHeight, time, 0)
                    {
                        IsClippedToChartRect = true,
                        ZOrder = ZOrder.E_BehindCurves,
                    });
                }
            }
            MsGraphControl.Invalidate();
        }

        protected override ICollection<int> GetSelectedCharges()
        {
            return gridIntensities.GetSelectedCharges();
        }
        protected override ICollection<int> GetSelectedMasses()
        {
            return gridIntensities.GetSelectedMasses();
        }

        private void CbxAutoFindPeakOnCheckedChanged(object sender, EventArgs e)
        {
            SetAutoFindPeak(cbxAutoFindPeak.Checked);        
        }

        private void ChromatogramFormOnResize(object sender, EventArgs e)
        {
            try
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => splitContainer1.Dock = DockStyle.Fill));
                    splitContainer1.Dock = DockStyle.None;
                }
            }
            catch (Exception exception)
            {
                Trace.TraceWarning("Exception while resizing:{0}", exception);
            }
        }

        private void ChromatogramFormOnResizeEnd(object sender, EventArgs e)
        {
        }

        private void CbxSmoothOnCheckedChanged(object sender, EventArgs e)
        {
            Smooth = cbxSmooth.Checked;
        }
    }
}
