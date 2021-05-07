/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Util
{
    public class PersistentString
    {
        public static readonly PersistentString EMPTY = new PersistentString(ImmutableList.Empty<string>());
        public PersistentString(IEnumerable<string> parts)
        {
            Parts = ImmutableList.ValueOf(parts);
        }

        public static PersistentString FromParts(params string[] parts)
        {
            return new PersistentString(parts);
        }

        public ImmutableList<string> Parts { get; private set; }

        public PersistentString Skip(int partCount)
        {
            return new PersistentString(Parts.Skip(partCount));
        }

        public PersistentString Concat(PersistentString persistentString)
        {
            return new PersistentString(Parts.Concat(persistentString.Parts));
        }

        public PersistentString Append(params string[] parts)
        {
            return new PersistentString(Parts.Concat(parts));
        }

        public static PersistentString Parse(string persistentString)
        {
            return new PersistentString(persistentString.Split(SEPARATOR).Select(part =>
            {
                var decoded = Uri.UnescapeDataString(part);
                return string.IsNullOrEmpty(decoded) ? null : decoded;
            }));
        }



        public override string ToString()
        {
            return string.Join(SEPARATOR.ToString(), Parts.Select(EscapePart));
        }

        public const char SEPARATOR = '|';

        public static string EscapePart(string part)
        {
            return Uri.EscapeDataString(part ?? string.Empty);
        }
    }
}
