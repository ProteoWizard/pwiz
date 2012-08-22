//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.IO;

namespace IDPicker
{
    public static partial class Util
    {
        public static string GetCommonFilename (IEnumerable<string> filepaths)
        {
            // find longest common case-insensitive prefix, but then restore the real case
            string commonFilename = "";
            LongestCommonPrefix(filepaths.Select(o => o.ToLower()), out commonFilename);
            if (!String.IsNullOrEmpty(commonFilename))
                commonFilename = filepaths.First().Substring(0, commonFilename.Length);

            // trim useless prefix and suffix characters
            commonFilename = commonFilename.Trim(' ', '_', '-');

            // use a generic 
            if (String.IsNullOrEmpty(commonFilename) || Path.GetPathRoot(commonFilename) == commonFilename.Replace('/', '\\'))
                commonFilename = Path.Combine(commonFilename, "idpicker-analysis-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssZ") + ".idpDB");
            else if (String.IsNullOrEmpty(Path.GetFileName(commonFilename)))
                commonFilename = Path.Combine(commonFilename, Path.GetFileName(Path.GetDirectoryName(commonFilename)) + ".idpDB");
            else
                commonFilename = Path.ChangeExtension(commonFilename.Replace(".pep.xml", ".pepXML"), ".idpDB");

