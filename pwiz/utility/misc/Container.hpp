//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#ifndef _CONTAINER_HPP_
#define _CONTAINER_HPP_

#include <vector>
#include <list>
#include <map>
#include <set>
#include <deque>
#include <stack>
#include <queue>
#include <algorithm>
#include <numeric>
#include <utility>
#include <boost/foreach.hpp>

using std::vector;
using std::list;
using std::map;
using std::multimap;
using std::set;
using std::multiset;
using std::deque;
using std::stack;
using std::queue;
using std::pair;
using std::make_pair;

using std::find;
using std::find_end;
using std::find_first_of;
using std::find_if;

using std::remove;
using std::remove_copy;
using std::remove_copy_if;
using std::remove_if;

using std::replace;
using std::replace_copy;
using std::replace_copy_if;
using std::replace_if;

using std::for_each;
using std::transform;
using std::accumulate;
using std::sort;
using std::stable_sort;

using std::binary_search;
using std::adjacent_find;

using std::equal_range;
using std::lower_bound;
using std::upper_bound;


#ifndef PWIZ_CONFIG_NO_CONTAINER_OUTPUT_OPERATORS

// output operators for standard containers
namespace std
{
    template<typename T1, typename T2>
    ostream& operator<< (ostream& o, const pair<T1, T2>& p)
    {
        return (o << "( " << p.first << ", " << p.second << " )");
    }

    template<typename T>
    ostream& operator<< (ostream& o, const vector<T>& v)
    {
        o << "(";
        for(const auto& i : v)
            o << " " << i;
        o << " )";

        return o;
    }

    template<typename T, typename P>
    ostream& operator<< (ostream& o, const set< T, P >& s)
    {
        o << "(";
        for (const auto& i : s)
            o << " " << i;
        o << " )";

        return o;
    }

    inline ostream& operator<< (ostream& o, const map< string, string >& m)
    {
        o << "(";
        for (const auto& p : m)
            o << " \"" << p.first << "\"->\"" << p.second << "\"";
        o << " )";

        return o;
    }

    template<typename KeyT>
    ostream& operator<< (ostream& o, const map< KeyT, string >& m)
    {
        o << "(";
        for (const auto& p : m)
            o << " " << p.first << "->\"" << p.second << "\"";
        o << " )";

        return o;
    }

    template<typename ValueT>
    ostream& operator<< (ostream& o, const map< string, ValueT >& m)
    {
        o << "(";
        for (const auto& p : m)
            o << " \"" << p.first << "\"->" << p.second << "";
        o << " )";

        return o;
    }

    template<typename KeyT, typename ValueT>
    ostream& operator<< (ostream& o, const map< KeyT, ValueT >& m)
    {
        o << "(";
        for (const auto& p : m)
            o << " " << p.first << "->" << p.second << "";
        o << " )";

        return o;
    }

    template<typename KeyT>
    set<KeyT> operator- (const set<KeyT>& lhs, const set<KeyT>& rhs)
    {
        set<KeyT> result;
        set_difference(lhs.begin(), lhs.end(), rhs.begin(), rhs.end(), insert_iterator<set<KeyT>>(result, result.begin()));
        return result;
    }
}

#endif // PWIZ_CONFIG_NO_CONTAINER_OUTPUT_OPERATORS

#endif // _CONTAINER_HPP_
