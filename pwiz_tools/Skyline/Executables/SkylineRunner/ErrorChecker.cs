using System;
using System.Linq;

namespace pwiz.SkylineRunner
{
    public class ErrorChecker
    {
        // We want to be able to distribute SkylineRunner as a single EXE file. So, requiring
        // resource DLLs for language translation is not possible.
        private static readonly string[] INTL_ERROR_PREFIXES =
        {
            "エラー：", // ja
            "错误："    // zh-CHS
        };

        public static bool IsErrorLine(string line)
        {
            // The English prefix can happen in any culture when running Skyline-daily with a new
            // untranslated error message.
            if (HasErrorPrefix(line, "Error:", StringComparison.InvariantCulture))
                return true;

            return INTL_ERROR_PREFIXES.Any(p => HasErrorPrefix(line, p, StringComparison.CurrentCulture));
        }


        private static bool HasErrorPrefix(string line, string prefix, StringComparison comparisonType)
        {
            int prefixIndex = line.IndexOf(prefix, comparisonType);
            if (prefixIndex == -1)
                return false;
            // The prefix could start the line or it could be preceded by a tab character
            // if the output includes a timestamp or memory stamp.
            return prefixIndex == 0 || line[prefixIndex - 1] == '\t';
        }
    }
}