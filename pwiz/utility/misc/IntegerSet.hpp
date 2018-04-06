//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#ifndef _INTEGERSET_HPP_
#define _INTEGERSET_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include <list>
#include <ostream>

namespace pwiz {
namespace util {


/// a virtual container of integers, accessible via an iterator interface,
/// stored as union of intervals
class PWIZ_API_DECL IntegerSet
{
    public:

    /// a single closed interval of integers 
    struct PWIZ_API_DECL Interval
    {
        int begin;
        int end;
        
        Interval(int a = 0); // allow int conversion
        Interval(int a, int b);

        bool contains(int n) const {return n>=begin && n<=end;}

        friend PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const Interval& interval);
        friend PWIZ_API_DECL std::istream& operator>>(std::istream& is, Interval& interval);
    };

    /// collection of Interval objects
    typedef std::list<Interval> Intervals;

    /// forward iterator providing readonly access to the virtual container 
    class PWIZ_API_DECL Iterator
    {
        public:

        /// \name instantiation 
        //@{
        /// default constructed Iterator marks end of any IntegerSet
        Iterator(); 

        /// initialized to beginning of the IntegerSet
        Iterator(const IntegerSet& integerSet);
        //@}

        /// \name forward iterator operators
        //@{
        Iterator& operator++();
        const Iterator operator++(int);
        int operator*() const; // note return by value
        bool operator!=(const Iterator& that) const; 
        bool operator==(const Iterator& that) const; 
        //@}

        /// \name standard iterator typedefs 
        //@{
        typedef std::forward_iterator_tag iterator_category;
        typedef int value_type;
        typedef int difference_type;
        typedef value_type* pointer;
        typedef value_type& reference;
        //@}

        private:
        Intervals::const_iterator it_;
        Intervals::const_iterator end_;
        int value_;
    };

    /// default construction
    IntegerSet();

    /// construction with a single integer
    explicit IntegerSet(int a);

    /// construction with a single interval
    IntegerSet(int a, int b);

    /// \name write access to the virtual container 
    //@{

    /// insert an interval of integers into the virtual container
    void insert(Interval interval);

    /// insert a single integer into the virtual container
    void insert(int a);

    /// insert an interval of integers into the virtual container
    void insert(int a, int b);

    /// insert intervals by parsing a string representing a 
    /// whitespace-delimited list of closed intervals:
    ///   parse(" [-3,2]  5  8-9  10- ");  // insert(-3,2); insert(5); insert(8,9); insert(10,INT_MAX);
    void parse(const std::string& intervalList);
    //@}

    /// \name const iterator interface to the virtual container 
    //@{
    typedef Iterator const_iterator;
    const_iterator begin() const; 
    const_iterator end() const; 
    //@}

    /// true iff IntegerSet is empty
    bool empty() const {return intervals_.empty();}

    /// true iff n is in the IntegerSet
    bool contains(int n) const;

    /// true iff n is an upper bound for the IntegerSet 
    bool hasUpperBound(int n) const; 

    /// returns the number of intervals in the set
    size_t intervalCount() const;

    /// returns the number of integers in the set
    size_t size() const;

    private:

    Intervals intervals_; 

    friend PWIZ_API_DECL std::ostream& operator<<(std::ostream& os, const IntegerSet& integerSet);
};


} // namespace util 
} // namespace pwiz


#endif // _INTEGERSET_HPP_

