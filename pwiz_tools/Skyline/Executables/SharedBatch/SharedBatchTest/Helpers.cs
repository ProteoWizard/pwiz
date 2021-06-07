/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using pwiz.Common.SystemUtil;

namespace SharedBatchTest
{

    /// <summary>
    /// A set of generic, static helper functions.
    /// </summary>
    public static class Helpers
    {

        /// <summary>
        /// Searches an Array for an item that matches criteria specified
        /// through a delegate function. Search starts at the given index.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <param name="startIndex">Starting index of the search.</param>
        /// <returns>The index in the Array of the match, or -1 if not found</returns>
        public static int IndexOf<TItem>(this IList<TItem> values, Predicate<TItem> found, int startIndex)
        {
            for (int i = startIndex; i < values.Count; i++)
            {
                if (found(values[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches an Array for an item that matches criteria specified
        /// through a delegate function.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <returns>The index in the Array of the match, or -1 if not found</returns>
        public static int IndexOf<TItem>(this IList<TItem> values, Predicate<TItem> found)
        {
            return IndexOf(values, found, 0);
        }

        /// <summary>
        /// Searches an Array for an item that matches criteria specified
        /// through a delegate function.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <returns>True if the accepting function returns true for an element</returns>
        public static bool Contains<TItem>(this IEnumerable<TItem> values, Predicate<TItem> found)
        {
            foreach (TItem value in values)
            {
                if (found(value))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Swaps two reference values in memory, making each contain
        /// the reference the other started with.
        /// </summary>
        /// <typeparam name="TItem">Type of the two values</typeparam>
        /// <param name="val1">Left value</param>
        /// <param name="val2">Right value</param>
        public static void Swap<TItem>(ref TItem val1, ref TItem val2)
        {
            TItem tmp = val1;
            val1 = val2;
            val2 = tmp;
        }

        /// <summary>
        /// Assigns the a source reference to the intended destination,
        /// only if they are <see cref="object.Equals(object,object)"/>.
        /// 
        /// This can be useful in combination with immutable objects,
        /// allowing the caller choose an existing object already referenced
        /// in a data structure over a newly created instance, if the two
        /// are identical in value.
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        public static void AssignIfEquals<TItem>(ref TItem dest, TItem src)
        {
            if (Equals(dest, src))
                dest = src;
        }

        /// <summary>
        /// Compare two IEnumerable instances for equality.
        /// </summary>
        /// <typeparam name="TItem">The type of element being enumerated</typeparam>
        /// <param name="e1">The first IEnumerable</param>
        /// <param name="e2">The second IEnumberable</param>
        /// <returns>True if the two IEnumerables enumerate over equal objects</returns>
        public static bool Equals<TItem>(IEnumerable<TItem> e1, IEnumerable<TItem> e2)
        {
            IEnumerator<TItem> enum1 = e1.GetEnumerator();
            IEnumerator<TItem> enum2 = e2.GetEnumerator();
            bool b1, b2;
            while (MoveNext(enum1, out b1, enum2, out b2))
            {
                if (!Equals(enum1.Current, enum2.Current))
                    break;
            }

            // If both enums have advanced to completion without finding
            // a difference, then they are equal.
            return (!b1 && !b2);
        }

        /// <summary>
        /// Call MoveNext on two IEnumerator instances in one operation,
        /// but avoid short-circuiting of (e1.MoveNext() && e2.MoveNext),
        /// and pass the return values of both as out parameters.
        /// </summary>
        /// <param name="e1">First Enumerator to advance</param>
        /// <param name="b1">Return value of e1.MoveNext()</param>
        /// <param name="e2">Second Enumerator to advance</param>
        /// <param name="b2">Return value of e2.MoveNext()</param>
        /// <returns>True if both calls to MoveNext() succeed</returns>
        private static bool MoveNext(IEnumerator e1, out bool b1,
            IEnumerator e2, out bool b2)
        {
            b1 = e1.MoveNext();
            b2 = e2.MoveNext();
            return b1 && b2;
        }

        /// <summary>
        /// Parses an enum value from a string, returning a default value,
        /// if the string fails to parse.
        /// </summary>
        /// <typeparam name="TEnum">The enum type</typeparam>
        /// <param name="value">The string to parse</param>
        /// <param name="defaultValue">The value to return, if parsing fails</param>
        /// <returns>An enum value of type <see cref="TEnum"/></returns>
        public static TEnum ParseEnum<TEnum>(string value, TEnum defaultValue) where TEnum : struct
        {
            if (Enum.TryParse(value, true, out TEnum result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// Given a localized string and an array of localized strings with the
        /// index of each localized string matching the desired enum value for
        /// an enum type, returns the enum value corresponding to the localized string.
        /// </summary>
        /// <typeparam name="TEnum">The enum type</typeparam>
        /// <param name="value">The localized string for which the enum value is desired</param>
        /// <param name="localizedStrings">Array of all localized strings</param>
        /// <returns>An enum value of type <see cref="TEnum"/></returns>
        public static TEnum EnumFromLocalizedString<TEnum>(string value, string[] localizedStrings)
        {
            int i = localizedStrings.IndexOf(v => Equals(v, value));
            if (i == -1)
                throw new ArgumentException(string.Format(@"The string '{0}' does not match an enum value ({1})", value, string.Join(@", ", localizedStrings)));
            return (TEnum)(object)i;
        }

        public static TEnum EnumFromLocalizedString<TEnum>(string value, string[] localizedStrings, TEnum defaultValue)
        {
            int i = localizedStrings.IndexOf(v => Equals(v, value));
            return (i == -1 ? defaultValue : (TEnum)(object)i);
        }

        /// <summary>
        /// Enumerate all possible values of the given enum type.
        /// </summary>
        public static IEnumerable<TEnum> GetEnumValues<TEnum>()
        {
            return Enum.GetValues(typeof(TEnum)).Cast<TEnum>();
        }

        public static int CountEnumValues<TEnum>()
        {
            return Enum.GetValues(typeof(TEnum)).Length;
        }

        public static string MakeId(IEnumerable<char> name)
        {
            return MakeId(name, false);
        }

        public static string MakeId(IEnumerable<char> name, bool capitalize)
        {
            StringBuilder sb = new StringBuilder();
            char lastC = '\0';
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (lastC == ' ')
                        sb.Append('_');
                    lastC = c;
                    if (capitalize && sb.Length == 0)
                        sb.Append(c.ToString(CultureInfo.InvariantCulture).ToUpperInvariant());
                    else
                        sb.Append(c);
                }
                // Must start with a letter or digit
                else if (lastC != '\0')
                {
                    // After the start _ okay (dashes turned out to be problematic)
                    if (c == '_' /* || c == '-'*/)
                        sb.Append(lastC = c);
                    // All other characters are replaced with _, but once the next
                    // letter or number is seen.
                    else if (char.IsLetterOrDigit(lastC))
                        lastC = ' ';
                }
            }
            return sb.ToString();
        }

        // ReSharper disable LocalizableElement
        // ReSharper restore LocalizableElement

        /// <summary>
        /// Given a proposed name and a set of existing names, returns a unique name by adding
        /// or incrementing an integer suffix.
        /// </summary>
        /// <param name="name">A proposed name to add</param>
        /// <param name="set">A set of existing names</param>
        /// <returns>A new unique name that can be safely added to the existing set without name conflict</returns>
        public static string GetUniqueName(string name, ICollection<string> set)
        {
            return GetUniqueName(name, s => !set.Contains(s));
        }

        public static string GetUniqueName(string name, Func<string, bool> isUnique)
        {
            if (isUnique(name))
                return name;

            int num = 1;
            // If the name has an integer suffix, start searching with the base name
            // and the integer suffix incremented by 1.
            int i = GetIntSuffixStart(name);
            if (i < name.Length)
            {
                num = int.Parse(name.Substring(i)) + 1;
                name = name.Substring(0, i);
            }
            // Loop until a unique base name and integer suffix combination is found.
            while (!isUnique(name + num))
                num++;
            return name + num;
        }

        /// <summary>
        /// Given a name returns the start index of an integer suffix, if the name has one,
        /// or the length of the string, if no integer suffix is present.
        /// </summary>
        /// <param name="name">A name to analyze</param>
        /// <returns>The starting position of an integer suffix or the length of the string, if the name does not have one</returns>
        private static int GetIntSuffixStart(string name)
        {
            for (int i = name.Length; i > 0; i--)
            {
                if (!int.TryParse(name.Substring(i - 1), out _))
                    return i;
            }
            return 0;
        }

        public static List<string> EnsureUniqueNames(List<string> names, HashSet<string> reservedNames = null)
        {
            var setUsedNames = reservedNames ?? new HashSet<string>();
            var result = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string baseName = names[i];
                // Make sure the next name added is unique
                string name = (baseName.Length != 0 ? baseName : @"1");
                for (int suffix = 2; setUsedNames.Contains(name); suffix++)
                    name = baseName + suffix;
                result.Add(name);
                // Add this name to the used set
                setUsedNames.Add(name);
            }
            return result;
        }

        /// <summary>
        /// Count the number of lines in the file specified.
        /// </summary>
        /// <param name="f">The filename to count lines in.</param>
        /// <returns>The number of lines in the file.</returns>
        public static long CountLinesInFile(string f)
        {
            long count = 0;
            using (StreamReader r = new StreamReader(f))
            {
                while (r.ReadLine() != null)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Count the number of lines in the string specified.
        /// </summary>
        /// <param name="s">The string to count lines in.</param>
        /// <returns>The number of lines in the string.</returns>
        public static long CountLinesInString(string s)
        {
            long count = 1;
            int start = 0;
            while ((start = s.IndexOf('\n', start)) != -1)
            {
                count++;
                start++;
            }
            return count;
        }

        private const char LABEL_SEP_CHAR = '_';
        private const string ELIPSIS = "...";
        private static readonly char[] SPACE_CHARS = { '_', '-', ' ', '.', ',' };

        /// <summary>
        /// Finds repetitive text in labels and removes the text to save space.
        /// </summary>
        /// <param name="labels">The labels we are removing redundant text from.</param>
        /// <param name="startLabelIndex">Index we want to start looking at, in case the Expected/Library
        /// label is showing.</param>
        /// <returns>Return </returns>
        public static bool RemoveRepeatedLabelText(string[] labels, int startLabelIndex)
        {
            // Check to see if there are any labels. 
            if (labels.Length == startLabelIndex)
                return false;

            // Creat a normalized set of labels to test for repeated text
            string[] labelsRemove = new string[labels.Length];

            Array.Copy(labels, labelsRemove, labels.Length);

            if (startLabelIndex != 0)
            {
                labelsRemove = new string[labelsRemove.Length - startLabelIndex];
                Array.Copy(labels, startLabelIndex, labelsRemove, 0, labelsRemove.Length);
            }

            for (int i = 0; i < labelsRemove.Length; i++)
                labelsRemove[i] = NormalizeSeparators(labelsRemove[i]);

            var labelParts = labelsRemove[0].Split(LABEL_SEP_CHAR);

            // If all labels start with the first part
            string replaceString = labelParts[0];
            string partFirst = replaceString + LABEL_SEP_CHAR;
            if (!labelsRemove.Contains(label => !label.StartsWith(partFirst)))
            {
                RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.start);
                return true;
            }

            // If all labels end with the last part
            replaceString = labelParts[labelParts.Length - 1];
            string partLast = LABEL_SEP_CHAR + replaceString;
            if (!labelsRemove.Contains(label => !label.EndsWith(partLast)))
            {
                RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.end);
                return true;
            }

            for (int i = 1; i < labelParts.Length - 1; i++)
            {
                replaceString = labelParts[i];
                if (string.IsNullOrEmpty(replaceString))
                    continue;
                string partMiddle = LABEL_SEP_CHAR + replaceString + LABEL_SEP_CHAR;
                // If all labels contain the middle part
                if (!labelsRemove.Contains(label => !label.Contains(partMiddle)))
                {
                    RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.middle);
                    return true;
                }
            }

            return false;
        }

        private static bool IsSpaceChar(char c)
        {
            return SPACE_CHARS.Contains(c);
        }

        private static string NormalizeSeparators(string startLabelText)
        {
            startLabelText = startLabelText.Replace(ELIPSIS, LABEL_SEP_CHAR.ToString(CultureInfo.InvariantCulture));
            foreach (var spaceChar in SPACE_CHARS)
            {
                startLabelText = startLabelText.Replace(spaceChar, LABEL_SEP_CHAR);
            }

            return startLabelText;
        }

        /// <summary>
        /// Truncates labels.
        /// </summary>
        /// <param name="labels">Labels text will be removed from.</param>
        /// <param name="startLabelIndex">Index we want to start looking at, in case the Expected/Library
        /// label is showing.</param>
        /// <param name="replaceString">Text being removed from labels.</param>
        /// <param name="location">Expected location of the replacement text</param>
        public static void RemoveString(string[] labels, int startLabelIndex, string replaceString, ReplaceLocation location)
        {
            for (int i = startLabelIndex; i < labels.Length; i++)
                labels[i] = RemoveString(labels[i], replaceString, location);
        }

        public enum ReplaceLocation { start, middle, end }

        private static string RemoveString(string label, string replaceString, ReplaceLocation location)
        {
            int startIndex = -1;
            while ((startIndex = label.IndexOf(replaceString, startIndex + 1, StringComparison.Ordinal)) != -1)
            {
                int endIndex = startIndex + replaceString.Length;
                // Not start string and does not end with space
                if ((startIndex != 0 && !IsSpaceChar(label[startIndex - 1])) ||
                    (startIndex == 0 && location != ReplaceLocation.start))
                    continue;

                // Not end string and does not start with space
                if ((endIndex != label.Length && !IsSpaceChar(label[endIndex])) ||
                    (endIndex == label.Length && location != ReplaceLocation.end))
                    continue;

                bool elipsisSeen = false;
                bool middle = true;
                // Check left of the string for the start of the label or a space char
                if (startIndex == 0)
                    middle = false;
                else if (startIndex >= ELIPSIS.Length && label.LastIndexOf(ELIPSIS, startIndex, StringComparison.Ordinal) == startIndex - ELIPSIS.Length)
                    elipsisSeen = true;
                else
                    startIndex--;

                // Check right of the string for the end of the label or a space char
                if (endIndex == label.Length)
                    middle = false;
                else if (label.IndexOf(ELIPSIS, endIndex, StringComparison.Ordinal) == endIndex)
                    elipsisSeen = true;
                else
                    endIndex++;
                label = label.Remove(startIndex, endIndex - startIndex);
                // Insert an elipsis, if this is in the middle and no elipsis has been seen
                if (middle && !elipsisSeen && location == ReplaceLocation.middle)
                    label = label.Insert(startIndex, ELIPSIS);
                return label;
            }
            return label;
        }

        public static string TruncateString(string s, int length)
        {
            return s.Length <= length ? s : s.Substring(0, length - ELIPSIS.Length) + ELIPSIS;
        }


        /// <summary>
        /// Try an action that might throw an exception commonly related to a file move or delete.
        /// If it fails, sleep for the indicated period and try again.
        /// 
        /// N.B. "TryTwice" is a historical misnomer since it actually defaults to trying four times,
        /// but the intent is clear: try more than once. Further historical note: formerly this only
        /// handled IOException, but in looping tests we also see UnauthorizedAccessException as a result
        /// of file locks that haven't been released yet.
        /// </summary>
        /// <param name="action">action to try</param>
        /// <param name="loopCount">how many loops to try before failing</param>
        /// <param name="milliseconds">how long (in milliseconds) to wait before the action is retried</param>
        public static void TryTwice(Action action, int loopCount = 4, int milliseconds = 500)
        {
            for (int i = 1; i < loopCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException exIO)
                {
                    ReportExceptionForRetry(milliseconds, exIO, i, loopCount);
                }
                catch (UnauthorizedAccessException exUA)
                {
                    ReportExceptionForRetry(milliseconds, exUA, i, loopCount);
                }
            }

            // Try the last time, and let the exception go.
            action();
        }

        private static void ReportExceptionForRetry(int milliseconds, Exception x, int loopCount, int maxLoopCount)
        {
            Trace.WriteLine(string.Format(@"Encountered the following exception (attempt {0} of {1}):", loopCount, maxLoopCount));
            Trace.WriteLine(x.Message);
            Thread.Sleep(milliseconds);
        }

        /// <summary>
        /// Try an action that might throw an exception.  If it does, sleep for a little while and
        /// try the action one more time.  This oddity is necessary because certain file system
        /// operations (like moving a directory) can fail due to temporary file locks held by
        /// anti-virus software.
        /// </summary>
        /// <typeparam name="TEx">type of exception to catch</typeparam>
        /// <param name="action">action to try</param>
        /// <param name="loopCount">how many loops to try before failing</param>
        /// <param name="milliseconds">how long (in milliseconds) to wait before the action is retried</param>
        public static void Try<TEx>(Action action, int loopCount = 4, int milliseconds = 500) where TEx : Exception
        {
            for (int i = 1; i < loopCount; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (TEx x)
                {
                    ReportExceptionForRetry(milliseconds, x, i, loopCount);
                }
            }

            // Try the last time, and let the exception go.
            action();
        }

        public static void WrapAndThrowException(Exception x)
        {
            // The thrown exception needs to be preserved to preserve
            // the original stack trace from which it was thrown.  In some cases,
            // its type must also be preserved, because existing code handles certain
            // exception types.  If this case threw only TargetInvocationException,
            // then more frequently the code would just have to have a blanket catch
            // of the base exception type, which could hide coding errors.
            if (x is InvalidDataException)
                throw new InvalidDataException(x.Message, x);
            if (x is IOException)
                throw new IOException(x.Message, x);
            if (x is OperationCanceledException)
                throw new OperationCanceledException(x.Message, x);
            throw new TargetInvocationException(x.Message, x);
        }

        public static double? ParseNullableDouble(string s)
        {
            double d;
            return double.TryParse(s, out d) ? d : (double?)null;
        }

        public static string NullableDoubleToString(double? d)
        {
            return d.HasValue ? d.Value.ToString(LocalizationHelper.CurrentCulture) : String.Empty;
        }
    }

    /// <summary>
    /// This is a replacement for Debug.Assert, having the advantage that it is not omitted in a retail build.
    /// </summary>
    public static class Assume
    {

        public static bool InvokeDebuggerOnFail { get; private set; } // When set, we will invoke the debugger rather than fail.
        public class DebugOnFail : IDisposable
        {
            private bool _pushPopInvokeDebuggerOnFail;

            public DebugOnFail(bool invokeDebuggerOnFail = true)
            {
                _pushPopInvokeDebuggerOnFail = InvokeDebuggerOnFail; // Push
                InvokeDebuggerOnFail = invokeDebuggerOnFail;
            }

            public void Dispose()
            {
                InvokeDebuggerOnFail = _pushPopInvokeDebuggerOnFail; // Pop
            }
        }

        public static void IsTrue(bool condition, string error = "")
        {
            if (!condition)
                Fail(error);
        }

        public static void IsFalse(bool condition, string error = "")
        {
            if (condition)
                Fail(error);
        }

        public static void IsNotNull(object o, string parameterName = "")
        {
            if (o == null)
                Fail(string.IsNullOrEmpty(parameterName) ? @"null object" : parameterName + @" is null");
        }

        public static void IsNull(object o, string parameterName = "")
        {
            if (o != null)
                Fail(string.IsNullOrEmpty(parameterName) ? @"non-null object" : parameterName + @" is not null");
        }

        public static void AreEqual(object left, object right, string error = "")
        {
            if (!Equals(left, right))
                Fail(error);
        }

        public static void AreNotEqual(object left, object right, string error = "")
        {
            if (Equals(left, right))
                Fail(error);
        }

        public static void AreEqual(double expected, double actual, double delta, string error = "")
        {
            if (Math.Abs(expected - actual) > delta)
                Fail(error);
        }

        public static void Fail(string error = "")
        {
            if (InvokeDebuggerOnFail)
            {
                // Try to launch devenv with our solution sln so it presents in the list of debugger options.
                // This makes for better code navigation and easier debugging.
                try
                {
                    var path = @"\pwiz_tools\Skyline";
                    var basedir = AppDomain.CurrentDomain.BaseDirectory;
                    if (!string.IsNullOrEmpty(basedir))
                    {
                        var index = basedir.IndexOf(path, StringComparison.Ordinal);
                        var solutionPath = basedir.Substring(0, index + path.Length);
                        var skylineSln = Path.Combine(solutionPath, "Skyline.sln");
                        // Try to give user a hint as to which debugger to pick
                        var skylineTesterSln = Path.Combine(solutionPath, "USE THIS FOR ASSUME FAIL DEBUGGING.sln");
                        if (File.Exists(skylineTesterSln))
                            File.Delete(skylineTesterSln);
                        File.Copy(skylineSln, skylineTesterSln);
                        Process.Start(skylineTesterSln);
                        Thread.Sleep(20000); // Wait for it to fire up sp it's offered in the list of debuggers
                    }
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }

                Console.WriteLine();
                if (!string.IsNullOrEmpty(error))
                    Console.WriteLine(error);
                Console.WriteLine(@"error encountered, launching debugger as requested by Assume.DebugOnFail");
                Debugger.Launch();
            }
            throw new AssumptionException(error);
        }

    }


    public class AssumptionException : Exception
    {
        public AssumptionException(string message)
            : base(message)
        {
        }
    }

}
