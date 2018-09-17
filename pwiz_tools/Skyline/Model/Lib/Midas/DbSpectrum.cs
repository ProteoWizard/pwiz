/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.IO;
using Ionic.Zlib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Util;
// ReSharper disable VirtualMemberCallInConstructor

namespace pwiz.Skyline.Model.Lib.Midas
{
    public class DbSpectrum : DbEntity
    {
        private double[] _mzs;
        private double[] _intensities;
        public override Type EntityClass
        {
            get { return typeof (DbSpectrum); }
        }

        // public virtual long? ID { get; set; } // in DbEntity
        public virtual DbResultsFile ResultsFile { get; set; }
        public virtual double PrecursorMz { get; set; }
        public virtual double? MatchedPrecursorMz { get; set; }
        public virtual string DocumentPeptide { get; set; }
        public virtual int? DocumentPrecursorCharge { get; set; }
        public virtual double RetentionTime { get; set; }
        public virtual byte[] MzBytes { get; set; }
        public virtual byte[] IntensityBytes { get; set; }

        public virtual bool HasPrecursorMatch { get { return MatchedPrecursorMz.HasValue; } }
        public virtual double[] Mzs { get { return _mzs ?? (_mzs = UncompressBytes(MzBytes)); } }
        public virtual double[] Intensities { get { return _intensities ?? (_intensities = UncompressBytes(IntensityBytes)); } }

        /// <summary>
        /// For NHibernate only
        /// </summary>
        protected DbSpectrum()
        {            
        }

        public DbSpectrum(DbSpectrum other)
            : this(other.ResultsFile, other.PrecursorMz, other.MatchedPrecursorMz, other.DocumentPeptide, other.DocumentPrecursorCharge, other.RetentionTime, other.MzBytes, other.IntensityBytes)
        {
            Id = other.Id;
        }

        private DbSpectrum(DbResultsFile resultsFile, double precursorMz, double? matchedPrecursorMz, string documentPeptide, int? documentPrecursorCharge, double retentionTime, byte[] mzBytes, byte[] intensityBytes)
        {
            PrecursorMz = precursorMz;
            MatchedPrecursorMz = matchedPrecursorMz;
            DocumentPeptide = documentPeptide;
            DocumentPrecursorCharge = documentPrecursorCharge;
            ResultsFile = resultsFile;
            RetentionTime = retentionTime;
            MzBytes = mzBytes;
            IntensityBytes = intensityBytes;
        }

        public DbSpectrum(DbResultsFile resultsFile, double precursorMz, double? matchedPrecursorMz, string documentPeptide, int? documentPrecursorCharge, double retentionTime, double[] mzs, double[] intensities)
            : this(resultsFile, precursorMz, matchedPrecursorMz, documentPeptide, documentPrecursorCharge, retentionTime, CompressBytes(mzs), CompressBytes(intensities))
        {
        }

        #region object overrides

        public virtual bool Equals(DbSpectrum other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   Equals(ResultsFile, other.ResultsFile) &&
                   Equals(PrecursorMz, other.PrecursorMz) &&
                   Equals(MatchedPrecursorMz, other.MatchedPrecursorMz) &&
                   Equals(DocumentPeptide, other.DocumentPeptide) &&
                   Equals(DocumentPrecursorCharge, other.DocumentPrecursorCharge) &&
                   Equals(RetentionTime, other.RetentionTime) &&
                   ArrayUtil.EqualsDeep(MzBytes, other.MzBytes) &&
                   ArrayUtil.EqualsDeep(IntensityBytes, other.IntensityBytes);
        }

        public override bool Equals(object obj)
        {
            return !ReferenceEquals(obj, null) && (ReferenceEquals(this, obj) || Equals(obj as DbSpectrum));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = base.GetHashCode();
                result = (result*397) ^ ResultsFile.GetHashCode();
                result = (result*397) ^ PrecursorMz.GetHashCode();
                result = (result*397) ^ MatchedPrecursorMz.GetHashCode();
                result = (result*397) ^ (DocumentPeptide == null ? 0 : DocumentPeptide.GetHashCode());
                result = (result*397) ^ DocumentPrecursorCharge.GetHashCode();
                result = (result*397) ^ RetentionTime.GetHashCode();
                result = (result*397) ^ MzBytes.GetHashCode();
                result = (result*397) ^ IntensityBytes.GetHashCode();
                return result;
            }
        }

        public override string ToString()
        {
            return string.Format(@"{0} m/z, RT: {1}", MatchedPrecursorMz ?? PrecursorMz, RetentionTime);
        }

        #endregion

        private static byte[] CompressBytes(double[] doubles)
        {
            var doubleBytes = new byte[doubles.Length * sizeof(double)];
            Buffer.BlockCopy(doubles, 0, doubleBytes, 0, doubleBytes.Length);
            using (var ms = new MemoryStream())
            {
                using (var compressor = new ZlibStream(ms, CompressionMode.Compress, CompressionLevel.Level3))
                    compressor.Write(doubleBytes, 0, doubleBytes.Length);
                var result = ms.ToArray();

                // If compression did not improve the situation, then use
                // uncompressed bytes.
                return result.Length >= doubleBytes.Length ? doubleBytes : result;
            }
        }

        private static double[] UncompressBytes(byte[] bytes)
        {
            byte[] uncompressed;
            try
            {
                uncompressed = ZlibStream.UncompressBuffer(bytes);
            }
            catch
            {
                uncompressed = bytes;
            }
            var doubles = new double[uncompressed.Length / sizeof(double)];
            Buffer.BlockCopy(uncompressed, 0, doubles, 0, uncompressed.Length);
            return doubles;
        }
    }
}
