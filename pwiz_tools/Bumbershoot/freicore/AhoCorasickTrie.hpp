//
// $Id$ 
//
//
// Original author: Tomas Petricek (http://www.codeproject.com/KB/recipes/ahocorasick.aspx)
// Copyright 2005 Tomas Petricek
//
// Adapted to C++ by: Matt Chambers <matt.chambers .@. vanderbilt.edu>
// Copyright 2011 Vanderbilt University
//
// Licensed under the Code Project Open License, Version 1.02 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.codeproject.com/info/cpol10.aspx
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//


#ifndef _AHOCORASICKTRIE_HPP_
#define _AHOCORASICKTRIE_HPP_


#include <string>
#include <vector>
#include <boost/shared_ptr.hpp>
#include <boost/foreach.hpp>


namespace freicore {


using std::set;
using std::vector;


struct ascii_translator
{
    static int size() {return 128;}
    static char translate(int index) {return static_cast<char>(index);}
    static int translate(char symbol) {return static_cast<int>(symbol);}
};


template <typename SymbolTranslator = ascii_translator, typename KeyType = std::string >
class AhoCorasickTrie
{
    public:

    typedef boost::shared_ptr<KeyType> shared_keytype;

    struct SearchResult
    {
        size_t offset() const {return _offset;}
        const shared_keytype& keyword() const {return _keyword;}

        private:
        size_t _offset;
        shared_keytype _keyword;
        friend class AhoCorasickTrie;

        SearchResult(size_t offset, const shared_keytype& keyword)
        : _offset(offset), _keyword(keyword)
        {}
    };

    /// default constructor
    AhoCorasickTrie() : _emptyHash(0), _root(0) {}

    /// construction by enumerating a range of shared_string
    template <typename FwdIterator>
    AhoCorasickTrie(FwdIterator begin, FwdIterator end) : _emptyHash(0), _root(0)
    {
        insert(begin, end);
    }

    ~AhoCorasickTrie()
    {
        if (_root)
            delete _root;
        if (_emptyHash)
            delete [] _emptyHash;
    }

    /// inserts a range of shared_string and rebuilds the trie
    template <typename FwdIterator>
    void insert(FwdIterator begin, FwdIterator end)
    {
        for (; begin != end; ++begin) _insert(*begin);
        _build();
    }

    /// inserts a single shared_string and rebuilds the trie
    void insert(const shared_keytype& keyword)
    {
        _insert(keyword);
        _build();
    }

    /// returns the first instance of a keyword in the text
    SearchResult find_first(const std::string& text)
    {
        if (!_root)
            return SearchResult(text.length(), shared_keytype());

        typename Node::Ptr ptr = _root;
        size_t offset = 0;

        while (offset < text.length())
        {
            typename Node::Ptr transition = 0;
            while (!transition)
            {
                int index = SymbolTranslator::translate(text[offset]);
                if (index < 0 || index > SymbolTranslator::size())
                    throw out_of_range(string("[AhoCorasickTrie::find_first] character '") + text[offset] + "' is not in the trie's alphabet");

                transition = ptr->transitionHash[index];
                if (ptr == _root) break;
                if (!transition) ptr = ptr->failure;
            }
            if (transition) ptr = transition;

            if (!ptr->results->empty())
            {
                const shared_keytype& result = *ptr->results->begin();
                return SearchResult(offset - result->length() + 1, result);
            }
            ++offset;
        }
        return SearchResult(text.length(), shared_keytype());
    }

    /// returns all instances of all keywords in the text
    vector<SearchResult> find_all(const std::string& text)
    {
        vector<SearchResult> results;

        if (!_root)
            return results;

        typename Node::Ptr ptr = _root;
        size_t offset = 0;

        while (offset < text.length())
        {
            typename Node::Ptr transition = 0;
            while (!transition)
            {
                int index = SymbolTranslator::translate(text[offset]);
                if (index < 0 || index > SymbolTranslator::size())
                    throw out_of_range(string("[AhoCorasickTrie::find_all] character '") + text[offset] + "' is not in the trie's alphabet");

                transition = ptr->transitionHash[index];
                if (ptr == _root) break;
                if (!transition) ptr = ptr->failure;
            }
            if (transition) ptr = transition;

            BOOST_FOREACH(const shared_keytype& result, *ptr->results)
                results.push_back(SearchResult(offset - static_cast<const string&>(*result).length() + 1, result));
            ++offset;
        }
        return results;
    }

    size_t size() const
    {
        return _keywords.size();
    }

    bool empty() const
    {
        return size() == 0;
    }

    void clear()
    {
        _keywords.clear();

        if (_root) { delete _root; _root = 0; }
        if (_emptyHash) { delete [] _emptyHash; _emptyHash = 0; }
    }

    private:

