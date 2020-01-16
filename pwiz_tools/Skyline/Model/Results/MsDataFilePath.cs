/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    public abstract class MsDataFileUri : Immutable, IComparable
    {
        public abstract MsDataFileUri GetLocation();
        public abstract string GetFilePath();
        public abstract string GetFileName();
        public abstract string GetFileNameWithoutExtension();
        public abstract override string ToString();
        public abstract DateTime GetFileLastWriteTime();
        public abstract string GetSampleName();
        public abstract int GetSampleIndex();
        public abstract string GetExtension();
        public abstract MsDataFileUri ToLower();
        public abstract MsDataFileUri Normalize();

        // TODO: Move LockMassParameters to ChromCachedFile
        public abstract LockMassParameters GetLockMassParameters();
        public abstract bool IsWatersLockmassCorrectionCandidate();
        /// <summary>
        /// Returns a copy of itself with updated lockmass parameters
        /// </summary>
        public abstract MsDataFileUri ChangeLockMassParameters(LockMassParameters lockMassParameters);

        // LEGACY: Trying to get rid of old pattern that stored MsDataFile open parameters on the URI
        public abstract bool LegacyGetCentroidMs1();
        public abstract bool LegacyGetCentroidMs2();
        public abstract MsDataFileUri RemoveLegacyParameters();

        public virtual MsDataFileUri RestoreLegacyParameters(bool centroidMs1, bool centroidMs2)
        {
            // Edge case where File > Share to an earlier version code end up without centroiding
            // on the URIs in XML and SKYD. Not worth supporting on UNIFI URIs
            return this;
        }

        public string GetSampleOrFileName()
        {
            return GetSampleName() ?? GetFileNameWithoutExtension();
        }

        public static MsDataFileUri Parse(string url)
        {
            if (url.StartsWith(UnifiUrl.UrlPrefix))
            {
                return new UnifiUrl(url);
            }

            return MsDataFilePath.ParseUri(url);
        }

        public abstract MsDataFileImpl OpenMsDataFile(bool simAsSpectra, bool preferOnlyMs1,
            bool centroidMs1, bool centroidMs2, bool ignoreZeroIntensityPoints);

        public int CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }
            var msDataFileUri = (MsDataFileUri) obj;
            return string.CompareOrdinal(ToString(), msDataFileUri.ToString());
        }
    }

    public class MsDataFilePath : MsDataFileUri
    {
        public static MsDataFilePath ParseUri(string url)
        {
            return new MsDataFilePath(SampleHelp.GetPathFilePart(url),
                SampleHelp.GetPathSampleNamePart(url),
                SampleHelp.GetPathSampleIndexPart(url),
                SampleHelp.GetLockmassParameters(url),
                SampleHelp.GetCentroidMs1(url),
                SampleHelp.GetCentroidMs2(url),
                SampleHelp.GetCombineIonMobilitySpectra(url));
        }
        public static readonly MsDataFilePath EMPTY = new MsDataFilePath(string.Empty);
        public MsDataFilePath(string filePath, LockMassParameters lockMassParameters = null)
            : this(filePath, null, -1, lockMassParameters, false, false, false)
        {
        }
        public MsDataFilePath(string filePath, string sampleName, int sampleIndex, LockMassParameters lockMassParameters = null)
            : this(filePath, sampleName, sampleIndex, lockMassParameters, false, false, false)
        {
        }
        private MsDataFilePath(string filePath, string sampleName, int sampleIndex, LockMassParameters lockMassParameters,
            bool centroidMs1, bool centroidMs2, bool combineIonMobilitySpectra)
        {
            FilePath = filePath;
            SampleName = sampleName;
            SampleIndex = sampleIndex;
            LockMassParameters = lockMassParameters ?? LockMassParameters.EMPTY;
            LegacyCentroidMs1 = centroidMs1;
            LegacyCentroidMs2 = centroidMs2;
            LegacyCombineIonMobilitySpectra = combineIonMobilitySpectra;
        }

        protected MsDataFilePath(MsDataFilePath msDataFilePath)
        {
            FilePath = msDataFilePath.FilePath;
            SampleName = msDataFilePath.SampleName;
            SampleIndex = msDataFilePath.SampleIndex;
            LockMassParameters = msDataFilePath.LockMassParameters;
            LegacyCentroidMs1 = msDataFilePath.LegacyCentroidMs1;
            LegacyCentroidMs2 = msDataFilePath.LegacyCentroidMs2;
            LegacyCombineIonMobilitySpectra = msDataFilePath.LegacyCombineIonMobilitySpectra;
        }

        public string FilePath { get; private set; }

        public MsDataFilePath SetFilePath(string filePath)
        {
            return new MsDataFilePath(this){FilePath = filePath};
        }
        public string SampleName { get; private set; }
        public int SampleIndex { get; private set; }
        public LockMassParameters LockMassParameters { get; private set; }
        public bool LegacyCentroidMs1 { get; private set; }
        public bool LegacyCentroidMs2 { get; private set; }
        public bool LegacyCombineIonMobilitySpectra { get; private set; } // BACKWARD COMPATIBILITY: Skyline-daily 19.1.9.338 & 350 stored URLs with this parameter

        public override MsDataFileUri GetLocation()
        {
            if (!LockMassParameters.IsEmpty || LegacyCentroidMs1 || LegacyCentroidMs2 || LegacyCombineIonMobilitySpectra)
                return new MsDataFilePath(FilePath, SampleName, SampleIndex);
            return this;
        }

        public override string GetFilePath()
        {
            return FilePath;
        }

        public override string GetFileNameWithoutExtension()
        {
            return Path.GetFileNameWithoutExtension(FilePath);
        }

        public override string GetExtension()
        {
            return Path.GetExtension(FilePath);
        }

        public override string GetFileName()
        {
            return Path.GetFileName(FilePath);
        }

        public override int GetSampleIndex()
        {
            return SampleIndex;
        }

        public override bool IsWatersLockmassCorrectionCandidate()
        {
            string filePath = GetFilePath();
            // Has to be a Waters .raw file, not just an mzML translation of one
            if (String.IsNullOrEmpty(filePath))
                return false; // Not even a file
            if (!GetFilePath().ToLowerInvariant().EndsWith(@".raw"))
                return false; // Return without even opening the file
            if (!Directory.Exists(filePath))
                return false; // Thermo .raw is a file, Waters .raw is actually a directory
            try
            {
                using (var f = new MsDataFileImpl(filePath))
                    return f.IsWatersLockmassCorrectionCandidate;
            }
            catch (Exception)
            {
                return false; // whatever that was, it wasn't a Waters lockmass file
            }
        }

        public override LockMassParameters GetLockMassParameters()
        {
            return LockMassParameters;
        }

        public override MsDataFileUri ChangeLockMassParameters(LockMassParameters lockMassParameters)
        {
            return new MsDataFilePath(FilePath, SampleName, SampleIndex, lockMassParameters,
                LegacyCentroidMs1, LegacyCentroidMs2, LegacyCombineIonMobilitySpectra);
        }

        public override bool LegacyGetCentroidMs1()
        {
            return LegacyCentroidMs1;
        }

        public override bool LegacyGetCentroidMs2()
        {
            return LegacyCentroidMs2;
        }

        public override string ToString()
        {
            return SampleHelp.LegacyEncodePath(FilePath, SampleName, SampleIndex, LockMassParameters,
                LegacyCentroidMs1, LegacyCentroidMs2, LegacyCombineIonMobilitySpectra);
        }

        public override DateTime GetFileLastWriteTime()
        {
            return File.GetLastWriteTime(FilePath);
        }

        public override string GetSampleName()
        {
            return SampleName;
        }

        public override MsDataFileUri ToLower()
        {
            return SetFilePath(FilePath.ToLower());
        }

        public override MsDataFileUri Normalize()
        {
            return SetFilePath(Path.GetFullPath(FilePath));
        }

        protected bool Equals(MsDataFilePath other)
        {
            return string.Equals(FilePath, other.FilePath) &&
                string.Equals(SampleName, other.SampleName) &&
                SampleIndex == other.SampleIndex &&
                LegacyCentroidMs1 == other.LegacyCentroidMs1 &&
                LegacyCentroidMs2 == other.LegacyCentroidMs2 &&
                LegacyCombineIonMobilitySpectra == other.LegacyCombineIonMobilitySpectra &&
                LockMassParameters.Equals(other.LockMassParameters);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MsDataFilePath) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = FilePath.GetHashCode();
                hashCode = (hashCode*397) ^ (SampleName != null ? SampleName.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ SampleIndex;
                hashCode = (hashCode*397) ^ (LockMassParameters != null ? LockMassParameters.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (LegacyCentroidMs1 ? 1 : 0);
                hashCode = (hashCode*397) ^ (LegacyCentroidMs2 ? 1 : 0);
                hashCode = (hashCode*397) ^ (LegacyCombineIonMobilitySpectra ? 1 : 0);
                return hashCode;
            }
        }

        public override MsDataFileImpl OpenMsDataFile(bool simAsSpectra, bool preferOnlyMs1,
            bool centroidMs1, bool centroidMs2, bool ignoreZeroIntensityPoints)
        {
            Assume.IsFalse(LegacyCentroidMs1 || LegacyCentroidMs2 || LegacyCombineIonMobilitySpectra);  // Only for backward compatibility. We are not expecting to use this value to open MsDataFileImpl objects
            return new MsDataFileImpl(FilePath, Math.Max(SampleIndex, 0), LockMassParameters, simAsSpectra,
                requireVendorCentroidedMS1: centroidMs1, requireVendorCentroidedMS2: centroidMs2,
                ignoreZeroIntensityPoints: ignoreZeroIntensityPoints, preferOnlyMsLevel: preferOnlyMs1 ? 1 : 0);
        }

        public override MsDataFileUri RemoveLegacyParameters()
        {
            // Remove LegacyCombineIonMobilitySpectra, LegacyCentroidMs1, LegacyCentroidMs2
            if (!LegacyCombineIonMobilitySpectra && !LegacyCentroidMs1 && !LegacyCentroidMs2)
                return this;

            // FUTURE: Remove LockMassParameters
            return new MsDataFilePath(FilePath, SampleName, SampleIndex, LockMassParameters);
        }

        public override MsDataFileUri RestoreLegacyParameters(bool centroidMs1, bool centroidMs2)
        {
            if (!centroidMs1 && !centroidMs2)
                return this;

            return new MsDataFilePath(FilePath, SampleName, SampleIndex, LockMassParameters,
                centroidMs1, centroidMs2, false);
        }
    }
}
