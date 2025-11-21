using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.ProteowizardWrapper;

namespace pwiz.CommonMsData
{
    /// <summary>
    /// Helper functions for specifying a single sample injected into a mass
    /// spectrometer.
    /// 
    /// Ideally this would be represented with a complete object, but that would
    /// require both XML and cache format changes.  Implemented late in v0.5,
    /// the simplest solution is to encode the necessary information into the
    /// existing path string used to identify a single sample file.
    /// 
    /// It's now (v3.5) being expanded to include other information needed to reproducibly 
    /// read raw data - lockmass settings, for example.  Probably ought to be moved out to 
    /// MSDataFileUri, really
    /// 
    /// </summary>
    public static class SampleHelp
    {
        private const string TAG_LOCKMASS_POS = "lockmass_pos";
        private const string TAG_LOCKMASS_NEG = "lockmass_neg";
        private const string TAG_LOCKMASS_TOL = "lockmass_tol";
        private const string TAG_CENTROID_MS1 = "centroid_ms1";
        private const string TAG_CENTROID_MS2 = "centroid_ms2";
        private const string TAG_COMBINE_IMS = "combine_ims";   // LEGACY: Introduced temporarily in 19.1.9.338 and 350
        private const string VAL_TRUE = "true";

        public static string EncodePath(string filePath, string sampleName, int sampleIndex, LockMassParameters lockMassParameters)
        {
            return LegacyEncodePath(filePath, sampleName, sampleIndex, lockMassParameters, false, false, false);
        }

