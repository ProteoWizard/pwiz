using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SharedBatch.Properties;

namespace SharedBatch
{
    public static class TextUtil
    {
        public const string EXT_XML = ".xml";
        public const string EXT_SKY = ".sky";
        public const string EXT_SKY_ZIP = ".sky.zip";
        public const string EXT_SKYR = ".skyr";
        public const string EXT_SKYD = ".skyd";
        public const string EXT_QCFG = ".qcfg";
        public const string EXT_BCFG = ".bcfg";
        public const string EXT_R = ".R";
        public const string EXT_CSV = ".csv";
        public const string EXT_LOG = ".log";
        public const string EXT_TMP = ".tmp";
        public const string EXT_APPREF = ".appref-ms";
        public const string EXT_SKYP = ".skyp";
        public const string EXT_ZIP = ".zip";

        public const char SEPARATOR_CSV = ',';

        public static string FILTER_XML
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_XML_XML_Files, EXT_XML); }
        }

        public static string FILTER_SKY
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_SKY_Skyline_Files, EXT_SKY); }
        }

        public static string FILTER_SKY_ZIP
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_SKY_ZIP_Shared_Files, EXT_SKY_ZIP); }
        }

        public static string FILTER_SKYR
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_SKYR_Skyline_Reports, EXT_SKYR); }
        }

        public static string FILTER_BCFG
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_BCFG_Skyline_Batch_Configuration_Files, EXT_BCFG); }
        }

        public static string FILTER_QCFG
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_QCFG_AutoQC_Configuration_Files, EXT_QCFG); }
        }

        public static string FILTER_CSV
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_CSV_CSV_Files, EXT_CSV); }
        }

        public static string FILTER_R
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_R_R_Files, EXT_R); }
        }

        public static string FILTER_ALL
        {
            get { return FileDialogFilter(Resources.TextUtil_FILTER_ALL_All_Files, @".*"); }
        }


        public static bool SuccessfulReplace(Validator validate, string oldText, string newText, string originalString, bool preferReplace, out string replacedString)
        {
            var oldPath = originalString;
            var newPath = TryReplaceStart(oldText, newText, originalString);
            replacedString = oldPath;
            if (string.IsNullOrEmpty(originalString))
                return false;
            var initialValidated = false;
            var replacedValidated = false;
            try
            {
                validate(oldPath);
                initialValidated = true;
            }
            catch (ArgumentException)
            {
                // Pass - expect oldPath to be invalid
            }

            try
            {
                validate(newPath);
                replacedValidated = true;
            }
            catch (ArgumentException)
            {
                // Pass
            }

            if (replacedValidated && (!initialValidated || preferReplace))
            {
                replacedString = newPath;
                return true;
            }

            return false;
        }

        public static string TryReplaceStart(string oldText, string newText, string originalString)
        {
            if (!originalString.StartsWith(oldText))
                return originalString;
            return newText + originalString.Substring(oldText.Length);
        }

        /// <summary>
        /// Returns a filter string suitable for a common file dialog (e.g. "CSV (Comma delimited) (*.csv)|*.csv")
        /// </summary>
        /// <param name="description">The description of the filter</param>
        /// <param name="exts">The file extention(s), beginning with the period (e.g. ".csv")</param>
        public static string FileDialogFilter(string description, params string[] exts)
        {
            var sb = new StringBuilder();
            foreach (var ext in exts)
            {
                if (sb.Length > 0)
                    sb.Append(';');
                sb.Append('*').Append(ext);
            }
            return string.Format(@"{0} ({1})|{1}", description, sb);
        }


        /// <summary>
        /// This function can be used as a replacement for String.Join("\n", ...)
        /// </summary>
        /// <param name="lines">A set of strings to be on separate lines</param>
        /// <returns>A single string containing the original set separated by new lines</returns>
        public static string LineSeparate(IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            foreach (string line in lines)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(line);
            }
            return sb.ToString();
        }

        /// <summary>
        /// This function can be used as a replacement for String.Join("\n", ...)
        /// </summary>
        /// <param name="lines">A set of strings to be on separate lines</param>
        /// <returns>A single string containing the original set separated by new lines</returns>
        public static string LineSeparate(params string[] lines)
        {
            return LineSeparate(lines.AsEnumerable());
        }

        # region Parsing numbers from different cultures

        public static int? GetNullableIntFromUiString(string integer, string inputName)
        {
            if (!TryGetNullableIntFromString(integer, CultureInfo.CurrentCulture, out int? result))
                throw new ArgumentException(string.Format(Resources.TextUtil_GetOptionalIntegerFromString__0__is_not_a_valid_value_for__1___Please_enter_an_integer_, integer, inputName));
            return result;
        }

        public static int? GetNullableIntFromInvariantString(string integer)
        {
            if (!TryGetNullableIntFromString(integer, CultureInfo.InvariantCulture, out int? result))
                throw new Exception(string.Format(Resources.TextUtil_GetOptionalIntegerFromInvariantString_Cound_not_parse___0___as_type___1__, integer, typeof(int?)));
            return result;
        }

        public static int? GetNullableIntFromString(string integer, CultureInfo culture)
        {
            if (!TryGetNullableIntFromString(integer, culture, out int? result))
                throw new Exception(string.Format(Resources.TextUtil_GetOptionalIntegerFromInvariantString_Cound_not_parse___0___as_type___1__, integer, typeof(int?)));
            return result;
        }

        private static bool TryGetNullableIntFromString(string integer, CultureInfo culture, out int? result)
        {
            result = null;
            if (string.IsNullOrEmpty(integer)) return true;
            var success = int.TryParse(integer, NumberStyles.Integer, culture, out int parsed);
            result = parsed;
            return success;
        }

        public static double? GetNullableDoubleFromString(string doubleString, CultureInfo culture)
        {
            if (!TryGetNullableDoubleFromString(doubleString, culture, out double? result))
                throw new Exception(string.Format(Resources.TextUtil_GetOptionalIntegerFromInvariantString_Cound_not_parse___0___as_type___1__, doubleString, typeof(double?)));
            return result;
        }

        private static bool TryGetNullableDoubleFromString(string doubleString, CultureInfo culture, out double? result)
        {
            result = null;
            if (string.IsNullOrEmpty(doubleString)) return true;
            var success = Double.TryParse(doubleString, NumberStyles.AllowDecimalPoint, culture, out double parsed);
            result = parsed;
            return success;
        }

        public static string ToInvariantCultureString(int? optionalInt)
        {
            if (optionalInt == null) return string.Empty;
            return ((int)optionalInt).ToString(CultureInfo.InvariantCulture);
        }

        public static string ToUiString(int integer)
        {
            return integer.ToString(CultureInfo.CurrentCulture);
        }

        public static string ToUiString(int? optionalInt)
        {
            if (optionalInt == null) return string.Empty;
            return ((int)optionalInt).ToString(CultureInfo.CurrentCulture);
        }

        
        // Changed DataProtectionScope from LocalMachine to CurrentUser
        // https://stackoverflow.com/questions/19164926/data-protection-api-scope-localmachine-currentuser
        public static string EncryptPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            try
            {
                var encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(password), null,
                    DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception e)
            {
                ProgramLog.Error("Error encrypting password. ", e);

            }
            return string.Empty;
        }

        public static string DecryptPassword(string encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
            {
                return string.Empty;
            }
            try
            {
                byte[] decrypted = ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedPassword), null,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception e)
            {
                ProgramLog.Error("Error decrypting password. ", e);
            }
            return string.Empty;
        }
        #endregion
    }
}
