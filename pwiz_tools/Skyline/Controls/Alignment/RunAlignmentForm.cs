using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using EnvDTE;
using pwiz.Common.Collections;
using pwiz.Common.Spectra;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Spectra.Alignment;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Model.Spectra.Alignment;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Alignment
{
    public partial class RunAlignmentForm : DockableFormEx
    {
        private bool _inUpdateControls;
        private List<CurveSettings> _curves = new List<CurveSettings>();
        private ImmutableList<KeyValuePair<CalculatedValues.TypedKey, object>> _calculatedValues;
        private RetentionTimeSource _xAxis;
        public RunAlignmentForm(SkylineWindow skylineWindow)
        {
            InitializeComponent();
            SkylineWindow = skylineWindow;
            IfNotUpdating(UpdateComboCurves);
            propertyGrid.SelectedObject = _curves[0];
        }

        public SkylineWindow SkylineWindow { get; }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SkylineWindow.DocumentUIChangedEvent += SkylineWindowOnDocumentUIChangedEvent;
            UpdateUI();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            SkylineWindow.DocumentUIChangedEvent -= SkylineWindowOnDocumentUIChangedEvent;
            base.OnHandleDestroyed(e);
        }


        private void SkylineWindowOnDocumentUIChangedEvent(object sender, DocumentChangedEventArgs e)
        {
            UpdateUI();
        }

        private void IfNotUpdating(Action action)
        {
            if (!_inUpdateControls)
            {
                try
                {
                    _inUpdateControls = true;
                    action();
                }
                finally
                {
                    _inUpdateControls = false;
                }
            }
        }

        public void UpdateUI()
        {
            UpdateComboXAxis();
            UpdateGraph();
            propertyGrid.Refresh();
            bool currentCurveIsNotBlank = comboCurves.SelectedIndex >= 0 && comboCurves.SelectedIndex < _curves.Count &&
                                          !Equals(_curves[comboCurves.SelectedIndex], new CurveSettings(SkylineWindow));
            toolButtonAddCurve.Enabled = currentCurveIsNotBlank;
            toolButtonDelete.Enabled = _curves.Count > 1 || _curves.Count == 1 && currentCurveIsNotBlank;
            toolButtonUp.Enabled = comboCurves.SelectedIndex > 0;
            toolButtonDown.Enabled = comboCurves.SelectedIndex < _curves.Count - 1;
            comboCurves.Visible = _curves.Count > 1;
        }

        private void UpdateComboXAxis()
        {
            var retentionTimeSources = RetentionTimeSource.ListRetentionTimeSources(SkylineWindow.Document).ToList();
            int selectedIndex = -1;
            if (_xAxis != null)
            {
                selectedIndex = retentionTimeSources.IndexOf(_xAxis);
            }

            if (selectedIndex < 0 && retentionTimeSources.Count > 0)
            {
                selectedIndex = 0;
            }
            comboXAxis.Items.Clear();
            comboXAxis.Items.AddRange(retentionTimeSources.ToArray());
            comboXAxis.SelectedIndex = selectedIndex;
            _xAxis = selectedIndex >= 0 ? retentionTimeSources[selectedIndex] : null;
        }

        private void UpdateGraph()
        {
            var calculatedValues = new CalculatedValues(_calculatedValues);
            zedGraphControl1.GraphPane.CurveList.Clear();
            var document = SkylineWindow.Document;
            var dataX = calculatedValues.GetValue(Tuple.Create(document, _xAxis),
                () => GetRetentionTimeData(document, _xAxis));
            if (dataX != null)
            {
                for (int iCurve = 0; iCurve < _curves.Count; iCurve++)
                {
                    var result = DisplayCurve(calculatedValues, document, dataX, _curves[iCurve]);
                    if (iCurve == comboCurves.SelectedIndex)
                    {
                        //_runAlignmentProperties.Result = result;

                    }
                }
            }
            else
            {
                //_runAlignmentProperties.Result = new CurveResult("No X-axis");
            }

            zedGraphControl1.GraphPane.AxisChange();
            zedGraphControl1.Invalidate();
            _calculatedValues = ImmutableList.ValueOf(calculatedValues.GetCurrentValues());
        }

        private CurveResult DisplayCurve(CalculatedValues calculatedValues, SrmDocument document, RetentionTimeData dataX, CurveSettings curveSettings)
        {

            var dataY = calculatedValues.GetValue(Tuple.Create(document, curveSettings.YAxis),
                () => GetRetentionTimeData(document, curveSettings.YAxis));
            if (dataY == null)
            {
                return new CurveResult("No Y-axis");
            }
            if (!string.IsNullOrEmpty(curveSettings.Caption))
            {
                var legendItem = new LineItem(curveSettings.Caption)
                {
                    Symbol = new Symbol(curveSettings.CurveFormat.SymbolType, curveSettings.CurveFormat.SymbolColor)
                    {
                        Size = curveSettings.CurveFormat.SymbolSize
                    }
                };
                if (curveSettings.CurveFormat.LineDashStyle.HasValue)
                {
                    legendItem.Line.Color = curveSettings.CurveFormat.LineColor;
                    legendItem.Line.Style = curveSettings.CurveFormat.LineDashStyle.Value;
                    legendItem.Line.Width = curveSettings.CurveFormat.LineWidth;
                }
                else
                {
                    legendItem.Line.IsVisible = false;
                }

                zedGraphControl1.GraphPane.CurveList.Add(legendItem);
            }

            var similarityMatrix = GetSimilarityMatrix(calculatedValues, dataX, dataY, curveSettings.RegressionOptions);

            if (similarityMatrix == null)
            {
                return new CurveResult("Cancelled");
            }
            
            var bestPath = new PointPairList(similarityMatrix.GetBestPointCandidates(null, null).Select(pt=>new PointPair(pt.X, pt.Y)).ToList());
            if (curveSettings.RegressionOptions.RegressionMethod.HasValue && curveSettings.CurveFormat.LineDashStyle.HasValue)
            {
                var lineItem = PerformKdeAlignment(bestPath);
                lineItem.Line.Style = curveSettings.CurveFormat.LineDashStyle.Value;
                lineItem.Line.Color = curveSettings.CurveFormat.LineColor;
                lineItem.Line.Width = curveSettings.CurveFormat.LineWidth;
                zedGraphControl1.GraphPane.CurveList.Add(lineItem);
            }

            zedGraphControl1.GraphPane.CurveList.Add(
                new LineItem(null, bestPath, curveSettings.CurveFormat.SymbolColor, curveSettings.CurveFormat.SymbolType)
                {
                    Line =
                    {
                        IsVisible = false
                    },
                    Symbol =
                    {
                        Size = curveSettings.CurveFormat.SymbolSize
                    }
                });


            return new CurveResult("Success")
            {
                AlignmentResult = new AlignmentResult()
                {
                }
            };
        }

        private SimilarityGrid GetSimilarityMatrix(CalculatedValues calculatedValues, RetentionTimeData dataX,
            RetentionTimeData dataY, RegressionOptions regressionOptions)
        {
            // dataX = dataX.ChangeSpectra(dataX.Spectra.TruncateSummariesTo(regressionOptions.SpectrumDigestLength));
            // dataY = dataY.ChangeSpectra(dataY.Spectra.TruncateSummariesTo(regressionOptions.SpectrumDigestLength));
            var key = Tuple.Create(dataX, dataY);
            if (calculatedValues.TryGetValue(key, out SimilarityGrid similarityMatrix))
            {
                return similarityMatrix;
            }
            using var longWaitDlg = new LongWaitDlg();
            longWaitDlg.PerformWork(this, 1000, new Action<ILongWaitBroker>(progressMonitor =>
            {
                similarityMatrix =
                    dataX.Spectra.GetSimilarityGrid(dataY.Spectra);
                calculatedValues.AddValue(key, similarityMatrix);
            }));
            return similarityMatrix;
        }

        private LineItem PerformKdeAlignment(IList<PointPair> pointPairList)
        {
            var kdeAligner = new KdeAligner();
            kdeAligner.Train(pointPairList.Select(point=>point.X).ToArray(), pointPairList.Select(point=>point.Y).ToArray(), CancellationToken.None);
            kdeAligner.GetSmoothedValues(out var xArr, out var yArr);
            return new LineItem(null, new PointPairList(xArr, yArr), Color.Black, SymbolType.None);
        }

        private static Color GetColor(double similarity)
        {
            var value = (int) (127 + similarity * 128);
            return Color.FromArgb(value, value, value);
        }


        public static bool AreCompatible(SpectrumMetadata spectrum1, SpectrumMetadata spectrum2)
        {
            return Equals(spectrum1.GetPrecursors(0), spectrum2.GetPrecursors(0));
        }


        public RetentionTimeData GetRetentionTimeData(SrmDocument document, RetentionTimeSource retentionTimeSource)
        {
            if (retentionTimeSource == null)
            {
                return null;
            }

            
            SpectrumSummaryList spectrumMetadataList = null;
            if (retentionTimeSource.MsDataFileUri != null)
            {
                var resultFileMetadata =
                    document.Settings.MeasuredResults?.GetResultFileMetaData(retentionTimeSource.MsDataFileUri);
                if (resultFileMetadata != null)
                {
                    spectrumMetadataList = new SpectrumSummaryList(resultFileMetadata.SpectrumSummaries);
                    spectrumMetadataList = ReduceMetadataList(spectrumMetadataList);
                }
            }

            return new RetentionTimeData(Array.Empty<MeasuredRetentionTime>(), spectrumMetadataList, null);
        }

        private SpectrumSummaryList ReduceMetadataList(SpectrumSummaryList metadataList)
        {
            int? msLevel = null;
            var spectra = new List<SpectrumSummary>();
            int? digestLength = 16;
            foreach (var spectrum in metadataList)
            {
                if (msLevel.HasValue && msLevel != spectrum.SpectrumMetadata.MsLevel)
                {
                    continue;
                }

                IList<double> digest = spectrum.SummaryValue?.ToList();
                if (digest?.Count > 0)
                {
                    while (digest.Count > digestLength.Value)
                    {
                        digest = SpectrumSummary.HaarWaveletTransform(digest);
                    }
                }

                spectra.Add(new SpectrumSummary(spectrum.SpectrumMetadata, digest));
            }

            return new SpectrumSummaryList(spectra);
        }

        private void propertyGrid_PropertyValueChanged(object s, System.Windows.Forms.PropertyValueChangedEventArgs e)
        {
            IfNotUpdating(() =>
            {
                UpdateComboCurves();
                UpdateUI();
            });
        }

        private void comboCurves_SelectedIndexChanged(object sender, EventArgs e)
        {
            IfNotUpdating(() =>
            {
                if (comboCurves.SelectedIndex >= 0)
                {
                    propertyGrid.SelectedObject = _curves[comboCurves.SelectedIndex];
                    UpdateUI();
                }
            });
        }

        private void toolButtonAddCurve_Click(object sender, EventArgs e)
        {
            IfNotUpdating(() =>
            {
                _curves.Add(new CurveSettings(SkylineWindow));
                UpdateComboCurves();
                comboCurves.SelectedIndex = _curves.Count - 1;
                propertyGrid.SelectedObject = _curves[comboCurves.SelectedIndex];
                UpdateUI();
            });
        }

        private void toolButtonDelete_Click(object sender, EventArgs e)
        {
            IfNotUpdating(() =>
            {
                if (comboCurves.SelectedIndex >= 0 && comboCurves.SelectedIndex < _curves.Count)
                {
                    _curves.RemoveAt(comboCurves.SelectedIndex);
                    if (_curves.Count == 0)
                    {
                        _curves.Add(new CurveSettings(SkylineWindow));
                    }
                }

                UpdateComboCurves();
                propertyGrid.SelectedObject = _curves[comboCurves.SelectedIndex];
                UpdateUI();
            });
        }

        private void UpdateComboCurves()
        {
            Assume.IsTrue(_inUpdateControls);
            if (_curves.Count == 0)
            {
                _curves.Add(new CurveSettings(SkylineWindow));
            }

            int newSelectedIndex = Math.Max(0, Math.Min(_curves.Count - 1, comboCurves.SelectedIndex));
            comboCurves.Items.Clear();
            comboCurves.Items.AddRange(_curves.Select(curve=>curve.ToString()).ToArray());
            comboCurves.SelectedIndex = newSelectedIndex;
        }

        private void toolButtonUp_Click(object sender, EventArgs e)
        {
            IfNotUpdating(() =>
            {
                int index = comboCurves.SelectedIndex;
                if (index <= 0 || index >= _curves.Count)
                {
                    return;
                }

                var curve = _curves[index];
                _curves.RemoveAt(index);
                _curves.Insert(index - 1, curve);
                comboCurves.SelectedIndex = index - 1;
                UpdateComboCurves();
                UpdateUI();
            });
        }

        private void toolButtonDown_Click(object sender, EventArgs e)
        {
            IfNotUpdating(() =>
            {
                int index = comboCurves.SelectedIndex;
                if (index < 0 || index >= _curves.Count - 1)
                {
                    return;
                }

                var curve = _curves[index];
                _curves.RemoveAt(index);
                _curves.Insert(index + 1, curve);
                comboCurves.SelectedIndex = index + 1;
                UpdateComboCurves();
                UpdateUI();
            });
        }

        private void comboXAxis_SelectedIndexChanged(object sender, EventArgs e)
        {
            IfNotUpdating(() =>
            {
                _xAxis = (RetentionTimeSource) comboXAxis.SelectedItem;
                UpdateUI();
            });
        }
    }
}
