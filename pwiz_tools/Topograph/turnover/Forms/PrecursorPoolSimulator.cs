using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MathNet.Numerics;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.Util;
using ZedGraph;

namespace pwiz.Topograph.ui.Forms
{
    public partial class PrecursorPoolSimulator : WorkspaceForm
    {
        private static Random _random = new Random(Environment.TickCount);
        private IDictionary<TracerFormula, double> _expected;
        private IList<IDictionary<TracerFormula, double>> _observedResults;
        private IList<double> _precursorEnrichmentsByPoint;
        private IList<double> _turnovers;
        private IList<IDictionary<TracerFormula, double>> _bestMatches;
        private CurveItem _scatterPlotCurve;
        private IList<TracerFormula> _tracerFormulas;


        public PrecursorPoolSimulator(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
            Turnover = 10;
            PrecursorPool = 30;
            LabelCount = 4;
            Noise = 5;
            PointCount = 1000;
        }
        public double PrecursorPool
        {
            get
            {
                return double.Parse(tbxPrecursorPool.Text);
            }
            set
            {
                tbxPrecursorPool.Text = value.ToString();
            }
        }
        public double Turnover
        {
            get
            {
                return double.Parse(tbxTurnover.Text);
            }
            set
            {
                tbxTurnover.Text = value.ToString();
            }
        }
        public int LabelCount
        {
            get
            {
                return int.Parse(tbxLabelCount.Text);
            }
            set
            {
                tbxLabelCount.Text = value.ToString();
            }
        }
        public double Noise
        {
            get
            {
                return double.Parse(tbxNoise.Text);
            }
            set
            {
                tbxNoise.Text = value.ToString();
            }
        }
        public int PointCount
        {
            get
            {
                return int.Parse(tbxPointCount.Text);
            }
            set
            {
                tbxPointCount.Text = value.ToString();
            }
        }
        private void btnGo_Click(object sender, EventArgs e)
        {
            graphDetails.GraphPane.CurveList.Clear();
            int pointCount = PointCount;
            double precursorPool = PrecursorPool;
            double turnover = Turnover;
            int labelCount = LabelCount;
            double noise = Noise;
            var points = new PointPairList();
            var tracerDef = Workspace.GetTracerDefs().FirstOrDefault(td => td.AminoAcidSymbol.HasValue);
            if (tracerDef == null)
            {
                MessageBox.Show(
                    "The simulation cannot be run because there is no amino acid tracer defined in this workspace");
                return;
            }
            
            string peptide = new string(tracerDef.AminoAcidSymbol.Value, labelCount);
            var turnoverCalculator = new TurnoverCalculator(Workspace, peptide);
            var initialTracerPercents = TracerPercentFormula.Parse("").SetElementCount(tracerDef.Name, tracerDef.InitialApe);
            var newTracerPercents = TracerPercentFormula.Parse("").SetElementCount(tracerDef.Name, precursorPool);
            var initialDistribution = turnoverCalculator.GetDistribution(initialTracerPercents);
            var newDistribution = turnoverCalculator.GetDistribution(newTracerPercents);
            var currentMixture = Mix(initialDistribution, (100.0 - turnover)/100, newDistribution, turnover/100);
            var keys = new List<TracerFormula>();
            for (int i = 0; i <= labelCount; i++)
            {
                keys.Add(TracerFormula.Parse("").SetElementCount(tracerDef.Name, i));
            }
            var bestMatches = new List<IDictionary<TracerFormula, double>>();
            var precursorEnrichments = new List<TracerPercentFormula>();
            var observedResults = new List<IDictionary<TracerFormula, double>>();

            while (points.Count < pointCount)
            {
                var noisyMixture = Perturb(keys, currentMixture, noise/100);
                double calcTurnover;
                double calcTurnoverScore;
                double calcPrecursorPool;
                IDictionary<TracerFormula, double> bestMatch;
                var calcMixture = turnoverCalculator.ComputePrecursorEnrichmentAndTurnover(noisyMixture, out calcTurnover, out calcTurnoverScore, out bestMatch);
                if (calcMixture == null)
                {
                    continue;
                }
                calcMixture.TryGetValue(tracerDef.Name, out calcPrecursorPool);
                points.Add(new PointPair(calcTurnover * 100, calcPrecursorPool));
                bestMatches.Add(bestMatch);
                precursorEnrichments.Add(calcMixture);
                observedResults.Add(noisyMixture);
            }
            graphResults.GraphPane.CurveList.Clear();
            graphResults.GraphPane.GraphObjList.Clear();
            var lineItem = graphResults.GraphPane.AddCurve(null, points, Color.Black);
            lineItem.Line.IsVisible = false;
            graphResults.GraphPane.XAxis.Title.Text = "% newly synthesized";
            graphResults.GraphPane.YAxis.Title.Text = "Precursor pool";
            graphResults.GraphPane.AxisChange();
            graphResults.Invalidate();
            dataGridView.Rows.Clear();
            var row = dataGridView.Rows[dataGridView.Rows.Add()];
            row.Cells[colQuantity.Index].Value = "% newly synthesized";
            var statsTurnover = new Statistics(points.Select(p => p.X).ToArray());
            row.Cells[colMean.Index].Value = statsTurnover.Mean();
            row.Cells[colMedian.Index].Value = statsTurnover.Median();
            row.Cells[colStdDev.Index].Value = statsTurnover.StdDev();

            row = dataGridView.Rows[dataGridView.Rows.Add()];
            var statsPrecursorPool = new Statistics(points.Select(p => p.Y).ToArray());
            row.Cells[colQuantity.Index].Value = "Precursor Pool";
            row.Cells[colMean.Index].Value = statsPrecursorPool.Mean();
            row.Cells[colMedian.Index].Value = statsPrecursorPool.Median();
            row.Cells[colStdDev.Index].Value = statsPrecursorPool.StdDev();
            _precursorEnrichmentsByPoint = points.Select(p=>p.Y).ToArray();
            _bestMatches = bestMatches;
            _observedResults = observedResults;
            _scatterPlotCurve = lineItem;
            _expected = currentMixture;
            _turnovers = points.Select(p => p.X).ToArray();
            _tracerFormulas = keys;
            ShowResult(-1);
        }

