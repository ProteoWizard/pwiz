using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Controls;
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
            splitContainer1.Panel2.Controls.Add(msGraphControl);
        }

        protected void ShowChromatograms()
        {
            if (msGraphControl.GraphPane == null)
            {
                // TODO: How can this happen?
                return;
            }
            msGraphControl.GraphPane.GraphObjList.Clear();
            msGraphControl.GraphPane.CurveList.Clear();
            var selectedCharges = gridIntensities.GetSelectedCharges();
            var selectedMasses = gridIntensities.GetSelectedMasses();
            var chromatograms = PeptideFileAnalysis.Chromatograms;
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
                        if (ExcludedMzs.IsExcluded(mzKey.MassIndex))
                        {
                            continue;
                        }
                    }
                    ChromatogramData chromatogram = chromatograms.GetChild(mzKey);
                    if (chromatogram == null)
                    {
                        continue;
                    }
                    var graphItem = new ChromatogramGraphItem
                                        {
                                            Title = label,
                                        };
                    graphItem.Color = TracerChromatogramForm.GetColor(iMass, PeptideAnalysis.GetMassCount());
                    var intensities = chromatogram.GetIntensities().ToArray();
                    if (Smooth)
                    {
                        intensities = ChromatogramData.SavitzkyGolaySmooth(intensities);
                    }
                    PointPairList points = new PointPairList(chromatogram.Times, intensities);
                    graphItem.Points = points;
                    var lineItem = (LineItem) msGraphControl.AddGraphItem(msGraphControl.GraphPane, graphItem);
                    lineItem.Line.Style = TracerChromatogramForm.GetDashStyle(charge - PeptideAnalysis.MinCharge);
                    lineItem.Line.Width = Settings.Default.ChromatogramLineWidth;
                    lineItem.Label.IsVisible = false;
                }
            }
        }

        public ExcludedMzs ExcludedMzs
        {
            get
            {
                if (PeptideFileAnalysis != null)
                {
                    return PeptideFileAnalysis.ExcludedMzs;
                }
                return PeptideAnalysis.ExcludedMzs;
            }
        }

        protected override void Recalc()
        {
            cbxAutoFindPeak.Checked = PeptideFileAnalysis.AutoFindPeak;
            cbxOverrideExcludedMzs.Checked = PeptideFileAnalysis.OverrideExcludedMzs;
            cbxSmooth.Checked = Smooth;
            if (msGraphControl.GraphPane == null)
            {
                // TODO(nicksh): listeners should have been detached.
                return;
            }
            using (PeptideFileAnalysis.GetReadLock())
            {
                ShowChromatograms();
                if (times.Count > 0)
                {
                    var backgroundLine = new LineObj(Color.DarkGray, times[0], PeptideFileAnalysis.Background, times[times.Count - 1], PeptideFileAnalysis.Background);
                    msGraphControl.GraphPane.GraphObjList.Add(backgroundLine);
                }
                double detectedLineHeight = msGraphControl.GraphPane.YAxis.Scale.Max * .9;
                if (PeptideFileAnalysis.FirstDetectedScan.HasValue)
                {
                    double time = TimeFromScanIndex(PeptideFileAnalysis.FirstDetectedScan.Value);
                    msGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, time, detectedLineHeight, time, 0));
                    if (PeptideFileAnalysis.LastDetectedScan != PeptideFileAnalysis.FirstDetectedScan)
                    {
                        time = TimeFromScanIndex(PeptideFileAnalysis.LastDetectedScan.Value);
                        msGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, time, detectedLineHeight, time, 0));
                    }
                }
                msGraphControl.Invalidate();
            }
        }

        protected void cbxOverrideExcludedMzs_CheckedChanged(object sender, EventArgs e)
        {
            PeptideFileAnalysis.OverrideExcludedMzs = cbxOverrideExcludedMzs.Checked;
        }

        protected override ICollection<int> GetSelectedCharges()
        {
            return gridIntensities.GetSelectedCharges();
        }
        protected override ICollection<int> GetSelectedMasses()
        {
            return gridIntensities.GetSelectedMasses();
        }

        private void cbxAutoFindPeak_CheckedChanged(object sender, EventArgs e)
        {
            SetAutoFindPeak(cbxAutoFindPeak.Checked);        
        }

        private void cbxOverrideExcludedMasses_CheckedChanged(object sender, EventArgs e)
        {
            PeptideFileAnalysis.OverrideExcludedMzs = cbxOverrideExcludedMzs.Checked;
        }

        private void ChromatogramForm_Resize(object sender, EventArgs e)
        {
            try
            {
                BeginInvoke(new Action(() => splitContainer1.Dock = DockStyle.Fill));
                splitContainer1.Dock = DockStyle.None;
            }
            catch
            {
                
            }
        }

        private void ChromatogramForm_ResizeEnd(object sender, EventArgs e)
        {
        }

        private void cbxSmooth_CheckedChanged(object sender, EventArgs e)
        {
            Smooth = cbxSmooth.Checked;
        }
    }
}
