/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.ChromLib.Data
{
    public class Precursor : ChromLibEntity<Precursor>
    {
        public Precursor()
        {
            Transitions = new List<Transition>();
        }
        public virtual Peptide Peptide { get; set; }
        public virtual SampleFile SampleFile { get; set; }
        public virtual string IsotopeLabel { get; set; }
        public virtual double Mz { get; set; }
        public virtual int Charge { get; set; }
        public virtual double NeutralMass { get; set; }
        public virtual string ModifiedSequence { get; set; }  // CONSIDER: bspratt/nicksh More appropriately called TextId?
        public virtual double CollisionEnergy { get; set; }
        public virtual double DeclusteringPotential { get; set; }
        public virtual double TotalArea { get; set; }
        public virtual int NumTransitions { get; set; }
        public virtual int NumPoints { get; set; }
        public virtual double AverageMassErrorPPM { get; set; }
        public virtual byte[] Chromatogram { get; set; }
        public virtual ChromatogramTimeIntensities ChromatogramData
        {
            get 
            { 
                if (null == Chromatogram)
                {
                    return null;
                }
                var expectedSize = (sizeof (float) + sizeof (float)*NumTransitions)*NumPoints;

                var uncompressedBytes = Chromatogram.Uncompress(expectedSize,false); // don't throw if the uncompressed buffer isn't the size we expected, that's normal here per NickSh

                float[] times;
                float[][] intensities;

                short[][] massErrors; // dummy variable
                int[][] scanIds; // dummy variable

                ChromatogramCache.BytesToTimeIntensities(uncompressedBytes, NumPoints, NumTransitions,
                    false, false, false, false, // for now, no mass errors or scan IDs (TODO: what about chromatogram libraries for DIA?)
                    out times, out intensities, out massErrors, out scanIds);
                return new ChromatogramTimeIntensities(times, intensities, massErrors, scanIds);
            }
            set
            {
                if (null == value)
                {
                    Chromatogram = null;
                    return;
                }
                var uncompressed = ChromatogramCache.TimeIntensitiesToBytes(value.Times, value.Intensities, value.MassErrors, value.ScanIds);
                Chromatogram = uncompressed.Compress(3);
            }
        }
        public virtual ICollection<Transition> Transitions { get; set; }

        public class ChromatogramTimeIntensities
        {
            public ChromatogramTimeIntensities(float[] times, float[][] intensities, short[][] massErrors, int[][] scanIds)
            {
                Times = times;
                Intensities = intensities;
                MassErrors = massErrors;
                ScanIds = scanIds;
            }
            public float[] Times { get; private set; }
            public float[][] Intensities { get; private set; }
            public short[][] MassErrors { get; private set; }
            public int[][] ScanIds { get; private set; }
        }
    }
}