            return commonFilename;
        }

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

        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        public static extern long StrFormatByteSize (long fileSize, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

        public static string GetFileSizeByteString (string filename)
        {
            return GetFileSizeByteString(new System.IO.FileInfo(filename).Length);
        }

        public static string GetFileSizeByteString (long fileSize)
        {
            var buffer = new StringBuilder(100);
            StrFormatByteSize(fileSize, buffer, 100);
            return buffer.ToString();
        }

        /* Aho-Corasick text search algorithm implementation
         * 
         * For more information visit
         *		- http://www.cs.uku.fi/~kilpelai/BSA05/lectures/slides04.pdf
         */

        /// <summary>
        /// Interface containing all methods to be implemented
        /// by string search algorithm
        /// </summary>
        public interface IStringSearchAlgorithm
        {
            #region Methods & Properties

            /// <summary>
            /// List of keywords to search for
            /// </summary>
            IEnumerable<string> Keywords { get; set; }


            /// <summary>
            /// Searches passed text and returns all occurrences of any keyword
            /// </summary>
            /// <param name="text">Text to search</param>
            /// <returns>Array of occurrences</returns>
            IEnumerable<StringSearchResult> FindAll (string text);

            /// <summary>
            /// Searches passed text and returns first occurrence of any keyword
            /// </summary>
            /// <param name="text">Text to search</param>
            /// <returns>First occurrence of any keyword (or StringSearchResult.Empty if text doesn't contain any keyword)</returns>
            StringSearchResult FindFirst (string text);

            /// <summary>
            /// Searches passed text and returns true if text contains any keyword
            /// </summary>
            /// <param name="text">Text to search</param>
            /// <returns>True when text contains any keyword</returns>
            bool ContainsAny (string text);

            #endregion
        }

        /// <summary>
        /// Structure containing results of search 
        /// (keyword and position in original text)
        /// </summary>
        public struct StringSearchResult
        {
            #region Members

            private int _index;
            private string _keyword;

            /// <summary>
            /// Initialize string search result
            /// </summary>
            /// <param name="index">Index in text</param>
            /// <param name="keyword">Found keyword</param>
            public StringSearchResult (int index, string keyword)
            {
                _index = index; _keyword = keyword;
            }


            /// <summary>
            /// Returns index of found keyword in original text
            /// </summary>
            public int Index
            {
                get { return _index; }
            }


            /// <summary>
            /// Returns keyword found by this result
            /// </summary>
            public string Keyword
            {
                get { return _keyword; }
            }


            /// <summary>
            /// Returns empty search result
            /// </summary>
            public static StringSearchResult Empty
            {
                get { return new StringSearchResult(-1, ""); }
            }

            #endregion
        }


        /// <summary>
        /// Class for searching string for one or multiple 
        /// keywords using efficient Aho-Corasick search algorithm
        /// </summary>
        public class StringSearch : IStringSearchAlgorithm
        {
            #region Objects

            /// <summary>
            /// Tree node representing character and its 
            /// transition and failure function
            /// </summary>
            class TreeNode
            {
                #region Constructor & Methods

                /// <summary>
                /// Initialize tree node with specified character
                /// </summary>
                /// <param name="parent">Parent node</param>
                /// <param name="c">Character</param>
                public TreeNode (TreeNode parent, char c)
                {
                    _char = Convert.ToByte(c);
                    _parent = parent;
                    _transHash = new TreeNode[26];
                }


                /// <summary>
                /// Adds pattern ending in this node
                /// </summary>
                /// <param name="result">Pattern</param>
                public void AddResult (string result)
                {
                    if (_results == null)
                        _results = new List<string>();
                    else if (_results.Contains(result))
                        return;
                    _results.Add(result);
                }

                /// <summary>
                /// Adds trabsition node
                /// </summary>
                /// <param name="node">Node</param>
                public void AddTransition (TreeNode node)
                {
                    _transHash[AminoAcidToIndex(node.Char)] = node;
                }


                /// <summary>
                /// Returns transition to specified character (if exists)
                /// </summary>
                /// <param name="c">Character</param>
                /// <returns>Returns TreeNode or null</returns>
                public TreeNode GetTransition (char c)
                {
                    return _transHash[AminoAcidToIndex(c)];
                }


                /// <summary>
                /// Returns true if node contains transition to specified character
                /// </summary>
                /// <param name="c">Character</param>
                /// <returns>True if transition exists</returns>
                public bool ContainsTransition (char c)
                {
                    return GetTransition(c) != null;
                }

                static private int AminoAcidToIndex (char aa){return (int) aa - 'A';}
                static private char IndexToAminoAcid (int i){return Convert.ToChar('A' + i);}

                #endregion
                #region Properties

                private byte _char;
                private TreeNode _parent;
                private TreeNode _failure;
                private List<string> _results;
                private TreeNode[] _transHash;
                private static List<string> _noResults = new List<string>();

                /// <summary>
                /// Character
                /// </summary>
                public char Char
                {
                    get { return Convert.ToChar(_char); }
                }


                /// <summary>
                /// Parent tree node
                /// </summary>
                public TreeNode Parent
                {
                    get { return _parent; }
                }


                /// <summary>
                /// Failure function - descendant node
                /// </summary>
                public TreeNode Failure
                {
                    get { return _failure; }
                    set { _failure = value; }
                }


                /// <summary>
                /// Transition function - list of descendant nodes
                /// </summary>
                public IEnumerable<TreeNode> Transitions
                {
                    get { return _transHash.Where(o => o != null); }
                }


                /// <summary>
                /// Returns list of patterns ending by this letter
                /// </summary>
                public List<string> Results
                {
                    get { return _results != null ? _results : _noResults; }
                }

                #endregion
            }

            #endregion
            #region Local fields

            /// <summary>
            /// Root of keyword tree
            /// </summary>
            private TreeNode _root;

            /// <summary>
            /// Keywords to search for
            /// </summary>
            private IEnumerable<string> _keywords;

            #endregion

            #region Initialization

            /// <summary>
            /// Initialize search algorithm (Build keyword tree)
            /// </summary>
            /// <param name="keywords">Keywords to search for</param>
            public StringSearch (IEnumerable<string> keywords)
            {
                Keywords = keywords;
            }


            /// <summary>
            /// Initialize search algorithm with no keywords
            /// (Use Keywords property)
            /// </summary>
            public StringSearch ()
            { }

            #endregion
            #region Implementation

            /// <summary>
            /// Build tree from specified keywords
            /// </summary>
            void BuildTree ()
            {
                // Build keyword tree and transition function
                _root = new TreeNode(null, ' ');
                foreach (string p in _keywords)
                {
                    // add pattern to tree
                    TreeNode nd = _root;
                    foreach (char c in p)
                    {
                        TreeNode ndNew = null;
                        foreach (TreeNode trans in nd.Transitions)
                            if (trans.Char == c) { ndNew = trans; break; }

                        if (ndNew == null)
                        {
                            ndNew = new TreeNode(nd, c);
                            nd.AddTransition(ndNew);
                        }
                        nd = ndNew;
                    }
                    nd.AddResult(p);
                }

                // Find failure functions
                var nodes = new List<TreeNode>();
                // level 1 nodes - fail to root node
                foreach (TreeNode nd in _root.Transitions)
                {
                    nd.Failure = _root;
                    foreach (TreeNode trans in nd.Transitions) nodes.Add(trans);
                }
                // other nodes - using BFS
                while (nodes.Count != 0)
                {
                    var newNodes = new List<TreeNode>();
                    foreach (TreeNode nd in nodes)
                    {
                        TreeNode r = nd.Parent.Failure;
                        char c = nd.Char;

                        while (r != null && !r.ContainsTransition(c)) r = r.Failure;
                        if (r == null)
                            nd.Failure = _root;
                        else
                        {
                            nd.Failure = r.GetTransition(c);
                            foreach (string result in nd.Failure.Results)
                                nd.AddResult(result);
                        }

                        // add child nodes to BFS list 
                        foreach (TreeNode child in nd.Transitions)
                            newNodes.Add(child);
                    }
                    nodes = newNodes;
                }
                _root.Failure = _root;
            }


            #endregion
            #region Methods & Properties

            /// <summary>
            /// Keywords to search for (setting this property is slow, because
            /// it requieres rebuilding of keyword tree)
            /// </summary>
            public IEnumerable<string> Keywords
            {
                get { return _keywords; }
                set
                {
                    _keywords = value;
                    BuildTree();
                }
            }


            /// <summary>
            /// Searches passed text and returns all occurrences of any keyword
            /// </summary>
            /// <param name="text">Text to search</param>
            /// <returns>Array of occurrences</returns>
            public IEnumerable<StringSearchResult> FindAll (string text)
            {
                var ret = new List<StringSearchResult>();
                TreeNode ptr = _root;
                int index = 0;

                while (index < text.Length)
                {
                    TreeNode trans = null;
                    while (trans == null)
                    {
                        trans = ptr.GetTransition(text[index]);
                        if (ptr == _root) break;
                        if (trans == null) ptr = ptr.Failure;
                    }
                    if (trans != null) ptr = trans;

                    foreach (string found in ptr.Results)
                        ret.Add(new StringSearchResult(index - found.Length + 1, found));
                    index++;
                }
                return ret;
            }


            /// <summary>
            /// Searches passed text and returns first occurrence of any keyword
            /// </summary>
            /// <param name="text">Text to search</param>
            /// <returns>First occurrence of any keyword (or StringSearchResult.Empty if text doesn't contain any keyword)</returns>
            public StringSearchResult FindFirst (string text)
            {
                TreeNode ptr = _root;
                int index = 0;

                while (index < text.Length)
                {
                    TreeNode trans = null;
                    while (trans == null)
                    {
                        trans = ptr.GetTransition(text[index]);
                        if (ptr == _root) break;
                        if (trans == null) ptr = ptr.Failure;
                    }
                    if (trans != null) ptr = trans;

                    foreach (string found in ptr.Results)
                        return new StringSearchResult(index - found.Length + 1, found);
                    index++;
                }
                return StringSearchResult.Empty;
            }


            /// <summary>
            /// Searches passed text and returns true if text contains any keyword
            /// </summary>
            /// <param name="text">Text to search</param>
            /// <returns>True when text contains any keyword</returns>
            public bool ContainsAny (string text)
            {
                TreeNode ptr = _root;
                int index = 0;

                while (index < text.Length)
                {
                    TreeNode trans = null;
                    while (trans == null)
                    {
                        trans = ptr.GetTransition(text[index]);
                        if (ptr == _root) break;
                        if (trans == null) ptr = ptr.Failure;
                    }
                    if (trans != null) ptr = trans;

                    if (ptr.Results.Count > 0) return true;
                    index++;
                }
                return false;
            }

            #endregion
        }
    }
}