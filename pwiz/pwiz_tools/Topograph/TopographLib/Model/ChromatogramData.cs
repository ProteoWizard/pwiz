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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Model
{
    public class ChromatogramData : EntityModel<DbChromatogram>
    {
        public ChromatogramData(PeptideFileAnalysis peptideFileAnalysis, DbChromatogram dbChromatogram) : base(peptideFileAnalysis.Workspace, dbChromatogram)
        {
        }
        public ChromatogramData(PeptideFileAnalysis peptideFileAnalysis, ChromatogramGenerator.Chromatogram chromatogram) : base(peptideFileAnalysis.Workspace)
        {
            MzKey = chromatogram.MzKey;
            MzRange = chromatogram.MzRange;
            Points = chromatogram.Points;
        }
        protected override void Load(DbChromatogram entity)
        {
            base.Load(entity);
            MzKey = entity.MzKey;
            MzRange = entity.MzRange;
            Points = entity.ChromatogramPoints;
        }
        public MzKey MzKey { get; private set; }
        public int MassIndex { get { return MzKey.MassIndex; } }
        public int Charge { get { return MzKey.Charge; } }
        public MzRange MzRange { get; private set; }
        public IList<ChromatogramPoint> Points { get; private set; }
        public Chromatograms Chromatograms { get { return (Chromatograms) Parent;}}
        public int[] ScanIndexes { get { return Chromatograms.ScanIndexesArray; } }
        public double[] Times { get { return Chromatograms.TimesArray; } }
        private IList<double> _intensities;
        private double _massAccuarcy;
        public IList<double> GetIntensities()
        {
            lock(this)
            {
                var massAccuracy = Chromatograms.PeptideFileAnalysis.PeptideAnalysis.GetMassAccuracy();
                if (_intensities != null && massAccuracy == _massAccuarcy)
                {
                    return _intensities;
                }
                var intensities = new double[Points.Count];
                for (int i = 0; i < Points.Count; i++)
                {
                    intensities[i] = Points[i].GetIntensity(MzRange, massAccuracy);
                }
                _massAccuarcy = massAccuracy;
                _intensities = new ReadOnlyCollection<double>(intensities);
                return _intensities;
            }
        }
        public static double[] SavitzkyGolaySmooth(IList<double> intRaw)
        {
            if (intRaw.Count < 9)
            {
                return intRaw.ToArray();
            }
            double[] intSmooth = new double[intRaw.Count];
            for (int i = 0; i < 4; i ++)
            {
                intSmooth[i] = intRaw[i];
            }
            for (int i = 4; i < intSmooth.Length - 4; i++)
            {
                double sum = 59 * intRaw[i] +
                    54 * (intRaw[i - 1] + intRaw[i + 1]) +
                    39 * (intRaw[i - 2] + intRaw[i + 2]) +
                    14 * (intRaw[i - 3] + intRaw[i + 3]) -
                    21 * (intRaw[i - 4] + intRaw[i + 4]);
                intSmooth[i] = (float)(sum / 231);
            }
            for (int i = intSmooth.Length - 4; i < intSmooth.Length; i++ )
            {
                intSmooth[i] = intRaw[i];
            }
            return intSmooth;
        }
    }
}