    struct SharedKeyTypeFastLessThan
    {
        bool operator() (const shared_keytype& lhs, const shared_keytype& rhs) const
        {
            const string& lhsStr = static_cast<const string&>(*lhs);
            const string& rhsStr = static_cast<const string&>(*rhs);
            if (lhsStr.length() == rhsStr.length())
                return lhsStr < rhsStr;
            return lhsStr.length() < rhsStr.length();
        }
    };

    typedef set<shared_keytype, SharedKeyTypeFastLessThan> SharedKeyTypeSet;

    struct Node
    {
        typedef Node* Ptr;

        Node(SharedKeyTypeSet& emptyResults, Ptr*& emptyHash, Ptr parent, char c)
        : value(c),
          parent(parent),
          failure(0),
          results(&emptyResults),
          transitionHash(emptyHash),
          emptyHash(emptyHash)
        {}

        ~Node()
        {
            if (!results->empty())
                delete results;
            if (transitionHash != emptyHash)
            {
                for (int i=0; i < SymbolTranslator::size(); ++i)
                    if (transitionHash[i])
                        delete transitionHash[i];
                delete [] transitionHash;
            }
        }

        void addResult(const shared_keytype& result)
        {
            if (results->empty())
                results = new SharedKeyTypeSet;
            results->insert(result);
        }

        void addTransition(char c, Ptr& node)
        {
            if (transitionHash == emptyHash)
            {
                transitionHash = new Ptr[SymbolTranslator::size()];
                std::fill(transitionHash, transitionHash+SymbolTranslator::size(), Ptr());
            }
            transitionHash[SymbolTranslator::translate(c)] = node;
        }

        char value;
        Ptr parent;
        Ptr failure;
        SharedKeyTypeSet* results;
        Ptr* transitionHash;
        Ptr*& emptyHash;
    };

    void _insert(const shared_keytype& keyword)
    {
        _isDirty = true;
        _keywords.insert(keyword);
    }

    SharedKeyTypeSet _emptyResults;
    typename Node::Ptr* _emptyHash;

    void _build()
    {
        if (!_isDirty)
            return;

        if (!_emptyHash)
        {
            _emptyHash = new typename Node::Ptr[SymbolTranslator::size()];
            std::fill(_emptyHash, _emptyHash+SymbolTranslator::size(), typename Node::Ptr());
        }

        // Build keyword tree and transition function
        _root = new Node(_emptyResults, _emptyHash, typename Node::Ptr(), SymbolTranslator::translate(0));
        BOOST_FOREACH(const shared_keytype& keyword, _keywords)
        {
            // add pattern to tree
            typename Node::Ptr node = _root;
            BOOST_FOREACH(char c, static_cast<const string&>(*keyword))
            {
                typename Node::Ptr newNode = node->transitionHash[SymbolTranslator::translate(c)];

                if (!newNode)
                {
                    newNode = new Node(_emptyResults, _emptyHash, node, c);
                    node->addTransition(c, newNode);
                }
                node = newNode;
            }
            node->addResult(keyword);
        }

        // Find failure functions
        vector<typename Node::Ptr> nodes;

        // level 1 nodes - fail to root node
        for (int i=0; i < SymbolTranslator::size(); ++i)
        {
            const typename Node::Ptr& depth1Node = _root->transitionHash[i];
            if (!depth1Node)
                continue;

            depth1Node->failure = _root;
            for (int j=0; j < SymbolTranslator::size(); ++j)
            {
                const typename Node::Ptr& depth2Node = depth1Node->transitionHash[j];
                if (depth2Node)
                    nodes.push_back(depth2Node);
            }
        }

        // other nodes - using BFS
        while (!nodes.empty())
        {
            vector<typename Node::Ptr> nextLevelNodes;
            BOOST_FOREACH(const typename Node::Ptr& node, nodes)
            {
                typename Node::Ptr r = node->parent->failure;
                char c = node->value;

                while (r && !r->transitionHash[SymbolTranslator::translate(c)]) r = r->failure;
                if (!r)
                    node->failure = _root;
                else
                {
                    node->failure = r->transitionHash[SymbolTranslator::translate(c)];
                    BOOST_FOREACH(const shared_keytype& result, *node->failure->results)
                        node->addResult(result);
                }

                // add child nodes to BFS list 
                for (int i=0; i < SymbolTranslator::size(); ++i)
                {
                    const typename Node::Ptr& transition = node->transitionHash[i];
                    if (transition)
                        nextLevelNodes.push_back(transition);
                }
            }
            nodes = nextLevelNodes;
        }
        _root->failure = _root;

        _isDirty = false;
    }

    bool _isDirty;
    SharedKeyTypeSet _keywords;
    typename Node::Ptr _root;
};


} // namespace freicore


#endif // _AHOCORASICKTRIE_HPP_
