//
// IntegerSet.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "IntegerSet.hpp"
#include <iostream>
#include <iterator>
#include <stdexcept>
#include <algorithm>


namespace pwiz {
namespace util {


using namespace std;


// IntegerSet::Interval implementation


IntegerSet::Interval::Interval(int a)
:   begin(a), end(a) 
{}


IntegerSet::Interval::Interval(int a, int b)
:   begin(a), end(b)
{
    if (a>b) throw runtime_error("[IntegerSet::Interval] Instantiation with a>b");
}


ostream& operator<<(ostream& os, const IntegerSet::Interval& interval)
{
    os << "[" << interval.begin << "," << interval.end << "]";
    return os;
}


// IntegerSet implementation


IntegerSet::IntegerSet() {}
IntegerSet::IntegerSet(int a) {insert(a);}
IntegerSet::IntegerSet(int a, int b) {insert(a,b);}


namespace {

inline bool beginBefore(const IntegerSet::Interval& i, const IntegerSet::Interval& j)
{
    return (i.begin < j.begin);
}

inline bool endBefore(const IntegerSet::Interval& i, const IntegerSet::Interval& j)
{
    return (i.end < j.end);
}

} // namespace


void IntegerSet::insert(Interval interval) 
{
    // eat any subintervals 

    Intervals::iterator eraseBegin = lower_bound(intervals_.begin(), intervals_.end(), 
                                                 interval.begin, beginBefore);

    Intervals::iterator eraseEnd = lower_bound(intervals_.begin(), intervals_.end(), 
                                               interval.end, endBefore);

    Intervals::iterator insertionPoint = intervals_.erase(eraseBegin, eraseEnd);

    // eat our left neighbor if it's next to us 

    if (insertionPoint != intervals_.begin())
    {
        --insertionPoint;
        const Interval& left = *insertionPoint;
        if (left.end >= interval.begin - 1)
        {
            interval.begin = left.begin;
            insertionPoint = intervals_.erase(insertionPoint);
        }
        else 
        {
            ++insertionPoint;
        }
    }

    // eat our right neighbor if it's next to us 

    if (insertionPoint != intervals_.end())
    {
        const Interval& right = *insertionPoint;
        if (right.begin <= interval.end + 1)
        {
            interval.end = right.end;
            insertionPoint = intervals_.erase(insertionPoint);
        }
    }

    // insert the interval

    intervals_.insert(insertionPoint, interval);
}


void IntegerSet::insert(int a) 
{
    insert(Interval(a));
}


void IntegerSet::insert(int a, int b) 
{
    insert(Interval(a,b));
}


IntegerSet::const_iterator IntegerSet::begin() const 
{
    return IntegerSet::Iterator(*this);
}


IntegerSet::const_iterator IntegerSet::end() const
{
    return IntegerSet::Iterator();
}


ostream& operator<<(ostream& os, const IntegerSet& integerSet)
{
    copy(integerSet.intervals_.begin(), integerSet.intervals_.end(), 
         ostream_iterator<IntegerSet::Interval>(os," "));
    return os;
}


// IntegerSet::Iterator implementation


namespace {
// used for default-constructed Iterators, so that it_ and end_ 
// can be intialized and are comparable
IntegerSet::Intervals nothing_;  
} // namespace


IntegerSet::Iterator::Iterator() 
:   it_(nothing_.end()),
    end_(nothing_.end()),
    value_(0)
{}


IntegerSet::Iterator::Iterator(const IntegerSet& integerSet)
:   it_(integerSet.intervals_.begin()),
    end_(integerSet.intervals_.end()),
    value_(it_!=end_ ? it_->begin : 0) 
{}


IntegerSet::Iterator& IntegerSet::Iterator::operator++()
{
    value_++;

    // when we finish an interval, jump to the next
    if (value_ > it_->end)
    {
        ++it_;
        value_ = it_!=end_ ? it_->begin : 0;  
    }

    return *this;
}


const IntegerSet::Iterator IntegerSet::Iterator::operator++(int)
{
    Iterator temp(*this);
    this->operator++();
    return temp;
}


int IntegerSet::Iterator::operator*() const
{
    if (it_ == end_)
        throw runtime_error("[IntegerSet::Iterator::operator*()] Invalid dereference.");

    return value_;
}


bool IntegerSet::Iterator::operator!=(const Iterator& that) const
{
    return !this->operator==(that);
}


bool IntegerSet::Iterator::operator==(const Iterator& that) const
{
    // true in two cases:
    // 1) "this" and "that" are both valid and equal
    // 2) "this" is at the end, and "that" is the special default constructed nothing_.end()

    return (it_!=end_ && that.it_!=that.end_ && it_==that.it_ && value_==that.value_ ||
            it_==end_ && that.it_==nothing_.end());
}


} // namespace util 
} // namespace pwiz