        private static IDictionary<TKey, double> Mix<TKey>(IDictionary<TKey, double> dist1, double amount1, IDictionary<TKey, double> dist2, double amount2)
        {
            var result = new Dictionary<TKey, double>();
            var allKeys = new HashSet<TKey>(dist1.Keys);
            allKeys.UnionWith(dist2.Keys);
            foreach (var key in allKeys)
            {
                double value1;
                dist1.TryGetValue(key, out value1);
                double value2;
                dist2.TryGetValue(key, out value2);
                result.Add(key, value1 * amount1 + value2 * amount2);
            }
            return result;
        }

        private static IDictionary<TKey, double> Perturb<TKey>(IEnumerable<TKey> keys, IDictionary<TKey, double> dict, double noise)
        {
            var result = new Dictionary<TKey, double>();
            foreach (var key in keys)
            {
                double value;
                dict.TryGetValue(key, out value);
                result.Add(key, value + GetRandomNoise(noise));
            }
            return result;
        }

        private static double GetRandomNoise(double magnitude)
        {
            var randomNumber = _random.NextDouble() * 2 - 1;
            return magnitude*Fn.ErfInverse(randomNumber);
        }

        private bool GraphResults_MouseUpEvent(ZedGraphControl sender, MouseEventArgs e)
        {
            if (_scatterPlotCurve == null)
            {
                return false;
            }
            double x, y;
            graphResults.GraphPane.ReverseTransform(new PointF(e.X, e.Y), out x, out y);
            CurveItem nearestCurve;
            int nearestIndex;
            if (!sender.GraphPane.FindNearestPoint(new PointF(e.X, e.Y), _scatterPlotCurve, out nearestCurve,
                                                          out nearestIndex))
            {
                return false;
            }
            ShowResult(nearestIndex);
            return false;
        }

        private void ShowResult(int resultIndex)
        {
            graphDetails.GraphPane.CurveList.Clear();
            PointPairList observedPoints = new PointPairList();
            PointPairList bestMatchPoints = new PointPairList();
            PointPairList expectedPoints = new PointPairList();
            for (int i = 0; i < _tracerFormulas.Count; i++)
            {
                var tracerFormula = _tracerFormulas[i];
                double expected;
                _expected.TryGetValue(tracerFormula, out expected);
                expectedPoints.Add(new PointPair(i, expected));
                if (resultIndex >= 0)
                {
                    double observed;
                    _observedResults[resultIndex].TryGetValue(tracerFormula, out observed);
                    observedPoints.Add(i, observed);
                    double bestMatch;
                    _bestMatches[resultIndex].TryGetValue(tracerFormula, out bestMatch);
                    bestMatchPoints.Add(i, bestMatch);
                }
            }
            if (resultIndex >= 0)
            {
                graphDetails.GraphPane.AddBar("\"Observed\" distribution", observedPoints, Color.Black);
                graphDetails.GraphPane.AddBar(
                    _turnovers[resultIndex].ToString("0") + "% newly synthesized from " +
                    _precursorEnrichmentsByPoint[resultIndex] + " precursor", 
                    bestMatchPoints,
                    Color.Blue);
            }
            graphDetails.GraphPane.AddBar("Expected distribution", expectedPoints, Color.Green);
            graphDetails.GraphPane.AxisChange();
            graphDetails.Invalidate();
        }
    }
}
