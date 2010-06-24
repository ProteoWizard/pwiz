//
// $Id: $
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace IDPicker
{
    public static partial class Util
    {
        public static string[] StringCollectionToStringArray (StringCollection collection)
        {
            string[] output = new string[collection.Count];
            collection.CopyTo(output, 0);
            return output;
        }

        public static string[] ReplaceKeysWithValues (string[] input, KeyValuePair<string, string>[] kvPairs)
        {
            List<string> output = new List<string>();
            foreach (string str in input)
            {
                string outStr = str;
                foreach (KeyValuePair<string, string> kvp in kvPairs)
                    outStr = outStr.Replace(kvp.Key, kvp.Value);
                output.Add(outStr);
            }
            return output.ToArray();
        }

        public static int UniqueSubstring (string item, IEnumerable<string> items, out string sequence)
        {
            sequence = item;
            string substring;
            if (LongestCommonSubstring(items.Concat(new string[] { item }), out substring) > 0)
                sequence = sequence.Remove(sequence.IndexOf(substring), substring.Length);
            return sequence.Length;
        }

        public static int LongestCommonPrefix (IEnumerable<string> strings, out string sequence)
        {
            sequence = string.Empty;
            if (strings.Count() == 0)
                return 0;

            sequence = strings.First();
            foreach (string str in strings.Skip(1))
                if (LongestCommonPrefix(sequence, str, out sequence) == 0)
                    return 0;
            return sequence.Length;
        }

        public static int LongestCommonPrefix (string str1, string str2, out string sequence)
        {
            sequence = string.Empty;
            if (String.IsNullOrEmpty(str1) || String.IsNullOrEmpty(str2))
                return 0;

            StringBuilder sequenceBuilder = new StringBuilder();
            for (int i = 0; i < str1.Length && i < str2.Length && str1[i] == str2[i]; ++i)
                sequenceBuilder.Append(str1[i]);
            sequence = sequenceBuilder.ToString();
            return sequence.Length;
        }

        public static int LongestCommonSubstring (IEnumerable<string> strings, out string sequence)
        {
            sequence = string.Empty;
            if (strings.Count() == 0)
                return 0;

            sequence = strings.First();
            foreach (string str in strings.Skip(1))
                if (LongestCommonSubstring(sequence, str, out sequence) == 0)
                    return 0;
            return sequence.Length;
        }

        public static int LongestCommonSubstring (string str1, string str2, out string sequence)
        {
            sequence = string.Empty;
            if (String.IsNullOrEmpty(str1) || String.IsNullOrEmpty(str2))
                return 0;

            int[,] num = new int[str1.Length, str2.Length];
            int maxlen = 0;
            int lastSubsBegin = 0;
            StringBuilder sequenceBuilder = new StringBuilder();

            for (int i = 0; i < str1.Length; i++)
            {
                for (int j = 0; j < str2.Length; j++)
                {
                    if (str1[i] != str2[j])
                        num[i, j] = 0;
                    else
                    {
                        if ((i == 0) || (j == 0))
                            num[i, j] = 1;
                        else
                            num[i, j] = 1 + num[i - 1, j - 1];

                        if (num[i, j] > maxlen)
                        {
                            maxlen = num[i, j];
                            int thisSubsBegin = i - num[i, j] + 1;
                            if (lastSubsBegin == thisSubsBegin)
                            {//if the current LCS is the same as the last time this block ran
                                sequenceBuilder.Append(str1[i]);
                            }
                            else //this block resets the string builder if a different LCS is found
                            {
                                lastSubsBegin = thisSubsBegin;
                                sequenceBuilder.Remove(0, sequenceBuilder.Length);//clear it
                                sequenceBuilder.Append(str1.Substring(lastSubsBegin, (i + 1) - lastSubsBegin));
                            }
                        }
                    }
                }
            }
            sequence = sequenceBuilder.ToString();
            return maxlen;
        }
    }
}