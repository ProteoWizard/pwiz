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
using System.IO;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;
using zlib;

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
        public virtual string ModifiedSequence { get; set; }
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
                byte[] uncompressedBytes;
                if (expectedSize == Chromatogram.Length)
                {
                    uncompressedBytes = Chromatogram;
                }
                else
                {
                    var memoryStream = new MemoryStream();
                    ZOutputStream zstream = new ZOutputStream(memoryStream);
                    zstream.Write(Chromatogram, 0, Chromatogram.Length);
                    zstream.finish();
                    uncompressedBytes = memoryStream.ToArray();
                }
                float[] times;
                float[][] intensities;
                short[][] massErrors;
                ChromatogramCache.BytesToTimeIntensities(uncompressedBytes, NumPoints, NumTransitions, false, 
                    out times, out intensities, out massErrors);
                return new ChromatogramTimeIntensities(times, intensities, massErrors);
            }
            set
            {
                if (null == value)
                {
                    Chromatogram = null;
                    return;
                }
                var uncompressed = ChromatogramCache.TimeIntensitiesToBytes(value.Times, value.Intensities, value.MassErrors);
                Chromatogram = uncompressed.Compress(3);
            }
        }
        public virtual ICollection<Transition> Transitions { get; set; }

        public class ChromatogramTimeIntensities
        {
            public ChromatogramTimeIntensities(float[] times, float[][] intensities, short[][] massErrors)
            {
                Times = times;
                Intensities = intensities;
                MassErrors = massErrors;
            }
            public float[] Times { get; private set; }
            public float[][] Intensities { get; private set; }
            public short[][] MassErrors { get; private set; }
        }
    }
}
