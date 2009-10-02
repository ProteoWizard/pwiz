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
            Mz = chromatogram.Mz;
            Intensities = ArrayConverter.ToDoubles(chromatogram.Intensities.ToArray());
            PeakMzs = ArrayConverter.ToDoubles(chromatogram.PeakMzs.ToArray());
            MassAccuracy = 100000;
        }
        protected override void Load(DbChromatogram entity)
        {
            base.Load(entity);
            MzKey = entity.MzKey;
            Mz = entity.Mz;
            Intensities = entity.Intensities;
            PeakMzs = entity.PeakMzs;
            MassAccuracy = 100000;
        }
        public MzKey MzKey { get; private set; }
        public int MassIndex { get { return MzKey.MassIndex; } }
        public int Charge { get { return MzKey.Charge; } }
        public double MassAccuracy { get; private set; }
        public double Mz { get; private set; }
        public double[] Intensities { get; private set; }
        public double[] PeakMzs { get; private set; }
        public bool IsMzAccurate(int iScan)
        {
            return Math.Abs(PeakMzs[iScan] - Mz) * MassAccuracy < Mz;
        }
        public double GetMzError(int iScan)
        {
            if (Intensities[iScan] == 0)
            {
                return 0;
            }
            return MathNet.Numerics.Fn.Erf((PeakMzs[iScan] - Mz) * MassAccuracy / Mz);
        }
        public PeptideFileAnalysis PeptideFileAnalysis { get { return ((Chromatograms) Parent).PeptideFileAnalysis; } }
        public int[] ScanIndexes { get { return PeptideFileAnalysis.ScanIndexesArray; } }
        public double[] Times { get { return PeptideFileAnalysis.TimesArray; } }
    }
}
