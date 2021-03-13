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
