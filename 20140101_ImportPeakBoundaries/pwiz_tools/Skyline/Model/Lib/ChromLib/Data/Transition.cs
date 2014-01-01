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
using System;

namespace pwiz.Skyline.Model.Lib.ChromLib.Data
{
    public class Transition : ChromLibEntity<Transition>
    {
        public virtual Precursor Precursor { get; set; }
        public virtual double Mz { get; set; }
        public virtual int Charge { get; set; }
        public virtual double NeutralMass { get; set; }
        public virtual double NeutralLossMass { get; set; }
        public virtual string FragmentType { get; set; }
        public virtual int FragmentOrdinal { get; set; }
        public virtual int MassIndex { get; set; }
        public virtual double Area { get; set; }
        public virtual double Height { get; set; }
        public virtual double Fwhm { get; set; }
        public virtual int ChromatogramIndex { get; set; }
        public virtual Tuple<float[], float[], short[]> GetChromatogramData()
        {
            var timeIntensityErrors = Precursor.ChromatogramData;
            return new Tuple<float[], float[], short[]>(timeIntensityErrors.Times, 
                SafeIndex(timeIntensityErrors.Intensities, ChromatogramIndex), 
                SafeIndex(timeIntensityErrors.MassErrors, ChromatogramIndex));
        }

        private static T SafeIndex<T>(T[] array, int index)
        {
            if (null == array || array.Length <= index)
            {
                return default(T);
            }
            return array[index];
        }
    }
}
