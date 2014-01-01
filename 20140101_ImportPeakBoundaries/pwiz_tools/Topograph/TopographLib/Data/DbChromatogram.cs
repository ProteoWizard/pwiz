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
using System.Collections.Generic;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Data
{
    public class DbChromatogram : DbEntity<DbChromatogram>
    {
        public virtual DbChromatogramSet ChromatogramSet { get; set; }
        public virtual int MassIndex { get; set; }
        public virtual int Charge { get; set; }
        public virtual double MzMin { get; set; }
        public virtual double MzMax { get; set; }
        public virtual byte[] PointsBytes { get; set; }
        public virtual int? UncompressedSize { get; set; }
        public virtual IList<ChromatogramPoint> ChromatogramPoints
        {
            get
            {
                if (UncompressedSize == null)
                {
                    return ChromatogramPoint.FromByteArray(PointsBytes);
                }
                var uncompressedBytes = ArrayConverter.Uncompress(PointsBytes, UncompressedSize.Value);
                return ChromatogramPoint.FromByteArray(uncompressedBytes);
            }
            set
            {
                var uncompressedBytes = ChromatogramPoint.ToByteArray(value);
                PointsBytes = ArrayConverter.Compress(uncompressedBytes);
                UncompressedSize = uncompressedBytes.Length;
            }
        }
        
        public virtual int[] ScanIndexes
        {
            get
            {
                return ChromatogramSet.ScanIndexes;
            }
        }
        
        public virtual double[] Times
        {
            get
            {
                return ChromatogramSet.Times;
            }
        }
        public virtual MzKey MzKey
        {
            get
            {
                return new MzKey(Charge, MassIndex);
            }
            set
            {
                Charge = value.Charge;
                MassIndex = value.MassIndex;
            }
        }
        public virtual MzRange MzRange
        {
            get
            {
                return new MzRange(MzMin, MzMax);
            }
            set 
            { 
                MzMin = value.Min;
                MzMax = value.Max; 
            }
        }
    }
}
