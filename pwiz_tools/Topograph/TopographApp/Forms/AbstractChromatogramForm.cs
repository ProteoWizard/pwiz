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
using System.Drawing;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Topograph.ui.Controls;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;
using pwiz.Topograph.ui.Properties;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class AbstractChromatogramForm : PeptideFileAnalysisForm
    {
        protected MSGraphControl msGraphControl;
        protected BoxObj selectionBoxObj;
        private SelectionDragging selectionDragging;
        protected IList<double> times;
        private Point _ptClick;
        private WorkspaceVersion _workspaceVersion;
        private bool _updatePending;
        private bool _smooth;
        private AbstractChromatogramForm() : base(null)
        {
            
        }
        protected AbstractChromatogramForm(PeptideFileAnalysis peptideFileAnalysis) : base(peptideFileAnalysis)
        {
            InitializeComponent();
            msGraphControl = new MSGraphControlEx
                                 {
                                     Dock = DockStyle.Fill
                                 };
            msGraphControl.DoubleClickEvent += msGraphControl_DoubleClickEvent;
            msGraphControl.MouseMoveEvent += msGraphControl_MouseMoveEvent;
            msGraphControl.MouseDownEvent += msGraphControl_MouseDownEvent;
            msGraphControl.MouseUpEvent += msGraphControl_MouseUpEvent;
            msGraphControl.ContextMenuBuilder += msGraphControl_ContextMenuBuilder;
            Smooth = Settings.Default.SmoothChromatograms;
        }

        void msGraphControl_ContextMenuBuilder(ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            menuStrip.Items.Insert(0, toolStripMenuItemShowSpectrum);
            menuStrip.Items.Insert(0, toolStripMenuItemSmooth);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateUi();
        }

        private SelectionDragging GetSelectionDragging(Point pt)
        {
//            if (!PeptideFileAnalysis.PeakStart.HasValue || !PeptideFileAnalysis.PeakEnd.HasValue)
//            {
//                return SelectionDragging.none;
//            }
//            const int mouseTolerance = 2;
//            float xPeakStart =
//                msGraphControl.GraphPane.XAxis.Scale.Transform(
//                TimeFromScanIndex(PeptideFileAnalysis.PeakStart.Value));
//            float xPeakEnd =
//                msGraphControl.GraphPane.XAxis.Scale.Transform(
//                    TimeFromScanIndex(PeptideFileAnalysis.PeakEnd.Value));
//            if (Math.Abs(pt.X - xPeakStart) <= Math.Abs(pt.X - xPeakEnd))
//            {
//                if (Math.Abs(pt.X - xPeakStart) <= mouseTolerance)
//                {
//                    return SelectionDragging.start;
//                }
//            }
//            else
//            {
//                if (Math.Abs(pt.X - xPeakEnd) <= mouseTolerance)
//                {
//                    return SelectionDragging.end;
//                }
//            }
            return SelectionDragging.none;
        }
        private void GetPeakStartEnd(SelectionDragging selectionDragging, double curValue, out double peakStart, out double peakEnd)
        {
            double value2 = 0;
            peakStart = Math.Min(curValue, value2);
            peakEnd = Math.Max(curValue, value2);
        }
        int ScanIndexFromTime(double time)
        {
            return PeptideFileAnalysis.Chromatograms.ScanIndexFromTime(time);
        }

        protected double TimeFromScanIndex(int scanIndex)
        {
            return PeptideFileAnalysis.Chromatograms.TimeFromScanIndex(scanIndex);
        }

        protected virtual bool msGraphControl_MouseDownEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            selectionDragging = GetSelectionDragging(e.Location);
            if (selectionDragging != SelectionDragging.none)
            {
                return true;
            }
            return false;
        }

       

        protected virtual bool msGraphControl_MouseMoveEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (selectionDragging != SelectionDragging.none)
            {
                double value = msGraphControl.GraphPane.XAxis.Scale.ReverseTransform(e.X);
                double peakStart, peakEnd;
                GetPeakStartEnd(selectionDragging, value, out peakStart, out peakEnd);
                msGraphControl.GraphPane.GraphObjList.Remove(selectionBoxObj);
                selectionBoxObj = new BoxObj(peakStart, int.MaxValue,
                                                 peakEnd - peakStart,
                                                 int.MaxValue, Color.Goldenrod,
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

        protected virtual bool msGraphControl_MouseUpEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            _ptClick = e.Location;
            if (selectionDragging == SelectionDragging.none)
            {
                return false;
            }
//            int value1 = ScanIndexFromTime(msGraphControl.GraphPane.XAxis.Scale.ReverseTransform(e.X));
//            int value2;
//            if (selectionDragging == SelectionDragging.start)
//            {
//                value2 = PeptideFileAnalysis.PeakEnd.Value;
//            }
//            else
//            {
//                value2 = PeptideFileAnalysis.PeakStart.Value;
//            }
//            PeptideFileAnalysis.AutoFindPeak = false;
//            PeptideFileAnalysis.SetPeakStartEnd(Math.Min(value1, value2), Math.Max(value1, value2), PeptideFileAnalysis.Chromatograms);
//            selectionDragging = SelectionDragging.none;
//            Recalc();
            return true;
        }

        protected virtual bool msGraphControl_DoubleClickEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            return false;
//            double x, y;
//            msGraphControl.GraphPane.ReverseTransform(e.Location, out x, out y);
//            int scanIndex = PeptideFileAnalysis.Chromatograms.ScanIndexFromTime(x);
//            DisplaySpectrum(scanIndex);
//            return true;
        }

        protected virtual ICollection<int> GetSelectedCharges()
        {
            var result = new List<int>();
            for (int i = PeptideAnalysis.MinCharge; i <= PeptideAnalysis.MaxCharge; i++)
            {
                result.Add(i);
            }
            return result;
        }

        protected virtual ICollection<int> GetSelectedMasses()
        {
            var result = new List<int>();
            int massCount = PeptideAnalysis.GetMassCount();
            for (int i = 0; i < massCount; i++)
            {
                result.Add(i);
            }
            return result;
        }

        void DisplaySpectrum(int scanIndex)
        {
            if (!TurnoverForm.Instance.EnsureMsDataFile(PeptideFileAnalysis.MsDataFile, true))
            {
                return;
            }
            SpectrumForm spectrumForm;
            spectrumForm = new SpectrumForm(PeptideFileAnalysis.MsDataFile)
            {
                ScanIndex = scanIndex,
            };
            spectrumForm.SetPeptideAnalysis(PeptideAnalysis);
            spectrumForm.Show(TopLevelControl);
            double minMz = 2000;
            double maxMz = 0;
            var selectedCharges = GetSelectedCharges();
            var selectedMasses = GetSelectedMasses();
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
                    var mzRange = PeptideAnalysis.GetMzs()[charge][iMass];
                    minMz = Math.Min(minMz, mzRange.Min);
                    maxMz = Math.Max(maxMz, mzRange.Max);
                }
            }
            if (minMz <= maxMz)
            {
                spectrumForm.Zoom(minMz - 1, maxMz + 1);
            }
        }


        protected bool Smooth
        {
            get
            {
                return _smooth;
            }
            set
            {
                if (_smooth == value)
                {
                    return;
                }
                _smooth = value;
                toolStripMenuItemSmooth.Checked = _smooth;
                UpdateUi();
            }
        }

        protected void UpdateUi()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            if (_updatePending)
            {
                return;
            }
            _updatePending = true;
            BeginInvoke(new Action(UpdateUiNow));
        }
        
        private void UpdateUiNow()
        {
            try
            {
                if (PeptideFileAnalysis == null)
                {
                    return;
                }
                _workspaceVersion = Workspace.WorkspaceVersion;
                if (PeptideFileAnalysis.GetChromatograms() == null)
                {
                    return;
                }
                times = PeptideFileAnalysis.Chromatograms.Times ?? new double[0];
                Recalc();
            }
            finally
            {
                _updatePending = false;
            }
        }

        protected override void OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
            base.OnWorkspaceEntitiesChanged(args);
            if (args.Contains(PeptideFileAnalysis) || args.Contains(PeptideFileAnalysis.Peaks) 
                || args.Contains(PeptideAnalysis) || !Equals(Workspace.WorkspaceVersion, _workspaceVersion))
            {
                UpdateUi();
            }
        }

        protected virtual void Recalc()
        {
            
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

        private void toolStripMenuItemShowSpectrum_Click(object sender, EventArgs e)
        {
            double x, y;
            msGraphControl.GraphPane.ReverseTransform(_ptClick, out x, out y);
            int scanIndex = PeptideFileAnalysis.Chromatograms.ScanIndexFromTime(x);
            DisplaySpectrum(scanIndex);
        }
        protected void SetAutoFindPeak(bool autoFindPeak)
        {
            if (autoFindPeak == PeptideFileAnalysis.AutoFindPeak)
            {
                return;
            }
            if (autoFindPeak)
            {
                PeptideFileAnalysis.SetAutoFindPeak();
            }
            else
            {
                var newPeaks = PeptideFileAnalysis.Peaks.ChangeBasePeak(PeptideFileAnalysis.Peaks.BaseTracerFormula);
                newPeaks.AutoFindPeak = false;
                PeptideFileAnalysis.SetDistributions(newPeaks);
            }

        }

        private void toolStripMenuItemSmooth_Click(object sender, EventArgs e)
        {
            Smooth = toolStripMenuItemSmooth.Checked;
        }
        protected PeptideFileAnalysis UpdateDataFileCombo(ComboBox comboBox)
        {
            var selectedItem = comboBox.SelectedItem as OverlayFileAnalysisItem;
            PeptideFileAnalysis selectedFileAnalysis = null;
            if (selectedItem != null)
            {
                selectedFileAnalysis = selectedItem.PeptideFileAnalysis;
            }
            comboBox.Items.Clear();
            comboBox.Items.Add("");
            PeptideFileAnalysis selectedPeptideFileAnalysis = null;
            foreach (var peptideFileAnalysis in PeptideAnalysis.FileAnalyses.ListChildren())
            {
                if (peptideFileAnalysis.Equals(PeptideFileAnalysis))
                {
                    continue;
                }
                comboBox.Items.Add(new OverlayFileAnalysisItem(peptideFileAnalysis));
                if (peptideFileAnalysis.Equals(selectedFileAnalysis))
                {
                    selectedPeptideFileAnalysis = peptideFileAnalysis;
                    comboBox.SelectedIndex = comboBox.Items.Count - 1;
                }
            }
            return selectedPeptideFileAnalysis;
        }

        protected class OverlayFileAnalysisItem
        {
            public OverlayFileAnalysisItem(PeptideFileAnalysis peptideFileAnalysis)
            {
                PeptideFileAnalysis = peptideFileAnalysis;
            }

            public PeptideFileAnalysis PeptideFileAnalysis { get; private set; }
            public override string ToString()
            {
                if (PeptideFileAnalysis.ValidationStatus == ValidationStatus.reject || !PeptideFileAnalysis.FirstDetectedScan.HasValue)
                {
                    return "(" + PeptideFileAnalysis.MsDataFile.Label + ")";
                }
                return PeptideFileAnalysis.MsDataFile.Label;
            }
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

        public void AddAnnotations(MSGraphPane graphPane, Graphics g, MSPointList pointList, GraphObjList annotations)
        {
        }

        public MSGraphItemType GraphItemType
        {
            get { return MSGraphItemType.chromatogram; }
        }

        public MSGraphItemDrawMethod GraphItemDrawMethod
        {
            get { return MSGraphItemDrawMethod.line; }
        }

        public void CustomizeXAxis(Axis axis)
        {
            axis.Title.Text = "Time";
            CustomizeAxis(axis);
        }

        public void CustomizeYAxis(Axis axis)
        {
            axis.Title.Text = "Intensity";
            axis.Scale.MaxAuto = true;
            CustomizeAxis(axis);
        }

        public IPointList Points
        {
            get;
            set;
        }
    }
}