        /// <summary>
        /// Use directly only when access to combineIonMobilitySpectra is required for legacy testing
        /// </summary>
        public static string LegacyEncodePath(string filePath, string sampleName, int sampleIndex, LockMassParameters lockMassParameters,
            bool centroidMS1, bool centroidMS2, bool combineIonMobilitySpectra)
        {
            var parameters = new List<string>();
            const string pairFormat = "{0}={1}";
            string filePart;
            if (!(string.IsNullOrEmpty(sampleName) && -1 == sampleIndex))
            {
                // Info for distinguishing a single sample within a WIFF file.
                filePart = string.Format(@"{0}|{1}|{2}", filePath, sampleName ?? string.Empty, sampleIndex);
            }
            else
            {
                filePart = filePath;
            }

            if (lockMassParameters != null && !lockMassParameters.IsEmpty)
            {
                if (lockMassParameters.LockmassPositive.HasValue)
                    parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_LOCKMASS_POS, lockMassParameters.LockmassPositive.Value));
                if (lockMassParameters.LockmassNegative.HasValue)
                    parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_LOCKMASS_NEG, lockMassParameters.LockmassNegative.Value));
                if (lockMassParameters.LockmassTolerance.HasValue)
                    parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_LOCKMASS_TOL, lockMassParameters.LockmassTolerance.Value));
            }
            if (centroidMS1)
            {
                parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_CENTROID_MS1, VAL_TRUE));
            }
            if (centroidMS2)
            {
                parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_CENTROID_MS2, VAL_TRUE));
            }
            if (combineIonMobilitySpectra)
            {
                parameters.Add(string.Format(CultureInfo.InvariantCulture, pairFormat, TAG_COMBINE_IMS, VAL_TRUE));
            }

            return parameters.Any() ? string.Format(@"{0}?{1}", filePart, string.Join(@"&", parameters)) : filePart;
        }

        public static string EscapeSampleId(string sampleId)
        {
            var invalidFileChars = Path.GetInvalidFileNameChars();
            var invalidNameChars = new[] {',', '.', ';'};
            if (sampleId.IndexOfAny(invalidFileChars) == -1 &&
                sampleId.IndexOfAny(invalidNameChars) == -1)
                return sampleId;
            var sb = new StringBuilder();
            foreach (char c in sampleId)
                sb.Append(invalidFileChars.Contains(c) || invalidNameChars.Contains(c) ? '_' : c);
            return sb.ToString();
        }

        public static string GetLocationPart(string path)
        {
            return path.Split('?')[0];
        }

        public static string GetPathFilePart(string path)
        {
            path = GetLocationPart(path); // Just in case the url args contain '|'
            if (path.IndexOf('|') == -1)
                return path;
            return path.Split('|')[0];
        }

        public static bool HasSamplePart(string path)
        {
            path = GetLocationPart(path); // Just in case the url args contain '|'
            string[] parts = path.Split('|');

            return parts.Length == 3 && int.TryParse(parts[2], out _);
        }

        public static string GetPathSampleNamePart(string path)
        {
            path = GetLocationPart(path); // Just in case the url args contain '|'
            if (path.IndexOf('|') == -1)
                return null;
            return path.Split('|')[1];
        }

        public static string GetPathSampleNamePart(MsDataFileUri msDataFileUri)
        {
            return msDataFileUri.GetSampleName();
        }

        public static int GetPathSampleIndexPart(string path)
        {
            path = GetLocationPart(path); // Just in case the url args contain '|'
            int sampleIndex = -1;
            if (path.IndexOf('|') != -1)
            {
                string[] parts = path.Split('|');
                int index;
                if (parts.Length == 3 && int.TryParse(parts[2], out index))
                    sampleIndex = index;
            }
            return sampleIndex;
        }        

        /// <summary>
        /// Returns just the file name from a path that may contain sample information.
        /// </summary>
        /// <param name="path">The full path with any sample information</param>
        /// <returns>The file name part</returns>
        public static string GetFileName(string path)
        {
            return Path.GetFileName(GetPathFilePart(path));
        }

        public static string GetFileName(MsDataFileUri msDataFileUri)
        {
            return msDataFileUri.GetFileName();
        }

        public static bool GetCentroidMs1(string path)
        {
            return ParseParameterBool(TAG_CENTROID_MS1, path) ?? false;
        }

        public static bool GetCentroidMs2(string path)
        {
            return ParseParameterBool(TAG_CENTROID_MS2, path) ?? false;
        }

        public static bool GetCombineIonMobilitySpectra(string path)
        {
            return ParseParameterBool(TAG_COMBINE_IMS, path) ?? false;
        }

        /// <summary>
        /// Returns a sample name for any file path, using either the available sample
        /// information on the path, or the file basename, if no sample information is present.
        /// </summary>
        /// <param name="path">The full path with any sample information</param>
        /// <returns>The sample name part or file basename</returns>
        public static string GetFileSampleName(string path)
        {
            return GetPathSampleNamePart(path) ?? Path.GetFileNameWithoutExtension(path);
        }

        private static string ParseParameter(string name, string url)
        {
            var parts = url.Split('?');
            if (parts.Length > 1)
            {
                var parameters = parts[1].Split('&');
                var parameter = parameters.FirstOrDefault(p => p.StartsWith(name));
                if (parameter != null)
                {
                    return parameter.Split('=')[1];
                }
            }
            return null;
        }

        private static bool? ParseParameterBool(string name, string url)
        {
            var valStr = ParseParameter(name, url);
            if (valStr != null)
            {
                return valStr.Equals(VAL_TRUE);
            }
            return null;
        }

        private static double? ParseParameterDouble(string name, string url)
        {
            var valStr = ParseParameter(name, url);
            if (valStr != null)
            {
                double dval;
                if (double.TryParse(valStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out dval))
                    return dval;
            }
            return null;
        }

        private static int? ParseParameterInt(string name, string url) 
        {
            var valStr = ParseParameter(name, url);
            if (valStr != null)
            {
                int ival;
                if (int.TryParse(valStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out ival))
                    return ival;
            }
            return null;
        }

        public static LockMassParameters GetLockmassParameters(string url)
        {
            if (string.IsNullOrEmpty(url))
                return LockMassParameters.EMPTY;
            return LockMassParameters.Create(ParseParameterDouble(TAG_LOCKMASS_POS, url), ParseParameterDouble(TAG_LOCKMASS_NEG, url), ParseParameterDouble(TAG_LOCKMASS_TOL, url));
        }
    }
}