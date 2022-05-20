/* Aho-Corasick text search algorithm implementation
 * https://www.codeproject.com/Articles/12383/Aho-Corasick-string-matching-in-C
 *
 * Code Project Open License (CPOL) 1.02
 *
 * This License governs Your use of the Work. This License is intended to allow developers to use the Source Code and Executable Files provided as part of the Work in any application in any form.
 *
 * The main points subject to the terms of the License are:
 *
 * Source Code and Executable Files can be used in commercial applications;
 * Source Code and Executable Files can be redistributed; and
 * Source Code can be modified to create derivative works.
 * No claim of suitability, guarantee, or any warranty whatsoever is provided. The software is provided "as-is".
 * The Article(s) accompanying the Work may not be distributed or republished without the Author's consent
 *
 * This License is entered between You, the individual or other entity reading or otherwise making use of the Work licensed pursuant to this License and the individual or other entity which offers the Work under the terms of this License ("Author").
 *
 * For more information visit
 *   - http://www.cs.uku.fi/~kilpelai/BSA05/lectures/slides04.pdf
 */

using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
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
        IEnumerable<string> Keywords { get; }

        /// <summary>
        /// Searches passed text and returns all occurrences of any keyword
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <returns>Array of occurrences</returns>
        StringSearchResult[] FindAll(string text);

        /// <summary>
        /// Searches passed text and returns first occurrence of any keyword
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <returns>First occurrence of any keyword (or StringSearchResult.Empty if text doesn't contain any keyword)</returns>
        StringSearchResult FindFirst(string text);

        /// <summary>
        /// Searches passed text and returns true if text contains any keyword
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <returns>True when text contains any keyword</returns>
        bool ContainsAny(string text);

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
        public StringSearchResult(int index,string keyword)
        {
            _index=index; _keyword=keyword;
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
            get { return new StringSearchResult(-1,""); }
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
            public TreeNode(TreeNode parent,char c)
            {
                _char=c; _parent=parent;
                _results=new ArrayList();
                _resultsAr=new string[] {};

                _transitionsAr=new TreeNode[] {};
                _transHash=new Hashtable();
            }


            /// <summary>
            /// Adds pattern ending in this node
            /// </summary>
            /// <param name="result">Pattern</param>
            public void AddResult(string result)
            {
                if (_results.Contains(result)) return;
                _results.Add(result);
                _resultsAr=(string[])_results.ToArray(typeof(string));
            }

            /// <summary>
            /// Adds trabsition node
            /// </summary>
            /// <param name="node">Node</param>
            public void AddTransition(TreeNode node)
            {
                _transHash.Add(node.Char,node);
                TreeNode[] ar=new TreeNode[_transHash.Values.Count];
                _transHash.Values.CopyTo(ar,0);
                _transitionsAr=ar;
            }


            /// <summary>
            /// Returns transition to specified character (if exists)
            /// </summary>
            /// <param name="c">Character</param>
            /// <returns>Returns TreeNode or null</returns>
            public TreeNode GetTransition(char c)
            {
                return (TreeNode)_transHash[c];
            }


            /// <summary>
            /// Returns true if node contains transition to specified character
            /// </summary>
            /// <param name="c">Character</param>
            /// <returns>True if transition exists</returns>
            public bool ContainsTransition(char c)
            {
                return GetTransition(c)!=null;
            }

            #endregion
            #region Properties
            
            private char _char;
            private TreeNode _parent;
            private TreeNode _failure;
            private ArrayList _results;
            private TreeNode[] _transitionsAr;
            private string[] _resultsAr;
            private Hashtable _transHash;

            /// <summary>
            /// Character
            /// </summary>
            public char Char
            {
                get { return _char; }
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
                set { _failure=value; } 
            }


            /// <summary>
            /// Transition function - list of descendant nodes
            /// </summary>
            public TreeNode[] Transitions
            {
                get { return _transitionsAr; }
            }


            /// <summary>
            /// Returns list of patterns ending by this letter
            /// </summary>
            public string[] Results
            {
                get { return _resultsAr; }
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
        /// <param name="cancelToken">A token that can be used to abort building the Aho-Corasick tree</param>
        public StringSearch(IEnumerable<string> keywords, CancellationToken cancelToken = default(CancellationToken))
        {
            SetKeywords(keywords, cancelToken);
        }


        /// <summary>
        /// Initialize search algorithm with no keywords
        /// (Use Keywords property)
        /// </summary>
        public StringSearch()
        { }

        #endregion
        #region Implementation

        /// <summary>
        /// Build tree from specified keywords
        /// </summary>
        void BuildTree(CancellationToken cancelToken)
        {
            // Build keyword tree and transition function
            _root=new TreeNode(null,' ');
            foreach(string p in _keywords)
            {
                cancelToken.ThrowIfCancellationRequested();

                // add pattern to tree
                TreeNode nd=_root;
                foreach(char c in p)
                {
                    TreeNode ndNew=null;
                    foreach(TreeNode trans in nd.Transitions)
                        if (trans.Char==c) { ndNew=trans; break; }

                    if (ndNew==null) 
                    { 
                        ndNew=new TreeNode(nd,c);
                        nd.AddTransition(ndNew);
                    }
                    nd=ndNew;
                }
                nd.AddResult(p);
            }

            // Find failure functions
            ArrayList nodes=new ArrayList();
            // level 1 nodes - fail to root node
            foreach(TreeNode nd in _root.Transitions)
            {
                nd.Failure=_root;
                foreach(TreeNode trans in nd.Transitions) nodes.Add(trans);
            }
            // other nodes - using BFS
            while(nodes.Count!=0)
            {
                cancelToken.ThrowIfCancellationRequested();

                ArrayList newNodes=new ArrayList();
                foreach(TreeNode nd in nodes)
                {
                    TreeNode r=nd.Parent.Failure;
                    char c=nd.Char;

                    while(r!=null&&!r.ContainsTransition(c)) r=r.Failure;
                    if (r==null)
                        nd.Failure=_root;
                    else
                    {
                        nd.Failure=r.GetTransition(c);
                        foreach(string result in nd.Failure.Results)
                            nd.AddResult(result);
                    }
  
                    // add child nodes to BFS list 
                    foreach(TreeNode child in nd.Transitions)
                        newNodes.Add(child);
                }
                nodes=newNodes;
            }
            _root.Failure=_root;
        }


        #endregion
        #region Methods & Properties

        /// <summary>
        /// Keywords to search for
        /// </summary>
        public IEnumerable<string> Keywords => _keywords;

        public void SetKeywords(IEnumerable<string> keywords, CancellationToken cancelToken = default(CancellationToken))
        {
            _keywords = keywords;
            BuildTree(cancelToken);
        }


        /// <summary>
        /// Searches passed text and returns all occurrences of any keyword
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <returns>Array of occurrences</returns>
        public StringSearchResult[] FindAll(string text)
        {
            ArrayList ret=new ArrayList();
            TreeNode ptr=_root;
            int index=0;

            while(index<text.Length)
            {
                TreeNode trans=null;
                while(trans==null)
                {
                    trans=ptr.GetTransition(text[index]);
                    if (ptr==_root) break;
                    if (trans==null) ptr=ptr.Failure;
                }
                if (trans!=null) ptr=trans;

                foreach(string found in ptr.Results)
                    ret.Add(new StringSearchResult(index-found.Length+1,found));
                index++;
            }
            return (StringSearchResult[])ret.ToArray(typeof(StringSearchResult));
        }


        /// <summary>
        /// Searches passed text and returns first occurrence of any keyword
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <returns>First occurrence of any keyword (or StringSearchResult.Empty if text doesn't contain any keyword)</returns>
        public StringSearchResult FindFirst(string text)
        {
            ArrayList ret=new ArrayList();
            TreeNode ptr=_root;
            int index=0;

            while(index<text.Length)
            {
                TreeNode trans=null;
                while(trans==null)
                {
                    trans=ptr.GetTransition(text[index]);
                    if (ptr==_root) break;
                    if (trans==null) ptr=ptr.Failure;
                }
                if (trans!=null) ptr=trans;

                foreach(string found in ptr.Results)
                    return new StringSearchResult(index-found.Length+1,found);
                index++;
            }
            return StringSearchResult.Empty;
        }


        /// <summary>
        /// Searches passed text and returns true if text contains any keyword
        /// </summary>
        /// <param name="text">Text to search</param>
        /// <returns>True when text contains any keyword</returns>
        public bool ContainsAny(string text)
        {
            TreeNode ptr=_root;
            int index=0;

            while(index<text.Length)
            {
                TreeNode trans=null;
                while(trans==null)
                {
                    trans=ptr.GetTransition(text[index]);
                    if (ptr==_root) break;
                    if (trans==null) ptr=ptr.Failure;
                }
                if (trans!=null) ptr=trans;

                if (ptr.Results.Length>0) return true;
                index++;
            }
            return false;
        }

        #endregion
    }
}
