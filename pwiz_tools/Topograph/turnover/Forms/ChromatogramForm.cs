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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using MSGraph;
using NHibernate;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ChromatogramForm : PeptideFileAnalysisForm
    {
        private MSGraphControl msGraphControl;
        private Chromatograms chromatograms;
        private BoxObj selectionBoxObj;
        private SelectionDragging selectionDragging;
        private IList<double> times;
        public ChromatogramForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
            gridIntensities.PeptideFileAnalysis = PeptideFileAnalysis;
            gridIntensities.SelectionChanged += (o, e) => PeptideFileAnalysisChanged();
            gridIntensities.CurrentCellChanged += (o, e) => PeptideFileAnalysisChanged();
            msGraphControl = new MSGraphControl
                                 {
                                     Dock = DockStyle.Fill
                                 };
            msGraphControl.DoubleClickEvent += msGraphControl_DoubleClickEvent;
            msGraphControl.MouseMoveEvent += msGraphControl_MouseMoveEvent;
            msGraphControl.MouseDownEvent += msGraphControl_MouseDownEvent;
            msGraphControl.MouseUpEvent += msGraphControl_MouseUpEvent;
            splitContainer1.Panel2.Controls.Add(msGraphControl);
            Text = "Chromatograms";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            PeptideFileAnalysisChanged();
        }

        private SelectionDragging GetSelectionDragging(Point pt)
        {
            if (!PeptideFileAnalysis.PeakStart.HasValue || !PeptideFileAnalysis.PeakEnd.HasValue)
            {
                return SelectionDragging.none;
            }
            const int mouseTolerance = 2;
            float xPeakStart =
                msGraphControl.GraphPane.XAxis.Scale.Transform(
                TimeFromScanIndex(PeptideFileAnalysis.PeakStart.Value));
            float xPeakEnd =
                msGraphControl.GraphPane.XAxis.Scale.Transform(
                    TimeFromScanIndex(PeptideFileAnalysis.PeakEnd.Value));
            if (Math.Abs(pt.X - xPeakStart) <= Math.Abs(pt.X - xPeakEnd))
            {
                if (Math.Abs(pt.X - xPeakStart) <= mouseTolerance)
                {
                    return SelectionDragging.start;
                }
            }
            else
            {
                if (Math.Abs(pt.X - xPeakEnd) <= mouseTolerance)
                {
                    return SelectionDragging.end;
                }
            }
            return SelectionDragging.none;
        }
        private void GetPeakStartEnd(SelectionDragging selectionDragging, double curValue, out double peakStart, out double peakEnd)
        {
            double value2 = (selectionDragging == SelectionDragging.start
                                ? TimeFromScanIndex(PeptideFileAnalysis.PeakEnd.Value)
                                : TimeFromScanIndex(PeptideFileAnalysis.PeakStart.Value));
            peakStart = Math.Min(curValue, value2);
            peakEnd = Math.Max(curValue, value2);
        }
        int ScanIndexFromTime(double time)
        {
            return PeptideFileAnalysis.ScanIndexFromTime(time);
        }

        double TimeFromScanIndex(int scanIndex)
        {
            return PeptideFileAnalysis.TimeFromScanIndex(scanIndex);
        }

        bool msGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            selectionDragging = GetSelectionDragging(e.Location);
            if (selectionDragging != SelectionDragging.none)
            {
                return true;
            }
            return false;
        }

        bool msGraphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (selectionDragging != SelectionDragging.none)
            {
                double value = msGraphControl.GraphPane.XAxis.Scale.ReverseTransform(e.X);
                double peakStart, peakEnd;
                GetPeakStartEnd(selectionDragging, value, out peakStart, out peakEnd);
                msGraphControl.GraphPane.GraphObjList.Remove(selectionBoxObj);
                selectionBoxObj = new BoxObj(peakStart, msGraphControl.GraphPane.YAxis.Scale.Max * .8,
                                                 peakEnd - peakStart,
                                                 msGraphControl.GraphPane.YAxis.Scale.Max, Color.Goldenrod,
                                                 Color.Goldenrod)
                {
                    IsClippedToChartRect = true,
                    ZOrder = ZOrder.F_BehindGrid
                };
                msGraphControl.GraphPane.GraphObjList.Add(selectionBoxObj);
                msGraphControl.Refresh();
                return true;
            }
            if (GetSelectionDragging(e.Location) != SelectionDragging.none)
            {
                sender.Cursor = Cursors.SizeWE;
                return true;
            }
            return false;
        }

        bool msGraphControl_MouseUpEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (selectionDragging == SelectionDragging.none)
            {
                return false;
            }
            int value1 = ScanIndexFromTime(msGraphControl.GraphPane.XAxis.Scale.ReverseTransform(e.X));
            int value2;
            if (selectionDragging == SelectionDragging.start)
            {
                value2 = PeptideFileAnalysis.PeakEnd.Value;
            }
            else
            {
                value2 = PeptideFileAnalysis.PeakStart.Value;
            }
            PeptideFileAnalysis.AutoFindPeak = false;
            PeptideFileAnalysis.SetPeakStartEnd(Math.Min(value1, value2), Math.Max(value1, value2), chromatograms);
            using (ISession session = PeptideFileAnalysis.Workspace.OpenWriteSession())
            {
                ITransaction transaction = session.BeginTransaction();
                PeptideFileAnalysis.Save(session);
                transaction.Commit();
            }
            selectionDragging = SelectionDragging.none;
            Recalc();
            return true;
        }

        public ExcludedMzs ExcludedMzs {
            get
            {
                if (PeptideFileAnalysis != null)
                {
                    return PeptideFileAnalysis.ExcludedMzs;
                }
                return PeptideAnalysis.ExcludedMzs;
            }
        }

        bool msGraphControl_DoubleClickEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            double x, y;
            msGraphControl.GraphPane.ReverseTransform(e.Location, out x, out y);
            int scanIndex = PeptideFileAnalysis.ScanIndexFromTime(x);
            SpectrumForm spectrumForm = new SpectrumForm(PeptideFileAnalysis.MsDataFile)
            {
                ScanIndex = scanIndex,
            };
            spectrumForm.Show(this);
            double minMz = 2000;
            double maxMz = 0;
            var selectedCharges = gridIntensities.GetSelectedCharges();
            var selectedMasses = gridIntensities.GetSelectedMasses();
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
            {
                if (selectedCharges.Count > 0 && !selectedCharges.Contains(charge))
                {
                    continue;
                }
                for (int iMass = 0; iMass < PeptideAnalysis.GetMassCount(); iMass++)
                {
                    if (selectedMasses.Count > 0 && !selectedMasses.Contains(iMass))
                    {
                        continue;
                    }
                    double mz = PeptideAnalysis.GetMzs()[charge][iMass];
                    minMz = Math.Min(minMz, mz);
                    maxMz = Math.Max(maxMz, mz);
                }
            }
            if (minMz <= maxMz)
            {
                spectrumForm.Zoom(minMz - 1, maxMz + 1);
            }
            return true;
        }

        protected override void PeptideFileAnalysisChanged()
        {
            base.PeptideFileAnalysisChanged();
            chromatograms = PeptideFileAnalysis.GetChromatograms();
            if (chromatograms != null)
            {
                times = PeptideFileAnalysis.Times ?? new List<double>();
                Recalc();
            }
        }

        private Color GetColor(MzKey mzKey)
        {
            return gridIntensities.GetColor(mzKey);
        }

        private void ShowChromatograms()
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
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge ++ )
            {
                if (selectedCharges.Count > 0 && !selectedCharges.Contains(charge))
                {
                    continue;
                }
                for (int iMass = 0; iMass < PeptideAnalysis.GetMassCount(); iMass++)
                {
                    var mzKey = new MzKey(charge, iMass);
                    if (selectedMasses.Count > 0)
                    {
                        if (!selectedMasses.Contains(iMass))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        if (ExcludedMzs.IsExcluded(mzKey))
                        {
                            continue;
                        }
                    }
                    ChromatogramData chromatogram = chromatograms.GetChild(mzKey);
                    if (chromatogram == null)
                    {
                        continue;
                    }
                    var graphItem = new ChromatogramGraphItem();
                    graphItem.Color = GetColor(mzKey);

                    PointPairList points = new PointPairList(chromatogram.Times, chromatogram.GetAccurateIntensities().ToArray());
                    graphItem.Points = points;
                    msGraphControl.AddGraphItem(msGraphControl.GraphPane, graphItem);
                }
            }
        }

        public void Recalc()
        {
            if (msGraphControl.GraphPane == null)
            {
                // TODO(nicksh): listeners should have been detached.
                return;
            }
            cbxAutoFindPeak.Checked = PeptideFileAnalysis.AutoFindPeak;
            cbxOverrideExcludedMzs.Checked = PeptideFileAnalysis.OverrideExcludedMzs;
            if (!PeptideFileAnalysis.EnsureCalculated())
            {
                return;
            }
            ShowChromatograms();
            double selStart = TimeFromScanIndex(PeptideFileAnalysis.PeakStart.Value);
            double selEnd = TimeFromScanIndex(PeptideFileAnalysis.PeakEnd.Value);
            double selectionBoxHeight = msGraphControl.GraphPane.YAxis.Scale.Max*.8;
            selectionBoxObj = new BoxObj(selStart, selectionBoxHeight,
                                             selEnd - selStart,
                                             selectionBoxHeight, Color.Goldenrod,
                                             Color.Goldenrod)
                                      {
                                          IsClippedToChartRect = true,
                                          ZOrder = ZOrder.F_BehindGrid
                                      };
            msGraphControl.GraphPane.GraphObjList.Add(selectionBoxObj);
            var backgroundLine = new LineObj(Color.DarkGray, times[0], PeptideFileAnalysis.Background, times[times.Count - 1], PeptideFileAnalysis.Background);
            msGraphControl.GraphPane.GraphObjList.Add(backgroundLine);
            double detectedLineHeight = msGraphControl.GraphPane.YAxis.Scale.Max * .9;
            double time = TimeFromScanIndex(PeptideFileAnalysis.FirstDetectedScan);
            msGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, time, detectedLineHeight, time, 0));
            if (PeptideFileAnalysis.LastDetectedScan != PeptideFileAnalysis.FirstDetectedScan)
            {
                time = TimeFromScanIndex(PeptideFileAnalysis.LastDetectedScan);
                msGraphControl.GraphPane.GraphObjList.Add(new LineObj(Color.Black, time, detectedLineHeight, time, 0));
            }
            msGraphControl.Invalidate();
        }

