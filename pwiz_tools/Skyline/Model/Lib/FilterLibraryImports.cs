using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib
{
    public class LibEntry
    {
        public string _fullText;
        public Dictionary<string, string> _categoryDict;
        public string InChiKey;
        public Adduct _adduct = Adduct.M_PLUS;
        public string LibKey;
        internal LibEntry(string text)
        {
            _fullText = text;
            _categoryDict = new Dictionary<string, string>();
        }

        internal void UpdateDict(string field, string value)
        {
            _categoryDict[field] = value;
        }

        /// <summary>
        /// Checks if a specified object meets the criteria set
        /// </summary>
        public bool MeetsCriteria(ImportFilteringParser.FilterCriteria criteria)
        {
            if (!_categoryDict.ContainsKey(criteria.FilterField))
            {
                return criteria.LibFilterType == ImportFilteringParser.LibFilterType.does_not_contain;
            }

            if (criteria.LibFilterType == ImportFilteringParser.LibFilterType.matches_exactly)
            {
                // CONSIDER (henryts): add case matching?
                return _categoryDict[criteria.FilterField]
                    .Equals(criteria.FilterValue, StringComparison.OrdinalIgnoreCase);
            } else if (criteria.LibFilterType == ImportFilteringParser.LibFilterType.greater_than || criteria.LibFilterType == ImportFilteringParser.LibFilterType.less_than)
            {
                if (double.TryParse(_categoryDict[criteria.FilterField], out double num) && double.TryParse(criteria.FilterValue, out double target))
                {
                    var greaterThan = num > target;
                    return criteria.LibFilterType == ImportFilteringParser.LibFilterType.greater_than
                        ? greaterThan
                        : !greaterThan;
                }

                // We cannot parse the field or value as a double so we cannot determine if it is a match
                return false;
            } else if (criteria.LibFilterType == ImportFilteringParser.LibFilterType.within_tolerance)
            {

                if (double.TryParse(_categoryDict[criteria.FilterField], out double num) && double.TryParse(criteria.FilterValue, out double target))
                {
                    return Math.Abs(num - target) <= criteria.Tolerance;
                }
            }

            var contains = _categoryDict[criteria.FilterField].Contains(criteria.FilterValue);

            return criteria.LibFilterType == ImportFilteringParser.LibFilterType.contains ? contains : !contains;
        }

        public void CreateLibKey()
        {
            LibKey = new LibKey(InChiKey, _adduct).ToString();
        }
    }

    public abstract class ImportFilteringParser
    {
        public enum LibFilterType
        {
            contains,
            does_not_contain,
            matches_exactly,
            greater_than,
            less_than,
            within_tolerance
        }
        public struct FilterCriteria
        {
            public LibFilterType LibFilterType;
            public string FilterField;
            public string FilterValue;
            public double Tolerance;

            public FilterCriteria(LibFilterType filterType, string field, string value, double tolerance = 0)
            {
                LibFilterType = filterType;
                FilterField = field;
                FilterValue = value;
                Tolerance = tolerance;
            }
        }
        // Categories and  values available
        protected Dictionary<string, HashSet<string>> categories;
        public List<LibEntry> _entries;

        public void WriteMatchingEntries(string filepath, List<FilterCriteria> criteria)
        {
            var matchingEntries = Filter(criteria);
            WriteToFile(filepath, matchingEntries);
        }
        public List<LibEntry> Filter(List<FilterCriteria> criteriaList)
        {
            var current = _entries;
            foreach (var criteria in criteriaList)
            {
                current = current.Where(entry => entry.MeetsCriteria(criteria)).ToList();
            }

            return current;
        }

        public abstract void WriteToFile(string filepath, List<LibEntry> matches);
    }
    public class MspParser : ImportFilteringParser
    {
        private readonly string libraryFilepath;
        private const string NEWLINE = "\n";
        private const string DOUBLE_QUOTE = "\"";

        public MspParser(string filePath)
        {
            categories = new Dictionary<string, HashSet<string>>();
            _entries = new List<LibEntry>();
            libraryFilepath = filePath;
        }

        public Dictionary<string, HashSet<string>> CreateCategories()
        {
            ParseLib();
            return categories;
        }

        /// <summary>
        /// Split a library into separate entries
        /// </summary>
        private void ParseLib()
        {
            // Create a new buffered stream from the filepath
            string line;
            var file = new StreamReader(libraryFilepath);
            while ((line = file.ReadLine()) != null)
            {
                string entry = string.Empty;
                while (line != null && !line.Equals(string.Empty))
                {
                    entry += line + NEWLINE;

                    line = file.ReadLine();
                }

                if (entry != string.Empty)
                {
                    ParseEntry(entry);
                }
            }
        }

        // Regular expressions copied from NistLibSepc.cs
        // If we decide to make this part of Skyline, we should make sure they share these resources
        private static readonly RegexOptions NOCASE = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;
        private static readonly Regex REGEX_ADDUCT = new Regex(@"^Precursor_type: (.*)", NOCASE);
        private static readonly Regex REGEX_INCHIKEY = new Regex(@"^(?:Synon:.* )?InChIKey: (.*)", NOCASE);

        // Original regular expressions that do not need to be shared with Skyline
        private static readonly Regex REGEX_COLON_SPLIT = new Regex(@"^: (.*)", NOCASE);

        private void GetLibraryKey()
        {
            // We need to determine the library key so that we can see if two molecules are duplicates

            // Library key usually consists of InchiKey + adduct

            // When we sort lexicographically, duplicates will appear next to each other

            // We then only filter the duplicates

            // Write a new file minus the duplicates we ruled out

        }

        public List<LibEntry> FindDuplicates()
        {
            var duplicateEntries = new List<LibEntry>();
            // Find which entires in the library have matching molecule+adduct specifications
            // First sort all of our entries by their library key
            var sortedEnumerable = _entries.OrderBy(entry => entry.LibKey);
            var sortedEntries = sortedEnumerable.ToList();
            // Any matches will be adjacent
            for(int i = 1; i < _entries.Count; i++)
            {
                if (sortedEntries[i].LibKey.Equals(sortedEntries[i - 1].LibKey))
                {
                    duplicateEntries.Add(sortedEntries[i]);
                }
            }

            return duplicateEntries;
        }
        private void TryResolveDuplicate(LibEntry entryOne, LibEntry entryTwo)
        {
            // If the two entries only differ in retention index, then pick the entry with the more peaks

            // Find the number of peaks

            // Return the entry with the greater number of peaks
        }

        /// <summary>
        /// Find the categories and values present in a given entry
        /// </summary>
        private void ParseEntry(string text)
        {
            // Switch to breaking whenever we reach a "Name:" line
            const string comment = "Comment";
            var entry = new LibEntry(text);
            // Split the entry into separate lines
            string [] lines = text.Split(new []{NEWLINE}, StringSplitOptions.None);

            // By convention each line is a new field, unless it begins with "Comment:" or "Synon:" in which case it could contain several fields
            foreach (var line in lines)
            {
                // Check for metadata we retain. If we find this in the line, there is no need to divide it further
                var match = REGEX_INCHIKEY.Match(line);
                if (match.Success)
                {
                    entry.InChiKey = match.Groups[1].Value;
                    continue;
                }

                match = REGEX_ADDUCT.Match(line);
                if (match.Success)
                {
                    //entry._adduct = match.Groups[1].Value;
                    continue;
                }

                var parts = line.Split(':');
                if (parts[0].StartsWith(comment)) // Have to implement regex here
                {
                    var dict = FormatComment(parts[1]);
                    foreach (var pair in dict)
                    {
                        UpdateDictionary(pair.Key, pair.Value);
                        entry.UpdateDict(pair.Key, pair.Value);
                    }
                }
                else
                {
                    // This line is not a comment, so there's no need to subdivide it further
                    if (parts.Length > 1)
                    {
                        entry.UpdateDict(parts[0], parts[1]);
                        UpdateDictionary(parts[0], parts[1]);
                    }
                }
            }

            entry.CreateLibKey();
            _entries.Add(entry);
        }

        /// <summary>
        /// Pick apart a string like "Single Pep=Tryptic Mods=2(0,A,iTRAQ)(9,K,iTRAQ) Fullname=K.AAAAAGAGLK.G Charge=2 Parent=544.8369 Se=1(^G1:sc=4.21937e-013) Mz_diff=1ppm Purity=88.1 HCD=27.4851226806641eV"
        /// </summary>
        /// <param name="comment">Text of the comment, excluding the "Comment:" prefix</param>
        /// <returns>A dictionary of field value pairs</returns>
        public static Dictionary<string, string> FormatComment(string comment)
        {
            var fields = new Dictionary<string, string>();
            // Split by space, except when it is in quotes
            var parts = Regex.Split(comment, " (?=([^\"]*\"[^\"]*\")*[^\"]*$)");
            foreach (var part in parts)
            {
                // Split into two parts by the first equals sign
                // This will not be helpful if there is an equals sign in the field name
                var kvp = part.Split(new[] { '=' }, 2);

                if (kvp.Length > 1)
                {
                    // Remove all double quotes from the field name and field value
                    fields[RemoveQuotes(kvp[0])] = RemoveQuotes(kvp[1]);
                }
            }

            return fields;
        }

        /// <summary>
        /// Add new filter fields and filter values to the dictionary if they are not already present
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        private void UpdateDictionary(string field, string value)
        {
            field = RemoveQuotes(field);
            value = RemoveQuotes(value);
            if (!categories.ContainsKey(field))
            {
                categories[field] = new HashSet<string> {value};
            }
            else
            {
                categories[field].Add(value);
            }
        }

        private static string RemoveQuotes(string str)
        {
            return str.Replace(DOUBLE_QUOTE, string.Empty);
        }

        public override void WriteToFile(string filepath, List<LibEntry> entries)
        {
            var textList = new List<string>();
            foreach (var entry in entries)
            {
                textList.Add(entry._fullText);
                // Convention is to put a blank line between entries
                textList.Add(string.Empty);
            }
            File.WriteAllLines(filepath, textList);
        }
    }
}