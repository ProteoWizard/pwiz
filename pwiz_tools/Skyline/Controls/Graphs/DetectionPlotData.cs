using pwiz.Skyline.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;
using Settings = pwiz.Skyline.Controls.Graphs.DetectionsPlotPane.Settings;

namespace pwiz.Skyline.Controls.Graphs
{
    public class DetectionPlotData : IDisposable
    {

        private List<QData> _precursorData;
        private List<QData> _peptideData;
        private bool _isValid = false;
        private Dictionary<DetectionsPlotPane.TargetType, DataSet> _data = new Dictionary<DetectionsPlotPane.TargetType, DataSet>();

        public float QValueCutoff { get; private set; }
        public bool IsValid => _isValid;
        public int ReplicateCount { get; private set; }

        public DataSet GetData(DetectionsPlotPane.TargetType target)
        {
            return _data[target];

        }

        public List<string> ReplicateNames { get; private set; }

        public static DetectionPlotData INVALID = new DetectionPlotData(null);

        public DetectionPlotData(SrmDocument document)
        {
            if (document == null || Settings.QValueCutoff == 0 || Settings.QValueCutoff == 1 ||
                !document.Settings.HasResults) return;

            QValueCutoff = Settings.QValueCutoff;
            if (document.MoleculeTransitionGroupCount == 0 || document.PeptideCount == 0 ||
                document.MeasuredResults.Chromatograms.Count == 0)
                return;

            _precursorData = new List<QData>(document.MoleculeTransitionGroupCount);
            _peptideData = new List<QData>(document.PeptideCount);
            ReplicateCount = document.MeasuredResults.Chromatograms.Count;


            ReplicateNames = (from chromatogram in document.MeasuredResults.Chromatograms
                select chromatogram.Name).ToList();

            var qs = new List<float>(ReplicateCount);
            var _thisPeptideData = new List<List<float>>();

            foreach (var peptide in document.Peptides)
            {
                _thisPeptideData.Clear();
                //iterate over peptide's precursors
                foreach (var precursor in peptide.TransitionGroups)
                {
                    if (precursor.IsDecoy) continue;
                    qs.Clear();
                    //get q-values for precursor replicates
                    foreach (var i in Enumerable.Range(0, ReplicateCount).ToArray())
                    {
                        var chromInfo = precursor.GetSafeChromInfo(i).FirstOrDefault(c => c.OptimizationStep == 0);
                        if (chromInfo != null && chromInfo.QValue.HasValue)
                            qs.Add(chromInfo.QValue.Value);
                        else
                            qs.Add(float.NaN);
                    }

                    _precursorData.Add(new QData(precursor.Id, qs));
                    _thisPeptideData.Add(qs.ToList());
                }

                if (_thisPeptideData.Count > 0)
                {
                    _peptideData.Add(new QData(peptide.Id, 
                        Enumerable.Range(0, ReplicateCount).Select(
                            i =>
                            {
                                var min = new Statistics(_thisPeptideData.Select(lst => (double) lst[i])).Min();
                                return (float) min;
                            }).ToList()
                    ));
                }
            }

            _data[DetectionsPlotPane.TargetType.PRECURSOR] = new DataSet(_precursorData, ReplicateCount, QValueCutoff);
            _data[DetectionsPlotPane.TargetType.PEPTIDE] = new DataSet(_peptideData, ReplicateCount, QValueCutoff);

            _isValid = true;
        }

    public void Dispose()
        {
            _precursorData.Clear();
            _peptideData.Clear();
        }
        public class DataSet
        {
            private List<int> _count;
            private List<int> _cumulative;
            private List<int> _all;
            private List<QData> _data;
            private float _qValueCutoff;


            public IEnumerable<int> TargetsCount => _count;
            public IEnumerable<int> TargetsCumulative => _cumulative;
            public IEnumerable<int> TargetsAll => _all;

            public double MaxCount
            {
                get { return new Statistics(TargetsCumulative.Select(i => (double)i)).Max(); }
            }

            public DataSet(List<QData> data, int replicateCount, float qValueCutoff)
            {
                _data = data;
                _qValueCutoff = qValueCutoff;
                _count = Enumerable.Range(0, replicateCount)
                    .Select(i => data.Count(t => t.QValues[i] < qValueCutoff)).ToList();
                _cumulative = Enumerable.Range(0, replicateCount)
                    .Select(i => data.Count(t => t.MinQValues[i] < qValueCutoff)).ToList();
                _all = Enumerable.Range(0, replicateCount)
                    .Select(i => data.Count(t => t.MaxQValues[i] < qValueCutoff)).ToList();
            }

            /// <summary>
            /// Returns count of targets detected in at least minRep replicates
            /// </summary>
            public int getCountForMinReplicates(int minRep)
            {
                return _data.Count(qs => qs.QValues.Count(q => q < _qValueCutoff) >= minRep);
            }


        }

        /// <summary>
        /// List of q-values across replicates for a single target (peptide or precursor)
        /// It is also equipped with lists of running mins and maxes for this target.
        /// </summary>
        public class QData
        {
            public QData(Identity target, List<float> qValues)
            {
                Target = target;
                QValues = ImmutableList.ValueOf(qValues);

                // Calculate running mins and maxes while taking NaNs into account
                var mins = Enumerable.Repeat(float.NaN, qValues.Count).ToList();
                var maxes = Enumerable.Repeat(float.NaN, qValues.Count).ToList();
                if (!qValues.All(float.IsNaN))
                {
                    bool runningNaN = true;
                    for (int i = 0; i < qValues.Count; i++)
                    {
                        //if this and all previous values are NaN
                        if (float.IsNaN(qValues[i]))
                            if (runningNaN) continue;
                            else
                            {
                                mins[i] = mins[i - 1];
                                maxes[i] = maxes[i - 1];
                            }
                        else
                        {
                            if (runningNaN)
                            {
                                mins[i] = maxes[i] = qValues[i];
                                runningNaN = false;
                            }
                            else
                            {
                                mins[i] = (mins[i - 1] > qValues[i]) ? qValues[i] : mins[i - 1];
                                maxes[i] = (maxes[i - 1] < qValues[i]) ? qValues[i] : maxes[i - 1];
                            }
                        }
                    }
                }
                MinQValues = ImmutableList.ValueOf(mins);
                MaxQValues = ImmutableList.ValueOf(maxes);
            }
            public Identity Target { get; private set; }

            public ImmutableList<float> QValues { get; private set; }
            public ImmutableList<float> MinQValues { get; private set; }
            public ImmutableList<float> MaxQValues { get; private set; }

        }
    }

}
