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
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Data
{
    public class DbMsDataFile : DbEntity<DbMsDataFile>, IComparable<DbMsDataFile>
    {
        public virtual String Name { get; set; }
        public virtual String Label { get; set; }
        public virtual String Cohort { get; set; }
        public virtual String Sample { get; set; }
        public virtual double? TimePoint { get; set; }

        /// <devdoc>
        /// Currently PrecursorPool is only allowed to be a double, but in the future it may also be
        /// a <see cref="TracerPercentFormula"/>, so we persist it as a string.
        /// </devdoc>
        public virtual string PrecursorPool { get; set; }
        public virtual byte[] TimesBytes { get; set; }
        public virtual byte[] TotalIonCurrentBytes { get; set; }
        public virtual byte[] MsLevels { get; set; }
        public virtual double[] Times {
            get
            {
                byte[] timesBytes = TimesBytes;
                if (timesBytes == null)
                {
                    return null;
                }
                return ArrayConverter.FromBytes<double>(timesBytes);
            }
            set
            {
                TimesBytes = ArrayConverter.ToBytes(value);
            }
        }

        public virtual double[] TotalIonCurrent
        {
            get
            {
                byte[] ticBytes = TotalIonCurrentBytes;
                if (ticBytes == null)
                {
                    return null;
                }
                return ArrayConverter.FromBytes<double>(ticBytes);
            }
            set
            {
                TotalIonCurrentBytes = ArrayConverter.ToBytes(value);
            }
        }

        public virtual int ScanCount { get { return TimesBytes.Length/8; } }
        public override string ToString()
        {
            return Name;
        }
        public virtual int CompareTo(DbMsDataFile that)
        {
            return string.Compare(Name, that.Name, StringComparison.CurrentCultureIgnoreCase);
        }
        public virtual double SafeGetTime(int scanIndex)
        {
            scanIndex = Math.Min(scanIndex, ScanCount - 1);
            if (scanIndex < 0)
            {
                return 0;
            }
            var times = new double[1];
            var byteLength = Buffer.ByteLength(times);
            Buffer.BlockCopy(TimesBytes, byteLength * scanIndex, times, 0, byteLength);
            return times[0];
        }
    }
}
