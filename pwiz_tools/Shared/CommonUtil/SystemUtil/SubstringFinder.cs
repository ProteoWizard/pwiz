/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Class which pre-processes a string up front in order to be able to quickly
    /// answer the question as to whether that string contains a substring.
    /// </summary>
    public class SubstringFinder
    {
        private readonly TrieNode _root;
        // The lowest valued character that was found in the string to be searched
        private readonly char _minCharacter;
        private readonly string _stringToBeSearched;
        private readonly int _maxLengthInTrie;

        /// <summary>
        /// Constructs a new SubstringFinder that can look for substrings in a particular value
        /// </summary>
        public SubstringFinder(string stringToBeSearched) : this(stringToBeSearched, stringToBeSearched.Length)
        {
        }
        
        /// <summary>
        /// Constructs a new SubstringFinder which can look for substrings up to a particular length contained inside of a larger string.
        /// </summary>
        public SubstringFinder(string stringToBeSearched, int maxSubstringLength)
        {
            _maxLengthInTrie = maxSubstringLength;
            _stringToBeSearched = stringToBeSearched;
            if (string.IsNullOrEmpty(stringToBeSearched) || maxSubstringLength == 0)
            {
                _root = TrieNode.EMPTY;
                return;
            }

            // Find out what the lowest and highest values characters in the string are, so we know
            // how big to make the arrays in our TrieNode's.
            _minCharacter = stringToBeSearched.Min();
            var maxCharacter = stringToBeSearched.Max();
            int childArraySize = maxCharacter - _minCharacter + 1;

            _root = new TrieNode(childArraySize);
            // Add all possible substrings to the Trie
            for (int substringStart = 0; substringStart < stringToBeSearched.Length; substringStart++)
            {
                // Add the substring which begins at character index "start"
                var node = _root;
                int end = Math.Min(substringStart + maxSubstringLength, stringToBeSearched.Length);
                for (int position = substringStart; position < end; position++)
                {
                    bool isLastPosition = position == end - 1;
                    int indexInChildren = stringToBeSearched[position] - _minCharacter;
                    var child = node._children[indexInChildren];
                    if (child._children == null || !isLastPosition && child._children.Length == 0)
                    {
                        child = isLastPosition ? TrieNode.EMPTY : new TrieNode(childArraySize);
                        node._children[indexInChildren] = child;
                    }
                    node = child;
                }
            }
        }

        public bool ContainsSubstring(string substring)
        {
            if (substring.Length <= _maxLengthInTrie)
            {
                return TrieContainsSubstring(substring);
            }
            if (!TrieContainsSubstring(substring.Substring(0, _maxLengthInTrie)))
            {
                return false;
            }
            return _stringToBeSearched.Contains(substring);
        }

        private bool TrieContainsSubstring(string substring)
        {
            var node = _root;
            for (int i = 0; i < substring.Length; i++)
            {
                if (node.IsNull)
                {
                    return false;
                }

                node = GetChild(node, substring[i]);
            }
            return !node.IsNull;
        }

        private TrieNode GetChild(TrieNode parent, char ch)
        {
            int index = ch - _minCharacter;
            if (index < 0 || index >= parent._children.Length)
            {
                return default(TrieNode);
            }

            return parent._children[index];
        }

        private struct TrieNode
        {
            public static readonly TrieNode EMPTY = new TrieNode(Array.Empty<TrieNode>());
            /// <summary>
            /// The children of this node. The first element in the array corresponds to the character <see cref="_minCharacter" />.
            /// </summary>
            public readonly TrieNode[] _children;

            private TrieNode(TrieNode[] array)
            {
                _children = array;
            }
            public TrieNode(int childArraySize) : this(new TrieNode[childArraySize])
            {
            }
            public bool IsNull => _children == null;
        }
    }
}
