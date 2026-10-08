/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Text;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The validity record every durable pipeline artifact carries INSIDE itself: which task
    /// wrote it, under which Osprey version, and with which validity key. An artifact is
    /// reusable by a later run exactly when it exists and its stamp
    /// <see cref="IsCurrent">is current</see> for the task and key that run would write.
    ///
    /// <para>The stamp lives in the artifact, not beside it, because every artifact commits
    /// through <see cref="FileSaver"/> (presence proves completeness) and is written once: a
    /// stamp written into the same atomic commit cannot drift from the content it describes,
    /// cannot outlive it, and cannot be re-certified onto a stale file by a later process.
    /// Each format stores the same one-line text (<see cref="ToString"/>) in its own slot -
    /// a parquet footer entry (<see cref="PARQUET_KEY"/>), the first property of a JSON
    /// document (<see cref="JSON_PROPERTY"/>), the opening comment of an HTML page
    /// (<see cref="ToHtmlComment"/>), a blib metadata row, or the trailer of a binary sidecar
    /// (<see cref="BinarySidecarStamp"/>).</para>
    ///
    /// <para>A different version is never current: an artifact written by another build is
    /// recomputed. <c>OSPREY_VERSION_OVERRIDE</c> (see <see cref="OspreyVersion.Current"/>)
    /// is the deliberate developer override for reusing artifacts across builds.</para>
    /// </summary>
    public sealed class ArtifactStamp
    {
        /// <summary>Leading token of the serialized form; the trailing digit is its layout version.</summary>
        public const string PREFIX = @"osprey-validity/1";

        /// <summary>Key of the footer key/value entry that holds the stamp in a parquet artifact.</summary>
        public const string PARQUET_KEY = @"osprey.validity";

        /// <summary>Name of the property that holds the stamp, written FIRST in a JSON artifact.</summary>
        public const string JSON_PROPERTY = @"osprey_validity";

        private const string TASK_FIELD = @"task=";
        private const string VERSION_FIELD = @"version=";
        private const string KEY_FIELD = @"key=";

        private const string HTML_COMMENT_OPEN = @"<!--";
        private const string HTML_COMMENT_CLOSE = @"-->";

        /// <summary>
        /// How much of a text artifact <see cref="TryReadJsonHead"/> and
        /// <see cref="TryReadHtmlHead"/> read looking for the leading stamp. A stamp is a few
        /// hundred bytes; the artifacts it heads can be tens of megabytes, which is why the stamp
        /// is written first and read alone.
        /// </summary>
        private const int HEAD_BYTES = 64 * 1024;

        public ArtifactStamp(string task, string version, string key)
        {
            if (string.IsNullOrEmpty(task) || task.IndexOf(';') >= 0)
                throw new ArgumentException(@"A task name is required and may not contain ';'", nameof(task));
            if (string.IsNullOrEmpty(version) || version.IndexOf(';') >= 0)
                throw new ArgumentException(@"A version is required and may not contain ';'", nameof(version));
            Task = task;
            Version = version;
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }

        /// <summary>
        /// The stamp a writer embeds in an artifact it is producing now: this build's
        /// <see cref="OspreyVersion.Current"/>.
        /// </summary>
        public static ArtifactStamp ForCurrentBuild(string task, string key)
        {
            return new ArtifactStamp(task, OspreyVersion.Current, key);
        }

        /// <summary>
        /// Parse the serialized form, or return null when <paramref name="text"/> is not a
        /// stamp. Never throws: "I cannot tell" resolves to recomputing the artifact.
        /// </summary>
        public static ArtifactStamp Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.StartsWith(PREFIX + @";", StringComparison.Ordinal))
                return null;
            // The key is last because it is free text that itself contains ';' and '='.
            int taskStart = PREFIX.Length + 1;
            if (!MatchesAt(text, taskStart, TASK_FIELD))
                return null;
            int taskEnd = text.IndexOf(';', taskStart);
            if (taskEnd < 0)
                return null;
            int versionStart = taskEnd + 1;
            if (!MatchesAt(text, versionStart, VERSION_FIELD))
                return null;
            int versionEnd = text.IndexOf(';', versionStart);
            if (versionEnd < 0)
                return null;
            int keyStart = versionEnd + 1;
            if (!MatchesAt(text, keyStart, KEY_FIELD))
                return null;
            string task = text.Substring(taskStart + TASK_FIELD.Length, taskEnd - taskStart - TASK_FIELD.Length);
            string version = text.Substring(versionStart + VERSION_FIELD.Length,
                versionEnd - versionStart - VERSION_FIELD.Length);
            if (task.Length == 0 || version.Length == 0)
                return null;
            return new ArtifactStamp(task, version, text.Substring(keyStart + KEY_FIELD.Length));
        }

        /// <summary>
        /// Read the stamp from the leading <see cref="JSON_PROPERTY"/> of a JSON artifact without
        /// parsing the rest of the document. Returns null when the file is missing, unreadable,
        /// or does not start with the stamp property.
        /// </summary>
        public static ArtifactStamp TryReadJsonHead(string path)
        {
            string head = ReadHead(path);
            return head == null ? null : Parse(ReadLeadingStringProperty(head, JSON_PROPERTY));
        }

        /// <summary>
        /// Read the stamp from the leading comment of an HTML artifact
        /// (<see cref="ToHtmlComment"/>). Null when the file is missing, unreadable, or does not
        /// open with a stamp comment.
        /// </summary>
        public static ArtifactStamp TryReadHtmlHead(string path)
        {
            string head = ReadHead(path);
            if (head == null || !head.StartsWith(HTML_COMMENT_OPEN, StringComparison.Ordinal))
                return null;
            int close = head.IndexOf(HTML_COMMENT_CLOSE, StringComparison.Ordinal);
            return close < 0
                ? null
                : Parse(head.Substring(HTML_COMMENT_OPEN.Length, close - HTML_COMMENT_OPEN.Length));
        }

        /// <summary>
        /// The stamp as the comment an HTML artifact opens with, ahead of its DOCTYPE (where HTML
        /// permits comments), so a browser ignores it and a resume check reads it alone.
        /// </summary>
        public string ToHtmlComment()
        {
            return HTML_COMMENT_OPEN + ToString() + HTML_COMMENT_CLOSE;
        }

        /// <summary>Name of the task that wrote the artifact.</summary>
        public string Task { get; }

        /// <summary><see cref="OspreyVersion.Current"/> of the build that wrote the artifact.</summary>
        public string Version { get; }

        /// <summary>The writing task's validity key for this artifact.</summary>
        public string Key { get; }

        /// <summary>
        /// True when this stamp says the artifact was written by <paramref name="task"/>, by this
        /// build (<see cref="OspreyVersion.Current"/>), with <paramref name="key"/> - the only
        /// case in which a run may reuse it instead of recomputing it.
        /// </summary>
        public bool IsCurrent(string task, string key)
        {
            return string.Equals(Task, task, StringComparison.Ordinal) &&
                   string.Equals(Version, OspreyVersion.Current, StringComparison.Ordinal) &&
                   string.Equals(Key, key, StringComparison.Ordinal);
        }

        /// <summary>The one-line serialized form every artifact family stores.</summary>
        public override string ToString()
        {
            return PREFIX + @";" + TASK_FIELD + Task + @";" + VERSION_FIELD + Version + @";" + KEY_FIELD + Key;
        }

        private static bool MatchesAt(string text, int index, string token)
        {
            return string.CompareOrdinal(text, index, token, 0, token.Length) == 0;
        }

        /// <summary>
        /// The first <see cref="HEAD_BYTES"/> of a text artifact, decoded as UTF-8, or null when
        /// it cannot be read.
        /// </summary>
        private static string ReadHead(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buffer = new byte[(int)Math.Min(stream.Length, HEAD_BYTES)];
                    int read = 0;
                    while (read < buffer.Length)
                    {
                        int n = stream.Read(buffer, read, buffer.Length - read);
                        if (n <= 0)
                            break;
                        read += n;
                    }
                    return Encoding.UTF8.GetString(buffer, 0, read);
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// The string value of <paramref name="name"/> when it is the FIRST property of the JSON
        /// object that <paramref name="json"/> starts with, else null. Deliberately strict: the
        /// writers put the stamp first, so anything else is a document without a stamp.
        /// </summary>
        private static string ReadLeadingStringProperty(string json, string name)
        {
            int i = SkipWhitespace(json, 0);
            if (i >= json.Length || json[i] != '{')
                return null;
            i = SkipWhitespace(json, i + 1);
            string propertyName = ReadJsonString(json, ref i);
            if (!string.Equals(propertyName, name, StringComparison.Ordinal))
                return null;
            i = SkipWhitespace(json, i);
            if (i >= json.Length || json[i] != ':')
                return null;
            i = SkipWhitespace(json, i + 1);
            return ReadJsonString(json, ref i);
        }

        private static int SkipWhitespace(string json, int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
            return i;
        }

        /// <summary>
        /// Read the JSON string literal starting at <paramref name="i"/>, advancing past its
        /// closing quote. Null when there is no complete literal there.
        /// </summary>
        private static string ReadJsonString(string json, ref int i)
        {
            if (i >= json.Length || json[i] != '"')
                return null;
            var sb = new StringBuilder();
            for (i++; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    i++;
                    return sb.ToString();
                }
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (++i >= json.Length)
                    return null;
                switch (json[i])
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 >= json.Length ||
                            !int.TryParse(json.Substring(i + 1, 4), NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture, out int code))
                        {
                            return null;
                        }
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default:
                        return null;
                }
            }
            return null;
        }
    }
}