//        public static ChromatogramForm Show(DockableForm sibling, Workspace workspace, long peptideDataId)
//        {
//            ChromatogramForm form;
//            if (chromatogramForms.TryGetValue(peptideDataId, out form))
//            {
//                form.Activate();
//                return form;
//            }
//            ChromatogramGenerator chromatogramGenerator = new ChromatogramGenerator(workspace);
//            DbPeptideSearchResult peptideData;
//            DbMsDataFile msDataFile;
//            List<ChromatogramGenerator.Chromatogram> chromatograms;
//            using (ISession session = workspace.OpenSession())
//            {
//                peptideData = session.Get<DbPeptideSearchResult>(peptideDataId);
//                msDataFile = peptideData.MsDataFile;
//                msDataFile = session.Get<DbMsDataFile>(msDataFile.Id);
//                chromatograms = chromatogramGenerator.GetRequiredChromatograms(peptideData);
//            }
//
//            if (chromatograms.Count != 0)
//            {
//                if (!TurnoverForm.Instance.EnsureMsDataFile(msDataFile))
//                {
//                    return null;
//                }
//            }
//            chromatogramGenerator.GenerateChromatograms(peptideData.MsDataFile, chromatograms, i => true);
//            return Show(sibling, new PeptideAnalysis(workspace, peptideData));
//        }
        private enum SelectionDragging
        {
            none,
            start,
            end,
        }

        private void cbxAutoFindPeak_CheckedChanged(object sender, EventArgs e)
        {
            PeptideFileAnalysis.AutoFindPeak = cbxAutoFindPeak.Checked;
        }

        private void cbxOverrideExcludedMzs_CheckedChanged(object sender, EventArgs e)
        {
            PeptideFileAnalysis.OverrideExcludedMzs = cbxOverrideExcludedMzs.Checked;
        }
    }
    public class ChromatogramGraphItem : IMSGraphItemInfo
    {
        public string Title { get; set; }
        public Color Color { get; set; }

        public void CustomizeAxis(Axis axis)
        {
            axis.Title.FontSpec.Family = "Arial";
            axis.Title.FontSpec.Size = 14;
            axis.Color = axis.Title.FontSpec.FontColor = Color.Black;
            axis.Title.FontSpec.Border.IsVisible = false;
        }

        public PointAnnotation AnnotatePoint(PointPair point)
        {
            return new PointAnnotation();
        }

        public void AddAnnotations(MSPointList pointList, GraphObjList annotations)
        {
        }

        public MSGraphItemType GraphItemType
        {
            get { return MSGraphItemType.Chromatogram; }
        }

        public MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraphItemDrawMethod.Line; }
        }

        public void CustomizeXAxis(Axis axis)
        {
            axis.Title.Text = "Time";
            CustomizeAxis(axis);
        }

        public void CustomizeYAxis(Axis axis)
        {
            axis.Title.Text = "Intensity";
            CustomizeAxis(axis);
        }

        public IPointList Points
        {
            get;
            set;
        }
    }
}
