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
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Results.RemoteApi.Unifi;

namespace pwiz.Skyline.Model.Results
{
    public abstract class MsDataFileUri : Immutable
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
        public abstract LockMassParameters GetLockMassParameters();
        public abstract bool IsWatersLockmassCorrectionCandidate();
        /// <summary>
        /// Returns a copy of itself with updated lockmass parameters
        /// </summary>
        public abstract MsDataFileUri ChangeLockMassParameters(LockMassParameters lockMassParameters);
        public abstract bool GetCentroidMs1();
        public abstract bool GetCentroidMs2();
        /// <summary>
        /// Returns a copy of itself with updated centroiding parameters
        /// </summary>
        public abstract MsDataFileUri ChangeCentroiding(bool centroidMS1, bool centroidMS2);

        public MsDataFileUri ChangeParameters(SrmDocument doc, LockMassParameters lockMassParameters)
        {
            return doc.Settings.TransitionSettings.FullScan.ApplySettings(ChangeLockMassParameters(lockMassParameters));
        }

        public string GetSampleOrFileName()
        {
            return GetSampleName() ?? GetFileNameWithoutExtension();
        }

        public static MsDataFileUri Parse(string url)
        {
            if (url.StartsWith(ChorusUrl.ChorusUrlPrefix))
            {
                return new ChorusUrl(url);
            }
            if (url.StartsWith(UnifiUrl.UrlPrefix))
            {
                return new UnifiUrl(url);
            }
            return new MsDataFilePath(SampleHelp.GetPathFilePart(url), 
                SampleHelp.GetPathSampleNamePart(url), 
                SampleHelp.GetPathSampleIndexPart(url),
                SampleHelp.GetLockmassParameters(url),
                SampleHelp.GetCentroidMs1(url),
                SampleHelp.GetCentroidMs2(url));
        }

        public abstract MsDataFileImpl OpenMsDataFile(bool simAsSpectra, int preferOnlyMsLevel);
    }

    public class MsDataFilePath : MsDataFileUri
    {
        public static readonly MsDataFilePath EMPTY = new MsDataFilePath(string.Empty);
        public MsDataFilePath(string filePath, LockMassParameters lockMassParameters = null, 
            bool centroidMs1=false, bool centroidMs2=false)
            : this(filePath, null, -1, lockMassParameters, centroidMs1, centroidMs2)
        {
        }
        public MsDataFilePath(string filePath, string sampleName, int sampleIndex, LockMassParameters lockMassParameters = null,
            bool centroidMs1 = false, bool centroidMs2 = false)
        {
            FilePath = filePath;
            SampleName = sampleName;
            SampleIndex = sampleIndex;
            LockMassParameters = lockMassParameters ?? LockMassParameters.EMPTY;
            CentroidMs1 = centroidMs1;
            CentroidMs2 = centroidMs2;
        }

        protected MsDataFilePath(MsDataFilePath msDataFilePath)
        {
            FilePath = msDataFilePath.FilePath;
            SampleName = msDataFilePath.SampleName;
            SampleIndex = msDataFilePath.SampleIndex;
            LockMassParameters = msDataFilePath.LockMassParameters;
            CentroidMs1 = msDataFilePath.CentroidMs1;
            CentroidMs2 = msDataFilePath.CentroidMs2;
        }

        public string FilePath { get; private set; }

        public MsDataFilePath SetFilePath(string filePath)
        {
            return new MsDataFilePath(this){FilePath = filePath};
        }
        public string SampleName { get; private set; }
        public int SampleIndex { get; private set; }
        public LockMassParameters LockMassParameters { get; private set; }
        public bool CentroidMs1 { get; private set; }
        public bool CentroidMs2 { get; private set; }

        public override MsDataFileUri GetLocation()
        {
            if (!LockMassParameters.IsEmpty || CentroidMs1 || CentroidMs2)
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
            if (!GetFilePath().ToLowerInvariant().EndsWith(".raw"))  // Not L10N
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
            return new MsDataFilePath(FilePath, SampleName, SampleIndex, lockMassParameters, CentroidMs1, CentroidMs2);
        }

        public override bool GetCentroidMs1()
        {
            return CentroidMs1;
        }

        public override bool GetCentroidMs2()
        {
            return CentroidMs2;
        }

        public override MsDataFileUri ChangeCentroiding(bool centroidMS1, bool centroidMS2)
        {
            return new MsDataFilePath(FilePath, SampleName, SampleIndex, LockMassParameters, centroidMS1, centroidMS2);
        }

        public override string ToString()
        {
            return SampleHelp.EncodePath(FilePath, SampleName, SampleIndex, LockMassParameters, CentroidMs1, CentroidMs2);
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
                CentroidMs1 == other.CentroidMs1 &&
                CentroidMs2 == other.CentroidMs2 &&
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
                hashCode = (hashCode*397) ^ (CentroidMs1 ? 1 : 0);
                hashCode = (hashCode*397) ^ (CentroidMs2 ? 1 : 0);
                return hashCode;
            }
        }

        public override MsDataFileImpl OpenMsDataFile(bool simAsSpectra, int preferOnlyMsLevel)
        {
            return new MsDataFileImpl(FilePath, Math.Max(SampleIndex, 0), LockMassParameters, simAsSpectra,
                requireVendorCentroidedMS1: CentroidMs1, requireVendorCentroidedMS2: CentroidMs2,
                ignoreZeroIntensityPoints: true, preferOnlyMsLevel: preferOnlyMsLevel);
        }
    }
}
